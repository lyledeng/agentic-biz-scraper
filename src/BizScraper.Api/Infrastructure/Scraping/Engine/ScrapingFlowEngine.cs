using System.Diagnostics;
using System.Text.Json;
using BizScraper.Api.Features.BusinessSearch.Exceptions;
using BizScraper.Api.Infrastructure.Scraping.Engine.Actions;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class ScrapingFlowEngine(
    IEnumerable<IActionHandler> actionHandlers,
    ConditionEvaluator conditionEvaluator,
    OutputSchemaValidator outputSchemaValidator,
    ILogger<ScrapingFlowEngine> logger)
{
    private readonly Dictionary<string, IActionHandler> _handlers =
        actionHandlers.ToDictionary(h => h.ActionType, StringComparer.OrdinalIgnoreCase);

    public async Task<FlowExecutionResult> ExecuteAsync(
        FlowDefinitionV2 definition,
        ActionContext context,
        CancellationToken cancellationToken)
    {
        // Populate V2 context properties
        context.Environment = definition.Environment;
        context.Scripts = definition.Scripts;

        // T037: Initialize variables with default values
        InitializeVariableDefaults(definition.Variables, context);

        // T008: Build sensitive variable names set for log redaction
        var sensitiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in definition.Variables)
        {
            if (variable.Sensitive == true)
            {
                sensitiveNames.Add(variable.Name);
            }
        }
        context.SensitiveVariableNames = sensitiveNames;

        logger.FlowStartedV2(
            definition.Metadata.Id, definition.Metadata.Name, definition.Metadata.Author,
            definition.Metadata.State, definition.Metadata.Endpoint, context.CorrelationId);

        try
        {
            // T035: Viewport configuration
            if (definition.Environment?.Viewport is { } viewport)
            {
                await context.Page.SetViewportSizeAsync(viewport.Width, viewport.Height);
            }

            await ExecuteActionsAsync(definition.Actions, context, cancellationToken);

            logger.FlowCompletedV2(definition.Metadata.Id, definition.Metadata.Name, context.CorrelationId);

            object? outputValue;
            if (definition.Output.Compose is { Count: > 0 } compose)
            {
                // Build a composite output from multiple variables
                var composed = new Dictionary<string, object?>();
                foreach (var (propertyName, variableName) in compose)
                {
                    var raw = context.Variables.GetValueOrDefault(variableName);
                    composed[propertyName] = TryParseJsonString(raw);
                }
                outputValue = composed;
            }
            else
            {
                outputValue = TryParseJsonString(context.Variables.GetValueOrDefault(definition.Output.VariableName));
            }

            // T059: Advisory output schema validation
            outputSchemaValidator.Validate(outputValue, definition.Output.Schema, definition.Metadata.Name);

            return new FlowExecutionResult(outputValue, context.Truncated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CheckTextMatchException)
        {
            try
            {
                await context.CaptureScreenshotAsync("error", force: true, cancellationToken);
            }
            catch
            {
                // Best-effort screenshot on error
            }

            throw;
        }
        catch (Exception ex)
        {
            try
            {
                await context.CaptureScreenshotAsync("error", force: true, cancellationToken);
            }
            catch
            {
                // Best-effort screenshot on error
            }

            throw new ScrapingException(
                $"Flow '{definition.Metadata.Name}' failed: {ex.Message}", ex);
        }
    }

    internal async Task ExecuteActionsAsync(
        IReadOnlyList<FlowActionV2> actions,
        ActionContext context,
        CancellationToken cancellationToken)
    {
        // T053: Stable sort by Order when present; null-Order actions retain original relative position
        var sortedActions = actions
            .Select((a, idx) => (Action: a, OriginalIndex: idx))
            .OrderBy(x => x.Action.Order ?? int.MaxValue)
            .ThenBy(x => x.OriginalIndex)
            .Select(x => x.Action)
            .ToList();

        for (var i = 0; i < sortedActions.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var action = sortedActions[i];

            var conditionMet = await conditionEvaluator.EvaluateAsync(action.Condition, context, cancellationToken);
            if (!conditionMet)
            {
                logger.ActionSkipped(i, action.Type, action.Id ?? "(no id)", action.Condition?.Type, context.CorrelationId);
                continue;
            }

            if (!_handlers.TryGetValue(action.Type, out var handler))
            {
                throw new InvalidOperationException($"No handler registered for action type '{action.Type}'.");
            }

            var actionId = action.Id ?? "(no id)";
            var description = action.Description ?? "(no description)";
            var details = handler.GetLogDetails(context, action);

            logger.ActionStarted(i, action.Type, actionId, description, details, context.CorrelationId);

            try
            {
                // T036: Per-action retry with backoff
                var sw = Stopwatch.StartNew();
                await ExecuteWithRetryAsync(handler, context, action, cancellationToken);
                sw.Stop();

                if (action.Screenshot is not null)
                {
                    var milestone = VariableSubstitution.Resolve(action.Screenshot.Milestone, context.Variables);
                    await context.CaptureScreenshotAsync(milestone, action.Screenshot.Force ?? false, cancellationToken);
                }

                var completionDetails = handler.GetCompletionDetails(context, action);
                logger.ActionCompleted(i, action.Type, actionId, sw.ElapsedMilliseconds, completionDetails, context.CorrelationId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.ActionFailed(i, action.Type, actionId, ex.Message, details, context.CorrelationId, ex);
                throw;
            }
        }
    }

    private async Task ExecuteWithRetryAsync(
        IActionHandler handler,
        ActionContext context,
        FlowActionV2 action,
        CancellationToken cancellationToken)
    {
        var retries = context.Environment?.Retries;
        if (retries is null)
        {
            await handler.ExecuteAsync(context, action, cancellationToken);
            return;
        }

        var delayMs = retries.DelayMs;
        for (var attempt = 0; attempt <= retries.MaxAttempts; attempt++)
        {
            try
            {
                await handler.ExecuteAsync(context, action, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CheckTextMatchException)
            {
                throw; // Never retry check-text match exceptions
            }
            catch (Exception) when (attempt < retries.MaxAttempts)
            {
                logger.RetryAttempt(action.Type, action.Id, attempt + 1, retries.MaxAttempts, delayMs);
                await Task.Delay(delayMs, cancellationToken);
                delayMs = (int)(delayMs * retries.BackoffMultiplier);
            }
        }
    }

    private static void InitializeVariableDefaults(IReadOnlyList<VariableV2> variables, ActionContext context)
    {
        foreach (var variable in variables)
        {
            if (variable.DefaultValue is not { } defaultElement)
            {
                continue;
            }

            // Only apply default if not already set at runtime
            if (context.Variables.ContainsKey(variable.Name))
            {
                continue;
            }

            context.Variables[variable.Name] = ConvertJsonElement(defaultElement);
        }
    }

    private static object? ConvertJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static object? TryParseJsonString(object? value)
    {
        if (value is string s && s.Length > 1 && (s[0] == '{' || s[0] == '['))
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(s);
            }
            catch (JsonException)
            {
                // Not valid JSON — return as-is
            }
        }

        // Normalize List<object?> (from loop aggregation) to JsonElement.
        // Use camelCase so .NET record properties match the JSON conventions expected by mappers.
        if (value is List<object?> list)
        {
            return JsonSerializer.SerializeToElement(list, CamelCaseOptions);
        }

        return value;
    }
}

/// <summary>
/// Result of a scraping flow execution containing output data and truncation status.
/// </summary>
public sealed record FlowExecutionResult(object? Output, bool Truncated);
