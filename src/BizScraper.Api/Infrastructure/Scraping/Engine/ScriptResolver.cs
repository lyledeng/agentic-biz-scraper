using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal static class ScriptResolver
{
    public static string Resolve(FlowActionV2 action, IReadOnlyDictionary<string, ScriptDefinition>? scripts)
    {
        if (!string.IsNullOrEmpty(action.ScriptRef))
        {
            if (scripts is null || !scripts.TryGetValue(action.ScriptRef, out var script))
            {
                throw new InvalidOperationException(
                    $"Script reference '{action.ScriptRef}' not found in scripts dictionary.");
            }

            return script.Body;
        }

        if (!string.IsNullOrEmpty(action.Javascript))
        {
            return action.Javascript;
        }

        throw new InvalidOperationException(
            $"Action '{action.Id ?? action.Type}' has neither 'scriptRef' nor 'javascript' defined.");
    }
}
