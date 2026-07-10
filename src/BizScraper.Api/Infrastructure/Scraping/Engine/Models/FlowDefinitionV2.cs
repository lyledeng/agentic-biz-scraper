using System.Text.Json;
using System.Text.Json.Serialization;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Models;

/// <summary>
/// Root V2 flow definition describing a complete scraping workflow.
/// </summary>
public sealed record FlowDefinitionV2(
    string SchemaVersion,
    FlowMetadata Metadata,
    FlowEnvironment? Environment,
    BrowserConfig? Browser,
    IReadOnlyList<VariableV2> Variables,
    IReadOnlyList<FlowActionV2> Actions,
    IReadOnlyDictionary<string, ScriptDefinition>? Scripts,
    OutputDeclarationV2 Output);

/// <summary>
/// Optional per-definition browser mode override. When present, controls which browser pool the definition executes in.
/// </summary>
/// <param name="Mode">Browser mode: "local" launches a local browser; "remote" connects to the Azure Playwright workspace.</param>
/// <param name="Channel">Browser channel for local mode (e.g., "chrome", "msedge"). Ignored for remote mode. When null, uses bundled Chromium.</param>
/// <param name="Headless">Whether to run in headless mode for local launches. Ignored for remote mode. Defaults to true.</param>
public sealed record BrowserConfig(string Mode, string? Channel, bool Headless = true);

/// <summary>
/// Descriptive metadata for a flow definition including slug, state, and authorship.
/// </summary>
public sealed record FlowMetadata
{
    public required string Id { get; init; }
    public string? DefinitionSlug { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string State { get; init; }
    public required string Endpoint { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
    public string? Author { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>
/// Browser and execution environment configuration for a scraping flow.
/// </summary>
public sealed record FlowEnvironment
{
    public ViewportConfig? Viewport { get; init; }
    public string? UserAgent { get; init; }
    public string? Locale { get; init; }
    public int? Timeout { get; init; }
    public int? SelectorTimeoutMs { get; init; }
    public RetryConfig? Retries { get; init; }
}

/// <summary>
/// Browser viewport dimensions for scraping flows.
/// </summary>
public sealed record ViewportConfig(int Width, int Height);

/// <summary>
/// Retry configuration with exponential backoff for flow actions.
/// </summary>
public sealed record RetryConfig(int MaxAttempts, int DelayMs, double BackoffMultiplier);

/// <summary>
/// Variable declaration with source, default value, and sensitivity metadata.
/// </summary>
public sealed record VariableV2
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public bool? Required { get; init; }
    public JsonElement? DefaultValue { get; init; }
    public bool? Sensitive { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// A single action in a V2 flow with multi-selector targeting, conditions, and type-specific properties.
/// </summary>
public sealed record FlowActionV2
{
    // V2 identity
    public string? Id { get; init; }
    public int? Order { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public string? Group { get; init; }
    public ScreenshotConfig? Screenshot { get; init; }
    public ConditionV2? Condition { get; init; }
    public ActionMetadata? Metadata { get; init; }

    // navigate
    public string? Url { get; init; }
    public string? WaitUntil { get; init; }

    // fill / click — v2 target OR v1 flat selector
    public ActionTarget? Target { get; init; }
    public string? Selector { get; init; }
    public string? Value { get; init; }

    // click
    public string? WaitAfter { get; init; }

    // wait-for-load
    public string? State { get; init; }

    // wait-for-condition / extract
    public string? Javascript { get; init; }
    public int? TimeoutMs { get; init; }

    // extract
    public string? StoreAs { get; init; }
    public string? PostProcessor { get; init; }
    public string? ScriptRef { get; init; }

    // check-text
    public string? Pattern { get; init; }
    public bool? IsRegex { get; init; }
    public string? OnMatch { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }

    // screenshot
    public string? Milestone { get; init; }
    public bool? Force { get; init; }

    // download
    public string? LinkSelector { get; init; }
    public string? ValidateExtension { get; init; }
    public string? FilenamePattern { get; init; }

    // loop
    public string? LoopType { get; init; }
    [JsonConverter(typeof(MaxIterationsConverter))]
    public int? MaxIterations { get; init; }
    public ConditionV2? TerminateWhen { get; init; }
    public IReadOnlyList<FlowActionV2>? Actions { get; init; }
    public string? AggregateInto { get; init; }

    // call-service
    public string? ServiceName { get; init; }
    public string? InputVariable { get; init; }
    public string? OutputVariable { get; init; }
}

/// <summary>
/// Multi-selector targeting specification with ranked selector entries.
/// </summary>
public sealed record ActionTarget(
    IReadOnlyList<SelectorEntry> Selectors,
    string? Description);

/// <summary>
/// A single CSS or XPath selector with a confidence ranking.
/// </summary>
public sealed record SelectorEntry(string Strategy, string Value, double Confidence);

/// <summary>
/// Recorder-captured context metadata (passthrough, not executed on).
/// </summary>
public sealed record ActionMetadata
{
    public string? Timestamp { get; init; }
    public string? PageUrl { get; init; }
    public string? PageTitle { get; init; }
}

/// <summary>
/// Extended condition supporting compound and/or logic and multi-selector targets.
/// </summary>
public sealed record ConditionV2
{
    public required string Type { get; init; }

    // Leaf conditions
    public string? Selector { get; init; }
    public ActionTarget? Target { get; init; }
    public string? Text { get; init; }
    public string? VariableName { get; init; }

    // "not" — single inner condition
    [JsonPropertyName("condition")]
    public ConditionV2? InnerCondition { get; init; }

    // "and" / "or" — array of inner conditions
    public IReadOnlyList<ConditionV2>? Conditions { get; init; }
}

/// <summary>
/// Named reusable script with language, body, and optional description.
/// </summary>
public sealed record ScriptDefinition(string Language, string Body, string? Description);

/// <summary>
/// V2 output declaration specifying a variable name, type, and optional JSON schema.
/// </summary>
public sealed record OutputDeclarationV2
{
    public required string VariableName { get; init; }
    public required string Type { get; init; }
    public JsonElement? Schema { get; init; }

    /// <summary>
    /// Optional map of property names to variable names for composing a multi-variable output object.
    /// When set, the output is a JSON object built from the listed variables instead of a single variable.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Compose { get; init; }
}

// T004: Schema violation result
internal sealed record SchemaViolation(string Path, string Expected, string Actual);

/// <summary>
/// Screenshot configuration for capturing visual milestones during flow execution.
/// </summary>
public sealed record ScreenshotConfig(
    string Milestone,
    bool? Force);

/// <summary>
/// Handles JSON deserialization of maxIterations which can be an integer or a "${variable}" string.
/// </summary>
internal sealed class MaxIterationsConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String => null, // Variable reference — resolved at runtime
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token type {reader.TokenType} for maxIterations.")
        };

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
