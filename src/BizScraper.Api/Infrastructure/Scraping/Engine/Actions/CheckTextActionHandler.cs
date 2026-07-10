using System.Text.RegularExpressions;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class CheckTextActionHandler : IActionHandler
{
    public string ActionType => "check-text";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action) =>
        $"pattern={action.Pattern}, onMatch={action.OnMatch}";

    public string? GetCompletionDetails(ActionContext context, FlowActionV2 action)
    {
        var matched = context.Variables.TryGetValue("__checkTextMatched", out var val) && val is true;
        return $"matched={( matched ? "true" : "false")}";
    }

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var bodyText = await context.Page.InnerTextAsync("body");
        var pattern = VariableSubstitution.Resolve(action.Pattern, context.Variables);

        var matched = action.IsRegex == true
            ? Regex.IsMatch(bodyText, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5))
            : bodyText.Contains(pattern, StringComparison.OrdinalIgnoreCase);

        context.Variables["__checkTextMatched"] = matched;

        if (!matched)
        {
            return;
        }

        var onMatch = action.OnMatch ?? string.Empty;

        if (string.Equals(onMatch, "throw", StringComparison.OrdinalIgnoreCase))
        {
            // T065: Resolve errorMessage with variable interpolation
            var errorMessage = !string.IsNullOrEmpty(action.ErrorMessage)
                ? VariableSubstitution.Resolve(action.ErrorMessage, context.Variables)
                : $"Check-text matched pattern '{pattern}' with onMatch=throw.";

            throw new CheckTextMatchException(
                action.ErrorType ?? "check-text-match",
                errorMessage);
        }

        if (string.Equals(onMatch, "store", StringComparison.OrdinalIgnoreCase))
        {
            // Extract the matched text from the page and store it in the specified variable
            var extractJs = action.Javascript;
            if (!string.IsNullOrWhiteSpace(extractJs) && !string.IsNullOrWhiteSpace(action.StoreAs))
            {
                var resolvedJs = VariableSubstitution.ResolveJavaScript(extractJs, context.Variables);
                var result = await context.Page.EvaluateAsync<string?>(resolvedJs);
                context.Variables[action.StoreAs] = result ?? pattern;
            }
            else if (!string.IsNullOrWhiteSpace(action.StoreAs))
            {
                // Store the matched pattern text directly
                var warningElement = await context.Page.QuerySelectorAsync(action.Selector ?? "body");
                var text = warningElement is not null ? await warningElement.InnerTextAsync() : bodyText;
                context.Variables[action.StoreAs] = text.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    ? pattern
                    : text;
            }

            return;
        }

        if (string.Equals(onMatch, "skip", StringComparison.OrdinalIgnoreCase))
        {
            context.Variables["__skipRemaining"] = true;
        }
    }
}

/// <summary>
/// Exception thrown when a check-text action detects a matching error pattern.
/// </summary>
public sealed class CheckTextMatchException(string errorType, string message) : Exception(message)
{
    public string ErrorType { get; } = errorType;
}
