namespace BizScraper.Api.Features.ExecuteScript.Exceptions;

/// <summary>
/// Thrown when required runtime parameters are missing from an execute-script request.
/// </summary>
public sealed class MissingParametersException(string slug, IReadOnlyList<string> missingParameters)
    : Exception($"Missing required parameters for definition '{slug}': {string.Join(", ", missingParameters)}.")
{
    public string Slug { get; } = slug;
    public IReadOnlyList<string> MissingParameters { get; } = missingParameters;
}
