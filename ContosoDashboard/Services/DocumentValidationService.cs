using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public sealed record DocumentValidationResult(bool IsValid, string? Error, string Title, string? Description, string Category, string Tags);

public interface IDocumentValidationService
{
    DocumentValidationResult ValidateMetadata(string? title, string? description, string? category, IEnumerable<string>? tags);
    bool IsAllowedFile(string fileName, string contentType, long length, out string error);
    bool IsValidDateRange(DateTime? from, DateTime? to);
    string NormalizeTags(IEnumerable<string>? tags);
}

public sealed class DocumentValidationService : IDocumentValidationService
{
    private static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new[] { "application/pdf" },
        [".doc"] = new[] { "application/msword" }, [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        [".xls"] = new[] { "application/vnd.ms-excel" }, [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        [".ppt"] = new[] { "application/vnd.ms-powerpoint" }, [".pptx"] = new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        [".txt"] = new[] { "text/plain" }, [".jpg"] = new[] { "image/jpeg" }, [".jpeg"] = new[] { "image/jpeg" }, [".png"] = new[] { "image/png" }
    };
    public DocumentValidationResult ValidateMetadata(string? title, string? description, string? category, IEnumerable<string>? tags)
    {
        var cleanTitle = title?.Trim() ?? string.Empty;
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        var cleanCategory = category?.Trim() ?? string.Empty;
        if (cleanTitle.Length == 0 || cleanTitle.Length > 255) return new(false, "Title is required and must be 255 characters or fewer.", cleanTitle, cleanDescription, cleanCategory, string.Empty);
        if (cleanDescription?.Length > 2000) return new(false, "Description must be 2000 characters or fewer.", cleanTitle, cleanDescription, cleanCategory, string.Empty);
        if (!DocumentCategories.All.Contains(cleanCategory)) return new(false, "Select a valid document category.", cleanTitle, cleanDescription, cleanCategory, string.Empty);
        string normalized;
        try { normalized = NormalizeTags(tags); }
        catch (ArgumentException ex) { return new(false, ex.Message, cleanTitle, cleanDescription, cleanCategory, string.Empty); }
        return new(true, null, cleanTitle, cleanDescription, cleanCategory, normalized);
    }
    public bool IsAllowedFile(string fileName, string contentType, long length, out string error)
    {
        error = string.Empty;
        if (length < 1 || length > 25 * 1024 * 1024) { error = "Each file must be between 1 byte and 25 MiB."; return false; }
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
        { error = "The file name is not valid."; return false; }
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !Allowed.TryGetValue(extension, out var types) ||
            string.IsNullOrWhiteSpace(contentType) || !types.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        { error = "This file type is not supported."; return false; }
        var safe = Path.GetFileName(fileName);
        if (!string.Equals(safe, fileName, StringComparison.Ordinal) || safe.Contains("..", StringComparison.Ordinal))
        { error = "The file name is not valid."; return false; }
        return true;
    }
    public bool IsValidDateRange(DateTime? from, DateTime? to) => !from.HasValue || !to.HasValue || from.Value.Date <= to.Value.Date;
    public string NormalizeTags(IEnumerable<string>? tags)
    {
        var source = (tags ?? Enumerable.Empty<string>()).ToArray();
        if (source.Length > 0 && source.Any(t => string.IsNullOrWhiteSpace(t)))
            throw new ArgumentException("Tags must not be empty.");
        var values = source.Select(t => t.Trim()).ToArray();
        if (values.Length > 20 || values.Any(t => t.Length > 50)) throw new ArgumentException("Tags must contain at most 20 values of 50 characters or fewer.");
        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length) throw new ArgumentException("Tags must not contain duplicates.");
        return string.Join(", ", values);
    }
}
