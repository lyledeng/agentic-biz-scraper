using BizScraper.Api.Infrastructure.Scraping.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class FlowDefinitionLoaderSlugTests
{
    [Fact]
    public void GetDefinitionBySlug_ExistingSlug_ReturnsDefinition()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("us-co-business-search");

        Assert.NotNull(definition);
        Assert.Equal("us-co-business-search", definition.Metadata.DefinitionSlug);
    }

    [Fact]
    public void GetDefinitionBySlug_NonExistentSlug_ReturnsNull()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("nonexistent-slug");

        Assert.Null(definition);
    }

    [Fact]
    public void GetDefinitionBySlug_CaseInsensitive()
    {
        var loader = CreateLoader();
        var definition = loader.GetDefinitionBySlug("US-CO-BUSINESS-SEARCH");

        Assert.NotNull(definition);
    }

    [Fact]
    public void SlugIndex_ContainsAllDefinitionsWithSlugs()
    {
        var loader = CreateLoader();

        Assert.True(loader.SlugIndex.Count >= 12, $"Expected at least 12 definitions with slugs, found {loader.SlugIndex.Count}");
        Assert.Contains("us-co-business-search", loader.SlugIndex.Keys);
        Assert.Contains("us-co-entity-details", loader.SlugIndex.Keys);
        Assert.Contains("us-ia-business-search", loader.SlugIndex.Keys);
        Assert.Contains("us-ia-entity-details", loader.SlugIndex.Keys);
        Assert.Contains("us-wy-business-search", loader.SlugIndex.Keys);
        Assert.Contains("us-wy-entity-details", loader.SlugIndex.Keys);
        Assert.Contains("de-de-business-search", loader.SlugIndex.Keys);
        Assert.Contains("de-de-entity-details", loader.SlugIndex.Keys);
        Assert.Contains("us-mo-business-search", loader.SlugIndex.Keys);
        Assert.Contains("us-mo-entity-details", loader.SlugIndex.Keys);
        Assert.Contains("us-wa-business-search", loader.SlugIndex.Keys);
        Assert.Contains("us-wa-entity-details", loader.SlugIndex.Keys);
    }

    [Fact]
    public void LoadAndValidateAll_DuplicateSlug_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"slug-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "def-a.json"), """
                {
                    "schemaVersion": "2.0",
                    "metadata": { "id": "def-a", "definitionSlug": "duplicate-slug", "name": "Def A", "state": "CO", "endpoint": "search" },
                    "variables": [],
                    "actions": [],
                    "output": { "variableName": "results", "type": "array" }
                }
                """);

            File.WriteAllText(Path.Combine(tempDir, "def-b.json"), """
                {
                    "schemaVersion": "2.0",
                    "metadata": { "id": "def-b", "definitionSlug": "duplicate-slug", "name": "Def B", "state": "WY", "endpoint": "search" },
                    "variables": [],
                    "actions": [],
                    "output": { "variableName": "results", "type": "array" }
                }
                """);

            var loader = new FlowDefinitionLoader();
            Assert.Throws<InvalidOperationException>(() => loader.LoadAndValidateAll(tempDir, NullLogger.Instance));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static FlowDefinitionLoader CreateLoader()
    {
        var loader = new FlowDefinitionLoader();
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        loader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
        return loader;
    }
}
