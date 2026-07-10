namespace BizScraper.Api.Features.ExecuteScript.Exceptions;

/// <summary>
/// Thrown when the requested definition slug does not match any registered flow definition.
/// </summary>
public sealed class UnknownDefinitionException(string slug)
    : Exception($"No definition found matching '{slug}'.")
{
    public string Slug { get; } = slug;
}
