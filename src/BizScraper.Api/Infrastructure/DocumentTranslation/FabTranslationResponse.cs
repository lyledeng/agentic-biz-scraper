using System.Text.Json.Serialization;

namespace BizScraper.Api.Infrastructure.DocumentTranslation;

/// <summary>
/// Response from the FAB document translation API.
/// </summary>
public sealed record FabTranslationResponse(
    [property: JsonPropertyName("output")] FabTranslationOutput? Output);

/// <summary>
/// Translation output containing the translated markdown, detected source language, and raw content.
/// </summary>
public sealed record FabTranslationOutput(
    [property: JsonPropertyName("translatedMarkdown")] string? TranslatedMarkdown,
    [property: JsonPropertyName("sourceLanguage")] string? SourceLanguage,
    [property: JsonPropertyName("content")] string? Content);
