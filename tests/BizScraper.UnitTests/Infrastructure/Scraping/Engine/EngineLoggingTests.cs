using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Logging;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class EngineLoggingTests
{
    private readonly CollectingLogger _logger = new();

    [Fact]
    public void ActionStarted_EmitsAtInformationLevel()
    {
        _logger.ActionStarted(0, "navigate", "nav-1", "Go to page", "url=http://test.com", "corr-123");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("nav-1", entry.Message, StringComparison.Ordinal);
        Assert.Contains("corr-123", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionCompleted_EmitsAtInformationLevel_WithElapsedMs()
    {
        _logger.ActionCompleted(0, "navigate", "nav-1", 42, "details", "corr-123");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("42ms", entry.Message, StringComparison.Ordinal);
        Assert.Contains("corr-123", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionSkipped_EmitsAtInformationLevel_WithCondition()
    {
        _logger.ActionSkipped(0, "fill", "fill-1", "element-exists", "corr-456");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("element-exists", entry.Message, StringComparison.Ordinal);
        Assert.Contains("corr-456", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionFailed_EmitsAtErrorLevel_WithCorrelationId()
    {
        _logger.ActionFailed(0, "click", "click-1", "Timeout", "target=btn", "corr-789", new InvalidOperationException("Timeout"));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("corr-789", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Timeout", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RetryAttempt_EmitsAtWarningLevel()
    {
        _logger.RetryAttempt("navigate", "nav-1", 2, 3, 1000);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("2/3", entry.Message, StringComparison.Ordinal);
        Assert.Contains("1000ms", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionStarted_NullDetails_StillEmitsEntry()
    {
        _logger.ActionStarted(0, "navigate", "nav-1", "desc", null, "corr-123");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
    }

    [Fact]
    public void ActionCompleted_NullDetails_StillEmitsEntry()
    {
        _logger.ActionCompleted(0, "navigate", "nav-1", 10, null, "corr-123");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
    }

    private sealed class CollectingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
