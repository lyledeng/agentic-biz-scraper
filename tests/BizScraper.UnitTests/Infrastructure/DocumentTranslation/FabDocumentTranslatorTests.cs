using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.DocumentTranslation;
using Flurl.Http;
using Flurl.Http.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BizScraper.UnitTests.Infrastructure.DocumentTranslation;

public sealed class FabDocumentTranslatorTests : IDisposable
{
    private readonly HttpTest _httpTest = new();
    private readonly byte[] _samplePdf = [0x25, 0x50, 0x44, 0x46]; // %PDF

    private FabDocumentTranslator CreateTranslator(string endpointUrl = "https://fab.test/api/translate", string authToken = "test-token", int timeoutSeconds = 60)
    {
        var options = Options.Create(new FabDocumentTranslatorOptions
        {
            EndpointUrl = endpointUrl,
            AuthToken = authToken,
            TimeoutSeconds = timeoutSeconds
        });
        return new FabDocumentTranslator(options, NullLogger<FabDocumentTranslator>.Instance);
    }

    [Fact]
    public async Task TranslateAsync_ValidPdf_ReturnsMarkdown()
    {
        _httpTest.RespondWithJson(new
        {
            output = new { translatedMarkdown = "# Heading\n\nSome content", sourceLanguage = "de" }
        });

        var translator = CreateTranslator();
        var result = await translator.TranslateAsync(_samplePdf, CancellationToken.None);

        Assert.Equal("# Heading\n\nSome content", result.TranslatedMarkdown);
        Assert.Equal("de", result.SourceLanguage);
    }

    [Fact]
    public async Task TranslateAsync_5xxResponse_Throws()
    {
        // 3 attempts (1 initial + 2 retries), all 500
        _httpTest.RespondWith(status: 500);
        _httpTest.RespondWith(status: 500);
        _httpTest.RespondWith(status: 500);

        var translator = CreateTranslator();

        await Assert.ThrowsAsync<FlurlHttpException>(
            () => translator.TranslateAsync(_samplePdf, CancellationToken.None));
    }

    [Fact]
    public async Task TranslateAsync_EmptyTranslation_Throws()
    {
        _httpTest.RespondWithJson(new
        {
            output = new { translatedMarkdown = "", sourceLanguage = "de" }
        });

        var translator = CreateTranslator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => translator.TranslateAsync(_samplePdf, CancellationToken.None));
    }

    [Fact]
    public async Task TranslateAsync_NullOutput_Throws()
    {
        _httpTest.RespondWithJson(new { output = (object?)null });

        var translator = CreateTranslator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => translator.TranslateAsync(_samplePdf, CancellationToken.None));
    }

    [Fact]
    public async Task TranslateAsync_MissingEndpointUrl_Throws()
    {
        var translator = CreateTranslator(endpointUrl: "");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => translator.TranslateAsync(_samplePdf, CancellationToken.None));
    }

    [Fact]
    public async Task TranslateAsync_MissingAuthToken_Throws()
    {
        var translator = CreateTranslator(authToken: "");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => translator.TranslateAsync(_samplePdf, CancellationToken.None));
    }

    [Fact]
    public async Task TranslateAsync_SendsCorrectAuthHeader()
    {
        _httpTest.RespondWithJson(new
        {
            output = new { translatedMarkdown = "# Test", sourceLanguage = "de" }
        });

        var translator = CreateTranslator(authToken: "my-secret");
        await translator.TranslateAsync(_samplePdf, CancellationToken.None);

        _httpTest.ShouldHaveMadeACall()
            .WithHeader("x-authentication", "api-key my-secret");
    }

    [Fact]
    public async Task TranslateAsync_SendsBase64EncodedPdf()
    {
        _httpTest.RespondWithJson(new
        {
            output = new { translatedMarkdown = "# Test", sourceLanguage = "de" }
        });

        var translator = CreateTranslator();
        await translator.TranslateAsync(_samplePdf, CancellationToken.None);

        _httpTest.ShouldHaveMadeACall()
            .WithContentType("application/json");
    }

    public void Dispose() => _httpTest.Dispose();
}
