using System.Collections.Concurrent;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContosoDashboard.Tests.Services;

public class DocumentConcurrencyTests
{
    [Fact]
    public async Task Repeated_uploads_use_distinct_opaque_storage_names_even_for_duplicate_titles()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        await db.SaveChangesAsync();
        var auth = new DocumentTestAuthorization(db, 1);
        var service = DocumentTestData.Service(db, auth);

        for (var i = 0; i < 12; i++)
            Assert.True((await service.UploadAsync("Same title", null, "Other", null, null,
                [new DocumentUploadRequest("same.txt", "text/plain", new MemoryStream([1, 2]), 2)])).Single().Succeeded);

        var names = db.Documents.Select(d => d.StorageName).ToList();
        Assert.Equal(12, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Single(db.Documents.Select(d => d.Title).Distinct());
    }

    [Fact]
    public async Task Simultaneous_uploads_from_independent_requests_each_get_a_unique_opaque_storage_name()
    {
        var databaseName = Guid.NewGuid().ToString();
        var storage = new ConcurrentMemoryStorage();
        await using (var seed = NewContext(databaseName))
        {
            seed.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
            await seed.SaveChangesAsync();
        }

        var uploadTasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await using var context = NewContext(databaseName);
            var service = new DocumentService(context, storage, new DeterministicMalwareScanner(),
                new DocumentTestAuthorization(context, 1), new DocumentValidationService(), new NotificationService(context),
                NullLogger<DocumentService>.Instance, new DocumentAuditService(context));
            return (await service.UploadAsync($"Concurrent {i}", null, "Other", null, null,
                [new DocumentUploadRequest($"file{i}.txt", "text/plain", new MemoryStream([1, 2, 3]), 3)])).Single();
        });
        var results = await Task.WhenAll(uploadTasks);

        Assert.All(results, r => Assert.True(r.Succeeded, r.Error));
        await using var verify = NewContext(databaseName);
        var names = await verify.Documents.Select(d => d.StorageName).ToListAsync();
        Assert.Equal(10, names.Count);
        Assert.Equal(10, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Concurrent_replacement_requests_leave_the_document_in_a_single_consistent_state_with_no_orphaned_storage()
    {
        var databaseName = Guid.NewGuid().ToString();
        var storage = new ConcurrentMemoryStorage();
        int documentId;
        await using (var seed = NewContext(databaseName))
        {
            seed.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
            await seed.SaveChangesAsync();
            var seedAuth = new DocumentTestAuthorization(seed, 1);
            var seedService = new DocumentService(seed, storage, new DeterministicMalwareScanner(), seedAuth,
                new DocumentValidationService(), new NotificationService(seed), NullLogger<DocumentService>.Instance, new DocumentAuditService(seed));
            var uploaded = (await seedService.UploadAsync("Original", null, "Other", null, null,
                [new DocumentUploadRequest("original.txt", "text/plain", new MemoryStream([1]), 1)])).Single();
            documentId = uploaded.Document!.DocumentId;
        }

        var replaceTasks = Enumerable.Range(0, 2).Select(async i =>
        {
            await using var context = NewContext(databaseName);
            var service = new DocumentService(context, storage, new DeterministicMalwareScanner(),
                new DocumentTestAuthorization(context, 1), new DocumentValidationService(), new NotificationService(context),
                NullLogger<DocumentService>.Instance, new DocumentAuditService(context));
            return await service.ReplaceAsync(documentId,
                new DocumentUploadRequest($"replacement{i}.txt", "text/plain", new MemoryStream([2, 2]), 2));
        });
        var outcomes = await Task.WhenAll(replaceTasks);

        Assert.All(outcomes, o => Assert.True(o.Succeeded, o.Error));
        await using var verify = NewContext(databaseName);
        var document = await verify.Documents.SingleAsync(d => d.DocumentId == documentId);
        // Exactly one storage name survives as "current"; no name is both referenced by the document and deleted.
        Assert.DoesNotContain(document.StorageName, storage.Deleted);
        Assert.True(storage.StillStored(document.StorageName));
    }

    private static ApplicationDbContext NewContext(string databaseName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class ConcurrentMemoryStorage : IFileStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        public ConcurrentBag<string> Deleted { get; } = new();

        public Task<TemporaryStorageHandle> CreateTemporaryAsync(Stream content, CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            var name = "tmp-" + Guid.NewGuid().ToString("N");
            _files[name] = copy.ToArray();
            return Task.FromResult(new TemporaryStorageHandle(name));
        }
        public Task<CommittedStorageHandle> CommitAsync(TemporaryStorageHandle temporaryHandle, string opaqueStorageName, CancellationToken cancellationToken = default)
        {
            _files[opaqueStorageName] = _files[temporaryHandle.Name];
            _files.TryRemove(temporaryHandle.Name, out _);
            return Task.FromResult(new CommittedStorageHandle(opaqueStorageName));
        }
        public Task<Stream> OpenReadAsync(string opaqueStorageName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_files[opaqueStorageName]));
        public Task<bool> DeleteAsync(string opaqueStorageName, CancellationToken cancellationToken = default)
        {
            _files.TryRemove(opaqueStorageName, out _);
            Deleted.Add(opaqueStorageName);
            return Task.FromResult(true);
        }
        public bool StillStored(string opaqueStorageName) => _files.ContainsKey(opaqueStorageName);
    }
}
