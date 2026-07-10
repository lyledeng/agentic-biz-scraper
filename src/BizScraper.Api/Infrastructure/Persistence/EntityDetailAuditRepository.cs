using System.Data;
using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Features.EntityDetails.Logging;
using Microsoft.Data.Sqlite;

namespace BizScraper.Api.Infrastructure.Persistence;

/// <summary>
/// Persists entity detail scraping audit trail entries to SQLite.
/// </summary>
public sealed class EntityDetailAuditRepository(IConfiguration configuration, ILogger<EntityDetailAuditRepository> logger)
{
    internal const string InsertEntityDetailAuditEntrySql = """
        INSERT INTO EntityDetailAuditEntries
            (Id, DetailsUrl, EntityIdNumber, EntityName, EntityStatus, CertificateUrl, DiagnosticsUrl, DocumentStorageUrl, DurationMs, Status, ErrorDetail, CorrelationId, CreatedAt, StateCode)
        VALUES
            ($Id, $DetailsUrl, $EntityIdNumber, $EntityName, $EntityStatus, $CertificateUrl, $DiagnosticsUrl, $DocumentStorageUrl, $DurationMs, $Status, $ErrorDetail, $CorrelationId, $CreatedAt, $StateCode);
        """;

    public async Task SaveAsync(EntityDetailAuditEntry entry, CancellationToken cancellationToken)
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

    internal static void ConfigureSaveCommand(SqliteCommand command, EntityDetailAuditEntry entry)
    {
        command.CommandType = CommandType.Text;
        command.CommandText = InsertEntityDetailAuditEntrySql;
        command.Parameters.AddWithValue("$Id", entry.Id.ToString());
        command.Parameters.AddWithValue("$DetailsUrl", entry.DetailsUrl);
        command.Parameters.AddWithValue("$EntityIdNumber", (object?)entry.EntityIdNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$EntityName", (object?)entry.EntityName ?? DBNull.Value);
        command.Parameters.AddWithValue("$EntityStatus", (object?)entry.EntityStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("$CertificateUrl", (object?)entry.CertificateUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$DiagnosticsUrl", (object?)entry.DiagnosticsUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$DocumentStorageUrl", (object?)entry.DocumentStorageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$DurationMs", entry.DurationMs);
        command.Parameters.AddWithValue("$Status", entry.Status);
        command.Parameters.AddWithValue("$ErrorDetail", (object?)entry.ErrorDetail ?? DBNull.Value);
        command.Parameters.AddWithValue("$CorrelationId", entry.CorrelationId);
        command.Parameters.AddWithValue("$CreatedAt", entry.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("$StateCode", (object?)entry.StateCode ?? DBNull.Value);
    }
}
