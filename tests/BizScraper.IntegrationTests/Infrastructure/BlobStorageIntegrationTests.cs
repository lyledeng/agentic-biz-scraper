using Azure.Storage.Blobs;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.Storage;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Sockets;

namespace BizScraper.IntegrationTests.Infrastructure;

public sealed class BlobStorageIntegrationTests : IAsyncLifetime
{
    private const string AzuriteAccountName = "integrationtests";
    private const string AzuriteAccountKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private readonly IContainer _container = new ContainerBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite:3.31.0")
        .WithEnvironment("AZURITE_ACCOUNTS", $"{AzuriteAccountName}:{AzuriteAccountKey}")
        .WithPortBinding(10000, true)
        .WithCommand("azurite-blob", "--blobHost", "0.0.0.0", "--skipApiVersionCheck")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task UploadAsync_CreatesContainerAndUploadsBlob()
    {
        var port = _container.GetMappedPublicPort(10000);
        var connectionString = BuildConnectionString(port);
        var options = new CloudStorageOptions
        {
            Enabled = true,
            ConnectionString = connectionString,
            ContainerName = "diagnostics"
        };
        var client = new AzureBlobStorageClient(options, NullLogger<AzureBlobStorageClient>.Instance);
        var localFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(localFilePath, "payload");

        try
        {
            await WaitForAzuriteAsync(port);
            await EnsureContainerExistsAsync(client);
            await client.UploadAsync("req-1/file.txt", localFilePath, CancellationToken.None);

            var containerClient = new BlobContainerClient(connectionString, "diagnostics");
            var exists = await containerClient.GetBlobClient("req-1/file.txt").ExistsAsync();
            Assert.True(exists.Value);
        }
        finally
        {
            File.Delete(localFilePath);
        }
    }

    private static async Task WaitForAzuriteAsync(int port)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", port);
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        throw new TimeoutException("Azurite did not become ready in time.");
    }

    private static async Task EnsureContainerExistsAsync(AzureBlobStorageClient client)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                await client.EnsureContainerExistsAsync(CancellationToken.None);
                return;
            }
            catch (Exception exception)
            {
                lastException = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        throw new TimeoutException("Azurite blob container could not be created in time.", lastException);
    }

    private static string BuildConnectionString(int port) =>
        $"DefaultEndpointsProtocol=http;AccountName={AzuriteAccountName};AccountKey={AzuriteAccountKey};BlobEndpoint=http://127.0.0.1:{port}/{AzuriteAccountName};";
}