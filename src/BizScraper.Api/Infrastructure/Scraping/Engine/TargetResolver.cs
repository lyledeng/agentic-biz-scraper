using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class TargetResolver(
    ILogger<TargetResolver> logger,
    IAgentTargetResolver? agentTargetResolver = null)
{
    private static readonly HashSet<string> KnownStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "css", "xpath", "role", "name"
    };

    public async Task<ILocator> ResolveAsync(
        ActionTarget target,
        IPage page,
        Dictionary<string, object?> variables,
        int selectorTimeoutMs,
        CancellationToken cancellationToken,
        BrowserActionType actionType = BrowserActionType.Click)
    {
        var sorted = target.Selectors
            .Where(s => KnownStrategies.Contains(s.Strategy))
            .OrderByDescending(s => s.Confidence)
            .ToList();

        var attempted = new List<string>(sorted.Count);

        foreach (var entry in sorted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedValue = VariableSubstitution.Resolve(entry.Value, variables);
            var locator = CreateLocator(page, entry.Strategy, resolvedValue);
            attempted.Add($"{entry.Strategy}={resolvedValue}");

            logger.SelectorProbeStarted(entry.Strategy, resolvedValue, entry.Confidence);

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(selectorTimeoutMs);

                var count = await locator.CountAsync();
                if (count > 0)
                {
                    logger.SelectorProbeSucceeded(entry.Strategy, resolvedValue);
                    return locator.First;
                }

                logger.SelectorProbeNotFound(entry.Strategy, resolvedValue);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.SelectorProbeTimedOut(entry.Strategy, resolvedValue, selectorTimeoutMs);
            }
        }

        var observation = new BrowserObservation(
            page.Url,
            await page.TitleAsync(),
            attempted,
            actionType,
            target.Description,
            DateTimeOffset.UtcNow);

        var fallbackResult = agentTargetResolver is null
            ? null
            : await agentTargetResolver.ResolveTargetAsync(observation, cancellationToken);

        if (fallbackResult?.Success == true)
        {
            var fallbackLocator = CreateLocatorFromResolutionResult(page, fallbackResult);
            if (fallbackLocator is not null)
            {
                logger.LogDebug("Agent fallback resolver produced a target for {ActionType}", actionType);
                return fallbackLocator.First;
            }
        }

        throw new TargetResolutionException(
            $"All selectors exhausted for target '{target.Description ?? "unnamed"}'. Attempted: [{string.Join(", ", attempted)}]",
            attempted);
    }

    private static ILocator CreateLocator(IPage page, string strategy, string value) =>
        strategy.ToUpperInvariant() switch
        {
            "CSS" => page.Locator(value),
            "XPATH" => page.Locator($"xpath={value}"),
            "ROLE" => page.GetByRole(ParseRole(value)),
            "NAME" => page.Locator($"[name=\"{value}\"]"),
            _ => throw new InvalidOperationException($"Unknown selector strategy: {strategy}")
        };

    private static ILocator? CreateLocatorFromResolutionResult(IPage page, AgentTargetResolutionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ResolvedSelector))
        {
            return CreateLocator(page, "css", result.ResolvedSelector);
        }

        if (!string.IsNullOrWhiteSpace(result.ResolvedXPath))
        {
            return CreateLocator(page, "xpath", result.ResolvedXPath);
        }

        if (!string.IsNullOrWhiteSpace(result.ResolvedRole))
        {
            return CreateLocator(page, "role", result.ResolvedRole);
        }

        if (!string.IsNullOrWhiteSpace(result.ResolvedName))
        {
            return CreateLocator(page, "name", result.ResolvedName);
        }

        return null;
    }

    private static AriaRole ParseRole(string value) =>
        Enum.TryParse<AriaRole>(value, ignoreCase: true, out var role)
            ? role
            : throw new InvalidOperationException($"Unknown ARIA role: {value}");
}

internal sealed class TargetResolutionException(string message, IReadOnlyList<string> attemptedSelectors)
    : InvalidOperationException(message)
{
    public IReadOnlyList<string> AttemptedSelectors { get; } = attemptedSelectors;
}

