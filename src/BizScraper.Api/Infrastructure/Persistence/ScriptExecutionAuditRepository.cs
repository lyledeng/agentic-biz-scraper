using System.Data;
using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Features.ExecuteScript.Logging;
using Microsoft.Data.Sqlite;

namespace BizScraper.Api.Infrastructure.Persistence;

/// <summary>
/// Persists generic script execution audit trail entries to SQLite.
/// </summary>
public sealed class ScriptExecutionAuditRepository(IConfiguration configuration, ILogger<ScriptExecutionAuditRepository> logger)
{
    internal const string InsertSql = """
        INSERT INTO ScriptExecutionAuditEntries
            (Id, DefinitionSlug, CorrelationId, InputParameters, DurationMs, Status, ErrorDetail, DiagnosticsUrl, CreatedAt, CallerIdentity)
        VALUES
            ($Id, $DefinitionSlug, $CorrelationId, $InputParameters, $DurationMs, $Status, $ErrorDetail, $DiagnosticsUrl, $CreatedAt, $CallerIdentity);
        """;

    public async Task SaveAsync(ScriptExecutionAuditEntry entry, CancellationToken cancellationToken)
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
            logger.AuditWriteFailed(entry.CorrelationId, exception);
        }
    }

    internal static void ConfigureSaveCommand(SqliteCommand command, ScriptExecutionAuditEntry entry)
    {
        command.CommandType = CommandType.Text;
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$Id", entry.Id.ToString());
        command.Parameters.AddWithValue("$DefinitionSlug", entry.DefinitionSlug);
        command.Parameters.AddWithValue("$CorrelationId", entry.CorrelationId);
        command.Parameters.AddWithValue("$InputParameters", (object?)entry.InputParameters ?? DBNull.Value);
        command.Parameters.AddWithValue("$DurationMs", entry.DurationMs);
        command.Parameters.AddWithValue("$Status", entry.Status);
        command.Parameters.AddWithValue("$ErrorDetail", (object?)entry.ErrorDetail ?? DBNull.Value);
        command.Parameters.AddWithValue("$DiagnosticsUrl", (object?)entry.DiagnosticsUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$CreatedAt", entry.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("$CallerIdentity", (object?)entry.CallerIdentity ?? DBNull.Value);
    }
}
