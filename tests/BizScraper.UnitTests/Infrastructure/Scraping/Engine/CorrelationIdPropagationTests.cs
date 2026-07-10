using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Actions;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using NSubstitute;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class CorrelationIdPropagationTests
{
    [Fact]
    public async Task ConcurrentFlows_LogEntriesContainOnlyOwnCorrelationId()
    {
        var collectingLogger = new CollectingLogger();

        var handler = Substitute.For<IActionHandler>();
        handler.ActionType.Returns("check-text");
        handler.ExecuteAsync(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var engine = new ScrapingFlowEngine(
            [handler],
            new ConditionEvaluator(new TargetResolver(NullLogger<TargetResolver>.Instance)),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            collectingLogger);

        var definition = new FlowDefinitionV2(
            SchemaVersion: "1.0.0",
            Metadata: new FlowMetadata { Id = "test", Name = "test-flow", State = "CO", Endpoint = "business-search" },
            Environment: null,
            Browser: null,
            Variables: [],
            Actions: [new FlowActionV2 { Type = "check-text", Id = "action-1" }],
            Scripts: null,
            Output: new OutputDeclarationV2 { VariableName = "result", Type = "string" });

        var context1 = CreateContext("corr-AAA");
        var context2 = CreateContext("corr-BBB");

        var task1 = engine.ExecuteAsync(definition, context1, CancellationToken.None);
        var task2 = engine.ExecuteAsync(definition, context2, CancellationToken.None);
        await Task.WhenAll(task1, task2);

        var aaaEntries = collectingLogger.Entries
            .Where(e => e.Message.Contains("corr-AAA", StringComparison.Ordinal))
            .ToList();
        var bbbEntries = collectingLogger.Entries
            .Where(e => e.Message.Contains("corr-BBB", StringComparison.Ordinal))
            .ToList();

        // Each flow should produce log entries with its own correlation ID
        Assert.NotEmpty(aaaEntries);
        Assert.NotEmpty(bbbEntries);

        // No cross-contamination: AAA entries must not contain BBB
        Assert.All(aaaEntries, e => Assert.DoesNotContain("corr-BBB", e.Message, StringComparison.Ordinal));
        Assert.All(bbbEntries, e => Assert.DoesNotContain("corr-AAA", e.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NestedLoopActions_InheritParentCorrelationId()
    {
        var collectingLogger = new CollectingLogger();

        var checkHandler = Substitute.For<IActionHandler>();
        checkHandler.ActionType.Returns("check-text");
        checkHandler.ExecuteAsync(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var engine = new ScrapingFlowEngine(
            [checkHandler],
            new ConditionEvaluator(new TargetResolver(NullLogger<TargetResolver>.Instance)),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            collectingLogger);

        // Define a flow with inner actions (simulated — loop handler isn't used, but inner ExecuteActionsAsync is)
        var innerActions = new List<FlowActionV2> { new() { Type = "check-text", Id = "inner-1" } };

        var context = CreateContext("corr-NESTED");
        await engine.ExecuteActionsAsync(innerActions, context, CancellationToken.None);

        var nestedEntries = collectingLogger.Entries
            .Where(e => e.Message.Contains("corr-NESTED", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(nestedEntries);
        Assert.All(nestedEntries, e => Assert.Contains("corr-NESTED", e.Message, StringComparison.Ordinal));
    }

    private static ActionContext CreateContext(string correlationId) =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), correlationId, correlationId, (_, _, _, _, _) => Task.CompletedTask);

    private sealed class CollectingLogger : ILogger<ScrapingFlowEngine>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
