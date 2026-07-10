using BizScraper.Api.Features.Documents.Handlers;
using BizScraper.Api.Features.Documents.Models;
using BizScraper.Api.Features.Documents.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BizScraper.Api.Features.Documents.Endpoints;

/// <summary>
/// Registers the PDF document proxy streaming endpoint.
/// </summary>
public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/documents/{**blobPath}",
                async Task<IResult> (string blobPath, bool? download,
                    HttpContext httpContext, StreamDocumentHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    if (!BlobPathValidator.IsValid(blobPath))
                    {
                        return Results.Problem(
                            title: "Invalid blob path",
                            detail: $"The blob path '{blobPath}' does not match the allowed pattern.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    var ifNoneMatch = httpContext.Request.Headers.IfNoneMatch.FirstOrDefault();
                    var query = new StreamDocumentQuery(blobPath, ifNoneMatch, download ?? false);
                    var result = await handler.HandleAsync(query, cancellationToken);

                    // Set caching headers for successful stream responses
                    if (result is FileStreamHttpResult)
                    {
                        httpContext.Response.Headers.CacheControl = "public, max-age=86400, immutable";
                    }

                    return result;
                })
            .WithName("StreamDocument")
            .WithTags("Documents")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return endpoints;
    }
}
