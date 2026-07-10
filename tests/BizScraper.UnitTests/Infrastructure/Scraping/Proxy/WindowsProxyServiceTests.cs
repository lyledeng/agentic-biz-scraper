using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Features.HealthCheck;
using BizScraper.Api.Infrastructure.Scraping.Proxy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Proxy;

public sealed class WindowsProxyServiceTests
{
    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        var options = Options.Create(new WindowsProxyOptions
        {
            EndpointUrl = "https://vm.example.com/mvpoc/bizscrapper-api",
            TimeoutSeconds = 180
        });

        var service = new WindowsProxyService(options, NullLogger<WindowsProxyService>.Instance);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task CheckWindowsProxyHealthAsync_NotConfigured_ReturnsNotConfigured()
    {
        var options = Options.Create(new WindowsProxyOptions { EndpointUrl = "" });
        var service = new WindowsProxyService(options, NullLogger<WindowsProxyService>.Instance);

        var result = await service.CheckWindowsProxyHealthAsync(CancellationToken.None);

        Assert.Equal("not-configured", result.Status);
        Assert.Null(result.Endpoint);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckWindowsProxyHealthAsync_Unreachable_ReturnsUnhealthy()
    {
        var options = Options.Create(new WindowsProxyOptions
        {
            EndpointUrl = "https://unreachable.invalid.local/api"
        });
        var service = new WindowsProxyService(options, NullLogger<WindowsProxyService>.Instance);

        var result = await service.CheckWindowsProxyHealthAsync(CancellationToken.None);

        Assert.Equal("unhealthy", result.Status);
        Assert.Equal("https://unreachable.invalid.local/api", result.Endpoint);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ForwardAsync_Unreachable_ThrowsWindowsProxyException503()
    {
        var options = Options.Create(new WindowsProxyOptions
        {
            EndpointUrl = "https://unreachable.invalid.local/api",
            TimeoutSeconds = 5
        });
        var service = new WindowsProxyService(options, NullLogger<WindowsProxyService>.Instance);

        var ex = await Assert.ThrowsAsync<WindowsProxyException>(
            () => service.ForwardAsync("test-slug", null, "corr-1", "Bearer token", CancellationToken.None));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal(30, ex.RetryAfterSeconds);
    }

    [Fact]
    public async Task ForwardAsync_Timeout_ThrowsWindowsProxyException504()
    {
        // Using an IP that will definitely time out (non-routable)
        var options = Options.Create(new WindowsProxyOptions
        {
            EndpointUrl = "https://192.0.2.1",
            TimeoutSeconds = 1
        });
        var service = new WindowsProxyService(options, NullLogger<WindowsProxyService>.Instance);

        var ex = await Assert.ThrowsAsync<WindowsProxyException>(
            () => service.ForwardAsync("test-slug", null, "corr-timeout", null, CancellationToken.None));

        // Should be either 503 (connection failure) or 504 (timeout) depending on timing
        Assert.True(ex.StatusCode is 503 or 504);
    }

    [Fact]
    public void WindowsProxyOptions_IsConfigured_WhenEndpointSet()
    {
        var opts = new WindowsProxyOptions { EndpointUrl = "https://vm.example.com" };
        Assert.True(opts.IsConfigured);
    }

    [Fact]
    public void WindowsProxyOptions_NotConfigured_WhenEndpointEmpty()
    {
        var opts = new WindowsProxyOptions();
        Assert.False(opts.IsConfigured);
    }

    [Fact]
    public void WindowsProxyOptions_NotConfigured_WhenEndpointWhitespace()
    {
        var opts = new WindowsProxyOptions { EndpointUrl = "   " };
        Assert.False(opts.IsConfigured);
    }
}
