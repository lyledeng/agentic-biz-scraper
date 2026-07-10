using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.Scraping;
using Microsoft.Extensions.Configuration;

namespace BizScraper.UnitTests.Common.Configuration;

public sealed class DiagnosticsOptionsTests
{
    [Fact]
    public void Defaults_AreConfigured()
    {
        var options = new DiagnosticsOptions();

        Assert.Equal(TracingMode.Always, options.TracingMode);
        Assert.True(options.ScreenshotsEnabled);
        Assert.Equal("diagnostics", options.OutputPath);
        Assert.False(options.CloudStorage.Enabled);
        Assert.Equal("diagnostics", options.CloudStorage.ContainerName);
    }

    [Fact]
    public void ConfigurationBinding_ParsesCustomValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Playwright:Diagnostics:TracingMode"] = "Off",
                ["Playwright:Diagnostics:ScreenshotsEnabled"] = "false",
                ["Playwright:Diagnostics:OutputPath"] = "custom-diagnostics",
                ["Playwright:Diagnostics:CloudStorage:Enabled"] = "true",
                ["Playwright:Diagnostics:CloudStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                ["Playwright:Diagnostics:CloudStorage:ContainerName"] = "diagnostics-dev"
            })
            .Build();

        var options = new DiagnosticsOptions();
        configuration.GetSection("Playwright:Diagnostics").Bind(options);

        Assert.Equal(TracingMode.Off, options.TracingMode);
        Assert.False(options.ScreenshotsEnabled);
        Assert.Equal("custom-diagnostics", options.OutputPath);
        Assert.True(options.CloudStorage.Enabled);
        Assert.Equal("UseDevelopmentStorage=true", options.CloudStorage.ConnectionString);
        Assert.Equal("diagnostics-dev", options.CloudStorage.ContainerName);
    }

    [Fact]
    public void ResolveDiagnosticsOutputPath_UsesContentRootForRelativePaths()
    {
        var contentRoot = Path.Combine("C:", "repo", "src");
        var resolved = DiagnosticsPathHelper.ResolveDiagnosticsOutputPath("diagnostics", contentRoot);

        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "diagnostics")), resolved);
    }
}