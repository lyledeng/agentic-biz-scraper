using BizScraper.Api.Features.ExecuteScript.Exceptions;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Features.ExecuteScript.Validation;

internal sealed class ExecuteScriptValidator
{
    public void Validate(string? definition, Dictionary<string, object?>? parameters, FlowDefinitionV2 flowDefinition)
    {
        var requiredParams = flowDefinition.Variables
            .Where(v => string.Equals(v.Source, "runtime", StringComparison.OrdinalIgnoreCase)
                        && v.Required is true
                        && !string.Equals(v.Name, "correlationId", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Name)
            .ToList();

        var missing = requiredParams
            .Where(p => parameters is null || !parameters.ContainsKey(p) || parameters[p] is null || (parameters[p] is string s && string.IsNullOrWhiteSpace(s)))
            .ToList();

        if (missing.Count > 0)
        {
            throw new MissingParametersException(definition!, missing);
        }
    }
}
