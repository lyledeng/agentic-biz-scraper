using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using BizScraper.Api.Common;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Extensions;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Domain.Entities;
using BizScraper.Api.Features.BusinessSearch.Exceptions;
using BizScraper.Api.Features.ExecuteScript.Commands;
using BizScraper.Api.Features.ExecuteScript.Exceptions;
using BizScraper.Api.Features.ExecuteScript.Logging;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Metrics;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Features.ExecuteScript.Validation;
using BizScraper.Api.Infrastructure.Persistence;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Proxy;
using LiteBus.Commands.Abstractions;
using Microsoft.Extensions.Options;

namespace BizScraper.Api.Features.ExecuteScript.Handlers;

internal sealed class ExecuteScriptHandler(
    FlowDefinitionLoader flowLoader,
    GenericScriptScraper scraper,
    ExecuteScriptValidator validator,
    ScriptExecutionAuditRepository auditRepository,
    IBlobStorageClient blobStorageClient,
    SearchResultMapper searchResultMapper,
    EntityDetailMapper entityDetailMapper,
    ConcurrentDictionary<string, SemaphoreSlim> semaphores,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<ExecuteScriptHandler> logger,
    IWindowsProxyService windowsProxyService,
    IOptions<WindowsProxyOptions> windowsProxyOptions) : ICommandHandler<ExecuteScriptCommand, ExecuteScriptResponse>
{
    public async Task<ExecuteScriptResponse> HandleAsync(ExecuteScriptCommand command, CancellationToken cancellationToken)
    {
        var definitionSlug = command.Definition;
        var correlationId = command.CorrelationId;
        var stopwatch = Stopwatch.StartNew();

        // Resolve definition first so we can compute blobPrefix
        var definition = flowLoader.GetDefinitionBySlug(definitionSlug)
            ?? throw new UnknownDefinitionException(definitionSlug);

        // Pre-processing: decode uniqueKey for entity-details definitions (before validation,
        // since the decoded params satisfy the definition's required runtime variables)
        var parameters = command.Parameters ?? [];
        if (IsEntityDetailsDefinition(definitionSlug) && parameters.TryGetValue("uniqueKey", out var uniqueKeyObj) && TryGetString(uniqueKeyObj, out var uniqueKeyStr))
        {
            if (!UniqueKeyEncoder.TryDecode(uniqueKeyStr, out var decodedState, out var decodedParams, out var decodeError))
            {
                throw new ValidationException(decodeError);
            }

            // Validate decoded state matches the target definition's state
            var expectedStatePrefix = GetExpectedStatePrefix(definitionSlug);
            if (expectedStatePrefix is not null && !string.Equals(decodedState, expectedStatePrefix, StringComparison.Ordinal))
            {
                throw new ValidationException(
                    $"UniqueKey state '{decodedState}' does not match target definition state prefix '{expectedStatePrefix}'.");
            }

            // Replace uniqueKey with decoded runtime params
            parameters = new Dictionary<string, object?>(parameters);
            parameters.Remove("uniqueKey");
            foreach (var (key, value) in decodedParams)
            {
                parameters[key] = value;
            }
        }

        // Validate parameters (after uniqueKey decode so injected params are present)
        validator.Validate(definitionSlug, parameters, definition);

        // Windows proxy intercept: forward to Windows VM and return directly, bypassing
        // the local scraper, mappers, audit, and all post-processing. The Windows VM
        // runs the full handler independently and handles its own post-processing.
        var proxyOptions = windowsProxyOptions.Value;
        if (definition.Browser is not null &&
            definition.Browser.Mode.Equals("windows", StringComparison.OrdinalIgnoreCase) &&
            proxyOptions.IsConfigured)
        {
            var authHeader = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            return await windowsProxyService.ForwardAsync(definitionSlug, command.Parameters, correlationId, authHeader, cancellationToken);
        }

        if (definition.Browser is not null &&
            definition.Browser.Mode.Equals("windows", StringComparison.OrdinalIgnoreCase) &&
            !proxyOptions.IsConfigured)
        {
            logger.LogWarning("Windows proxy not configured for '{DefinitionSlug}' — falling back to local browser.", definitionSlug);
        }

        // Compute enriched blob prefix for cloud storage folder
        var blobPrefix = GenericScriptScraper.BuildBlobPrefix(definitionSlug, parameters!, correlationId);
        var diagnosticsUrl = blobStorageClient.GetFolderUrl(blobPrefix)?.AbsoluteUri;

        // Acquire per-slug semaphore
        var defaultLimit = configuration.GetValue("ExecuteScript:ConcurrencyLimits:Default", 1);
        var semaphore = semaphores.GetOrAdd(definitionSlug, _ => new SemaphoreSlim(defaultLimit, defaultLimit));

        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            logger.ExecuteScriptBusy(definitionSlug, correlationId);
            await WriteAuditAsync(definitionSlug, correlationId, command.Parameters, diagnosticsUrl, stopwatch.ElapsedMilliseconds, "Busy", "Concurrent request limit reached.", cancellationToken);
            throw new ServiceBusyException($"Definition '{definitionSlug}' is currently at capacity. Please retry after the indicated interval.", 30);
        }

        ExecuteScriptMetrics.RequestStarted(definitionSlug);
        logger.ExecuteScriptStarted(definitionSlug, correlationId);

        try
        {
            var timeoutSeconds = configuration.GetValue("Playwright:RequestTimeoutSeconds", 120);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            FlowExecutionResult result;
            try
            {
                result = await scraper.ExecuteAsync(definition, parameters!, command.CorrelationId, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                logger.ExecuteScriptTimedOut(definitionSlug, correlationId);
                ExecuteScriptMetrics.RequestFailed(definitionSlug, stopwatch.Elapsed.TotalMilliseconds);
                await WriteAuditAsync(definitionSlug, correlationId, command.Parameters, diagnosticsUrl, stopwatch.ElapsedMilliseconds, "Timeout", $"Execution timed out after {timeoutSeconds}s.", cancellationToken);
                throw new TimeoutException($"Execution of '{definitionSlug}' timed out after {timeoutSeconds} seconds.");
            }

            // Post-processing: apply mapper based on definition slug suffix
            var data = ApplyPostProcessingMapper(result.Output, definitionSlug);

            var response = new ExecuteScriptResponse
            {
                Definition = definitionSlug,
                CorrelationId = correlationId,
                Truncated = result.Truncated,
                ResultCount = data is UnifiedSearchResult[] arr ? arr.Length : null,
                Data = data
            };

            logger.ExecuteScriptCompleted(definitionSlug, stopwatch.ElapsedMilliseconds, correlationId);
            ExecuteScriptMetrics.RequestCompleted(definitionSlug, stopwatch.Elapsed.TotalMilliseconds);
            await WriteAuditAsync(definitionSlug, correlationId, command.Parameters, diagnosticsUrl, stopwatch.ElapsedMilliseconds, "Success", null, cancellationToken);
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.ExecuteScriptFailed(definitionSlug, correlationId, exception);
            ExecuteScriptMetrics.RequestFailed(definitionSlug, stopwatch.Elapsed.TotalMilliseconds);
            await WriteAuditAsync(definitionSlug, correlationId, command.Parameters, diagnosticsUrl, stopwatch.ElapsedMilliseconds, MapStatus(exception), exception.Message, cancellationToken);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static string MapStatus(Exception exception) => exception switch
    {
        UnknownDefinitionException => "ValidationError",
        MissingParametersException => "ValidationError",
        ServiceBusyException => "Busy",
        TimeoutException => "Timeout",
        _ => "Error"
    };

    private Task WriteAuditAsync(
        string definitionSlug, string correlationId, Dictionary<string, object?>? parameters,
        string? diagnosticsUrl, long durationMs, string status, string? errorDetail,
        CancellationToken cancellationToken) => auditRepository.SaveAsync(
            new ScriptExecutionAuditEntry
            {
                DefinitionSlug = definitionSlug,
                CorrelationId = correlationId,
                InputParameters = parameters is not null ? JsonSerializer.Serialize(parameters) : null,
                DurationMs = durationMs,
                Status = status,
                ErrorDetail = errorDetail,
                DiagnosticsUrl = diagnosticsUrl,
                CallerIdentity = httpContextAccessor.HttpContext?.User.GetCallerIdentity()
            }, cancellationToken);

    private object? ApplyPostProcessingMapper(object? output, string definitionSlug)
    {
        var jsonElement = output switch
        {
            JsonElement je => je,
            Dictionary<string, object?> dict => JsonSerializer.SerializeToElement(dict),
            string s when !string.IsNullOrEmpty(s) => TryParseJsonElement(s),
            _ => (JsonElement?)null
        };

        if (jsonElement is not { } element)
        {
            return output;
        }

        if (definitionSlug.EndsWith("-business-search", StringComparison.OrdinalIgnoreCase))
        {
            var mapped = searchResultMapper.MapToUnified(element, definitionSlug);
            if (mapped is not null)
            {
                return mapped;
            }
        }

        if (definitionSlug.EndsWith("-entity-details", StringComparison.OrdinalIgnoreCase))
        {
            var mapped = entityDetailMapper.MapToUnified(element, definitionSlug);
            if (mapped is not null)
            {
                return mapped;
            }
        }

        return output;
    }

    private static bool IsEntityDetailsDefinition(string slug) =>
        slug.EndsWith("-entity-details", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetString(object? value, out string result)
    {
        switch (value)
        {
            case string s:
                result = s;
                return !string.IsNullOrWhiteSpace(s);
            case JsonElement { ValueKind: JsonValueKind.String } je:
                result = je.GetString()!;
                return !string.IsNullOrWhiteSpace(result);
            default:
                result = string.Empty;
                return false;
        }
    }

    private static string? GetExpectedStatePrefix(string slug)
    {
        var parts = slug.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}".ToUpperInvariant() : null;
    }

    private static JsonElement? TryParseJsonElement(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
