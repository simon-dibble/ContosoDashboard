using ContosoDashboard.Models;
using ContosoDashboard.Data;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Data;

public class DocumentSchemaTests
{
    [Fact]
    public void Document_uses_integer_identifier_and_text_category()
    {
        var document = new Document { Title = "t", Category = "Other", FileSize = 1 };
        Assert.IsType<int>(document.DocumentId);
        Assert.IsType<string>(document.Category);
        Assert.Equal(DocumentScanStatus.Pending, document.ScanStatus);
    }

    [Fact]
    public void Document_model_exposes_required_indexes_and_unique_storage_name()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SchemaInspection;Trusted_Connection=True;")
            .Options;
        using var context = new ApplicationDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Document))!;
        var indexes = entity.GetIndexes().Select(index => string.Join(",", index.Properties.Select(p => p.Name))).ToHashSet();

        Assert.Contains("UploadedByUserId,UploadedAt", indexes);
        Assert.Contains("ProjectId,UploadedAt", indexes);
        Assert.Contains("Category,UploadedAt", indexes);
        Assert.Contains("ScanStatus,UploadedAt", indexes);
        Assert.True(entity.GetIndexes().Single(i => i.Properties.Count == 1 && i.Properties[0].Name == "StorageName").IsUnique);
        var share = context.Model.FindEntityType(typeof(DocumentShare))!;
        Assert.True(share.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "DocumentId", "UserId" })).IsUnique);
        Assert.Equal("[RevokedAt] IS NULL", share.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "DocumentId", "UserId" })).GetFilter());
    }

    [Fact]
    public void Document_schema_has_audit_preserving_relationship()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SchemaInspection;Trusted_Connection=True;")
            .Options;
        using var context = new ApplicationDbContext(options);
        var foreignKey = context.Model.FindEntityType(typeof(DocumentActivity))!.GetForeignKeys()
            .Single(fk => fk.Properties.Single().Name == "DocumentId");

        Assert.Equal(DeleteBehavior.SetNull, foreignKey.DeleteBehavior);
    }

    [Fact]
    public async Task Scan_state_is_persisted_without_exposing_storage_path()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Documents.Add(new Document
        {
            Title = "Rejected", Category = "Other", StorageName = "opaque-storage",
            OriginalFileName = "rejected.txt", FileSize = 1, FileType = "text/plain",
            UploadedByUserId = 1, ScanStatus = DocumentScanStatus.Rejected
        });
        await context.SaveChangesAsync();

        var stored = await context.Documents.SingleAsync();
        Assert.Equal(DocumentScanStatus.Rejected, stored.ScanStatus);
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), stored.StorageName);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar.ToString(), stored.StorageName);
    }
}
