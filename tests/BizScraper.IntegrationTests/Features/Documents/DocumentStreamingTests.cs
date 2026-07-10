using System.Net;
using BizScraper.Api.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BizScraper.IntegrationTests.Features.Documents;

public sealed class DocumentStreamingTests : IClassFixture<DocumentTestFactory>
{
    private readonly HttpClient _client;

    public DocumentStreamingTests(DocumentTestFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task StreamDocument_ValidBlob_Returns200WithPdfContent()
    {
        var response = await _client.GetAsync("/api/v1/documents/test-correlation/sample.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StreamDocument_MissingBlob_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/documents/test-correlation/missing.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 404 NotFound but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StreamDocument_ConditionalMatch_Returns304()
    {
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/documents/test-correlation/sample.pdf")
        {
            Headers = { { "If-None-Match", "\"test-etag\"" } }
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.NotModified,
            $"Expected 304 NotModified but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task StreamDocument_ForceDownload_SetsContentDisposition()
    {
        var response = await _client.GetAsync("/api/v1/documents/test-correlation/sample.pdf?download=true");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Contains("sample.pdf", response.Content.Headers.ContentDisposition?.FileName ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamDocument_CacheHeaders_SetCorrectly()
    {
        var response = await _client.GetAsync("/api/v1/documents/test-correlation/sample.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Contains("public", response.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.MaxAge > TimeSpan.Zero);
    }
}

public sealed class DocumentTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBlobStorageClient>();
            services.AddSingleton<IBlobStorageClient>(new FakeBlobStorageClient());
            services.AddTestAuthentication();
        });
    }
}

internal sealed class FakeBlobStorageClient : IBlobStorageClient
{
    public Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Uri? GetFolderUrl(string folderPath) => null;

    public Task<BlobDownloadResult?> DownloadBlobAsync(string blobPath, CancellationToken cancellationToken)
    {
        if (string.Equals(blobPath, "test-correlation/sample.pdf", StringComparison.Ordinal))
        {
            var stream = new MemoryStream("fake-pdf-content"u8.ToArray());
            return Task.FromResult<BlobDownloadResult?>(
                new BlobDownloadResult(stream, "application/pdf", 16, "\"test-etag\"", "sample.pdf"));
        }

        return Task.FromResult<BlobDownloadResult?>(null);
    }

    public Task<string?> GetBlobETagAsync(string blobPath, CancellationToken cancellationToken)
    {
        if (string.Equals(blobPath, "test-correlation/sample.pdf", StringComparison.Ordinal))
        {
            return Task.FromResult<string?>("\"test-etag\"");
        }

        return Task.FromResult<string?>(null);
    }
}
