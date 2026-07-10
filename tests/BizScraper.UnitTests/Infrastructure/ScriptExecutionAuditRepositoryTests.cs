using System.Data;
using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace BizScraper.UnitTests.Infrastructure;

public sealed class ScriptExecutionAuditRepositoryTests
{
    [Fact]
    public void ConfigureSaveCommand_AddsCallerIdentityParameter()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();

        ScriptExecutionAuditRepository.ConfigureSaveCommand(
            command,
            new ScriptExecutionAuditEntry
            {
                DefinitionSlug = "us-co-business-search",
                CorrelationId = "req-1",
                DurationMs = 100,
                Status = "Success",
                CallerIdentity = "user@contoso.com"
            });

        Assert.Equal(CommandType.Text, command.CommandType);
        Assert.Contains("CallerIdentity", command.CommandText, StringComparison.Ordinal);
        Assert.Equal("user@contoso.com", command.Parameters["$CallerIdentity"].Value);
    }

    [Fact]
    public void ConfigureSaveCommand_SetsCallerIdentityToDBNull_WhenNull()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();

        ScriptExecutionAuditRepository.ConfigureSaveCommand(
            command,
            new ScriptExecutionAuditEntry
            {
                DefinitionSlug = "us-wy-entity-details",
                CorrelationId = "req-2",
                DurationMs = 50,
                Status = "Success",
                CallerIdentity = null
            });

        Assert.Equal(DBNull.Value, command.Parameters["$CallerIdentity"].Value);
    }
}
