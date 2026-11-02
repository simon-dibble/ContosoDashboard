using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.Services;
using System.Diagnostics;

namespace ContosoDashboard.Tests.Performance;

public class DocumentFilePerformanceTests
{
    [Fact]
    public async Task Valid_local_upload_and_read_complete_within_expected_bounds()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        await db.SaveChangesAsync();
        var storage = new MemoryDocumentStorage();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1), storage);
        var content = new byte[1024 * 1024];
        new Random(7).NextBytes(content);
        var stopwatch = Stopwatch.StartNew();

        var result = (await service.UploadAsync("Performance", null, "Other", null, null,
            [new DocumentUploadRequest("sample.txt", "text/plain", new MemoryStream(content), content.Length)])).Single();
        var read = await service.OpenReadAsync(result.Document!.DocumentId);
        stopwatch.Stop();

        Assert.True(result.Succeeded);
        Assert.NotNull(read);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"File operation took {stopwatch.Elapsed}.");
        await read!.Content.DisposeAsync();
    }

    [Fact]
    public async Task A_near_maximum_25_mebibyte_upload_completes_in_under_30_seconds()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        await db.SaveChangesAsync();
        var storage = new MemoryDocumentStorage();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1), storage);
        // Just under the 25 MiB hard limit enforced by DocumentValidationService/UploadOneAsync.
        var content = new byte[25 * 1024 * 1024 - 1];
        new Random(11).NextBytes(content);
        var stopwatch = Stopwatch.StartNew();

        var result = (await service.UploadAsync("Large upload", null, "Other", null, null,
            [new DocumentUploadRequest("large.pdf", "application/pdf", new MemoryStream(content), content.Length)])).Single();
        stopwatch.Stop();

        Assert.True(result.Succeeded, result.Error);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"A 25 MiB upload took {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData("report.pdf", "application/pdf")]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.png", "image/png")]
    public async Task Pdf_and_image_preview_reads_complete_in_under_3_seconds(string fileName, string contentType)
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        await db.SaveChangesAsync();
        var storage = new MemoryDocumentStorage();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1), storage);
        var content = new byte[2 * 1024 * 1024];
        new Random(fileName.GetHashCode()).NextBytes(content);
        var uploaded = (await service.UploadAsync("Preview", null, "Other", null, null,
            [new DocumentUploadRequest(fileName, contentType, new MemoryStream(content), content.Length)])).Single();
        Assert.True(uploaded.Succeeded, uploaded.Error);
        var stopwatch = Stopwatch.StartNew();

        var preview = await service.OpenReadAsync(uploaded.Document!.DocumentId);
        stopwatch.Stop();

        Assert.NotNull(preview);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Preview read took {stopwatch.Elapsed}.");
        await preview!.Content.DisposeAsync();
    }
}

