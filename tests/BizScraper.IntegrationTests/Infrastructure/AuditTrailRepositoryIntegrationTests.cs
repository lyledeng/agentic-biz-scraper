using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.IntegrationTests.Infrastructure;

public sealed class AuditTrailRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public AuditTrailRepositoryIntegrationTests()
    {
        _connectionString = "Data Source=AuditTrailIntegrationTest;Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS SearchAuditEntries (
                Id TEXT NOT NULL PRIMARY KEY,
                SearchTerm TEXT NOT NULL,
                ResultCount INTEGER NOT NULL,
                Truncated INTEGER NOT NULL,
                PagesScraped INTEGER NOT NULL,
                DurationMs INTEGER NOT NULL,
                Status TEXT NOT NULL,
                ErrorDetail TEXT NULL,
                CreatedAt TEXT NOT NULL,
                CorrelationId TEXT NOT NULL,
                DiagnosticsUrl TEXT NULL,
                State TEXT NULL,
                WarningMessage TEXT NULL,
                CallerIdentity TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task SaveAsync_InsertsRow()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AuditTrail"] = _connectionString
            })
            .Build();
        var repository = new AuditTrailRepository(configuration, NullLogger<AuditTrailRepository>.Instance);

        await repository.SaveAsync(
            new SearchAuditEntry
            {
                SearchTerm = "Acme",
                ResultCount = 2,
                Truncated = false,
                PagesScraped = 1,
                DurationMs = 100,
                Status = "Success",
                CorrelationId = "test-correlation",
                DiagnosticsUrl = "https://example.test/diagnostics/test-correlation"
            },
            CancellationToken.None);

        using var verifyCommand = _connection.CreateCommand();
        verifyCommand.CommandText = "SELECT COUNT(*), MAX(DiagnosticsUrl) FROM SearchAuditEntries";
        using var reader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("https://example.test/diagnostics/test-correlation", reader.GetString(1));
    }
}
