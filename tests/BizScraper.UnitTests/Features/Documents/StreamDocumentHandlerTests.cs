using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.Documents.Handlers;
using BizScraper.Api.Features.Documents.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Azure;
using Polly.Timeout;

namespace BizScraper.UnitTests.Features.Documents;

public sealed class StreamDocumentHandlerTests
{
    private readonly IBlobStorageClient _blobClient = Substitute.For<IBlobStorageClient>();
    private readonly ILogger<StreamDocumentHandler> _logger = Substitute.For<ILogger<StreamDocumentHandler>>();

    private StreamDocumentHandler CreateHandler() => new(_blobClient, _logger);

    [Fact]
    public async Task HandleAsync_ValidBlob_ReturnsStreamResult()
    {
        var stream = new MemoryStream("fake-pdf-content"u8.ToArray());
        var downloadResult = new BlobDownloadResult(stream, "application/pdf", 16, "\"etag123\"", "cert.pdf");
        _blobClient.DownloadBlobAsync("corr/cert.pdf", Arg.Any<CancellationToken>())
            .Returns(downloadResult);

        var query = new StreamDocumentQuery("corr/cert.pdf", null, false);
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Results.Stream returns a FileStreamHttpResult
        Assert.IsAssignableFrom<FileStreamHttpResult>(result);
    }

    [Fact]
    public async Task HandleAsync_BlobNotFound_Returns404()
    {
        _blobClient.DownloadBlobAsync("corr/missing.pdf", Arg.Any<CancellationToken>())
            .Returns((BlobDownloadResult?)null);

        var query = new StreamDocumentQuery("corr/missing.pdf", null, false);
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_StorageUnreachable_Returns502()
    {
        _blobClient.DownloadBlobAsync("corr/fail.pdf", Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(503, "Service unavailable"));

        var query = new StreamDocumentQuery("corr/fail.pdf", null, false);
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, problemResult.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_Timeout_Returns504()
    {
        _blobClient.DownloadBlobAsync("corr/slow.pdf", Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutRejectedException("Timed out"));

        var query = new StreamDocumentQuery("corr/slow.pdf", null, false);
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, problemResult.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_ConditionalRequestMatch_Returns304()
    {
        _blobClient.GetBlobETagAsync("corr/cached.pdf", Arg.Any<CancellationToken>())
            .Returns("\"etag-match\"");

        var query = new StreamDocumentQuery("corr/cached.pdf", "\"etag-match\"", false);
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        var statusResult = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_ConditionalRequestNoMatch_StreamsBlob()
    {
        _blobClient.GetBlobETagAsync("corr/changed.pdf", Arg.Any<CancellationToken>())
            .Returns("\"etag-new\"");
        var stream = new MemoryStream("pdf-data"u8.ToArray());
        var downloadResult = new BlobDownloadResult(stream, "application/pdf", 8, "\"etag-new\"", "changed.pdf");
        _blobClient.DownloadBlobAsync("corr/changed.pdf", Arg.Any<CancellationToken>())
            .Returns(downloadResult);

        var query = new StreamDocumentQuery("corr/changed.pdf", "\"etag-old\"", false);
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        Assert.IsAssignableFrom<FileStreamHttpResult>(result);
    }

    [Fact]
    public async Task HandleAsync_ForceDownload_SetsAttachmentDisposition()
    {
        var stream = new MemoryStream("pdf-data"u8.ToArray());
        var downloadResult = new BlobDownloadResult(stream, "application/pdf", 8, "\"etag1\"", "report.pdf");
        _blobClient.DownloadBlobAsync("corr/report.pdf", Arg.Any<CancellationToken>())
            .Returns(downloadResult);

        var query = new StreamDocumentQuery("corr/report.pdf", null, true);
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        var fileResult = Assert.IsAssignableFrom<FileStreamHttpResult>(result);
        Assert.Equal("report.pdf", fileResult.FileDownloadName);
    }
}
