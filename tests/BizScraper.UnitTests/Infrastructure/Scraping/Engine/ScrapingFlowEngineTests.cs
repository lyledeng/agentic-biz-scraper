using BizScraper.Api.Features.BusinessSearch.Exceptions;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Actions;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using NSubstitute;
using Polly;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class ScrapingFlowEngineTests
{
    private static FlowDefinitionV2 CreateMinimalDefinition() =>
        new(
            SchemaVersion: "1.0.0",
            Metadata: new FlowMetadata { Id = "test", Name = "test-flow", State = "CO", Endpoint = "business-search" },
            Environment: null,
            Browser: null,
            Variables: [],
            Actions: [new FlowActionV2 { Type = "check-text" }],
            Scripts: null,
            Output: new OutputDeclarationV2 { VariableName = "result", Type = "string" });

    private static ActionContext CreateStubContext()
    {
        var page = Substitute.For<IPage>();
        var browserContext = Substitute.For<IBrowserContext>();
        return new ActionContext(
            page,
            browserContext,
            correlationId: "test-correlation",
            blobPrefix: "test-correlation",
            captureScreenshot: (_, _, _, _, _) => Task.CompletedTask);
    }

    private static ConditionEvaluator CreateConditionEvaluator()
    {
        var targetResolver = new TargetResolver(NullLogger<TargetResolver>.Instance);
        return new ConditionEvaluator(targetResolver);
    }

    [Fact]
    public async Task ExecuteAsync_CheckTextMatchException_PropagatesWithoutWrapping()
    {
        var thrown = new CheckTextMatchException("exceeded-record-count", "Too many results");

        var handler = Substitute.For<IActionHandler>();
        handler.ActionType.Returns("check-text");
        handler.ExecuteAsync(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw thrown);

        var engine = new ScrapingFlowEngine(
            [handler],
            CreateConditionEvaluator(),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            NullLogger<ScrapingFlowEngine>.Instance);

        var ex = await Assert.ThrowsAsync<CheckTextMatchException>(
            () => engine.ExecuteAsync(CreateMinimalDefinition(), CreateStubContext(), CancellationToken.None));

        Assert.Same(thrown, ex);
    }

    [Fact]
    public async Task ExecuteAsync_GenericException_WrappedInScrapingException()
    {
        var inner = new InvalidOperationException("Something broke");

        var handler = Substitute.For<IActionHandler>();
        handler.ActionType.Returns("check-text");
        handler.ExecuteAsync(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw inner);

        var engine = new ScrapingFlowEngine(
            [handler],
            CreateConditionEvaluator(),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            NullLogger<ScrapingFlowEngine>.Instance);

        var ex = await Assert.ThrowsAsync<ScrapingException>(
            () => engine.ExecuteAsync(CreateMinimalDefinition(), CreateStubContext(), CancellationToken.None));

        Assert.Contains("test-flow", ex.Message, StringComparison.Ordinal);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public async Task PollyRetryPredicate_DoesNotRetry_ExceededRecordCountException()
    {
        // Replicate the same ShouldHandle predicate used in JsonDrivenSearchScraper.BuildPipeline
        var invocationCount = 0;

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<string>()
                    .Handle<PlaywrightException>()
                    .Handle<InvalidOperationException>()
            })
            .Build();

        var ex = await Assert.ThrowsAsync<ExceededRecordCountException>(async () =>
            await pipeline.ExecuteAsync<string>(_ =>
            {
                invocationCount++;
                throw new ExceededRecordCountException("Too many results");
            }, CancellationToken.None));

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public async Task ExecuteAsync_SensitiveVariables_PopulatesSensitiveVariableNames()
    {
        var definition = new FlowDefinitionV2(
            SchemaVersion: "1.0.0",
            Metadata: new FlowMetadata { Id = "test", Name = "test-flow", State = "CO", Endpoint = "business-search" },
            Environment: null,
            Browser: null,
            Variables: [
                new VariableV2 { Name = "query", Source = "runtime", Sensitive = false },
                new VariableV2 { Name = "password", Source = "runtime", Sensitive = true },
                new VariableV2 { Name = "apiKey", Source = "runtime", Sensitive = true },
                new VariableV2 { Name = "plain", Source = "runtime" }
            ],
            Actions: [new FlowActionV2 { Type = "check-text" }],
            Scripts: null,
            Output: new OutputDeclarationV2 { VariableName = "result", Type = "string" });

        ActionContext? capturedContext = null;
        var handler = Substitute.For<IActionHandler>();
        handler.ActionType.Returns("check-text");
        handler.ExecuteAsync(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.ArgAt<ActionContext>(0);
                return Task.CompletedTask;
            });

        var engine = new ScrapingFlowEngine(
            [handler],
            CreateConditionEvaluator(),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            NullLogger<ScrapingFlowEngine>.Instance);

        await engine.ExecuteAsync(definition, CreateStubContext(), CancellationToken.None);

        Assert.NotNull(capturedContext);
        Assert.Contains("password", capturedContext!.SensitiveVariableNames);
        Assert.Contains("apiKey", capturedContext.SensitiveVariableNames);
        Assert.DoesNotContain("query", capturedContext.SensitiveVariableNames);
        Assert.DoesNotContain("plain", capturedContext.SensitiveVariableNames);
    }

    [Fact]
    public async Task ExecuteAsync_NullSensitive_ExcludedFromSensitiveSet()
    {
        var definition = new FlowDefinitionV2(
            SchemaVersion: "1.0.0",
            Metadata: new FlowMetadata { Id = "test", Name = "test-flow", State = "CO", Endpoint = "business-search" },
            Environment: null,
            Browser: null,
            Variables: [
                new VariableV2 { Name = "normal", Source = "runtime", Sensitive = null },
                new VariableV2 { Name = "secret", Source = "runtime", Sensitive = true }
            ],
            Actions: [new FlowActionV2 { Type = "check-text" }],
            Scripts: null,
            Output: new OutputDeclarationV2 { VariableName = "result", Type = "string" });

        ActionContext? capturedContext = null;
        var handler = Substitute.For<IActionHandler>();
        handler.ActionType.Returns("check-text");
        handler.ExecuteAsync(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.ArgAt<ActionContext>(0);
                return Task.CompletedTask;
            });

        var engine = new ScrapingFlowEngine(
            [handler],
            CreateConditionEvaluator(),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            NullLogger<ScrapingFlowEngine>.Instance);

        await engine.ExecuteAsync(definition, CreateStubContext(), CancellationToken.None);

        Assert.NotNull(capturedContext);
        Assert.DoesNotContain("normal", capturedContext!.SensitiveVariableNames);
        Assert.Contains("secret", capturedContext.SensitiveVariableNames);
    }

    [Fact]
    public async Task ExecuteActionsAsync_NullActionId_LogsNoIdFallback()
    {
        var collectingLogger = new CollectingLogger();

        var handler = Substitute.For<IActionHandler>();
        handler.ActionType.Returns("check-text");
        handler.ExecuteAsync(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        handler.GetLogDetails(Arg.Any<ActionContext>(), Arg.Any<FlowActionV2>()).Returns((string?)null);

        var engine = new ScrapingFlowEngine(
            [handler],
            CreateConditionEvaluator(),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            collectingLogger);

        var actions = new List<FlowActionV2> { new() { Type = "check-text", Id = null, Description = null } };
        var context = CreateStubContext();
        await engine.ExecuteActionsAsync(actions, context, CancellationToken.None);

        var startedEntry = collectingLogger.Entries.First(e => e.Message.Contains("Action started", StringComparison.Ordinal));
        Assert.Contains("(no id)", startedEntry.Message, StringComparison.Ordinal);
        Assert.Contains("(no description)", startedEntry.Message, StringComparison.Ordinal);
    }

    private sealed class CollectingLogger : ILogger<ScrapingFlowEngine>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
