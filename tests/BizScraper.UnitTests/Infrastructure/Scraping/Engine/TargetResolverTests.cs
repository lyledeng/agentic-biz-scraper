using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NSubstitute;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class TargetResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesAgentResolver_WhenSelectorsAreExhausted()
    {
        var logger = Substitute.For<ILogger<TargetResolver>>();
        var agentResolver = Substitute.For<IAgentTargetResolver>();
        agentResolver.ResolveTargetAsync(Arg.Any<BrowserObservation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentTargetResolutionResult(
                true,
                "#fallback",
                null,
                null,
                null,
                AgentTargetResolutionAction.Click,
                "fallback")));

        var page = Substitute.For<IPage>();
        var initialLocator = Substitute.For<ILocator>();
        initialLocator.CountAsync().Returns(Task.FromResult(0));
        var fallbackLocator = Substitute.For<ILocator>();
        fallbackLocator.First.Returns(fallbackLocator);

        page.Locator(".missing").Returns(initialLocator);
        page.Locator("#fallback").Returns(fallbackLocator);

        var resolver = new TargetResolver(logger, agentResolver);
        List<SelectorEntry> selectors = [new("css", ".missing", 1.0)];
        var target = new ActionTarget(selectors, "Search box");

        var result = await resolver.ResolveAsync(target, page, [], 1000, CancellationToken.None);

        Assert.Same(fallbackLocator, result);
        string[] attempted = ["css=.missing"];
        await agentResolver.Received(1).ResolveTargetAsync(
            Arg.Is<BrowserObservation>(observation =>
                observation.ActionType == BrowserActionType.Click &&
                observation.TargetDescription == "Search box" &&
                observation.AttemptedSelectors.SequenceEqual(attempted)),
            Arg.Any<CancellationToken>());
    }
}
