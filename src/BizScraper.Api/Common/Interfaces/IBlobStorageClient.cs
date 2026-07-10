namespace BizScraper.Api.Common.Interfaces;

/// <summary>
/// Abstraction for uploading, downloading, and querying files in blob storage.
/// </summary>
public interface IBlobStorageClient
{
    Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken);

    Uri? GetFolderUrl(string folderPath);

    Task<BlobDownloadResult?> DownloadBlobAsync(string blobPath, CancellationToken cancellationToken);

    Task<string?> GetBlobETagAsync(string blobPath, CancellationToken cancellationToken);
}