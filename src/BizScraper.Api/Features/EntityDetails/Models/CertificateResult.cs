namespace BizScraper.Api.Features.EntityDetails.Models;

/// <summary>
/// Result of a certificate of good standing retrieval attempt.
/// </summary>
public sealed record CertificateResult(
    bool Available,
    string? StorageUrl,
    string? FileName,
    string? Error);
