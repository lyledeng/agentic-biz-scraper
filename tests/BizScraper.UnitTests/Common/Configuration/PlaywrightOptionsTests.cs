using BizScraper.Api.Common.Configuration;

namespace BizScraper.UnitTests.Common.Configuration;

public sealed class PlaywrightOptionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("wss://eastus.api.playwright.microsoft.com/workspaces/abc/browsers", true)]
    [InlineData("ws://localhost:1234", true)]
    public void IsRemoteMode_ReflectsBrowserEndpointPresence(string? endpoint, bool expectedRemote)
    {
        var options = new PlaywrightOptions { BrowserEndpoint = endpoint ?? string.Empty };

        Assert.Equal(expectedRemote, options.IsRemoteMode);
    }
}
