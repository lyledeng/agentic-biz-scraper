using BizScraper.Api.Common.Interfaces;

namespace BizScraper.Api.Infrastructure.Storage;

/// <summary>
/// No-op blob storage client for local development when cloud storage is disabled.
/// </summary>
public sealed class NullBlobStorageClient : IBlobStorageClient
{
    public Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken) => Task.CompletedTask;

    public Uri? GetFolderUrl(string folderPath) => null;

    public Task<BlobDownloadResult?> DownloadBlobAsync(string blobPath, CancellationToken cancellationToken) =>
        Task.FromResult<BlobDownloadResult?>(null);

    public Task<string?> GetBlobETagAsync(string blobPath, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}