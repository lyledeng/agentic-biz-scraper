namespace BizScraper.Api.Domain.Entities;

/// <summary>
/// Audit trail entry recorded for each generic script execution via the execute-script endpoint.
/// </summary>
public sealed class ScriptExecutionAuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string DefinitionSlug { get; init; }

    public required string CorrelationId { get; init; }

    public string? InputParameters { get; init; }

    public long DurationMs { get; init; }

    public required string Status { get; init; }

    public string? ErrorDetail { get; init; }

    public string? DiagnosticsUrl { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Authenticated caller identity: UPN for delegated tokens, app client ID for client-credentials tokens.
    /// </summary>
    public string? CallerIdentity { get; init; }
}
