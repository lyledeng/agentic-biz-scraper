namespace BizScraper.Api.Features.Documents.Models;

/// <summary>
/// Query for streaming a document from blob storage with conditional caching support.
/// </summary>
public sealed record StreamDocumentQuery(
    string BlobPath,
    string? IfNoneMatch,
    bool ForceDownload);
