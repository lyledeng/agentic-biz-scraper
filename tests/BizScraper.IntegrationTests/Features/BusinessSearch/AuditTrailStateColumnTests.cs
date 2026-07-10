using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.IntegrationTests.Features.BusinessSearch;

public sealed class AuditTrailStateColumnTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public AuditTrailStateColumnTests()
    {
        _connectionString = $"Data Source=StateColumnTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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
    public async Task SaveAsync_WritesStateAsCO_ForColoradoSearch()
    {
        var repository = CreateRepository();

        await repository.SaveAsync(
            new SearchAuditEntry
            {
                SearchTerm = "Acme",
                ResultCount = 1,
                Truncated = false,
                PagesScraped = 1,
                DurationMs = 100,
                Status = "Success",
                CorrelationId = "co-test",
                State = "CO"
            },
            CancellationToken.None);

        var state = await ReadStateAsync("co-test");
        Assert.Equal("CO", state);
    }

    [Fact]
    public async Task SaveAsync_WritesStateAsWY_ForWyomingSearch()
    {
        var repository = CreateRepository();

        await repository.SaveAsync(
            new SearchAuditEntry
            {
                SearchTerm = "Wendy",
                ResultCount = 37,
                Truncated = false,
                PagesScraped = 2,
                DurationMs = 5000,
                Status = "Success",
                CorrelationId = "wy-test",
                State = "WY"
            },
            CancellationToken.None);

        var state = await ReadStateAsync("wy-test");
        Assert.Equal("WY", state);
    }

    [Fact]
    public async Task SaveAsync_WritesNullState_WhenStateNotProvided()
    {
        var repository = CreateRepository();

        await repository.SaveAsync(
            new SearchAuditEntry
            {
                SearchTerm = "Legacy",
                ResultCount = 0,
                Truncated = false,
                PagesScraped = 1,
                DurationMs = 50,
                Status = "Success",
                CorrelationId = "null-state-test"
            },
            CancellationToken.None);

        var state = await ReadStateAsync("null-state-test");
        Assert.Null(state);
    }

    private AuditTrailRepository CreateRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AuditTrail"] = _connectionString
            })
            .Build();
        return new AuditTrailRepository(configuration, NullLogger<AuditTrailRepository>.Instance);
    }

    private async Task<string?> ReadStateAsync(string correlationId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT State FROM SearchAuditEntries WHERE CorrelationId = $CorrelationId";
        command.Parameters.AddWithValue("$CorrelationId", correlationId);
        var result = await command.ExecuteScalarAsync();
        return result is DBNull ? null : (string?)result;
    }
}
