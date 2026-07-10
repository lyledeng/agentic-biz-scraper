using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.BusinessSearch.Exceptions;
using Flurl.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace BizScraper.Api.Infrastructure.CaptchaSolving;

/// <summary>
/// Solves CAPTCHA challenges by sending image data to the FAB AI agent service.
/// </summary>
public sealed class FabCaptchaSolver(
    IOptions<FabAgentOptions> options,
    ILogger<FabCaptchaSolver> logger) : ICaptchaSolver
{
    private readonly FabAgentOptions _options = options.Value;
    private readonly ResiliencePipeline<FabCaptchaSolverResponse> _pipeline = BuildPipeline();

    public async Task<string> SolveAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.CaptchaSolver.EndpointUrl))
        {
            throw new CaptchaResolutionException("FAB CAPTCHA solver is not configured. Set FabAgent:CaptchaSolver:EndpointUrl.");
        }

        if (string.IsNullOrWhiteSpace(_options.CaptchaSolver.AuthToken))
        {
            throw new CaptchaResolutionException("FAB CAPTCHA solver is not configured. Set FabAgent:CaptchaSolver:AuthToken.");
        }

        var base64Image = Convert.ToBase64String(imageBytes);
        var request = new FabCaptchaSolverRequest(new FabCaptchaSolverInput("base64", base64Image));

        var response = await _pipeline.ExecuteAsync(async token =>
        {
            var result = await _options.CaptchaSolver.EndpointUrl
                .WithHeader("x-authentication", FormatAuthenticationHeader(_options.CaptchaSolver.AuthToken))
                .WithTimeout(TimeSpan.FromSeconds(_options.CaptchaSolver.TimeoutSeconds))
                .PostJsonAsync(request, cancellationToken: token)
                .ReceiveJson<FabCaptchaSolverResponse>();

            return result;
        }, cancellationToken);

        if (response?.Output is null || string.IsNullOrWhiteSpace(response.Output.CaptchaText))
        {
            throw new CaptchaResolutionException("FAB agent returned an empty or null CAPTCHA solution.");
        }

        logger.LogInformation("FAB CAPTCHA text received: '{CaptchaText}'.", response.Output.CaptchaText);
        logger.LogInformation("FAB CAPTCHA solved with confidence '{Confidence}'.", response.Output.Confidence);
        return response.Output.CaptchaText;
    }

    private static string FormatAuthenticationHeader(string authToken) =>
        authToken.StartsWith("api-key ", StringComparison.OrdinalIgnoreCase)
            ? authToken
            : $"api-key {authToken}";

    private static ResiliencePipeline<FabCaptchaSolverResponse> BuildPipeline()
    {
        var builder = new ResiliencePipelineBuilder<FabCaptchaSolverResponse>();
        builder.AddRetry(new RetryStrategyOptions<FabCaptchaSolverResponse>
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = new PredicateBuilder<FabCaptchaSolverResponse>()
                .Handle<FlurlHttpException>(ex => ex.StatusCode is >= 500 or null)
                .Handle<FlurlHttpTimeoutException>()
        });
        return builder.Build();
    }
}
