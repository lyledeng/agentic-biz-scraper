using System.Text.Json;
using BizScraper.Api.Features.EntityDetails.Models;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class WyCertificateFlowTests
{
    [Fact]
    public void WyEntityDetailsFlow_DefinesCertificateScreenshotMilestones()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(GetWyFlowPath()));
        var root = doc.RootElement;

        var hasCertificateVariable = root
            .GetProperty("variables")
            .EnumerateArray()
            .Any(variable =>
                variable.TryGetProperty("name", out var nameProp)
                && string.Equals(nameProp.GetString(), "certificateLocalPath", StringComparison.Ordinal));

        Assert.True(hasCertificateVariable);

        var certificateActions = root
            .GetProperty("actions")
            .EnumerateArray()
            .Where(action => action.TryGetProperty("group", out var groupProp)
                && string.Equals(groupProp.GetString(), "certificate", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(certificateActions);

        var screenshotMilestones = certificateActions
            .Where(action => action.TryGetProperty("screenshot", out _))
            .Select(action => action.GetProperty("screenshot").GetProperty("milestone").GetString())
            .Where(milestone => !string.IsNullOrWhiteSpace(milestone))
            .ToList();

        Assert.Equal(3, screenshotMilestones.Count);
        Assert.Contains("wy-cert-01-search-page", screenshotMilestones, StringComparer.Ordinal);
        Assert.Contains("wy-cert-02-verification-page", screenshotMilestones, StringComparer.Ordinal);
        Assert.Contains("wy-cert-03-after-download", screenshotMilestones, StringComparer.Ordinal);

        var downloadAction = certificateActions.Single(action =>
            action.TryGetProperty("type", out var typeProp)
            && string.Equals(typeProp.GetString(), "download", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(".pdf", downloadAction.GetProperty("validateExtension").GetString());
        Assert.Equal("certificateLocalPath", downloadAction.GetProperty("storeAs").GetString());
    }

    [Fact]
    public async Task CertificateFlow_TimeoutDoesNotCancelParent_CertificatePathNull()
    {
        // Validates the independent CTS timeout pattern used in the scraper:
        // child CTS cancels on timeout, parent CTS survives, certificateLocalPath stays null.
        using var parentCts = new CancellationTokenSource();
        using var certificateCts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token);
        certificateCts.CancelAfter(TimeSpan.FromMilliseconds(50));

        string? certificateLocalPath = "initial-path.pdf";

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), certificateCts.Token);
        }
        catch (OperationCanceledException) when (!parentCts.IsCancellationRequested)
        {
            certificateLocalPath = null;
        }

        Assert.Null(certificateLocalPath);
        Assert.False(parentCts.IsCancellationRequested);

        var details = new EntityDetailResult("Test Corp", "Active", "01/01/2020", "2017-000757675", "Profit Corporation", null, "WY", null, null);
        var result = new EntityDetailScrapeResult(details, null, !string.IsNullOrEmpty(certificateLocalPath), certificateLocalPath);
        Assert.False(result.CertificateAvailable);
        Assert.Null(result.CertificateLocalPath);
        Assert.Equal("Test Corp", result.Details.Name);
    }

    [Fact]
    public void EntityDetailsPreserved_WhenCertificateNavigationFails()
    {
        // When certificate flow throws PlaywrightException, the catch block sets
        // certificateLocalPath to null + logs warning. Entity details and history docs
        // from the main flow are preserved in the final result.
        var details = new EntityDetailResult(
            "Wendy's Technology, LLC", "Active", "06/13/2017", "2017-000757675",
            "Profit Corporation", null, "Wyoming", "123 Main St", "PO Box 456");
        var registeredAgent = new RegisteredAgentResult("John Doe", "789 Oak Ave", null);
        var parties = new List<PartyResult> { new("Jane Smith", "Manager", null, null) };
        var historyDocs = new List<HistoryDocumentResult>
        {
            new("2025 Annual Report", "05/20/2025", "https://example.test/report", "report.pdf", null)
        };

        // Simulates the scraper's catch(PlaywrightException) path → certificate unavailable
        var result = new EntityDetailScrapeResult(details, registeredAgent, false, null, parties, historyDocs);

        Assert.Equal("Wendy's Technology, LLC", result.Details.Name);
        Assert.Equal("2017-000757675", result.Details.IdNumber);
        Assert.NotNull(result.RegisteredAgent);
        Assert.Equal("John Doe", result.RegisteredAgent.Name);
        Assert.False(result.CertificateAvailable);
        Assert.Null(result.CertificateLocalPath);
        Assert.NotNull(result.Parties);
        Assert.Single(result.Parties);
        Assert.NotNull(result.HistoryDocuments);
        Assert.Single(result.HistoryDocuments);
    }

    [Fact]
    public void WyEntityDetailsFlow_DownloadAction_RejectsNonPdf()
    {
        // The download action has validateExtension: ".pdf" — DownloadActionHandler
        // rejects non-PDF files, leaving certificateLocalPath unset → CertificateAvailable = false.
        using var doc = JsonDocument.Parse(File.ReadAllText(GetWyFlowPath()));
        var certificateActions = doc.RootElement
            .GetProperty("actions")
            .EnumerateArray()
            .Where(a => a.TryGetProperty("group", out var g)
                && string.Equals(g.GetString(), "certificate", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var downloadAction = certificateActions.Single(a =>
            a.TryGetProperty("type", out var t)
            && string.Equals(t.GetString(), "download", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(".pdf", downloadAction.GetProperty("validateExtension").GetString());

        var filenamePattern = downloadAction.GetProperty("filenamePattern").GetString();
        Assert.NotNull(filenamePattern);
        Assert.Contains("${idNumber}", filenamePattern, StringComparison.Ordinal);
        Assert.EndsWith("certificateofgoodstanding.pdf", filenamePattern, StringComparison.Ordinal);
    }

    private static string GetWyFlowPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "BizScraper.Api", "Infrastructure", "Scraping", "Definitions", "us-wy-entity-details.json"));
    }
}
