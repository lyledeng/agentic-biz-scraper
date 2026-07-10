using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Resolves search result and entity detail mappers by definition slug prefix.
/// Returns passthrough mappers when no match is found.
/// </summary>
internal sealed class MapperRegistry
{
    private readonly Dictionary<string, ISearchResultMapper> _searchMappers;
    private readonly Dictionary<string, IEntityDetailMapper> _detailMappers;
    private static readonly PassthroughSearchMapper PassthroughSearch = new();
    private static readonly PassthroughEntityDetailMapper PassthroughDetail = new();

    public MapperRegistry(
        IEnumerable<ISearchResultMapper> searchMappers,
        IEnumerable<IEntityDetailMapper> detailMappers)
    {
        _searchMappers = new Dictionary<string, ISearchResultMapper>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapper in searchMappers)
        {
            _searchMappers[mapper.SlugPrefix] = mapper;
        }

        _detailMappers = new Dictionary<string, IEntityDetailMapper>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapper in detailMappers)
        {
            _detailMappers[mapper.SlugPrefix] = mapper;
        }
    }

    /// <summary>
    /// Get the search result mapper for the given definition slug.
    /// Returns PassthroughSearchMapper if no mapper is registered.
    /// </summary>
    public ISearchResultMapper GetSearchMapper(string definitionSlug)
    {
        var prefix = ExtractSlugPrefix(definitionSlug);
        return _searchMappers.GetValueOrDefault(prefix) ?? PassthroughSearch;
    }

    /// <summary>
    /// Get the entity detail mapper for the given definition slug.
    /// Returns PassthroughEntityDetailMapper if no mapper is registered.
    /// </summary>
    public IEntityDetailMapper GetDetailMapper(string definitionSlug)
    {
        var prefix = ExtractSlugPrefix(definitionSlug);
        return _detailMappers.GetValueOrDefault(prefix) ?? PassthroughDetail;
    }

    /// <summary>
    /// Extract slug prefix from a full definition slug.
    /// </summary>
    internal static string ExtractSlugPrefix(string slug)
    {
        var parts = slug.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : slug;
    }

    private sealed class PassthroughSearchMapper : ISearchResultMapper
    {
        public string SlugPrefix => "__passthrough__";

        public UnifiedSearchResult[]? Map(JsonElement output) => null;
    }

    private sealed class PassthroughEntityDetailMapper : IEntityDetailMapper
    {
        public string SlugPrefix => "__passthrough__";

        public UnifiedEntityDetailResponse? Map(JsonElement output) => null;
    }
}
