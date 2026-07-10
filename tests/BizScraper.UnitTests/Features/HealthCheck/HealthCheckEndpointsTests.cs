using BizScraper.Api.Features.HealthCheck;

namespace BizScraper.UnitTests.Features.HealthCheck;

public sealed class HealthCheckEndpointsTests
{
    private static readonly System.Text.Json.JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void ReadinessHealthResponse_AllHealthy_StatusIsReady()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("healthy", "chrome", null, null),
            ["remote"] = new("healthy", null, "wss://remote", null)
        };

        var response = new ReadinessHealthResponse("Ready", modes, ["us-ia-business-search"], []);

        Assert.Equal("Ready", response.Status);
        Assert.Equal(2, response.BrowserModes.Count);
        Assert.Equal("healthy", response.BrowserModes["local"].Status);
        Assert.Equal("healthy", response.BrowserModes["remote"].Status);
    }

    [Fact]
    public void ReadinessHealthResponse_OneUnhealthy_StatusIsDegraded()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("healthy", "chrome", null, null),
            ["remote"] = new("unhealthy", null, "wss://remote", "Connection failed")
        };

        var response = new ReadinessHealthResponse("Degraded", modes, ["us-ia-business-search"], []);

        Assert.Equal("Degraded", response.Status);
        Assert.Equal("healthy", response.BrowserModes["local"].Status);
        Assert.Equal("unhealthy", response.BrowserModes["remote"].Status);
        Assert.Equal("Connection failed", response.BrowserModes["remote"].Error);
    }

    [Fact]
    public void ReadinessHealthResponse_AllUnhealthy_StatusIsNotReady()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("unhealthy", null, null, "Launch failed"),
            ["remote"] = new("unhealthy", null, "wss://remote", "Connection failed")
        };

        var response = new ReadinessHealthResponse("NotReady", modes, [], []);

        Assert.Equal("NotReady", response.Status);
    }

    [Fact]
    public void ReadinessHealthResponse_DefinitionArrays_Populated()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("healthy", "chrome", null, null),
            ["remote"] = new("healthy", null, "wss://remote", null)
        };

        var localDefs = new[] { "us-ia-business-search", "us-ia-entity-details" };
        var remoteDefs = new[] { "us-co-business-search" };

        var response = new ReadinessHealthResponse("Ready", modes, localDefs, remoteDefs);

        Assert.Equal(2, response.DefinitionsRequiringLocal.Count);
        Assert.Contains("us-ia-business-search", response.DefinitionsRequiringLocal);
        Assert.Contains("us-ia-entity-details", response.DefinitionsRequiringLocal);
        Assert.Single(response.DefinitionsRequiringRemote);
        Assert.Contains("us-co-business-search", response.DefinitionsRequiringRemote);
    }

    [Fact]
    public void ModeStatus_NotConfigured_HasCorrectStatus()
    {
        var status = new ModeStatus("not-configured", null, null, null);

        Assert.Equal("not-configured", status.Status);
        Assert.Null(status.Channel);
        Assert.Null(status.Endpoint);
        Assert.Null(status.Error);
    }

    [Fact]
    public void ModeStatus_HealthyLocal_IncludesChannel()
    {
        var status = new ModeStatus("healthy", "chrome", null, null);

        Assert.Equal("healthy", status.Status);
        Assert.Equal("chrome", status.Channel);
        Assert.Null(status.Endpoint);
    }

    [Fact]
    public void ModeStatus_HealthyRemote_IncludesEndpoint()
    {
        var status = new ModeStatus("healthy", null, "wss://eastus.api.playwright.microsoft.com", null);

        Assert.Equal("healthy", status.Status);
        Assert.Null(status.Channel);
        Assert.Equal("wss://eastus.api.playwright.microsoft.com", status.Endpoint);
    }

    [Fact]
    public void ReadinessHealthResponse_JsonRoundTrip()
    {
        var modes = new Dictionary<string, ModeStatus>
        {
            ["local"] = new("healthy", "chrome", null, null),
            ["remote"] = new("unhealthy", null, "wss://remote", "timeout")
        };

        var response = new ReadinessHealthResponse("Degraded", modes, ["us-ia-business-search"], []);

        var json = System.Text.Json.JsonSerializer.Serialize(response, CamelCaseOptions);
        Assert.Contains("\"status\":\"Degraded\"", json, StringComparison.Ordinal);
        Assert.Contains("\"browserModes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"definitionsRequiringLocal\"", json, StringComparison.Ordinal);
    }
}
