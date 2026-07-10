using System.Text.Json;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class WaFilingDocumentTests
{
    [Fact]
    public void WaDetailDefinition_FilingHistoryScript_IncludesDocumentsArray()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "filings");
        Assert.NotNull(extractAction);
        Assert.Contains("documents", extractAction.Javascript!, StringComparison.Ordinal);
    }

    [Fact]
    public void WaDetailDefinition_FilingHistoryScript_ExtractsFilingNumber()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "filings");
        Assert.NotNull(extractAction);
        Assert.Contains("filingNumber", extractAction.Javascript!, StringComparison.Ordinal);
    }

    [Fact]
    public void WaDetailDefinition_HasMaxFilingDocumentsConfigVariable()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var maxFilingsVar = definition.Variables.FirstOrDefault(v => v.Name == "maxFilingDocuments");
        Assert.NotNull(maxFilingsVar);
        Assert.Equal("config", maxFilingsVar.Source);
        Assert.NotNull(maxFilingsVar.DefaultValue);
        Assert.Equal(JsonValueKind.Number, maxFilingsVar.DefaultValue.Value.ValueKind);
        Assert.Equal(10, maxFilingsVar.DefaultValue.Value.GetInt32());
    }

    private static FlowDefinitionLoader CreateLoader()
    {
        var loader = new FlowDefinitionLoader();
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        loader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
        return loader;
    }
}
