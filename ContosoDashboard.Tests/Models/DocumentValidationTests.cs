using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Models;

public class DocumentValidationTests
{
    private readonly DocumentValidationService _sut = new();

    [Fact]
    public void Accepts_all_categories_and_exact_size_boundary()
    {
        foreach (var category in new[] { "Project Documents", "Team Resources", "Personal Files", "Reports", "Presentations", "Other" })
            Assert.True(_sut.ValidateMetadata("A title", null, category, new[] { "one", "Two" }).IsValid);
        Assert.True(_sut.IsAllowedFile("x.pdf", "application/pdf", 25 * 1024 * 1024, out _));
        Assert.False(_sut.IsAllowedFile("x.pdf", "application/pdf", 25 * 1024 * 1024 + 1, out _));
        Assert.True(_sut.IsAllowedFile("x.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 1, out _));
        Assert.True(_sut.IsAllowedFile("x.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 1, out _));
        Assert.True(_sut.IsAllowedFile("x.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", 1, out _));
        Assert.True(_sut.IsAllowedFile("x.txt", "text/plain", 1, out _));
        Assert.True(_sut.IsAllowedFile("x.jpg", "image/jpeg", 1, out _));
        Assert.True(_sut.IsAllowedFile("x.png", "image/png", 1, out _));
        Assert.False(_sut.IsAllowedFile("x.png", "", 1, out _));
    }

    [Fact]
    public void Normalizes_tags_and_rejects_duplicate_or_empty_values()
    {
        Assert.Equal("one, Two", _sut.NormalizeTags(new[] { " one ", "Two" }));
        Assert.Throws<ArgumentException>(() => _sut.NormalizeTags(new[] { "one", "ONE" }));
        Assert.Throws<ArgumentException>(() => _sut.NormalizeTags(new[] { " ", "\t" }));
    }

    [Fact]
    public void Rejects_unsafe_file_names_and_invalid_dates()
    {
        Assert.False(_sut.IsAllowedFile("../x.pdf", "application/pdf", 1, out _));
        Assert.False(_sut.IsValidDateRange(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
        Assert.False(_sut.ValidateMetadata("", null, "Other", null).IsValid);
        Assert.Equal(DateTimeKind.Utc, new ContosoDashboard.Models.Document().UploadedAt.Kind);
    }
}
