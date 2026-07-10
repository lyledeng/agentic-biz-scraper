using System.Text.Json.Serialization;

namespace BizScraper.Api.Infrastructure.CaptchaSolving;

/// <summary>
/// Request payload for the FAB CAPTCHA solver API.
/// </summary>
public sealed record FabCaptchaSolverRequest(
	[property: JsonPropertyName("input")] FabCaptchaSolverInput Input);

/// <summary>
/// Input data for CAPTCHA solving containing the image format and base64-encoded data.
/// </summary>
public sealed record FabCaptchaSolverInput(
	[property: JsonPropertyName("format")] string Format,
	[property: JsonPropertyName("data")] string Data);

/// <summary>
/// Response from the FAB CAPTCHA solver API.
/// </summary>
public sealed record FabCaptchaSolverResponse(
	[property: JsonPropertyName("output")] FabCaptchaSolverOutput? Output);

/// <summary>
/// Solved CAPTCHA output containing the recognized text and confidence level.
/// </summary>
public sealed record FabCaptchaSolverOutput(
	[property: JsonPropertyName("captchaText")] string CaptchaText,
	[property: JsonPropertyName("confidence")] string Confidence);
