using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class GenericScriptScraperModeResolutionTests
{
    [Fact]
    public void ResolveEffectiveMode_WindowsMode_ProxyConfigured_ReturnsWindows()
    {
        var browser = new BrowserConfig("windows", "chrome", Headless: false);
        var options = new PlaywrightOptions();
        var windowsProxy = new WindowsProxyOptions { EndpointUrl = "https://vm.example.com/api" };

        var result = GenericScriptScraper.ResolveEffectiveMode(browser, options, windowsProxy);

        Assert.Equal("windows", result);
    }

    [Fact]
    public void ResolveEffectiveMode_WindowsMode_ProxyNotConfigured_FallsBackToLocal()
    {
        var browser = new BrowserConfig("windows", "chrome", Headless: false);
        var options = new PlaywrightOptions();
        var windowsProxy = new WindowsProxyOptions(); // empty endpoint

        var result = GenericScriptScraper.ResolveEffectiveMode(browser, options, windowsProxy);

        Assert.Equal("local", result);
    }

    [Fact]
    public void ResolveEffectiveMode_LocalMode_UnchangedWithProxy()
    {
        var browser = new BrowserConfig("local", null);
        var options = new PlaywrightOptions();
        var windowsProxy = new WindowsProxyOptions { EndpointUrl = "https://vm.example.com/api" };

        var result = GenericScriptScraper.ResolveEffectiveMode(browser, options, windowsProxy);

        Assert.Equal("local", result);
    }

    [Fact]
    public void ResolveEffectiveMode_RemoteMode_UnchangedWithProxy()
    {
        var browser = new BrowserConfig("remote", null);
        var options = new PlaywrightOptions { BrowserEndpoint = "wss://remote.example.com" };
        var windowsProxy = new WindowsProxyOptions { EndpointUrl = "https://vm.example.com/api" };

        var result = GenericScriptScraper.ResolveEffectiveMode(browser, options, windowsProxy);

        Assert.Equal("remote", result);
    }

    [Fact]
    public void ResolveEffectiveMode_NullBrowserConfig_UsesGlobalDefault()
    {
        var options = new PlaywrightOptions();
        var windowsProxy = new WindowsProxyOptions { EndpointUrl = "https://vm.example.com/api" };

        var result = GenericScriptScraper.ResolveEffectiveMode(null, options, windowsProxy);

        Assert.Equal("local", result);
    }
}
