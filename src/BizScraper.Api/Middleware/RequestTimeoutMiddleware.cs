using System.Text.Json;

namespace BizScraper.Api.Middleware;

/// <summary>
/// Middleware that enforces a configurable request timeout via a cancellation token.
/// </summary>
public sealed class RequestTimeoutMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var timeoutSeconds = configuration.GetValue<int?>("Playwright:RequestTimeoutSeconds") ?? 120;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var pipelineTask = next(context);
            var completedTask = await Task.WhenAny(pipelineTask, Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token));
            if (completedTask == pipelineTask)
            {
                await pipelineTask;
                return;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.RequestAborted.IsCancellationRequested)
        {
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        context.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                type = "https://BizScraper.local/errors/request-timeout",
                title = "Request Timeout",
                status = 504,
                detail = "The request exceeded the configured processing timeout."
            },
            cancellationToken: context.RequestAborted);
    }
}
