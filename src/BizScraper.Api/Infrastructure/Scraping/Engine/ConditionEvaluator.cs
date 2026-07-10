using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class ConditionEvaluator(TargetResolver targetResolver)
{
    private const int DefaultSelectorTimeoutMs = 5000;

    public async Task<bool> EvaluateAsync(ConditionV2? condition, ActionContext context, CancellationToken cancellationToken)
    {
        if (condition is null)
        {
            return true;
        }

        return condition.Type switch
        {
            "element-exists" => await ElementExistsAsync(condition, context, cancellationToken),
            "element-visible" => await ElementVisibleAsync(condition, context, cancellationToken),
            "text-contains" => await TextContainsAsync(condition.Text!, context.Page),
            "variable-truthy" => IsVariableTruthy(condition.VariableName, context),
            "variable-falsy" => !IsVariableTruthy(condition.VariableName, context),
            "not" => !await EvaluateAsync(condition.InnerCondition, context, cancellationToken),
            "and" => await EvaluateAndAsync(condition.Conditions, context, cancellationToken),
            "or" => await EvaluateOrAsync(condition.Conditions, context, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown condition type: {condition.Type}")
        };
    }

    private async Task<bool> ElementExistsAsync(ConditionV2 condition, ActionContext context, CancellationToken cancellationToken)
    {
        if (condition.Target is not null)
        {
            try
            {
                var timeoutMs = context.Environment?.SelectorTimeoutMs ?? DefaultSelectorTimeoutMs;
                await targetResolver.ResolveAsync(condition.Target, context.Page, context.Variables, timeoutMs, cancellationToken);
                return true;
            }
            catch (TargetResolutionException)
            {
                return false;
            }
        }

        var resolved = VariableSubstitution.Resolve(condition.Selector!, context.Variables);
        var count = await context.Page.Locator(resolved).CountAsync();
        return count > 0;
    }

    private async Task<bool> ElementVisibleAsync(ConditionV2 condition, ActionContext context, CancellationToken cancellationToken)
    {
        if (condition.Target is not null)
        {
            try
            {
                var timeoutMs = context.Environment?.SelectorTimeoutMs ?? DefaultSelectorTimeoutMs;
                var locator = await targetResolver.ResolveAsync(condition.Target, context.Page, context.Variables, timeoutMs, cancellationToken);
                return await locator.IsVisibleAsync();
            }
            catch (TargetResolutionException)
            {
                return false;
            }
        }

        var resolved = VariableSubstitution.Resolve(condition.Selector!, context.Variables);
        var locatorLegacy = context.Page.Locator(resolved).First;
        if (await locatorLegacy.CountAsync() == 0)
        {
            return false;
        }

        return await locatorLegacy.IsVisibleAsync();
    }

    private async Task<bool> EvaluateAndAsync(IReadOnlyList<ConditionV2>? conditions, ActionContext context, CancellationToken cancellationToken)
    {
        if (conditions is null or { Count: 0 })
        {
            return true;
        }

        foreach (var condition in conditions)
        {
            if (!await EvaluateAsync(condition, context, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> EvaluateOrAsync(IReadOnlyList<ConditionV2>? conditions, ActionContext context, CancellationToken cancellationToken)
    {
        if (conditions is null or { Count: 0 })
        {
            return false;
        }

        foreach (var condition in conditions)
        {
            if (await EvaluateAsync(condition, context, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> TextContainsAsync(string text, IPage page)
    {
        var bodyText = await page.InnerTextAsync("body");
        return bodyText.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVariableTruthy(string? variableName, ActionContext context)
    {
        if (string.IsNullOrEmpty(variableName) || !context.Variables.TryGetValue(variableName, out var value))
        {
            return false;
        }

        return value switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            _ => true
        };
    }
}
