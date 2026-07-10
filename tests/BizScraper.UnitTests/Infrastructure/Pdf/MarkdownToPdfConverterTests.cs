using BizScraper.Api.Infrastructure.Pdf;

namespace BizScraper.UnitTests.Infrastructure.Pdf;

public sealed class MarkdownToPdfConverterTests
{
    [Fact]
    public void ConvertToHtml_SimpleMarkdown_ProducesHtml()
    {
        var html = MarkdownToPdfConverter.ConvertMarkdownToHtml("# Heading\n\nSome **bold** text.");

        Assert.Contains("<h1", html, StringComparison.Ordinal);
        Assert.Contains("Heading", html, StringComparison.Ordinal);
        Assert.Contains("<strong>bold</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToHtml_PipeTable_ProducesTableHtml()
    {
        var markdown = "| Field | Value |\n|-------|-------|\n| Name | Acme |";
        var html = MarkdownToPdfConverter.ConvertMarkdownToHtml(markdown);

        Assert.Contains("<table", html, StringComparison.Ordinal);
        Assert.Contains("Acme", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToHtml_EmptyInput_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => MarkdownToPdfConverter.ConvertMarkdownToHtml(""));

    [Fact]
    public void ConvertToHtml_NullInput_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => MarkdownToPdfConverter.ConvertMarkdownToHtml(null!));

    [Fact]
    public void WrapInHtmlDocument_ProducesCompleteHtmlPage()
    {
        var bodyHtml = "<h1>Test</h1>";
        var document = MarkdownToPdfConverter.WrapInHtmlDocument(bodyHtml);

        Assert.Contains("<!DOCTYPE html>", document, StringComparison.Ordinal);
        Assert.Contains("<html", document, StringComparison.Ordinal);
        Assert.Contains("<head>", document, StringComparison.Ordinal);
        Assert.Contains("<style>", document, StringComparison.Ordinal);
        Assert.Contains("<h1>Test</h1>", document, StringComparison.Ordinal);
    }
}
