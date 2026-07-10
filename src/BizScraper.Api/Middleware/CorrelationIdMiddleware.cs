using MassTransit;
using Microsoft.Extensions.Primitives;

namespace BizScraper.Api.Middleware;

/// <summary>
/// Middleware that generates or propagates a correlation ID for request tracing.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out StringValues provided)
            && !StringValues.IsNullOrEmpty(provided)
            ? provided.ToString()
            : NewId.NextSequentialGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
