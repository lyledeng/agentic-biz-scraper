using System.Text.RegularExpressions;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal static partial class VariableSubstitution
{
    [GeneratedRegex(@"\$\{(\w+)\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();

    public static string Resolve(string? template, Dictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        return VariablePattern().Replace(template, match =>
        {
            var variableName = match.Groups[1].Value;
            if (variables.TryGetValue(variableName, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }

            throw new InvalidOperationException(
                $"Unresolved variable '${{{variableName}}}'. The variable was referenced but not provided at runtime.");
        });
    }

    /// <summary>
    /// Resolves variables with escaping safe for JavaScript string literals.
    /// Escapes backslashes, quotes, and newlines in substituted values.
    /// </summary>
    public static string ResolveJavaScript(string? template, Dictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        return VariablePattern().Replace(template, match =>
        {
            var variableName = match.Groups[1].Value;
            if (variables.TryGetValue(variableName, out var value) && value is not null)
            {
                return EscapeForJavaScript(value.ToString() ?? string.Empty);
            }

            // In JavaScript, ${...} is also template-literal syntax.
            // Leave unrecognised references intact so native JS works.
            return match.Value;
        });
    }

    private static string EscapeForJavaScript(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    public static int ResolveInt(string? template, Dictionary<string, object?> variables, int defaultValue)
    {
        if (string.IsNullOrEmpty(template))
        {
            return defaultValue;
        }

        var resolved = Resolve(template, variables);
        return int.TryParse(resolved, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Resolves a template but redacts values of sensitive variables with "***".
    /// </summary>
    public static string ResolveSafe(string? template, Dictionary<string, object?> variables, IReadOnlySet<string>? sensitiveNames)
    {
        if (string.IsNullOrEmpty(template) || sensitiveNames is null or { Count: 0 })
        {
            return Resolve(template, variables);
        }

        return VariablePattern().Replace(template, match =>
        {
            var variableName = match.Groups[1].Value;
            if (sensitiveNames.Contains(variableName))
            {
                return "***";
            }

            if (variables.TryGetValue(variableName, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }

            throw new InvalidOperationException(
                $"Unresolved variable '${{{variableName}}}'. The variable was referenced but not provided at runtime.");
        });
    }
}
