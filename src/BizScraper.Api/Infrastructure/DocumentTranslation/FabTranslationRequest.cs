using System.Text.Json.Serialization;

namespace BizScraper.Api.Infrastructure.DocumentTranslation;

/// <summary>
/// Request payload for the FAB document translation API.
/// </summary>
public sealed record FabTranslationRequest(
    [property: JsonPropertyName("input")] FabTranslationInput Input);

/// <summary>
/// Input data for document translation containing the format and base64-encoded document.
/// </summary>
public sealed record FabTranslationInput(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("data")] string Data);
