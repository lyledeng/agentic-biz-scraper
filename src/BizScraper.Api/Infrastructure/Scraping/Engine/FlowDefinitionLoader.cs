using System.Text.Json;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class FlowDefinitionLoader
{
    private readonly Dictionary<string, FlowDefinitionV2> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FlowDefinitionV2> _slugIndex = new(StringComparer.OrdinalIgnoreCase);

    public FlowDefinitionV2 GetDefinition(string state, string endpoint)
    {
        var key = $"{state}:{endpoint}";
        if (_definitions.TryGetValue(key, out var definition))
        {
            return definition;
        }

        throw new InvalidOperationException($"No flow definition found for state='{state}', endpoint='{endpoint}'.");
    }

    public FlowDefinitionV2? GetDefinitionBySlug(string slug) =>
        _slugIndex.GetValueOrDefault(slug);

    public IReadOnlyDictionary<string, FlowDefinitionV2> Definitions => _definitions;
    public IReadOnlyDictionary<string, FlowDefinitionV2> SlugIndex => _slugIndex;

    public void LoadAndValidateAll(string definitionsPath, ILogger logger)
    {
        if (!Directory.Exists(definitionsPath))
        {
            logger.LogInformation("No definitions directory found at {Path}, skipping flow definition loading.", definitionsPath);
            return;
        }

        var jsonFiles = Directory.GetFiles(definitionsPath, "*.json");
        if (jsonFiles.Length == 0)
        {
            logger.LogInformation("No JSON flow definitions found in {Path}.", definitionsPath);
            return;
        }

        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileName(file);
            try
            {
                var json = File.ReadAllText(file);
                var v2 = LoadDefinition(json, fileName);

                var key = $"{v2.Metadata.State}:{v2.Metadata.Endpoint}";
                if (!_definitions.TryAdd(key, v2))
                {
                    throw new InvalidOperationException(
                        $"Duplicate flow definition for state='{v2.Metadata.State}', endpoint='{v2.Metadata.Endpoint}' in '{fileName}'.");
                }

                if (!string.IsNullOrWhiteSpace(v2.Metadata.DefinitionSlug))
                {
                    if (!_slugIndex.TryAdd(v2.Metadata.DefinitionSlug, v2))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate definitionSlug '{v2.Metadata.DefinitionSlug}' in '{fileName}'. Each definition must have a unique slug.");
                    }
                }

                logger.LogInformation("Loaded flow definition: {Name} ({State}/{Endpoint}) slug={Slug} from {File}",
                    v2.Metadata.Name, v2.Metadata.State, v2.Metadata.Endpoint, v2.Metadata.DefinitionSlug ?? "(none)", fileName);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON in flow definition '{fileName}': {ex.Message}", ex);
            }
        }
    }

    internal static FlowDefinitionV2 LoadDefinition(string json, string fileName)
    {
        var v2 = JsonSerializer.Deserialize(json, FlowDefinitionV2JsonContext.Default.FlowDefinitionV2)
            ?? throw new InvalidOperationException($"Failed to deserialize flow definition from '{fileName}'.");

        ValidateV2(v2, fileName);
        return v2;
    }

    // V2 validation (T014)
    private static void ValidateV2(FlowDefinitionV2 definition, string fileName)
    {
        // Metadata required fields
        if (string.IsNullOrWhiteSpace(definition.Metadata.Id))
        {
            throw new InvalidOperationException($"Missing required field 'metadata.id' in '{fileName}'.");
        }

        if (string.IsNullOrWhiteSpace(definition.Metadata.Name))
        {
            throw new InvalidOperationException($"Missing required field 'metadata.name' in '{fileName}'.");
        }

        if (string.IsNullOrWhiteSpace(definition.Metadata.State))
        {
            throw new InvalidOperationException($"Missing required field 'metadata.state' in '{fileName}'.");
        }

        if (string.IsNullOrWhiteSpace(definition.Metadata.Endpoint))
        {
            throw new InvalidOperationException($"Missing required field 'metadata.endpoint' in '{fileName}'.");
        }

        if (definition.Actions is not { Count: > 0 })
        {
            throw new InvalidOperationException($"Flow definition '{fileName}' must contain at least one action.");
        }

        if (definition.Output is null || string.IsNullOrWhiteSpace(definition.Output.VariableName))
        {
            throw new InvalidOperationException($"Missing required 'output.variableName' in '{fileName}'.");
        }

        // Business-search definitions must declare a searchEntryUrl variable
        if (string.Equals(definition.Metadata.Endpoint, "business-search", StringComparison.OrdinalIgnoreCase))
        {
            var hasSearchEntryUrl = definition.Variables?.Any(
                v => string.Equals(v.Name, "searchEntryUrl", StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!hasSearchEntryUrl)
            {
                throw new InvalidOperationException(
                    $"Business-search definition '{fileName}' must declare a 'searchEntryUrl' variable.");
            }
        }

        // Unique action IDs
        var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ValidateV2Actions(definition.Actions, fileName, actionIds, definition.Scripts);
    }

    private static readonly HashSet<string> KnownSelectorStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "css", "xpath", "role", "name"
    };

    private static readonly HashSet<string> ValidActionTypes =
    [
        "navigate", "fill", "click", "wait-for-load", "wait-for-condition",
        "extract", "check-text", "screenshot", "download", "loop", "call-service"
    ];

    private static void ValidateV2Actions(
        IReadOnlyList<FlowActionV2> actions,
        string fileName,
        HashSet<string> actionIds,
        IReadOnlyDictionary<string, ScriptDefinition>? scripts)
    {
        foreach (var action in actions)
        {
            if (string.IsNullOrWhiteSpace(action.Type))
            {
                throw new InvalidOperationException($"Action missing required 'type' field in '{fileName}'.");
            }

            if (!ValidActionTypes.Contains(action.Type))
            {
                throw new InvalidOperationException($"Unknown action type '{action.Type}' in '{fileName}'.");
            }

            // Unique action ID
            if (!string.IsNullOrEmpty(action.Id) && !actionIds.Add(action.Id))
            {
                throw new InvalidOperationException($"Duplicate action ID '{action.Id}' in '{fileName}'.");
            }

            // ScriptRef validation
            if (!string.IsNullOrEmpty(action.ScriptRef))
            {
                if (!string.IsNullOrEmpty(action.Javascript))
                {
                    throw new InvalidOperationException(
                        $"Action '{action.Id ?? action.Type}' in '{fileName}' has both 'scriptRef' and 'javascript'. Only one is allowed.");
                }

                if (scripts is null || !scripts.ContainsKey(action.ScriptRef))
                {
                    throw new InvalidOperationException(
                        $"Script reference '{action.ScriptRef}' not found in scripts dictionary in '{fileName}'.");
                }
            }

            // Target validation
            if (action.Target is not null)
            {
                if (action.Target.Selectors is not { Count: > 0 })
                {
                    throw new InvalidOperationException(
                        $"Action '{action.Id ?? action.Type}' in '{fileName}' has empty target.selectors.");
                }

                foreach (var sel in action.Target.Selectors)
                {
                    if (!KnownSelectorStrategies.Contains(sel.Strategy))
                    {
                        throw new InvalidOperationException(
                            $"Unknown selector strategy '{sel.Strategy}' in action '{action.Id ?? action.Type}' in '{fileName}'.");
                    }
                }
            }

            // Recurse into loop sub-actions
            if (action.Type == "loop" && action.Actions is { Count: > 0 })
            {
                ValidateV2Actions(action.Actions, fileName, actionIds, scripts);
            }
        }
    }
}
