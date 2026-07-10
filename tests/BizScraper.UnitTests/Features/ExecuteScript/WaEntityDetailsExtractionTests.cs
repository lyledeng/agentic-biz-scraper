using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class WaEntityDetailsExtractionTests
{
    [Fact]
    public void WaDetailDefinition_HasGeneralInfoExtraction()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "details");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "General info extract action must have JavaScript");
    }

    [Fact]
    public void WaDetailDefinition_GeneralInfoScript_ExtractsExpectedFields()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "details");
        Assert.NotNull(extractAction);
        var js = extractAction.Javascript!;
        Assert.Contains("ubiNumber", js, StringComparison.Ordinal);
        Assert.Contains("businessType", js, StringComparison.Ordinal);
        Assert.Contains("formationDate", js, StringComparison.Ordinal);
        Assert.Contains("natureOfBusiness", js, StringComparison.Ordinal);
        Assert.Contains("expirationDate", js, StringComparison.Ordinal);
        Assert.Contains("jurisdiction", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WaDetailDefinition_GeneralInfoScript_ExtractsNonprofitFields()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "details");
        Assert.NotNull(extractAction);
        var js = extractAction.Javascript!;
        Assert.Contains("charitableCorporation", js, StringComparison.Ordinal);
        Assert.Contains("nonprofitEin", js, StringComparison.Ordinal);
        Assert.Contains("grossRevenueUnder500K", js, StringComparison.Ordinal);
        Assert.Contains("hasMembers", js, StringComparison.Ordinal);
        Assert.Contains("publicBenefitDesignation", js, StringComparison.Ordinal);
        Assert.Contains("hostHome", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WaDetailDefinition_HasRegisteredAgentExtraction()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "registeredAgent");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "Registered agent extract action must have JavaScript");
        var js = extractAction.Javascript!;
        Assert.Contains("streetAddress", js, StringComparison.Ordinal);
        Assert.Contains("mailingAddress", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WaDetailDefinition_HasGovernorsExtraction()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "governors");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "Governors extract action must have JavaScript");
        var js = extractAction.Javascript!;
        Assert.Contains("governorType", js, StringComparison.Ordinal);
        Assert.Contains("firstName", js, StringComparison.Ordinal);
        Assert.Contains("lastName", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WaDetailDefinition_HasFilingHistoryExtraction()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "filings");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "Filing history extract action must have JavaScript");
        var js = extractAction.Javascript!;
        Assert.Contains("filingNumber", js, StringComparison.Ordinal);
        Assert.Contains("filingType", js, StringComparison.Ordinal);
        Assert.Contains("effectiveDate", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WaDetailDefinition_HasBusinessIdRuntimeVariable()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var businessIdVar = definition.Variables.FirstOrDefault(v => v.Name == "businessId");
        Assert.NotNull(businessIdVar);
        Assert.Equal("runtime", businessIdVar.Source);
        Assert.True(businessIdVar.Required);
    }

    [Fact]
    public void WaDetailDefinition_HasCloudflareDetection()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        var checkAction = definition.Actions.FirstOrDefault(a =>
            a.Type == "check-text" && a.ErrorType == "cloudflare-blocked");
        Assert.NotNull(checkAction);
    }

    [Fact]
    public void WaDetailDefinition_OutputComposesAllSections()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        Assert.NotNull(definition.Output);
        Assert.NotNull(definition.Output.Compose);
        Assert.True(definition.Output.Compose.ContainsKey("details"));
        Assert.True(definition.Output.Compose.ContainsKey("registeredAgent"));
        Assert.True(definition.Output.Compose.ContainsKey("governors"));
        Assert.True(definition.Output.Compose.ContainsKey("filings"));
    }

    [Fact]
    public void WaDetailDefinition_OutputDoesNotIncludeNameHistory()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-entity-details");

        Assert.NotNull(definition);
        Assert.NotNull(definition.Output);
        Assert.NotNull(definition.Output.Compose);
        Assert.False(definition.Output.Compose.ContainsKey("nameHistory"),
            "FR-014: nameHistory is out of scope for initial implementation");
    }

    private static FlowDefinitionLoader CreateLoader()
    {
        var loader = new FlowDefinitionLoader();
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        loader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
        return loader;
    }
}
