using System.Security.Cryptography;

namespace ContosoDashboard.Services;

public sealed class DocumentStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "ContosoDashboard", "DocumentStorage");
    public long MaximumFileSize { get; set; } = 25 * 1024 * 1024;
}

public sealed record TemporaryStorageHandle(string Name);
public sealed record CommittedStorageHandle(string Name);

public interface IFileStorage
{
    Task<TemporaryStorageHandle> CreateTemporaryAsync(Stream content, CancellationToken cancellationToken = default);
    Task<CommittedStorageHandle> CommitAsync(TemporaryStorageHandle temporaryHandle, string opaqueStorageName, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string opaqueStorageName, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string opaqueStorageName, CancellationToken cancellationToken = default);
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        var configured = configuration["DocumentStorage:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.ExpandEnvironmentVariables(configured);
            _root = Path.GetFullPath(configured);
        }
        else
        {
            _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ContosoDashboard", "DocumentStorage"));
        }
        if (_root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Document storage must be outside publicly served web content.");
        _logger = logger;
        Directory.CreateDirectory(_root);
    }

    public async Task<TemporaryStorageHandle> CreateTemporaryAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var name = "tmp-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var path = Resolve(name);
        try
        {
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
            await content.CopyToAsync(target, cancellationToken);
            return new TemporaryStorageHandle(name);
        }
        catch
        {
            try { File.Delete(path); } catch { /* best-effort private temporary cleanup */ }
            throw;
        }
    }

    public Task<CommittedStorageHandle> CommitAsync(TemporaryStorageHandle temporaryHandle, string opaqueStorageName, CancellationToken cancellationToken = default)
    {
        if (temporaryHandle is null || string.IsNullOrWhiteSpace(temporaryHandle.Name))
            throw new ArgumentException("Invalid temporary storage handle.", nameof(temporaryHandle));
        var safeName = ValidateOpaqueName(opaqueStorageName);
        var source = Resolve(temporaryHandle.Name);
        var destination = Resolve(safeName);
        if (!File.Exists(source)) throw new FileNotFoundException("Temporary content is unavailable.");
        File.Move(source, destination, false);
        return Task.FromResult(new CommittedStorageHandle(safeName));
    }

    public Task<Stream> OpenReadAsync(string opaqueStorageName, CancellationToken cancellationToken = default)
    {
        var path = Resolve(ValidateOpaqueName(opaqueStorageName));
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        return Task.FromResult(stream);
    }

    public Task<bool> DeleteAsync(string opaqueStorageName, CancellationToken cancellationToken = default)
    {
        var path = Resolve(ValidateOpaqueName(opaqueStorageName));
        if (!File.Exists(path)) return Task.FromResult(true);
        File.Delete(path);
        return Task.FromResult(true);
    }

    private string Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') ||
            name.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(name))
            throw new ArgumentException("Storage names must be opaque file names.", nameof(name));
        var path = Path.GetFullPath(Path.Combine(_root, name));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Storage name is outside the private storage root.", nameof(name));
        return path;
    }

    private static string ValidateOpaqueName(string name)
    {
        if (name.Length < 8 ||
            name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ArgumentException("Storage names must be opaque file names.", nameof(name));
        return name;
    }
}
