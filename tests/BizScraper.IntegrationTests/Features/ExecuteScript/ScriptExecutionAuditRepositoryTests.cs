using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.IntegrationTests.Features.ExecuteScript;

public sealed class ScriptExecutionAuditRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ScriptExecutionAuditRepositoryTests()
    {
        _connectionString = $"Data Source=ScriptExecTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ScriptExecutionAuditEntries (
                Id TEXT NOT NULL PRIMARY KEY,
                DefinitionSlug TEXT NOT NULL,
                CorrelationId TEXT NOT NULL,
                InputParameters TEXT NULL,
                DurationMs INTEGER NOT NULL,
                Status TEXT NOT NULL,
                ErrorDetail TEXT NULL,
                DiagnosticsUrl TEXT NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                CallerIdentity TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task SaveAsync_InsertsAllFields()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:AuditTrail"] = _connectionString })
            .Build();
        var repository = new ScriptExecutionAuditRepository(configuration, NullLogger<ScriptExecutionAuditRepository>.Instance);

        var entry = new ScriptExecutionAuditEntry
        {
            DefinitionSlug = "us-co-business-search",
            CorrelationId = "test-correlation-1",
            InputParameters = """{"searchTerm":"Acme"}""",
            DurationMs = 2500,
            Status = "Success",
            DiagnosticsUrl = "https://blob.test/diag/test-correlation-1"
        };

        await repository.SaveAsync(entry, CancellationToken.None);

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ScriptExecutionAuditEntries WHERE CorrelationId = 'test-correlation-1'";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SaveAsync_PersistsIowaDefinitionSlug()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:AuditTrail"] = _connectionString })
            .Build();
        var repository = new ScriptExecutionAuditRepository(configuration, NullLogger<ScriptExecutionAuditRepository>.Instance);

        var entry = new ScriptExecutionAuditEntry
        {
            DefinitionSlug = "us-ia-entity-details",
            CorrelationId = "test-ia-audit-1",
            InputParameters = """{"uniqueKey":"abc"}""",
            DurationMs = 321,
            Status = "Success"
        };

        await repository.SaveAsync(entry, CancellationToken.None);

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT DefinitionSlug FROM ScriptExecutionAuditEntries WHERE CorrelationId = 'test-ia-audit-1'";
        var slug = Convert.ToString(await command.ExecuteScalarAsync());
        Assert.Equal("us-ia-entity-details", slug);
    }

    [Fact]
    public async Task SaveAsync_GracefullySkips_WhenConnectionStringMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var repository = new ScriptExecutionAuditRepository(configuration, NullLogger<ScriptExecutionAuditRepository>.Instance);

        var entry = new ScriptExecutionAuditEntry
        {
            DefinitionSlug = "test-slug",
            CorrelationId = "test-skip-1",
            DurationMs = 100,
            Status = "Success"
        };

        var exception = await Record.ExceptionAsync(() => repository.SaveAsync(entry, CancellationToken.None));
        Assert.Null(exception);
    }

    [Fact]
    public async Task SaveAsync_HandlesNullableFields_WithDbNull()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:AuditTrail"] = _connectionString })
            .Build();
        var repository = new ScriptExecutionAuditRepository(configuration, NullLogger<ScriptExecutionAuditRepository>.Instance);

        var entry = new ScriptExecutionAuditEntry
        {
            DefinitionSlug = "test-slug",
            CorrelationId = "test-nullable-1",
            DurationMs = 500,
            Status = "Error",
            InputParameters = null,
            ErrorDetail = null,
            DiagnosticsUrl = null
        };

        await repository.SaveAsync(entry, CancellationToken.None);

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT InputParameters FROM ScriptExecutionAuditEntries WHERE CorrelationId = 'test-nullable-1'";
        var result = await command.ExecuteScalarAsync();
        Assert.Equal(DBNull.Value, result);
    }
}
