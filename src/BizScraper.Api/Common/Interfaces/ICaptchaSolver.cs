namespace BizScraper.Api.Common.Interfaces;

/// <summary>
/// Abstraction for solving CAPTCHA challenges via an external AI service.
/// </summary>
public interface ICaptchaSolver
{
    Task<string> SolveAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
