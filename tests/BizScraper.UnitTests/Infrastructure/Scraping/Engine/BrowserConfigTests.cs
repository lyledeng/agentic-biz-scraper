using System.Text.Json;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class BrowserConfigTests
{
    [Theory]
    [InlineData("local")]
    [InlineData("remote")]
    public void BrowserConfig_ValidModes_Accepted(string mode)
    {
        var config = new BrowserConfig(mode, null);
        Assert.Equal(mode, config.Mode);
    }

    [Fact]
    public void BrowserConfig_ChannelDefaults_ToNull()
    {
        var config = new BrowserConfig("local", null);
        Assert.Null(config.Channel);
    }

    [Fact]
    public void BrowserConfig_HeadlessDefaults_ToTrue()
    {
        var config = new BrowserConfig("local", null);
        Assert.True(config.Headless);
    }

    [Fact]
    public void BrowserConfig_ExplicitChannel_Preserved()
    {
        var config = new BrowserConfig("local", "chrome", true);
        Assert.Equal("chrome", config.Channel);
    }

    [Fact]
    public void BrowserConfig_HeadlessFalse_Preserved()
    {
        var config = new BrowserConfig("local", "chrome", false);
        Assert.False(config.Headless);
    }

    [Fact]
    public void BrowserConfig_JsonRoundTrip_CamelCase()
    {
        var config = new BrowserConfig("local", "chrome", true);
        var json = JsonSerializer.Serialize(config, FlowDefinitionV2JsonContext.Default.BrowserConfig);
        var deserialized = JsonSerializer.Deserialize(json, FlowDefinitionV2JsonContext.Default.BrowserConfig);

        Assert.NotNull(deserialized);
        Assert.Equal("local", deserialized.Mode);
        Assert.Equal("chrome", deserialized.Channel);
        Assert.True(deserialized.Headless);
    }

    [Fact]
    public void BrowserConfig_DeserializeFromJson_WithDefaults()
    {
        const string json = """{"mode":"remote"}""";
        var config = JsonSerializer.Deserialize(json, FlowDefinitionV2JsonContext.Default.BrowserConfig);

        Assert.NotNull(config);
        Assert.Equal("remote", config.Mode);
        Assert.Null(config.Channel);
        Assert.True(config.Headless);
    }

    [Fact]
    public void FlowDefinitionV2_WithBrowser_Deserializes()
    {
        const string json = """
        {
          "schemaVersion": "1.0.0",
          "metadata": { "id": "test", "name": "Test", "state": "XX", "endpoint": "test" },
          "browser": { "mode": "local", "channel": "chrome", "headless": true },
          "variables": [],
          "actions": [],
          "output": { "variableName": "result", "type": "string" }
        }
        """;

        var definition = JsonSerializer.Deserialize(json, FlowDefinitionV2JsonContext.Default.FlowDefinitionV2);

        Assert.NotNull(definition);
        Assert.NotNull(definition.Browser);
        Assert.Equal("local", definition.Browser.Mode);
        Assert.Equal("chrome", definition.Browser.Channel);
        Assert.True(definition.Browser.Headless);
    }

    [Fact]
    public void FlowDefinitionV2_WithoutBrowser_BrowserIsNull()
    {
        const string json = """
        {
          "schemaVersion": "1.0.0",
          "metadata": { "id": "test", "name": "Test", "state": "XX", "endpoint": "test" },
          "variables": [],
          "actions": [],
          "output": { "variableName": "result", "type": "string" }
        }
        """;

        var definition = JsonSerializer.Deserialize(json, FlowDefinitionV2JsonContext.Default.FlowDefinitionV2);

        Assert.NotNull(definition);
        Assert.Null(definition.Browser);
    }
}
