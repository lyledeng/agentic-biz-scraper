namespace BizScraper.Api.Common.Interfaces;

/// <summary>Stream handle + metadata for a downloaded blob. Caller must dispose after streaming.</summary>
public sealed record BlobDownloadResult(
    Stream Content,
    string ContentType,
    long ContentLength,
    string ETag,
    string FileName) : IDisposable, IAsyncDisposable
{
    public void Dispose() => Content.Dispose();
    public async ValueTask DisposeAsync() => await Content.DisposeAsync().ConfigureAwait(false);
}
