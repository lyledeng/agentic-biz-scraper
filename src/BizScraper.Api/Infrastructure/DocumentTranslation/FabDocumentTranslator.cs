using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Interfaces;
using Flurl.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace BizScraper.Api.Infrastructure.DocumentTranslation;

/// <summary>
/// Translates foreign-language documents to English via the FAB document translation API.
/// </summary>
public sealed class FabDocumentTranslator(
    IOptions<FabDocumentTranslatorOptions> options,
    ILogger<FabDocumentTranslator> logger) : IDocumentTranslator
{
    private readonly FabDocumentTranslatorOptions _options = options.Value;
    private readonly ResiliencePipeline<FabTranslationResponse> _pipeline = BuildPipeline();

    public async Task<DocumentTranslationResult> TranslateAsync(byte[] documentBytes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.EndpointUrl))
        {
            throw new InvalidOperationException("FAB Document Translator is not configured. Set FabDocumentTranslator:EndpointUrl.");
        }

        if (string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            throw new InvalidOperationException("FAB Document Translator is not configured. Set FabDocumentTranslator:AuthToken.");
        }

        var base64Document = Convert.ToBase64String(documentBytes);
        var request = new FabTranslationRequest(new FabTranslationInput("base64", base64Document));

        var response = await _pipeline.ExecuteAsync(async token =>
        {
            var result = await _options.EndpointUrl
                .WithHeader("x-authentication", FormatAuthenticationHeader(_options.AuthToken))
                .WithTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
                .PostJsonAsync(request, cancellationToken: token)
                .ReceiveJson<FabTranslationResponse>();

            return result;
        }, cancellationToken);

        if (response?.Output is null)
        {
            logger.LogError("FAB Document Translator returned an empty or null translation. Raw response: {Response}",
                System.Text.Json.JsonSerializer.Serialize(response));
            throw new InvalidOperationException("FAB Document Translator returned an empty or null translation.");
        }

        var translatedMarkdown = ResolveTranslatedMarkdown(response.Output);
        if (string.IsNullOrWhiteSpace(translatedMarkdown))
        {
            logger.LogError("FAB Document Translator returned an empty or null translation. Raw response: {Response}",
                System.Text.Json.JsonSerializer.Serialize(response));
            throw new InvalidOperationException("FAB Document Translator returned an empty or null translation.");
        }

        var sourceLanguage = string.IsNullOrWhiteSpace(response.Output.SourceLanguage)
            ? "de"
            : response.Output.SourceLanguage;

        logger.LogInformation("FAB document translation received. Source language: '{SourceLanguage}'.", sourceLanguage);
        return new DocumentTranslationResult(translatedMarkdown, sourceLanguage);
    }

    private static string? ResolveTranslatedMarkdown(FabTranslationOutput output)
    {
        if (!string.IsNullOrWhiteSpace(output.TranslatedMarkdown))
        {
            return output.TranslatedMarkdown;
        }

        if (string.IsNullOrWhiteSpace(output.Content))
        {
            return null;
        }

        var content = output.Content.Trim();
        if (content.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = content.IndexOf('\n', StringComparison.Ordinal);
            if (firstNewline > 0)
            {
                content = content[(firstNewline + 1)..];
            }

            if (content.EndsWith("```", StringComparison.Ordinal))
            {
                content = content[..^3].TrimEnd();
            }
        }

        return content;
    }

    private static string FormatAuthenticationHeader(string authToken) =>
        authToken.StartsWith("api-key ", StringComparison.OrdinalIgnoreCase)
            ? authToken
            : $"api-key {authToken}";

    private static ResiliencePipeline<FabTranslationResponse> BuildPipeline()
    {
        var builder = new ResiliencePipelineBuilder<FabTranslationResponse>();
        builder.AddRetry(new RetryStrategyOptions<FabTranslationResponse>
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder<FabTranslationResponse>()
                .Handle<FlurlHttpException>(ex => ex.StatusCode is >= 500 or null)
                .Handle<FlurlHttpTimeoutException>()
        });
        return builder.Build();
    }
}
