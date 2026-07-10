using BizScraper.Api.Features.HealthCheck;

namespace BizScraper.UnitTests.Features.HealthCheck;

public sealed class HealthCheckWindowsProxyTests
{
    [Fact]
    public void WindowsProxyStatus_Healthy_HasEndpoint()
    {
        var status = new WindowsProxyStatus("healthy", "https://vm.example.com/api", null);

        Assert.Equal("healthy", status.Status);
        Assert.Equal("https://vm.example.com/api", status.Endpoint);
        Assert.Null(status.Error);
    }

    [Fact]
    public void WindowsProxyStatus_Unhealthy_HasError()
    {
        var status = new WindowsProxyStatus("unhealthy", "https://vm.example.com/api", "Connection refused");

        Assert.Equal("unhealthy", status.Status);
        Assert.NotNull(status.Error);
    }

    [Fact]
    public void WindowsProxyStatus_NotConfigured_NullEndpoint()
    {
        var status = new WindowsProxyStatus("not-configured", null, null);

        Assert.Equal("not-configured", status.Status);
        Assert.Null(status.Endpoint);
    }

    [Fact]
    public void ReadinessHealthResponse_WithWindowsProxy_IncludesProxyStatus()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("healthy", "chrome", null, null)
        };
        var windowsProxy = new WindowsProxyStatus("healthy", "https://vm.example.com/api", null);
        var windowsDefs = new[] { "us-ia-business-search", "us-ia-entity-details" };

        var response = new ReadinessHealthResponse("Ready", modes, ["us-co-business-search"], [], windowsProxy, windowsDefs);

        Assert.NotNull(response.WindowsProxy);
        Assert.Equal("healthy", response.WindowsProxy.Status);
        Assert.Equal(2, response.DefinitionsRoutedToWindows!.Count);
        Assert.Contains("us-ia-business-search", response.DefinitionsRoutedToWindows);
        Assert.Contains("us-ia-entity-details", response.DefinitionsRoutedToWindows);
    }

    [Fact]
    public void ReadinessHealthResponse_WithoutWindowsProxy_NullProxyStatus()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("healthy", "chrome", null, null)
        };

        var response = new ReadinessHealthResponse("Ready", modes, ["us-co-business-search"], []);

        Assert.Null(response.WindowsProxy);
        Assert.Null(response.DefinitionsRoutedToWindows);
    }

    [Fact]
    public void ReadinessHealthResponse_UnhealthyProxy_DoesNotAffectOverallStatus()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("healthy", "chrome", null, null)
        };
        var windowsProxy = new WindowsProxyStatus("unhealthy", "https://vm.example.com/api", "Connection refused");

        // Overall status is "Ready" because the local browser pool is healthy —
        // unhealthy Windows proxy doesn't cause NotReady
        var response = new ReadinessHealthResponse("Ready", modes, ["us-co-business-search"], [], windowsProxy, ["us-ia-business-search"]);

        Assert.Equal("Ready", response.Status);
        Assert.Equal("unhealthy", response.WindowsProxy!.Status);
    }
}
