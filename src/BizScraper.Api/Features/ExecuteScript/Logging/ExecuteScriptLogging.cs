namespace BizScraper.Api.Features.ExecuteScript.Logging;

internal static partial class ExecuteScriptLogging
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Execute-script started: definition={DefinitionSlug} [CorrelationId={CorrelationId}]")]
    public static partial void ExecuteScriptStarted(this ILogger logger, string definitionSlug, string correlationId);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Execute-script completed: definition={DefinitionSlug} durationMs={DurationMs} [CorrelationId={CorrelationId}]")]
    public static partial void ExecuteScriptCompleted(this ILogger logger, string definitionSlug, long durationMs, string correlationId);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Error, Message = "Execute-script failed: definition={DefinitionSlug} [CorrelationId={CorrelationId}]")]
    public static partial void ExecuteScriptFailed(this ILogger logger, string definitionSlug, string correlationId, Exception exception);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning, Message = "Execute-script rejected (busy): definition={DefinitionSlug} [CorrelationId={CorrelationId}]")]
    public static partial void ExecuteScriptBusy(this ILogger logger, string definitionSlug, string correlationId);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning, Message = "Execute-script timed out: definition={DefinitionSlug} [CorrelationId={CorrelationId}]")]
    public static partial void ExecuteScriptTimedOut(this ILogger logger, string definitionSlug, string correlationId);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Warning, Message = "Execute-script audit write failed [CorrelationId={CorrelationId}]")]
    public static partial void AuditWriteFailed(this ILogger logger, string correlationId, Exception exception);
}
