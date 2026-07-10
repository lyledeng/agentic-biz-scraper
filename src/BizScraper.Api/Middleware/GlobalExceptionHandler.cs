using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BizScraper.Api.Features.BusinessSearch.Exceptions;
using BizScraper.Api.Features.EntityDetails.Exceptions;
using BizScraper.Api.Features.ExecuteScript.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BizScraper.Api.Middleware;

/// <summary>
/// Maps domain exceptions to RFC 7807 ProblemDetails HTTP error responses.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IWebHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing request {Path}.", httpContext.Request.Path);

        var (statusCode, type, title, detail, retryAfterSeconds) = exception switch
        {
            UnknownDefinitionException unknownDefinitionException => (StatusCodes.Status400BadRequest, "https://BizScraper.local/errors/unknown-definition", "Bad Request", unknownDefinitionException.Message, (int?)null),
            MissingParametersException missingParametersException => (StatusCodes.Status400BadRequest, "https://BizScraper.local/errors/missing-parameters", "Bad Request", missingParametersException.Message, (int?)null),
            InvalidDetailsUrlException invalidDetailsUrlException => (StatusCodes.Status400BadRequest, "https://BizScraper.local/errors/invalid-details-url", "Invalid Details URL", invalidDetailsUrlException.Message, (int?)null),
            ValidationException validationException => (StatusCodes.Status400BadRequest, "https://BizScraper.local/errors/validation-error", "Validation Error", validationException.Message, (int?)null),
            ExceededRecordCountException exceededRecordCountException => (StatusCodes.Status422UnprocessableEntity, "https://BizScraper.local/errors/exceeded-record-count", "Exceeded Record Count", exceededRecordCountException.Message, (int?)null),
            ServiceBusyException serviceBusyException => (StatusCodes.Status503ServiceUnavailable, "https://BizScraper.local/errors/service-busy", "Service Busy", serviceBusyException.Message, serviceBusyException.RetryAfterSeconds),
            TimeoutException timeoutException => (StatusCodes.Status504GatewayTimeout, "https://BizScraper.local/errors/execution-timeout", "Execution Timeout", timeoutException.Message, (int?)null),
            ScrapingException scrapingException => (StatusCodes.Status502BadGateway, "https://BizScraper.local/errors/scraping-error", "Scraping Error", scrapingException.Message, (int?)null),
            CaptchaResolutionException captchaResolutionException => (StatusCodes.Status502BadGateway, "https://BizScraper.local/errors/captcha-resolution-failed", "CAPTCHA Resolution Failed", captchaResolutionException.Message, (int?)null),
            UpstreamException upstreamException => (StatusCodes.Status502BadGateway, "https://BizScraper.local/errors/upstream-error", "Upstream Error", upstreamException.Message, (int?)null),
            _ => (StatusCodes.Status500InternalServerError, "https://BizScraper.local/errors/unhandled-error", "Unhandled Error", "An unexpected error occurred.", (int?)null)
        };

        if (retryAfterSeconds is not null)
        {
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (!environment.IsProduction())
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
            problemDetails.Extensions["exceptionMessage"] = exception.Message;
        }

        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problemDetails, cancellationToken: cancellationToken);
        return true;
    }
}
