using ContosoDashboard.Models;

namespace ContosoDashboard.Tests.Services;

public class DocumentDashboardTests
{
    [Fact]
    public async Task Dashboard_recent_documents_and_counts_are_scoped_to_current_user()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "one@test", DisplayName = "One" },
            new User { UserId = 2, Email = "two@test", DisplayName = "Two" });
        db.Documents.AddRange(
            DocumentTestData.Document(1, 1, "Newest", DateTime.UtcNow),
            DocumentTestData.Document(2, 1, "Older", DateTime.UtcNow.AddDays(-1)),
            DocumentTestData.Document(3, 2, "Other user's", DateTime.UtcNow.AddDays(1)),
            new Document { DocumentId = 4, UploadedByUserId = 1, Title = "Rejected", Category = "Reports",
                StorageName = "rejected", OriginalFileName = "x", FileSize = 1, FileType = "text/plain",
                ScanStatus = DocumentScanStatus.Rejected });
        await db.SaveChangesAsync();
        var dashboard = new DashboardService(db);

        var recent = await dashboard.GetRecentDocumentsAsync(1, 5);
        var summary = await dashboard.GetDashboardSummaryAsync(1);

        Assert.Equal(["Newest", "Older"], recent.Select(d => d.Title));
        Assert.Equal(2, summary.DocumentCount);
    }

    [Fact]
    public async Task Notifications_are_user_scoped_and_unread_count_is_accurate()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "one@test", DisplayName = "One" },
            new User { UserId = 2, Email = "two@test", DisplayName = "Two" });
        db.Notifications.AddRange(
            new Notification { NotificationId = 1, UserId = 1, Title = "Document", Message = "Shared", Type = NotificationType.DocumentShared },
            new Notification { NotificationId = 2, UserId = 1, Title = "Read", Message = "Read", Type = NotificationType.DocumentShared, IsRead = true },
            new Notification { NotificationId = 3, UserId = 2, Title = "Other", Message = "Other", Type = NotificationType.DocumentShared });
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        Assert.Single(await service.GetUserNotificationsAsync(1, unreadOnly: true));
        Assert.Equal(1, await service.GetUnreadCountAsync(1));
    }
}
