using Azure;
using Azure.Storage.Blobs;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.BusinessSearch.Logging;
using BizScraper.Api.Infrastructure.Scraping;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace BizScraper.Api.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation with Polly resilience for upload, download, and ETag operations.
/// </summary>
public sealed class AzureBlobStorageClient : IBlobStorageClient
{
    private readonly BlobContainerClient? _containerClient;
    private readonly string _containerName;
    private readonly Uri _containerUri;
    private readonly ILogger<AzureBlobStorageClient> _logger;
    private readonly Func<string, string, CancellationToken, Task>? _uploadOverride;
    private readonly Func<CancellationToken, Task>? _ensureContainerOverride;
    private readonly ResiliencePipeline _uploadPipeline;
    private readonly ResiliencePipeline _downloadPipeline;

    public AzureBlobStorageClient(IOptions<DiagnosticsOptions> diagnosticsOptions, ILogger<AzureBlobStorageClient> logger)
        : this(diagnosticsOptions.Value.CloudStorage, logger)
    {
    }

    internal AzureBlobStorageClient(
        CloudStorageOptions options,
        ILogger<AzureBlobStorageClient> logger,
        Uri? containerUri = null,
        Func<string, string, CancellationToken, Task>? uploadOverride = null,
        Func<CancellationToken, Task>? ensureContainerOverride = null)
    {
        _containerName = options.ContainerName;
        _logger = logger;
        _uploadOverride = uploadOverride;
        _ensureContainerOverride = ensureContainerOverride;
        _uploadPipeline = BuildUploadPipeline();
        _downloadPipeline = BuildDownloadPipeline();

        if (containerUri is not null)
        {
            _containerUri = containerUri;
            return;
        }

        var clientOptions = new BlobClientOptions();
        clientOptions.Retry.MaxRetries = 0;
        var serviceClient = new BlobServiceClient(options.ConnectionString, clientOptions);
        _containerClient = serviceClient.GetBlobContainerClient(options.ContainerName);
        _containerUri = _containerClient.Uri;
    }

    public async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (_ensureContainerOverride is not null)
        {
            await _ensureContainerOverride(cancellationToken).ConfigureAwait(false);
            _logger.DiagnosticsContainerReady(_containerName);
            return;
        }

        await _containerClient!.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.DiagnosticsContainerReady(_containerName);
    }

    public async Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken)
    {
        await _uploadPipeline.ExecuteAsync(
                static async (state, token) =>
                {
                    if (state.UploadOverride is not null)
                    {
                        await state.UploadOverride(state.BlobPath, state.LocalFilePath, token).ConfigureAwait(false);
                        return;
                    }

                    await using var stream = File.OpenRead(state.LocalFilePath);
                    await state.ContainerClient!
                        .GetBlobClient(state.BlobPath)
                        .UploadAsync(stream, overwrite: true, cancellationToken: token)
                        .ConfigureAwait(false);
                },
                (UploadOverride: _uploadOverride, BlobPath: blobPath, LocalFilePath: localFilePath, ContainerClient: _containerClient),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Uri? GetFolderUrl(string folderPath)
    {
        // Encode each segment individually to preserve path separators
        var encodedPath = string.Join("/", folderPath.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"{_containerUri.AbsoluteUri.TrimEnd('/')}/{encodedPath}", UriKind.Absolute);
    }

    public async Task<BlobDownloadResult?> DownloadBlobAsync(string blobPath, CancellationToken cancellationToken)
    {
        return await _downloadPipeline.ExecuteAsync(
                async (state, token) =>
                {
                    var blobClient = state.ContainerClient!.GetBlobClient(state.BlobPath);
                    try
                    {
                        var response = await blobClient.DownloadStreamingAsync(cancellationToken: token).ConfigureAwait(false);
                        var details = response.Value.Details;
                        var contentType = details.ContentType ?? "application/pdf";
                        var fileName = state.BlobPath.Contains('/', StringComparison.Ordinal)
                            ? state.BlobPath[(state.BlobPath.LastIndexOf('/') + 1)..]
                            : state.BlobPath;
                        return new BlobDownloadResult(
                            response.Value.Content,
                            contentType,
                            details.ContentLength,
                            details.ETag.ToString(),
                            fileName);
                    }
                    catch (RequestFailedException ex) when (ex.Status is 404)
                    {
                        return null;
                    }
                },
                (ContainerClient: _containerClient, BlobPath: blobPath),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> GetBlobETagAsync(string blobPath, CancellationToken cancellationToken)
    {
        return await _downloadPipeline.ExecuteAsync(
                async (state, token) =>
                {
                    var blobClient = state.ContainerClient!.GetBlobClient(state.BlobPath);
                    try
                    {
                        var properties = await blobClient.GetPropertiesAsync(cancellationToken: token).ConfigureAwait(false);
                        return properties.Value.ETag.ToString();
                    }
                    catch (RequestFailedException ex) when (ex.Status is 404)
                    {
                        return null;
                    }
                },
                (ContainerClient: _containerClient, BlobPath: blobPath),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ResiliencePipeline BuildUploadPipeline()
    {
        var builder = new ResiliencePipelineBuilder();
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<RequestFailedException>(static exception => exception.Status is 408 or 429 or 500 or 502 or 503 or 504)
        });
        builder.AddTimeout(TimeSpan.FromSeconds(30));
        return builder.Build();
    }

    private static ResiliencePipeline BuildDownloadPipeline()
    {
        var builder = new ResiliencePipelineBuilder();
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<RequestFailedException>(static exception => exception.Status is 408 or 429 or 500 or 502 or 503 or 504)
        });
        builder.AddTimeout(TimeSpan.FromSeconds(30));
        return builder.Build();
    }
}