using ContosoDashboard.Models;
using ContosoDashboard.Tests.Services;
using System.Diagnostics;

namespace ContosoDashboard.Tests.Performance;

public class DocumentQueryPerformanceTests
{
    [Fact]
    public async Task List_of_authorized_and_inaccessible_records_is_bounded_and_fast()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "user@test", DisplayName = "User" });
        for (var i = 1; i <= 1000; i++)
            db.Documents.Add(DocumentTestData.Document(i, i % 2 == 0 ? 1 : 99, $"Document {i:D4}", DateTime.UtcNow.AddMinutes(-i)));
        await db.SaveChangesAsync();
        var auth = new DocumentTestAuthorization(db, 1);
        var service = DocumentTestData.Service(db, auth);
        var stopwatch = Stopwatch.StartNew();

        var documents = await service.ListAsync(search: "Document", sort: "date");

        stopwatch.Stop();
        Assert.Equal(500, documents.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Query took {stopwatch.Elapsed}.");
    }
}
