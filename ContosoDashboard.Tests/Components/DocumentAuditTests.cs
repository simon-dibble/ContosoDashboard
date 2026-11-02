namespace ContosoDashboard.Tests.Components;

public class DocumentAuditTests
{
    [Fact]
    public void Audit_navigation_is_administrator_only_and_page_is_authorized()
    {
        var nav = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Shared", "NavMenu.razor"));
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "DocumentAudit.razor"));
        Assert.Contains("<AuthorizeView Roles=\"Administrator\">", nav);
        Assert.Contains("documents/audit", nav);
        Assert.Contains("@attribute [Authorize(Roles = \"Administrator\")]", page);
        Assert.Contains("Audit data is unavailable or access was denied.", page);
    }
}
