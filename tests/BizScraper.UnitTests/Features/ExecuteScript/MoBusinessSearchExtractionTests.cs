using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class MoBusinessSearchExtractionTests
{
    [Fact]
    public void MoSearchDefinition_HasExtractAction_WithJavaScript()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        Assert.False(string.IsNullOrWhiteSpace(extractAction.Javascript),
            "Extract action must have JavaScript for row parsing");
    }

    [Fact]
    public void MoSearchDefinition_HasTruncationDetection()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-business-search");

        Assert.NotNull(definition);
        var checkAction = definition.Actions.FirstOrDefault(a => a.Type == "check-text");
        Assert.NotNull(checkAction);
    }

    [Fact]
    public void MoSearchDefinition_ExtractScript_ContainsPreviousNameCollapsing()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        Assert.Contains("previous", extractAction.Javascript!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoSearchDefinition_ExtractScript_GeneratesUniqueKey()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-business-search");

        Assert.NotNull(definition);
        var extractAction = definition.Actions.FirstOrDefault(a => a.Type == "extract" && a.StoreAs == "results");
        Assert.NotNull(extractAction);
        Assert.Contains("uniqueKey", extractAction.Javascript!, StringComparison.Ordinal);
    }

    [Fact]
    public void MoSearchDefinition_HasPaginationLoop()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-mo-business-search");

        Assert.NotNull(definition);
        var loopAction = definition.Actions.FirstOrDefault(a => a.Type == "loop");
        Assert.NotNull(loopAction);
    }

    private static FlowDefinitionLoader CreateLoader()
    {
        var loader = new FlowDefinitionLoader();
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        loader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
        return loader;
    }
}
