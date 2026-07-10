using System.Text.Json;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using NSubstitute;

namespace BizScraper.UnitTests.Features.ExecuteScript.Mappers;

public sealed class MapperRegistryTests
{
    [Fact]
    public void GetSearchMapper_ReturnsCorrectMapperForKnownSlug()
    {
        var coMapper = CreateSearchMapper("us-co");
        var registry = new MapperRegistry([coMapper], []);

        var result = registry.GetSearchMapper("us-co-business-search");

        Assert.Same(coMapper, result);
    }

    [Fact]
    public void GetSearchMapper_ReturnsPassthroughForUnknownSlug()
    {
        var coMapper = CreateSearchMapper("us-co");
        var registry = new MapperRegistry([coMapper], []);

        var result = registry.GetSearchMapper("us-tx-business-search");

        Assert.NotNull(result);
        Assert.NotSame(coMapper, result);
        // Passthrough returns null
        var output = JsonSerializer.SerializeToElement(new { results = Array.Empty<object>() });
        Assert.Null(result.Map(output));
    }

    [Fact]
    public void GetDetailMapper_ReturnsCorrectMapperForKnownSlug()
    {
        var wyMapper = CreateDetailMapper("us-wy");
        var registry = new MapperRegistry([], [wyMapper]);

        var result = registry.GetDetailMapper("us-wy-entity-details");

        Assert.Same(wyMapper, result);
    }

    [Fact]
    public void GetDetailMapper_ReturnsPassthroughForUnknownSlug()
    {
        var wyMapper = CreateDetailMapper("us-wy");
        var registry = new MapperRegistry([], [wyMapper]);

        var result = registry.GetDetailMapper("us-tx-entity-details");

        Assert.NotNull(result);
        Assert.NotSame(wyMapper, result);
        var output = JsonSerializer.SerializeToElement(new { details = new { } });
        Assert.Null(result.Map(output));
    }

    [Fact]
    public void ExtractSlugPrefix_ConsistentWithProcessorRegistry()
    {
        Assert.Equal("us-co", MapperRegistry.ExtractSlugPrefix("us-co-entity-details"));
        Assert.Equal("de-de", MapperRegistry.ExtractSlugPrefix("de-de-business-search"));
        Assert.Equal("us-wy", MapperRegistry.ExtractSlugPrefix("us-wy-business-search"));
    }

    [Fact]
    public void GetSearchMapper_CaseInsensitive()
    {
        var deMapper = CreateSearchMapper("de-de");
        var registry = new MapperRegistry([deMapper], []);

        var result = registry.GetSearchMapper("DE-DE-business-search");

        Assert.Same(deMapper, result);
    }

    private static ISearchResultMapper CreateSearchMapper(string slugPrefix)
    {
        var mapper = Substitute.For<ISearchResultMapper>();
        mapper.SlugPrefix.Returns(slugPrefix);
        return mapper;
    }

    private static IEntityDetailMapper CreateDetailMapper(string slugPrefix)
    {
        var mapper = Substitute.For<IEntityDetailMapper>();
        mapper.SlugPrefix.Returns(slugPrefix);
        return mapper;
    }
}
