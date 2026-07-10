namespace BizScraper.Api.Domain.Entities;

/// <summary>
/// Audit trail entry recorded for each entity detail scraping operation.
/// </summary>
public sealed class EntityDetailAuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string DetailsUrl { get; init; }

    public string? EntityIdNumber { get; init; }

    public string? EntityName { get; init; }

    public string? EntityStatus { get; init; }

    public string? CertificateUrl { get; init; }

    public string? DiagnosticsUrl { get; init; }

    public string? DocumentStorageUrl { get; init; }

    public long DurationMs { get; init; }

    public required string Status { get; init; }

    public string? ErrorDetail { get; init; }

    public required string CorrelationId { get; init; }

    public string? StateCode { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
