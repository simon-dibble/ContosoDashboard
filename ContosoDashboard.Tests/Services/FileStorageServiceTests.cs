using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class FileStorageServiceTests
{
    [Fact]
    public async Task Uses_opaque_names_read_only_streams_and_idempotent_cleanup()
    {
        var root = Path.Combine(Path.GetTempPath(), "ContosoDashboardTests", Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DocumentStorage:RootPath"] = root }).Build();
        var storage = new LocalFileStorage(config, NullLogger<LocalFileStorage>.Instance);
        await using var input = new MemoryStream(new byte[] { 1, 2, 3 });
        var temp = await storage.CreateTemporaryAsync(input);
        var committed = await storage.CommitAsync(temp, "opaque-" + Guid.NewGuid().ToString("N"));
        await using (var output = await storage.OpenReadAsync(committed.Name))
        {
            Assert.False(output.CanWrite);
            Assert.Equal(3, output.Length);
        }
        Assert.True(await storage.DeleteAsync(committed.Name));
        Assert.True(await storage.DeleteAsync(committed.Name));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.CommitAsync(temp, "../escape"));
    }

    [Fact]
    public void Rejects_a_public_web_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ContosoDashboardTests", "wwwroot", "uploads");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DocumentStorage:RootPath"] = root }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            new LocalFileStorage(config, NullLogger<LocalFileStorage>.Instance));
    }
}
