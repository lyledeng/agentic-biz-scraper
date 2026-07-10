using BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;
using NSubstitute;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine.DocumentProcessors;

public sealed class PostFlowDocumentProcessorRegistryTests
{
    [Theory]
    [InlineData("us-co-entity-details", "us-co")]
    [InlineData("us-wy-business-search", "us-wy")]
    [InlineData("de-de-business-search", "de-de")]
    [InlineData("us-ia-entity-details", "us-ia")]
    [InlineData("us-mo-business-search", "us-mo")]
    [InlineData("us-wa-entity-details", "us-wa")]
    public void ExtractSlugPrefix_ReturnsFirstTwoSegments(string slug, string expected)
    {
        var result = PostFlowDocumentProcessorRegistry.ExtractSlugPrefix(slug);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractSlugPrefix_SingleSegment_ReturnsAsIs()
    {
        var result = PostFlowDocumentProcessorRegistry.ExtractSlugPrefix("single");
        Assert.Equal("single", result);
    }

    [Fact]
    public void GetProcessor_ReturnsCorrectProcessorForSlug()
    {
        var wyProcessor = CreateProcessor("us-wy");
        var coProcessor = CreateProcessor("us-co");
        var registry = new PostFlowDocumentProcessorRegistry([wyProcessor, coProcessor]);

        var result = registry.GetProcessor("us-wy-entity-details");

        Assert.Same(wyProcessor, result);
    }

    [Fact]
    public void GetProcessor_ReturnsNullForUnregisteredSlug()
    {
        var wyProcessor = CreateProcessor("us-wy");
        var registry = new PostFlowDocumentProcessorRegistry([wyProcessor]);

        var result = registry.GetProcessor("us-tx-business-search");

        Assert.Null(result);
    }

    [Fact]
    public void GetProcessor_CaseInsensitiveMatching()
    {
        var deProcessor = CreateProcessor("de-de");
        var registry = new PostFlowDocumentProcessorRegistry([deProcessor]);

        var result = registry.GetProcessor("DE-DE-entity-details");

        Assert.Same(deProcessor, result);
    }

    [Fact]
    public void GetProcessor_EmptyRegistry_ReturnsNull()
    {
        var registry = new PostFlowDocumentProcessorRegistry([]);

        var result = registry.GetProcessor("us-co-business-search");

        Assert.Null(result);
    }

    private static IPostFlowDocumentProcessor CreateProcessor(string slugPrefix)
    {
        var processor = Substitute.For<IPostFlowDocumentProcessor>();
        processor.SlugPrefix.Returns(slugPrefix);
        return processor;
    }
}
