using System.Text.Json;
using System.Text.RegularExpressions;
using BizScraper.Api.Common;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Features.BusinessSearch.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Search;

/// <summary>
/// Maps Wyoming (US-WY) business-search output to unified search results.
/// Also consolidates the raw-text block parsing logic previously in WyomingResultPostProcessor.
/// </summary>
internal sealed partial class WySearchResultMapper : ISearchResultMapper
{
    public string SlugPrefix => "us-wy";

    public UnifiedSearchResult[]? Map(JsonElement output)
    {
        var items = GetResultsArray(output);
        var results = new List<UnifiedSearchResult>();

        foreach (var item in items.EnumerateArray())
        {
            // Raw block format: { text, detailsUrl } — parse into structured result
            var blockText = item.GetStringOrDefault("text");
            var detailsUrl = item.GetStringOrDefault("detailsUrl");
            var parsed = ParseResultBlock(blockText, detailsUrl);
            if (parsed is null)
            {
                continue;
            }

            results.Add(new UnifiedSearchResult
            {
                Name = parsed.Name,
                Identifier = parsed.Identifier,
                Status = parsed.Status,
                EntityType = parsed.EntityType,
                FormationDate = parsed.FormationDate,
                State = "WY",
                UniqueKey = UniqueKeyEncoder.Encode("US-WY", new Dictionary<string, string>
                {
                    ["detailsUrl"] = parsed.DetailsUrl
                }),
                StandingTax = parsed.StandingTax ?? string.Empty,
                StandingRA = parsed.StandingRA ?? string.Empty
            });
        }

        return [.. results];
    }

    // ── Raw-text block parser (consolidated from WyomingResultPostProcessor) ──

    /// <summary>
    /// Parse a WY raw text block + detailsUrl into a NormalizedSearchResult.
    /// </summary>
    internal static NormalizedSearchResult? ParseResultBlock(string blockText, string detailsUrl)
    {
        var normalizedText = NormalizeWhitespaceRegex().Replace(blockText ?? string.Empty, " ").Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return null;
        }

        var headerText = SliceBeforeFirstLabel(normalizedText, ["Status:", "Standing - Tax:", "Standing - RA:", "Filed On:"]);
        var dashIndex = headerText.LastIndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex <= 0)
        {
            return null;
        }

        var name = headerText[..dashIndex].Trim();
        var remainder = headerText[(dashIndex + 3)..].Trim();
        var parenOpen = remainder.LastIndexOf('(');
        var parenClose = remainder.LastIndexOf(')');

        string filingNumber;
        string entityType;

        if (parenOpen >= 0 && parenClose > parenOpen)
        {
            filingNumber = remainder[..parenOpen].Trim();
            entityType = remainder[(parenOpen + 1)..parenClose].Trim();
        }
        else
        {
            filingNumber = remainder;
            entityType = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(filingNumber))
        {
            return null;
        }

        var status = ExtractLabelValue(normalizedText, "Status:", ["Standing - Tax:", "Standing - RA:", "Filed On:"]);
        var standingTax = ExtractLabelValue(normalizedText, "Standing - Tax:", ["Standing - RA:", "Filed On:"]);
        var standingRA = ExtractLabelValue(normalizedText, "Standing - RA:", ["Filed On:"]);
        var filedOn = ExtractLabelValue(normalizedText, "Filed On:", []);

        return new NormalizedSearchResult(
            name,
            filingNumber,
            status,
            entityType,
            filedOn,
            detailsUrl,
            "WY",
            StandingTax: string.IsNullOrWhiteSpace(standingTax) ? null : standingTax,
            StandingRA: string.IsNullOrWhiteSpace(standingRA) ? null : standingRA);
    }

    private static string SliceBeforeFirstLabel(string value, IReadOnlyList<string> labels)
    {
        var firstIndex = labels
            .Select(label => value.IndexOf(label, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        return firstIndex >= 0 ? value[..firstIndex].Trim() : value;
    }

    private static string ExtractLabelValue(string value, string label, IReadOnlyList<string> stopLabels)
    {
        var startIndex = value.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += label.Length;
        var remaining = value[startIndex..];

        var endIndex = stopLabels
            .Select(stopLabel => remaining.IndexOf(stopLabel, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        return (endIndex >= 0 ? remaining[..endIndex] : remaining).Trim();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex NormalizeWhitespaceRegex();

    private sealed record WyomingRawBlock(
        string Text,
        string DetailsUrl);

    // ── Shared helper ──

    private static JsonElement GetResultsArray(JsonElement output)
    {
        if (output.ValueKind == JsonValueKind.Array)
        {
            return output;
        }

        if (output.ValueKind == JsonValueKind.Object && output.TryGetProperty("results", out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            return nested;
        }

        return JsonSerializer.SerializeToElement(Array.Empty<object>());
    }
}
