using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Pdf;

/// <summary>
/// Abstraction for converting markdown text to a PDF document.
/// </summary>
public interface IMarkdownToPdfConverter
{
    Task<byte[]> ConvertAsync(string markdown, IPage page, CancellationToken cancellationToken);
}
