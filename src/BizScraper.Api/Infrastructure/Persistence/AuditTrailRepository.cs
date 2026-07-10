using System.Data;
using BizScraper.Api.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace BizScraper.Api.Infrastructure.Persistence;

/// <summary>
/// Persists business search audit trail entries to SQLite.
/// </summary>
public sealed class AuditTrailRepository(IConfiguration configuration, ILogger<AuditTrailRepository> logger)
{
    internal const string InsertSearchAuditEntrySql = """
        INSERT INTO SearchAuditEntries
            (Id, SearchTerm, ResultCount, Truncated, PagesScraped, DurationMs, Status, ErrorDetail, CreatedAt, CorrelationId, DiagnosticsUrl, State, WarningMessage, CallerIdentity)
        VALUES
            ($Id, $SearchTerm, $ResultCount, $Truncated, $PagesScraped, $DurationMs, $Status, $ErrorDetail, $CreatedAt, $CorrelationId, $DiagnosticsUrl, $State, $WarningMessage, $CallerIdentity);
        """;

    public async Task SaveAsync(SearchAuditEntry entry, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("AuditTrail");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogInformation("Audit trail connection string not configured. Skipping audit write for {CorrelationId}.", entry.CorrelationId);
            return;
        }

        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            ConfigureSaveCommand(command, entry);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Audit trail persistence failed for {CorrelationId}.", entry.CorrelationId);
        }
    }

    internal static void ConfigureSaveCommand(SqliteCommand command, SearchAuditEntry entry)
    {
        command.CommandType = CommandType.Text;
        command.CommandText = InsertSearchAuditEntrySql;
        command.Parameters.AddWithValue("$Id", entry.Id.ToString());
        command.Parameters.AddWithValue("$SearchTerm", entry.SearchTerm);
        command.Parameters.AddWithValue("$ResultCount", entry.ResultCount);
        command.Parameters.AddWithValue("$Truncated", entry.Truncated ? 1 : 0);
        command.Parameters.AddWithValue("$PagesScraped", entry.PagesScraped);
        command.Parameters.AddWithValue("$DurationMs", entry.DurationMs);
        command.Parameters.AddWithValue("$Status", entry.Status);
        command.Parameters.AddWithValue("$ErrorDetail", (object?)entry.ErrorDetail ?? DBNull.Value);
        command.Parameters.AddWithValue("$CreatedAt", entry.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("$CorrelationId", entry.CorrelationId);
        command.Parameters.AddWithValue("$DiagnosticsUrl", (object?)entry.DiagnosticsUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$State", (object?)entry.State ?? DBNull.Value);
        command.Parameters.AddWithValue("$WarningMessage", (object?)entry.WarningMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$CallerIdentity", (object?)entry.CallerIdentity ?? DBNull.Value);
    }
}
