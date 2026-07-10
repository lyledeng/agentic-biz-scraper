using BizScraper.Api.Infrastructure.Scraping;

namespace BizScraper.UnitTests.Infrastructure.Scraping;

public sealed class ScrapingDiagnosticsScreenshotTests
{
    [Fact]
    public void BuildDiagnosticArtifactPath_UsesScreenshotNamingConvention()
    {
        var timestamp = new DateTimeOffset(2026, 3, 24, 14, 30, 25, 123, TimeSpan.Zero);

        var artifactPath = DiagnosticsPathHelper.BuildDiagnosticArtifactPath("C:\\diagnostics", "req:1", timestamp, "03-page-2", ".png");

        Assert.Equal(Path.Combine("C:\\diagnostics", "req-1", "20260324T143025123Z_03-page-2.png"), artifactPath);
    }
}