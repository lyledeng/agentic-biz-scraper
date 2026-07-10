using System.Text.Json.Serialization;

namespace BizScraper.Api.Features.EntityDetails.Models;

/// <summary>
/// A single history document filing with optional cloud storage URL.
/// </summary>
public sealed record HistoryDocumentResult(
    string EventTitle,
    string Date,
    string? StorageUrl,
    string? FileName,
    string? Error)
{
    [JsonIgnore]
    public string? LocalFilePath { get; init; }
}
