using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class TargetResolver(ILogger<TargetResolver> logger)
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
        CancellationToken cancellationToken)
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

