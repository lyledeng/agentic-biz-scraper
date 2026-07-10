using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class MoEntityDetailsExtractionTests
{
    [Fact]
    public void MoDetailDefinition_HasGeneralInfoExtraction()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "details");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "General info extract action must have JavaScript");
    }

    [Fact]
    public void MoDetailDefinition_GeneralInfoScript_ExtractsExpectedFields()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "details");
        Assert.NotNull(extractAction);
        var js = extractAction.Javascript!;
        Assert.Contains("charterNumber", js, StringComparison.Ordinal);
        Assert.Contains("domesticity", js, StringComparison.Ordinal);
        Assert.Contains("registeredAgent", js, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dateFormed", js, StringComparison.Ordinal);
    }

    [Fact]
    public void MoDetailDefinition_HasFilingsExtraction()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "filings");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "Filings extract action must have JavaScript");
    }

    [Fact]
    public void MoDetailDefinition_FilingsScript_ExtractsExpectedFields()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "filings");
        Assert.NotNull(extractAction);
        var js = extractAction.Javascript!;
        Assert.Contains("documentType", js, StringComparison.Ordinal);
        Assert.Contains("dateFiled", js, StringComparison.Ordinal);
        Assert.Contains("effectiveDate", js, StringComparison.Ordinal);
        Assert.Contains("rowIndex", js, StringComparison.Ordinal);
        Assert.Contains("hasViewDocument", js, StringComparison.Ordinal);
        Assert.Contains("action", js, StringComparison.Ordinal);
    }

    [Fact]
    public void MoDetailDefinition_HasAddressExtraction()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "addresses");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "Address extract action must have JavaScript");
    }

    [Fact]
    public void MoDetailDefinition_AddressScript_ExtractsExpectedFields()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "addresses");
        Assert.NotNull(extractAction);
        var js = extractAction.Javascript!;
        Assert.Contains("addressType", js, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("since", js, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoDetailDefinition_HasEntityIdRuntimeVariable()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-entity-details");

        Assert.NotNull(definition);
        var entityIdVar = definition.Variables.FirstOrDefault(v => v.Name == "entityId");
        Assert.NotNull(entityIdVar);
        Assert.Equal("runtime", entityIdVar.Source);
        Assert.True(entityIdVar.Required);
    }

    private static FlowDefinitionLoader CreateLoader()
    {
        var loader = new FlowDefinitionLoader();
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        loader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
        return loader;
    }
}
