using Markdig;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Pdf;

/// <summary>
/// Converts markdown text to PDF using a Playwright-rendered HTML intermediate.
/// </summary>
public sealed class MarkdownToPdfConverter : IMarkdownToPdfConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string ConvertMarkdownToHtml(string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markdown);
        return Markdown.ToHtml(markdown, Pipeline);
    }

    public static string WrapInHtmlDocument(string bodyHtml) =>
        "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\" />\n<style>\n" +
        "body { font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; line-height: 1.6; color: #333; }\n" +
        "h1 { font-size: 1.8em; border-bottom: 2px solid #0078d4; padding-bottom: 6px; }\n" +
        "h2 { font-size: 1.4em; margin-top: 1.5em; }\n" +
        "h3 { font-size: 1.1em; }\n" +
        "table { border-collapse: collapse; width: 100%; margin: 1em 0; }\n" +
        "th, td { border: 1px solid #ccc; padding: 8px 12px; text-align: left; }\n" +
        "th { background-color: #f5f5f5; font-weight: 600; }\n" +
        "tr:nth-child(even) { background-color: #fafafa; }\n" +
        "ul, ol { margin: 0.5em 0; padding-left: 1.5em; }\n" +
        "p { margin: 0.5em 0; }\n" +
        "</style>\n</head>\n<body>\n" + bodyHtml + "\n</body>\n</html>";

    public async Task<byte[]> ConvertAsync(string markdown, IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bodyHtml = ConvertMarkdownToHtml(markdown);
        var fullHtml = WrapInHtmlDocument(bodyHtml);

        await page.SetContentAsync(fullHtml, new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
            Margin = new Margin { Top = "20mm", Bottom = "20mm", Left = "15mm", Right = "15mm" }
        });
    }
}
