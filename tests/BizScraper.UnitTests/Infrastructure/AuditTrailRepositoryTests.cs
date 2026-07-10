using System.Data;
using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Infrastructure;

public sealed class AuditTrailRepositoryTests
{
    [Fact]
    public void ConfigureSaveCommand_AddsDiagnosticsUrlParameter()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();

        AuditTrailRepository.ConfigureSaveCommand(
            command,
            new SearchAuditEntry
            {
                SearchTerm = "Acme",
                ResultCount = 1,
                Truncated = false,
                PagesScraped = 1,
                DurationMs = 25,
                Status = "Success",
                CorrelationId = "req-1",
                DiagnosticsUrl = "https://example.test/diagnostics/req-1"
            });

        Assert.Equal(CommandType.Text, command.CommandType);
        Assert.Contains("DiagnosticsUrl", command.CommandText, StringComparison.Ordinal);
        Assert.Equal("https://example.test/diagnostics/req-1", command.Parameters["$DiagnosticsUrl"].Value);
    }

    [Fact]
    public async Task SaveAsync_DoesNotThrow_WhenConnectionStringMissing()
    {
        var repository = new AuditTrailRepository(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(), NullLogger<AuditTrailRepository>.Instance);

        await repository.SaveAsync(
            new SearchAuditEntry
            {
                SearchTerm = "Acme",
                Status = "Success",
                CorrelationId = "test"
            },
            CancellationToken.None);
    }

    [Fact]
    public void ConfigureSaveCommand_AddsCallerIdentityParameter()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();

        AuditTrailRepository.ConfigureSaveCommand(
            command,
            new SearchAuditEntry
            {
                SearchTerm = "Acme",
                ResultCount = 1,
                Truncated = false,
                PagesScraped = 1,
                DurationMs = 25,
                Status = "Success",
                CorrelationId = "req-1",
                CallerIdentity = "user@contoso.com"
            });

        Assert.Contains("CallerIdentity", command.CommandText, StringComparison.Ordinal);
        Assert.Equal("user@contoso.com", command.Parameters["$CallerIdentity"].Value);
    }

    [Fact]
    public void ConfigureSaveCommand_SetsCallerIdentityToDBNull_WhenNull()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();

        AuditTrailRepository.ConfigureSaveCommand(
            command,
            new SearchAuditEntry
            {
                SearchTerm = "Acme",
                ResultCount = 0,
                Truncated = false,
                PagesScraped = 0,
                DurationMs = 10,
                Status = "Success",
                CorrelationId = "req-2",
                CallerIdentity = null
            });

        Assert.Equal(DBNull.Value, command.Parameters["$CallerIdentity"].Value);
    }
}
