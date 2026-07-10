using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Options;

namespace BizScraper.Api.Features.HealthCheck;

/// <summary>
/// Registers liveness (/healthz) and readiness (/ready) health check endpoints.
/// </summary>
public static class HealthCheckEndpoints
{
    public static IEndpointRouteBuilder MapHealthCheckEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" }));
        endpoints.MapGet(
            "/ready",
            async Task<IResult> (GenericScriptScraper scraper, IOptions<PlaywrightOptions> opts, CancellationToken cancellationToken) =>
            {
                var health = await scraper.CanConnectAsync(cancellationToken);
                return health.Status == "NotReady"
                    ? Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not Ready", detail: "All browser modes are unhealthy.")
                    : Results.Ok(health);
            });

        return endpoints;
    }
}
