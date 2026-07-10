namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal static partial class EngineLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Flow started: {FlowName} ({State}/{Endpoint}) [CorrelationId={CorrelationId}]")]
    public static partial void FlowStarted(this ILogger logger, string flowName, string state, string endpoint, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Flow completed: {FlowName} [CorrelationId={CorrelationId}]")]
    public static partial void FlowCompleted(this ILogger logger, string flowName, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Action started: [{ActionIndex}] {ActionType} (id={ActionId}) - {Description} [{Details}] [CorrelationId={CorrelationId}]")]
    public static partial void ActionStarted(this ILogger logger, int actionIndex, string actionType, string? actionId, string? description, string? details, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Action completed: [{ActionIndex}] {ActionType} (id={ActionId}) in {ElapsedMs}ms [{Details}] [CorrelationId={CorrelationId}]")]
    public static partial void ActionCompleted(this ILogger logger, int actionIndex, string actionType, string? actionId, long elapsedMs, string? details, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Action skipped (condition false): [{ActionIndex}] {ActionType} (id={ActionId}) condition={Condition} [CorrelationId={CorrelationId}]")]
    public static partial void ActionSkipped(this ILogger logger, int actionIndex, string actionType, string? actionId, string? condition, string correlationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Action failed: [{ActionIndex}] {ActionType} (id={ActionId}) - {ErrorMessage} [{Details}] [CorrelationId={CorrelationId}]")]
    public static partial void ActionFailed(this ILogger logger, int actionIndex, string actionType, string? actionId, string errorMessage, string? details, string correlationId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Action retry: {ActionType} (id={ActionId}) attempt {Attempt}/{MaxAttempts} after {DelayMs}ms")]
    public static partial void RetryAttempt(this ILogger logger, string actionType, string? actionId, int attempt, int maxAttempts, int delayMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Variable resolved: {VariableName} = {VariableValue}")]
    public static partial void VariableResolved(this ILogger logger, string variableName, string? variableValue);

    // TargetResolver probe logging (T009)
    [LoggerMessage(Level = LogLevel.Debug, Message = "Selector probe: strategy={Strategy}, value={SelectorValue}, confidence={Confidence}")]
    public static partial void SelectorProbeStarted(this ILogger logger, string strategy, string selectorValue, double confidence);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Selector probe succeeded: strategy={Strategy}, value={SelectorValue}")]
    public static partial void SelectorProbeSucceeded(this ILogger logger, string strategy, string selectorValue);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Selector probe not found: strategy={Strategy}, value={SelectorValue}")]
    public static partial void SelectorProbeNotFound(this ILogger logger, string strategy, string selectorValue);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Selector probe timed out: strategy={Strategy}, value={SelectorValue}, timeoutMs={TimeoutMs}")]
    public static partial void SelectorProbeTimedOut(this ILogger logger, string strategy, string selectorValue, int timeoutMs);

    // V2 metadata flow logging (T038)
    [LoggerMessage(Level = LogLevel.Information, Message = "Flow started: {FlowId} ({FlowName}) by {Author} — {State}/{Endpoint} [CorrelationId={CorrelationId}]")]
    public static partial void FlowStartedV2(this ILogger logger, string flowId, string flowName, string? author, string state, string endpoint, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Flow completed: {FlowId} ({FlowName}) [CorrelationId={CorrelationId}]")]
    public static partial void FlowCompletedV2(this ILogger logger, string flowId, string flowName, string correlationId);

    // Schema violation logging (T058)
    [LoggerMessage(Level = LogLevel.Warning, Message = "Output schema violation in '{DefinitionName}': path={ViolationPath}, expected={Expected}, actual={Actual}")]
    public static partial void SchemaViolation(this ILogger logger, string definitionName, string violationPath, string expected, string actual);

    // Script resolution logging (T048)
    [LoggerMessage(Level = LogLevel.Debug, Message = "Script resolved: ref={ScriptRef} for action {ActionId}")]
    public static partial void ScriptResolved(this ILogger logger, string scriptRef, string? actionId);
}
