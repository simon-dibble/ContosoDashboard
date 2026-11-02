using ContosoDashboard.Services;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContosoDashboard.Tests.Services;

public class DocumentUploadTests
{
    [Fact]
    public void Per_file_validation_is_independent()
    {
        var validation = new DocumentValidationService();
        Assert.True(validation.IsAllowedFile("ok.pdf", "application/pdf", 1, out _));
        Assert.False(validation.IsAllowedFile("bad.exe", "application/octet-stream", 1, out _));
    }

    [Fact]
    public void Metadata_failure_does_not_depend_on_file_order()
    {
        var validation = new DocumentValidationService();
        var result = validation.ValidateMetadata(" ", null, "Other", null);

        Assert.False(result.IsValid);
        Assert.Contains("Title", result.Error);
    }

    [Fact]
    public async Task Scanner_rejection_is_a_non_disclosing_failure()
    {
        var scanner = new DeterministicMalwareScanner();
        await using var content = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE"));

        var result = await scanner.ScanAsync(content, "notes.txt", "text/plain");

        Assert.Equal(MalwareScanOutcome.Rejected, result.Outcome);
        Assert.DoesNotContain("EICAR", result.ReasonCode, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scanner_error_fails_closed()
    {
        var scanner = new DeterministicMalwareScanner();
        await using var content = new ThrowingStream();

        var result = await scanner.ScanAsync(content, "notes.txt", "text/plain");

        Assert.Equal(MalwareScanOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task Upload_returns_independent_results_for_multiple_files()
    {
        await using var context = NewContext();
        var storage = new RecordingStorage();
        var service = new DocumentService(context, storage, new DeterministicMalwareScanner(),
            new TestAuthorization(context), new DocumentValidationService(),
            NullLogger<DocumentService>.Instance);
        var files = new[]
        {
            new DocumentUploadRequest("good.txt", "text/plain", new MemoryStream(new byte[] { 1 }), 1),
            new DocumentUploadRequest("bad.exe", "application/octet-stream", new MemoryStream(new byte[] { 1 }), 1)
        };

        var results = await service.UploadAsync("Training notes", null, "Other", null, null, files);

        Assert.True(results[0].Succeeded);
        Assert.False(results[1].Succeeded);
        Assert.Single(context.Documents);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Non_seekable_sentinel_is_scanned_and_never_stored()
    {
        await using var context = NewContext();
        var storage = new RecordingStorage();
        var service = CreateService(context, storage);
        await using var content = new NonSeekableReadStream(
            System.Text.Encoding.UTF8.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE"));

        var result = (await service.UploadAsync("Training notes", null, "Other", null, null,
            new[] { new DocumentUploadRequest("notes.txt", "text/plain", content, content.ExpectedLength) }))[0];

        Assert.False(result.Succeeded);
        Assert.Equal(0, storage.Created);
        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task Metadata_failure_creates_no_temporary_content_or_record()
    {
        await using var context = NewContext();
        var storage = new RecordingStorage();
        var service = CreateService(context, storage);

        var result = (await service.UploadAsync(" ", null, "Other", null, null,
            new[] { new DocumentUploadRequest("good.txt", "text/plain", new MemoryStream(new byte[] { 1 }), 1) }))[0];

        Assert.False(result.Succeeded);
        Assert.Equal(0, storage.Created);
        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task Commit_failure_cleans_temporary_content_and_creates_no_record()
    {
        await using var context = NewContext();
        var storage = new RecordingStorage { ThrowOnCommit = true };
        var service = new DocumentService(context, storage, new DeterministicMalwareScanner(),
            new TestAuthorization(context), new DocumentValidationService(),
            NullLogger<DocumentService>.Instance);

        var result = (await service.UploadAsync("Training notes", null, "Other", null, null,
            new[] { new DocumentUploadRequest("good.txt", "text/plain", new MemoryStream(new byte[] { 1 }), 1) }))[0];

        Assert.False(result.Succeeded);
        Assert.Empty(context.Documents);
        Assert.Contains("tmp-", storage.Deleted.Single());
    }

    [Fact]
    public async Task Metadata_persistence_failure_removes_committed_content_and_record()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName).Options;
        await using var context = new FailingSaveContext(options);
        var storage = new RecordingStorage();
        var service = CreateService(context, storage);

        var result = (await service.UploadAsync("Training notes", null, "Other", null, null,
            new[] { new DocumentUploadRequest("good.txt", "text/plain", new MemoryStream(new byte[] { 1 }), 1) }))[0];

        Assert.False(result.Succeeded);
        Assert.Single(storage.Deleted);
        Assert.DoesNotContain("tmp-", storage.Deleted.Single());
        Assert.Empty(context.Documents.Local);
        await using var verification = new ApplicationDbContext(options);
        Assert.Empty(await verification.Documents.ToListAsync());
    }

    [Fact]
    public async Task Uploading_to_a_project_notifies_other_project_members_but_not_the_uploader()
    {
        await using var context = DocumentTestData.Context();
        context.Users.AddRange(
            new User { UserId = 1, Email = "uploader@test", DisplayName = "Uploader" },
            new User { UserId = 2, Email = "member@test", DisplayName = "Member" },
            new User { UserId = 3, Email = "manager@test", DisplayName = "Manager" });
        context.Projects.Add(new Project { ProjectId = 1, Name = "Project", ProjectManagerId = 3 });
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = 1, UserId = 1 },
            new ProjectMember { ProjectId = 1, UserId = 2 });
        await context.SaveChangesAsync();
        var authorization = new DocumentTestAuthorization(context, 1);
        var service = DocumentTestData.Service(context, authorization);

        var result = (await service.UploadAsync("Training notes", null, "Other", null, 1,
            new[] { new DocumentUploadRequest("good.txt", "text/plain", new MemoryStream(new byte[] { 1 }), 1) }))[0];

        Assert.True(result.Succeeded);
        var notifications = await context.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.UserId == 2);
        Assert.Contains(notifications, n => n.UserId == 3);
        Assert.DoesNotContain(notifications, n => n.UserId == 1);
        Assert.All(notifications, n => Assert.Equal(NotificationType.ProjectUpdate, n.Type));
    }

    private static DocumentService CreateService(ApplicationDbContext context, RecordingStorage storage) =>
        new(context, storage, new DeterministicMalwareScanner(),
            new TestAuthorization(context), new DocumentValidationService(),
            NullLogger<DocumentService>.Instance);

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestAuthorization(ApplicationDbContext context) : IDocumentAuthorizationService
    {
        public Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(1);
        public IQueryable<Document> AuthorizedDocuments(int userId, DocumentOperation operation = DocumentOperation.ReadMetadata) =>
            context.Documents.Where(d => d.UploadedByUserId == userId && !d.IsDeleted && d.ScanStatus == DocumentScanStatus.Passed);
        public Task<DocumentAuthorizationDecision> AuthorizeAsync(int userId, int? documentId, DocumentOperation operation, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentAuthorizationDecision(documentId.HasValue));
    }

    private sealed class RecordingStorage : IFileStorage
    {
        public bool ThrowOnCommit { get; init; }
        public int Created { get; private set; }
        public List<string> Deleted { get; } = new();
        public Task<TemporaryStorageHandle> CreateTemporaryAsync(Stream content, CancellationToken cancellationToken = default)
        {
            Created++;
            return Task.FromResult(new TemporaryStorageHandle("tmp-test"));
        }
        public Task<CommittedStorageHandle> CommitAsync(TemporaryStorageHandle temporaryHandle, string opaqueStorageName, CancellationToken cancellationToken = default)
        {
            if (ThrowOnCommit) throw new IOException("test");
            return Task.FromResult(new CommittedStorageHandle(opaqueStorageName));
        }
        public Task<Stream> OpenReadAsync(string opaqueStorageName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());
        public Task<bool> DeleteAsync(string opaqueStorageName, CancellationToken cancellationToken = default)
        {
            Deleted.Add(opaqueStorageName);
            return Task.FromResult(true);
        }
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("test");
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content);
        public long ExpectedLength => content.LongLength;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }

    private sealed class FailingSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("test persistence failure");
    }
}


