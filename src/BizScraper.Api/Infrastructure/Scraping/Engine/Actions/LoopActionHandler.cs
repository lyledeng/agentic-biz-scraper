using System.Text.Json;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class LoopActionHandler(
    IServiceProvider serviceProvider,
    ConditionEvaluator conditionEvaluator,
    ILogger<LoopActionHandler> logger) : IActionHandler
{
    private readonly Lazy<ScrapingFlowEngine> _engine = new(() => serviceProvider.GetRequiredService<ScrapingFlowEngine>());

    public string ActionType => "loop";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action) =>
        $"maxIterations={action.MaxIterations}, aggregateInto={action.AggregateInto}";

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        if (action.Actions is not { Count: > 0 })
        {
            return;
        }

        var maxIterations = action.MaxIterations
            ?? VariableSubstitution.ResolveInt(null, context.Variables, 25);

        // Initialize aggregation list if aggregateInto is specified
        List<object?>? aggregatedResults = null;
        if (!string.IsNullOrEmpty(action.AggregateInto))
        {
            // Seed with any existing pre-loop extraction (e.g., first page results)
            if (context.Variables.TryGetValue(action.AggregateInto, out var existing) && existing is not null)
            {
                aggregatedResults = FlattenToList(existing);
            }
            else
            {
                aggregatedResults = [];
            }
        }

        context.LoopDepth++;
        try
        {
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            context.LoopIteration = iteration;

            // Inject engine loop variables
            context.Variables["currentPage"] = iteration + 1;
            context.Variables["nextPage"] = iteration + 2;
            context.Variables["iterationCount"] = iteration;

            // Check termination condition before executing
            if (action.TerminateWhen is not null)
            {
                var shouldTerminate = await conditionEvaluator.EvaluateAsync(
                    action.TerminateWhen, context, cancellationToken);

                if (shouldTerminate)
                {
                    logger.LogDebug(
                        "Loop terminated by condition at iteration {Iteration}.", iteration);
                    break;
                }
            }

            try
            {
                await _engine.Value.ExecuteActionsAsync(action.Actions, context, cancellationToken);

                // After iteration, aggregate results if configured
                if (aggregatedResults is not null && !string.IsNullOrEmpty(action.AggregateInto))
                {
                    // Find the extract storeAs variable from nested actions
                    var extractAction = FindExtractAction(action.Actions);
                    if (extractAction?.StoreAs is not null &&
                        context.Variables.TryGetValue(extractAction.StoreAs, out var iterationResult) &&
                        iterationResult is not null)
                    {
                        var items = FlattenToList(iterationResult);
                        aggregatedResults.AddRange(items);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Loop iteration {Iteration} failed. Returning partial results.", iteration);
                context.Truncated = true;
                break;
            }
        }
        }
        finally
        {
            context.LoopDepth--;
        }

        // Store aggregated results back
        if (aggregatedResults is not null && !string.IsNullOrEmpty(action.AggregateInto))
        {
            context.Variables[action.AggregateInto] = aggregatedResults;
        }
    }

    private static FlowActionV2? FindExtractAction(IReadOnlyList<FlowActionV2> actions)
    {
        // Iterate in reverse: the actual data extraction typically comes after
        // helper extractions (e.g., saving paginator state, CAPTCHA images).
        for (var i = actions.Count - 1; i >= 0; i--)
        {
            if (actions[i].Type == "extract" && !string.IsNullOrEmpty(actions[i].StoreAs))
            {
                return actions[i];
            }
        }

        return null;
    }

    private static List<object?> FlattenToList(object value)
    {
        if (value is List<object?> existingList)
        {
            return [.. existingList];
        }

        if (value is string jsonString)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<object?>();
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        list.Add(element.Clone());
                    }

                    return list;
                }
            }
            catch (JsonException)
            {
                // Not a JSON array, treat as single value
            }

            return [value];
        }

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<object?>();
                foreach (var element in jsonElement.EnumerateArray())
                {
                    list.Add(element.Clone());
                }

                return list;
            }

            return [jsonElement.Clone()];
        }

        // Handle any other IEnumerable (e.g., List<NormalizedSearchResult> from post-processors)
        if (value is System.Collections.IEnumerable enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(item);
            }

            return list;
        }

        return [value];
    }
}
