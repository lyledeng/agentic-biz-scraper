using Azure;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Infrastructure;

public sealed class AzureBlobStorageClientTests
{
    [Fact]
    public async Task UploadAsync_RetriesTransientFailures_AndEventuallySucceeds()
    {
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "payload");
        var attempts = 0;

        try
        {
            var client = new AzureBlobStorageClient(
                new CloudStorageOptions { Enabled = true, ContainerName = "diagnostics" },
                NullLogger<AzureBlobStorageClient>.Instance,
                new Uri("https://example.blob.core.windows.net/diagnostics"),
                async (_, _, _) =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw new RequestFailedException(503, "temporary");
                    }

                    await Task.CompletedTask;
                });

            await client.UploadAsync("req-1/file.txt", filePath, CancellationToken.None);

            Assert.Equal(3, attempts);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GetFolderUrl_SanitizesCorrelationId()
    {
        var client = new AzureBlobStorageClient(
            new CloudStorageOptions { Enabled = true, ContainerName = "diagnostics" },
            NullLogger<AzureBlobStorageClient>.Instance,
            new Uri("https://example.blob.core.windows.net/diagnostics"));

        var folderUrl = client.GetFolderUrl("req:1");

        Assert.Equal(new Uri("https://example.blob.core.windows.net/diagnostics/req%3A1"), folderUrl);
    }

    [Fact]
    public async Task NullBlobStorageClient_IsNoOp_AndReturnsNullFolderUrl()
    {
        var client = new NullBlobStorageClient();

        await client.UploadAsync("req-1/file.txt", "C:\\temp\\file.txt", CancellationToken.None);

        Assert.Null(client.GetFolderUrl("req:1"));
    }
}