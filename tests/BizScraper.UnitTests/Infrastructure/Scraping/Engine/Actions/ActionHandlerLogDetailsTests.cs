using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Actions;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NSubstitute;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine.Actions;

public sealed class NavigateActionHandlerTests
{
    private readonly NavigateActionHandler _handler = new();

    [Fact]
    public void GetLogDetails_ReturnsUrlAndWaitUntil()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "navigate", Url = "https://example.com", WaitUntil = "domcontentloaded" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("url=https://example.com, waitUntil=domcontentloaded", details);
    }

    [Fact]
    public void GetLogDetails_NullWaitUntil_DefaultsToNetworkIdle()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "navigate", Url = "https://example.com" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("url=https://example.com, waitUntil=networkidle", details);
    }

    [Fact]
    public void GetLogDetails_ResolvesVariablesInUrl()
    {
        var context = CreateContext();
        context.Variables["host"] = "test.com";
        var action = new FlowActionV2 { Type = "navigate", Url = "https://${host}/search" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("url=https://test.com/search, waitUntil=networkidle", details);
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class FillActionHandlerTests
{
    [Fact]
    public void GetLogDetails_ReturnsTargetDescriptionAndValue()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "fill", Target = new ActionTarget([], "Search box"), Value = "Acme Corp" };
        var handler = new FillActionHandler(new TargetResolver(Substitute.For<ILogger<TargetResolver>>()));

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("target=Search box, value=Acme Corp", details);
    }

    [Fact]
    public void GetLogDetails_FallsBackToSelector_WhenNoTarget()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "fill", Selector = "#search", Value = "test" };
        var handler = new FillActionHandler(new TargetResolver(Substitute.For<ILogger<TargetResolver>>()));

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("target=#search, value=test", details);
    }

    [Fact]
    public void GetLogDetails_SensitiveVariable_ShowsCharCount()
    {
        var context = CreateContext();
        context.Variables["password"] = "s3cr3t!";
        context.SensitiveVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "password" };
        var action = new FlowActionV2 { Type = "fill", Target = new ActionTarget([], "Password field"), Value = "${password}" };
        var handler = new FillActionHandler(new TargetResolver(Substitute.For<ILogger<TargetResolver>>()));

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("target=Password field, value=[7 chars]", details);
    }

    [Fact]
    public void GetLogDetails_NonSensitiveVariable_ShowsFullValue()
    {
        var context = CreateContext();
        context.Variables["query"] = "Acme Corp";
        context.SensitiveVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "password" };
        var action = new FlowActionV2 { Type = "fill", Target = new ActionTarget([], "Search box"), Value = "${query}" };
        var handler = new FillActionHandler(new TargetResolver(Substitute.For<ILogger<TargetResolver>>()));

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("target=Search box, value=Acme Corp", details);
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class ClickActionHandlerTests
{
    [Fact]
    public void GetLogDetails_ReturnsTargetAndWaitAfter()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "click", Target = new ActionTarget([], "Submit button"), WaitAfter = "networkidle" };
        var handler = new ClickActionHandler(new TargetResolver(Substitute.For<ILogger<TargetResolver>>()));

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("target=Submit button, waitAfter=networkidle", details);
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class ScreenshotActionHandlerTests
{
    private readonly ScreenshotActionHandler _handler = new();

    [Fact]
    public void GetLogDetails_ReturnsMilestoneAndForce()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "screenshot", Milestone = "after-search", Force = true };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("milestone=after-search, force=True", details);
    }

    [Fact]
    public void GetLogDetails_ForceDefaults_ToFalse()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "screenshot", Milestone = "step-1" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("milestone=step-1, force=False", details);
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class WaitForLoadActionHandlerTests
{
    private readonly WaitForLoadActionHandler _handler = new();

    [Fact]
    public void GetLogDetails_ReturnsState()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "wait-for-load", State = "domcontentloaded" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("state=domcontentloaded", details);
    }

    [Fact]
    public void GetLogDetails_DefaultsToNetworkIdle()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "wait-for-load" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("state=networkidle", details);
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class WaitForConditionActionHandlerTests
{
    private readonly WaitForConditionActionHandler _handler = new();

    [Fact]
    public void GetLogDetails_ReturnsConditionAndTimeout()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "wait-for-condition", Javascript = "() => document.querySelector('.result')", TimeoutMs = 10000 };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("condition=() => document.querySelector('.result'), timeoutMs=10000", details);
    }

    [Fact]
    public void GetLogDetails_DefaultTimeout()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "wait-for-condition", Javascript = "() => true" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("condition=() => true, timeoutMs=5000", details);
    }

    [Fact]
    public void GetCompletionDetails_ReturnsOutcomeMet() =>
        Assert.Equal("outcome=met", _handler.GetCompletionDetails(CreateContext(), new FlowActionV2 { Type = "wait-for-condition" }));

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class ExtractActionHandlerTests
{
    [Fact]
    public void GetLogDetails_ReturnsScriptRefAndStoreAs()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "extract", ScriptRef = "get-results", StoreAs = "results" };
        var handler = new ExtractActionHandler();

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("script=get-results, storeAs=results", details);
    }

    [Fact]
    public void GetLogDetails_InlineScript_WhenNoScriptRef()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "extract", StoreAs = "data" };
        var handler = new ExtractActionHandler();

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("script=(inline), storeAs=data", details);
    }

    [Fact]
    public void GetCompletionDetails_ReturnsResultLength()
    {
        var context = CreateContext();
        context.Variables["results"] = "hello world";
        var action = new FlowActionV2 { Type = "extract", StoreAs = "results" };
        var handler = new ExtractActionHandler();

        var details = handler.GetCompletionDetails(context, action);

        Assert.Equal("resultLength=11", details);
    }

    [Fact]
    public void GetCompletionDetails_NullStoreAs_ReturnsNull()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "extract" };
        var handler = new ExtractActionHandler();

        Assert.Null(handler.GetCompletionDetails(context, action));
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class CheckTextActionHandlerTests
{
    private readonly CheckTextActionHandler _handler = new();

    [Fact]
    public void GetLogDetails_ReturnsPatternAndOnMatch()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "check-text", Pattern = "error", OnMatch = "throw" };

        var details = _handler.GetLogDetails(context, action);

        Assert.Equal("pattern=error, onMatch=throw", details);
    }

    [Fact]
    public void GetCompletionDetails_MatchedTrue()
    {
        var context = CreateContext();
        context.Variables["__checkTextMatched"] = true;
        var action = new FlowActionV2 { Type = "check-text" };

        Assert.Equal("matched=true", _handler.GetCompletionDetails(context, action));
    }

    [Fact]
    public void GetCompletionDetails_MatchedFalse()
    {
        var context = CreateContext();
        context.Variables["__checkTextMatched"] = false;
        var action = new FlowActionV2 { Type = "check-text" };

        Assert.Equal("matched=false", _handler.GetCompletionDetails(context, action));
    }

    [Fact]
    public void GetCompletionDetails_NoVariable_ReturnsFalse()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "check-text" };

        Assert.Equal("matched=false", _handler.GetCompletionDetails(context, action));
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class LoopActionHandlerTests
{
    [Fact]
    public void GetLogDetails_ReturnsMaxIterationsAndAggregateInto()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "loop", MaxIterations = 10, AggregateInto = "allResults" };
        var handler = new LoopActionHandler(Substitute.For<IServiceProvider>(), new ConditionEvaluator(new TargetResolver(Substitute.For<ILogger<TargetResolver>>())), Substitute.For<ILogger<LoopActionHandler>>());

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("maxIterations=10, aggregateInto=allResults", details);
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class CallServiceActionHandlerTests
{
    [Fact]
    public void GetLogDetails_ReturnsServiceInputOutput()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "call-service", ServiceName = "captcha-solver", InputVariable = "captchaImg", OutputVariable = "captchaText" };
        var handler = new CallServiceActionHandler(Substitute.For<BizScraper.Api.Common.Interfaces.ICaptchaSolver>());

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("service=captcha-solver, input=captchaImg, output=captchaText", details);
    }

    [Fact]
    public void GetLogDetails_LogsVariableNamesOnly_NotValues()
    {
        var context = CreateContext();
        context.Variables["captchaText"] = "abc123";
        var action = new FlowActionV2 { Type = "call-service", ServiceName = "captcha-solver", InputVariable = "captchaImg", OutputVariable = "captchaText" };
        var handler = new CallServiceActionHandler(Substitute.For<ICaptchaSolver>());

        var details = handler.GetLogDetails(context, action);

        // Should contain variable names but NOT resolved values
        Assert.Contains("output=captchaText", details, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", details, StringComparison.Ordinal);
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}

public sealed class DownloadActionHandlerTests
{
    [Fact]
    public void GetLogDetails_ReturnsSelectorAndFilenamePattern()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "download", LinkSelector = "a.pdf-link", FilenamePattern = "report.pdf" };
        var handler = new DownloadActionHandler(
            Substitute.For<BizScraper.Api.Common.Interfaces.IBlobStorageClient>(),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<ILogger<DownloadActionHandler>>());

        var details = handler.GetLogDetails(context, action);

        Assert.Equal("selector=a.pdf-link, filenamePattern=report.pdf", details);
    }

    [Fact]
    public void GetCompletionDetails_ReturnsDestination()
    {
        var context = CreateContext();
        context.Variables["downloadPath"] = "https://blob.storage/corr-1/report.pdf";
        var action = new FlowActionV2 { Type = "download", StoreAs = "downloadPath" };
        var handler = new DownloadActionHandler(
            Substitute.For<BizScraper.Api.Common.Interfaces.IBlobStorageClient>(),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<ILogger<DownloadActionHandler>>());

        var details = handler.GetCompletionDetails(context, action);

        Assert.Equal("destination=https://blob.storage/corr-1/report.pdf", details);
    }

    [Fact]
    public void GetCompletionDetails_NullStoreAs_ReturnsNull()
    {
        var context = CreateContext();
        var action = new FlowActionV2 { Type = "download" };
        var handler = new DownloadActionHandler(
            Substitute.For<BizScraper.Api.Common.Interfaces.IBlobStorageClient>(),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<ILogger<DownloadActionHandler>>());

        Assert.Null(handler.GetCompletionDetails(context, action));
    }

    private static ActionContext CreateContext() =>
        new(Substitute.For<IPage>(), Substitute.For<IBrowserContext>(), "corr-1", "blob", (_, _, _, _, _) => Task.CompletedTask);
}
