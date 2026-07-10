using System.Text.Json;
using System.Text.Json.Serialization;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Features.HealthCheck;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Models;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(FlowDefinitionV2))]
[JsonSerializable(typeof(FlowActionV2))]
[JsonSerializable(typeof(VariableV2))]
[JsonSerializable(typeof(OutputDeclarationV2))]
[JsonSerializable(typeof(ScreenshotConfig))]
[JsonSerializable(typeof(ConditionV2))]
[JsonSerializable(typeof(FlowMetadata))]
[JsonSerializable(typeof(FlowEnvironment))]
[JsonSerializable(typeof(ViewportConfig))]
[JsonSerializable(typeof(RetryConfig))]
[JsonSerializable(typeof(ActionTarget))]
[JsonSerializable(typeof(SelectorEntry))]
[JsonSerializable(typeof(ScriptDefinition))]
[JsonSerializable(typeof(ActionMetadata))]
[JsonSerializable(typeof(BrowserConfig))]
[JsonSerializable(typeof(ReadinessHealthResponse))]
[JsonSerializable(typeof(ModeStatus))]
[JsonSerializable(typeof(ExecuteScriptRequest))]
[JsonSerializable(typeof(ExecuteScriptResponse))]
[JsonSerializable(typeof(DefinitionInfo))]
[JsonSerializable(typeof(ParameterInfo))]
[JsonSerializable(typeof(DefinitionInfo[]))]
internal sealed partial class FlowDefinitionV2JsonContext : JsonSerializerContext;
