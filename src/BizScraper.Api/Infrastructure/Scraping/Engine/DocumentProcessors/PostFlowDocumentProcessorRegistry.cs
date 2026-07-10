namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Resolves post-flow document processors by definition slug prefix.
/// </summary>
internal sealed class PostFlowDocumentProcessorRegistry
{
    private readonly Dictionary<string, IPostFlowDocumentProcessor> _processors;

    public PostFlowDocumentProcessorRegistry(IEnumerable<IPostFlowDocumentProcessor> processors)
    {
        _processors = new Dictionary<string, IPostFlowDocumentProcessor>(StringComparer.OrdinalIgnoreCase);
        foreach (var processor in processors)
        {
            _processors[processor.SlugPrefix] = processor;
        }
    }

    /// <summary>
    /// Get the processor for the given definition slug, or null if none registered.
    /// </summary>
    public IPostFlowDocumentProcessor? GetProcessor(string definitionSlug)
    {
        var prefix = ExtractSlugPrefix(definitionSlug);
        return _processors.GetValueOrDefault(prefix);
    }

    /// <summary>
    /// Extract slug prefix from a full definition slug.
    /// "us-co-entity-details" → "us-co"
    /// </summary>
    internal static string ExtractSlugPrefix(string slug)
    {
        var parts = slug.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : slug;
    }
}
