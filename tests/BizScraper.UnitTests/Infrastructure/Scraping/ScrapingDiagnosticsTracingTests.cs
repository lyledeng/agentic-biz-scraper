using BizScraper.Api.Infrastructure.Scraping;

namespace BizScraper.UnitTests.Infrastructure.Scraping;

public sealed class ScrapingDiagnosticsTracingTests
{
    [Fact]
    public void BuildDiagnosticArtifactPath_UsesTraceNamingConvention()
    {
        var timestamp = new DateTimeOffset(2026, 3, 24, 14, 30, 25, 123, TimeSpan.Zero);

        var artifactPath = DiagnosticsPathHelper.BuildDiagnosticArtifactPath("C:\\diagnostics", "req:1", timestamp, "trace", ".zip");

        Assert.Equal(Path.Combine("C:\\diagnostics", "req-1", "20260324T143025123Z_trace.zip"), artifactPath);
    }
}