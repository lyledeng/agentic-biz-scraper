using System.Net;
using System.Text.Json;
using BizScraper.Api.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BizScraper.IntegrationTests.Features.Documents;

public sealed class DocumentErrorHandlingTests : IClassFixture<DocumentTestFactory>
{
    private readonly HttpClient _client;

    public DocumentErrorHandlingTests(DocumentTestFactory factory) =>
        _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/v1/documents/no-slash")]
    [InlineData("/api/v1/documents/folder/file name.pdf")]
    [InlineData("/api/v1/documents/folder/file@bad.pdf")]
    public async Task StreamDocument_InvalidBlobPath_Returns400ProblemDetails(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(body);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.NotNull(problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task StreamDocument_MissingBlob_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/documents/nonexistent/doc.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 404 NotFound but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(body);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Document not found", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task StreamDocument_ProblemDetails_ConformsToRFC9457()
    {
        var response = await _client.GetAsync("/api/v1/documents/nonexistent/doc.pdf");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(body);

        // RFC 9457 requires: type, title, status
        Assert.True(problem.RootElement.TryGetProperty("status", out _));
        Assert.True(problem.RootElement.TryGetProperty("title", out _));
    }
}
