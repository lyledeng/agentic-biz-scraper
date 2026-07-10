using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class WaBusinessSearchExtractionTests
{
    [Fact]
    public void WaSearchDefinition_HasExtractAction_WithJavaScript()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "Extract action must have JavaScript for row parsing");
    }

    [Fact]
    public void WaSearchDefinition_ExtractScript_GeneratesUniqueKey()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        Assert.Contains("uniqueKey", extractAction.Javascript!, StringComparison.Ordinal);
        Assert.Contains("US-WA", extractAction.Javascript!, StringComparison.Ordinal);
        Assert.Contains("businessId", extractAction.Javascript!, StringComparison.Ordinal);
        Assert.Contains("businessType", extractAction.Javascript!, StringComparison.Ordinal);
    }

    [Fact]
    public void WaSearchDefinition_ExtractScript_SetsFormationDateNull()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        Assert.Contains("formationDate: null", extractAction.Javascript!, StringComparison.Ordinal);
    }

    [Fact]
    public void WaSearchDefinition_ExtractScript_ExtractsExpectedFields()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        var js = extractAction.Javascript!;
        Assert.Contains("identifier", js, StringComparison.Ordinal);
        Assert.Contains("entityType", js, StringComparison.Ordinal);
        Assert.Contains("principalOfficeAddress", js, StringComparison.Ordinal);
        Assert.Contains("registeredAgentName", js, StringComparison.Ordinal);
        Assert.Contains("status", js, StringComparison.Ordinal);
        Assert.Contains("state: 'WA'", js, StringComparison.Ordinal);
    }

    [Fact]
    public void WaSearchDefinition_HasCloudflareDetection()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var checkAction = definition.Actions.FirstOrDefault(a =>
            a.Type == "check-text" && a.ErrorType == "cloudflare-blocked");
        Assert.NotNull(checkAction);
        Assert.Contains("cloudflare", checkAction.Pattern!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WaSearchDefinition_HasPaginationLoop()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var loopAction = definition.Actions.FirstOrDefault(a => a.Type == "loop");
        Assert.NotNull(loopAction);
        Assert.Equal("pagination", loopAction.LoopType);
    }

    [Fact]
    public void WaSearchDefinition_PaginationLoop_ChecksIsLastPage()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var isLastPageAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "isLastPage");
        Assert.NotNull(isLastPageAction);
        Assert.False(string.IsNullOrWhiteSpace(isLastPageAction.Javascript),
            "isLastPage check must have JavaScript");
    }

    [Fact]
    public void WaSearchDefinition_HasSearchTermRuntimeVariable()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var searchTermVar = definition.Variables.FirstOrDefault(v => v.Name == "searchTerm");
        Assert.NotNull(searchTermVar);
        Assert.Equal("runtime", searchTermVar.Source);
        Assert.True(searchTermVar.Required);
    }

    [Fact]
    public void WaSearchDefinition_FillAction_TargetsBusinessNamePlaceholder()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        // Fill is now done via an extract action using AngularJS ngModel.$setViewValue
        var fillExtract = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "_fillResult");
        Assert.NotNull(fillExtract);
        Assert.Contains("business", fillExtract.Javascript!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ngModelCtrl", fillExtract.Javascript!, StringComparison.Ordinal);
        Assert.Contains("setViewValue", fillExtract.Javascript!, StringComparison.Ordinal);
    }

    [Fact]
    public void WaSearchDefinition_ExtractScript_UsesAngularScope()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-wa-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        Assert.Contains("angular.element", extractAction.Javascript!, StringComparison.Ordinal);
        Assert.Contains("scope", extractAction.Javascript!, StringComparison.Ordinal);
        Assert.Contains("BusinessID", extractAction.Javascript!, StringComparison.Ordinal);
    }

    private static FlowDefinitionLoader CreateLoader()
    {
        var loader = new FlowDefinitionLoader();
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        loader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
        return loader;
    }
}
