using System.Text.Json;
using BizScraper.Api.Features.ExecuteScript.Commands;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using LiteBus.Commands.Abstractions;

namespace BizScraper.Api.Features.ExecuteScript.Endpoints;

/// <summary>
/// Registers the generic execute-script and definitions listing endpoints.
/// </summary>
public static class ExecuteScriptEndpoints
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEndpointRouteBuilder MapExecuteScriptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // v1 execute-script — 410 Gone with migration guidance
        endpoints.MapPost(
                "/api/v1/execute-script",
                () => Results.Problem(
                    detail: "This endpoint has been removed. Use POST /api/v2/execute-script instead.",
                    statusCode: StatusCodes.Status410Gone,
                    type: "https://tools.ietf.org/html/rfc9110#section-15.5.11",
                    title: "Gone"))
            .WithName("ExecuteScriptV1Gone")
            .WithTags("Deprecated")
            .ProducesProblem(StatusCodes.Status410Gone)
            .ExcludeFromDescription();

        // v2 execute-script — unified response shapes via post-processing mappers
        endpoints.MapPost(
                "/api/v2/execute-script",
                async Task<IResult> (ExecuteScriptRequest request, ICommandMediator commandMediator, HttpContext httpContext, CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(request.Definition))
                    {
                        return Results.Problem(
                            detail: "The 'definition' field is required and must not be empty.",
                            statusCode: StatusCodes.Status400BadRequest,
                            type: "https://tools.ietf.org/html/rfc9457",
                            title: "Validation Error");
                    }

                    var correlationId = httpContext.TraceIdentifier;

                    var response = await commandMediator.SendAsync(
                        new ExecuteScriptCommand(request.Definition.Trim(), request.Parameters, correlationId),
                        cancellationToken);

                    httpContext.Response.Headers["X-Correlation-Id"] = correlationId;
                    return Results.Json(response, CamelCaseOptions, statusCode: StatusCodes.Status200OK);
                })
            .WithName("ExecuteScriptV2")
            .WithTags("Execute Script v2")
            .WithSummary("Execute a scraping definition and return unified results")
            .WithDescription("Runs a JSON flow definition (e.g. us-co-business-search, us-ia-business-search, us-wy-entity-details) and returns data through post-processing mappers that produce UnifiedSearchResult[] or UnifiedEntityDetailResponse shapes.")
            .Accepts<ExecuteScriptRequest>("application/json")
            .Produces<ExecuteScriptResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        // v1 definitions — 410 Gone with migration guidance
        endpoints.MapGet(
                "/api/v1/definitions",
                () => Results.Problem(
                    detail: "This endpoint has been removed. Use GET /api/v2/definitions instead.",
                    statusCode: StatusCodes.Status410Gone,
                    type: "https://tools.ietf.org/html/rfc9110#section-15.5.11",
                    title: "Gone"))
            .WithName("ListDefinitionsV1Gone")
            .WithTags("Deprecated")
            .ProducesProblem(StatusCodes.Status410Gone)
            .ExcludeFromDescription();

        // v2 definitions — same handler
        endpoints.MapGet(
                "/api/v2/definitions",
                (FlowDefinitionLoader flowLoader) =>
                {
                    var definitions = flowLoader.SlugIndex
                        .Select(kvp => new DefinitionInfo
                        {
                            DefinitionSlug = kvp.Key,
                            Name = kvp.Value.Metadata.Name,
                            Description = kvp.Value.Metadata.Description,
                            State = kvp.Value.Metadata.State,
                            RequiredParameters = kvp.Value.Variables
                                .Where(v => string.Equals(v.Source, "runtime", StringComparison.OrdinalIgnoreCase)
                                            && v.Required is true
                                            && !string.Equals(v.Name, "correlationId", StringComparison.OrdinalIgnoreCase))
                                .Select(v => new ParameterInfo { Name = v.Name, Description = v.Description })
                                .ToList()
                        })
                        .OrderBy(d => d.DefinitionSlug, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    return Results.Json(definitions, statusCode: StatusCodes.Status200OK);
                })
            .WithName("ListDefinitionsV2")
            .WithTags("Execute Script v2")
            .WithSummary("List available scraping definitions")
            .WithDescription("Returns all registered flow definitions with their slug, name, description, state, and required parameters. Definition slugs follow the {country}-{state}-{action} convention (e.g. us-ia-business-search, de-de-entity-details).")
            .Produces<DefinitionInfo[]>();

        return endpoints;
    }
}
