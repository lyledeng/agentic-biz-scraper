using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BizScraper.IntegrationTests.Features.BusinessSearch;

public sealed class CloudStorageStartupTests
{
    [Fact]
    public void CreateClient_Throws_WhenCloudStorageEnabledWithoutRequiredSettings()
    {
        using var factory = new InvalidCloudStorageApplicationFactory();

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("CloudStorage:ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    private sealed class InvalidCloudStorageApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Playwright:Diagnostics:CloudStorage:Enabled"] = "true",
                    ["Playwright:Diagnostics:CloudStorage:ConnectionString"] = string.Empty,
                    ["Playwright:Diagnostics:CloudStorage:ContainerName"] = string.Empty
                });
            });
        }
    }
}