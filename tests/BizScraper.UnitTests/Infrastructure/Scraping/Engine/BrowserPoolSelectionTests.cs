using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class BrowserPoolSelectionTests
{
    [Fact]
    public void ResolveEffectiveMode_LocalOverride_ReturnsLocal()
    {
        var config = new BrowserConfig("local", "chrome");
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };

        var result = GenericScriptScraper.ResolveEffectiveMode(config, opts);

        Assert.Equal("local", result);
    }

    [Fact]
    public void ResolveEffectiveMode_RemoteOverride_WithEndpoint_ReturnsRemote()
    {
        var config = new BrowserConfig("remote", null);
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };

        var result = GenericScriptScraper.ResolveEffectiveMode(config, opts);

        Assert.Equal("remote", result);
    }

    [Fact]
    public void ResolveEffectiveMode_RemoteOverride_NoEndpoint_FallsBackToLocal()
    {
        var config = new BrowserConfig("remote", null);
        var opts = new PlaywrightOptions { BrowserEndpoint = "" };

        var result = GenericScriptScraper.ResolveEffectiveMode(config, opts);

        Assert.Equal("local", result);
    }

    [Fact]
    public void ResolveEffectiveMode_NoOverride_WithEndpoint_ReturnsRemote()
    {
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };

        var result = GenericScriptScraper.ResolveEffectiveMode(null, opts);

        Assert.Equal("remote", result);
    }

    [Fact]
    public void ResolveEffectiveMode_NoOverride_NoEndpoint_ReturnsLocal()
    {
        var opts = new PlaywrightOptions { BrowserEndpoint = "" };

        var result = GenericScriptScraper.ResolveEffectiveMode(null, opts);

        Assert.Equal("local", result);
    }

    [Fact]
    public void ResolveEffectiveMode_CaseInsensitive_Local()
    {
        var config = new BrowserConfig("LOCAL", "chrome");
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };

        var result = GenericScriptScraper.ResolveEffectiveMode(config, opts);

        Assert.Equal("local", result);
    }

    [Fact]
    public void ResolveEffectiveMode_CaseInsensitive_Remote()
    {
        var config = new BrowserConfig("REMOTE", null);
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };

        var result = GenericScriptScraper.ResolveEffectiveMode(config, opts);

        Assert.Equal("remote", result);
    }

    /// <summary>T014: Verify all 6 non-Iowa definition JSON files deserialize with Browser = null.</summary>
    [Theory]
    [InlineData("us-co-business-search")]
    [InlineData("us-co-entity-details")]
    [InlineData("us-wy-business-search")]
    [InlineData("us-wy-entity-details")]
    [InlineData("de-de-business-search")]
    [InlineData("de-de-entity-details")]
    public void NonIowaDefinitions_HaveNoBrowserOverride(string slug)
    {
        var definitionsPath = FindDefinitionsPath();
        var filePath = Path.Combine(definitionsPath, $"{slug}.json");
        Assert.True(File.Exists(filePath), $"Definition file not found: {filePath}");

        var json = File.ReadAllText(filePath);
        var definition = System.Text.Json.JsonSerializer.Deserialize(json, FlowDefinitionV2JsonContext.Default.FlowDefinitionV2);

        Assert.NotNull(definition);
        Assert.Null(definition.Browser);

        // Verify ResolveEffectiveMode returns global default
        var optsWithRemote = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };
        Assert.Equal("remote", GenericScriptScraper.ResolveEffectiveMode(definition.Browser, optsWithRemote));

        var optsWithoutRemote = new PlaywrightOptions { BrowserEndpoint = "" };
        Assert.Equal("local", GenericScriptScraper.ResolveEffectiveMode(definition.Browser, optsWithoutRemote));
    }

    /// <summary>T014: Verify Iowa definitions have Windows browser overrides.</summary>
    [Theory]
    [InlineData("us-ia-business-search")]
    [InlineData("us-ia-entity-details")]
    public void IowaDefinitions_HaveWindowsBrowserOverride(string slug)
    {
        var definitionsPath = FindDefinitionsPath();
        var filePath = Path.Combine(definitionsPath, $"{slug}.json");
        Assert.True(File.Exists(filePath), $"Definition file not found: {filePath}");

        var json = File.ReadAllText(filePath);
        var definition = System.Text.Json.JsonSerializer.Deserialize(json, FlowDefinitionV2JsonContext.Default.FlowDefinitionV2);

        Assert.NotNull(definition);
        Assert.NotNull(definition.Browser);
        Assert.Equal("windows", definition.Browser.Mode);
        Assert.Equal("msedge", definition.Browser.Channel);
        Assert.False(definition.Browser.Headless);
    }

    /// <summary>T016a: Adding a browser override routes to overridden mode.</summary>
    [Fact]
    public void AddingBrowserOverride_RoutesToOverriddenMode()
    {
        var config = new BrowserConfig("local", "chrome");
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };

        // Without override → remote (global default)
        Assert.Equal("remote", GenericScriptScraper.ResolveEffectiveMode(null, opts));

        // With override → local
        Assert.Equal("local", GenericScriptScraper.ResolveEffectiveMode(config, opts));
    }

    /// <summary>T016a: Removing browser override falls back to global default.</summary>
    [Fact]
    public void RemovingBrowserOverride_FallsBackToGlobalDefault()
    {
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote" };

        // With override → local
        var config = new BrowserConfig("local", "chrome");
        Assert.Equal("local", GenericScriptScraper.ResolveEffectiveMode(config, opts));

        // Without override → remote (global default)
        Assert.Equal("remote", GenericScriptScraper.ResolveEffectiveMode(null, opts));
    }

    private static string FindDefinitionsPath()
    {
        // Walk up from test bin directory to find src/BizScraper.Api/Infrastructure/Scraping/Definitions
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src", "BizScraper.Api", "Infrastructure", "Scraping", "Definitions");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Could not locate Definitions directory from test output path.");
    }
}
