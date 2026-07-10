namespace BizScraper.Api.Domain.Entities;

/// <summary>
/// Audit trail entry recorded for each business search operation.
/// </summary>
public sealed class SearchAuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string SearchTerm { get; init; }

    public int ResultCount { get; init; }

    public bool Truncated { get; init; }

    public int PagesScraped { get; init; }

    public long DurationMs { get; init; }

    public required string Status { get; init; }

    public string? ErrorDetail { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public required string CorrelationId { get; init; }

    public string? DiagnosticsUrl { get; init; }

    public string? State { get; init; }

    public string? WarningMessage { get; init; }

    /// <summary>
    /// Authenticated caller identity: UPN for delegated tokens, app client ID for client-credentials tokens.
    /// </summary>
    public string? CallerIdentity { get; init; }
}
