using System.Globalization;

namespace BizScraper.Api.Infrastructure.Scraping;

/// <summary>
/// Static utility methods for building diagnostic artifact paths (screenshots, traces).
/// Extracted from PlaywrightBrowserManager for reuse across browser lifecycle managers.
/// </summary>
internal static class DiagnosticsPathHelper
{
    internal static string BuildDiagnosticArtifactPath(string outputDirectory, string correlationId, DateTimeOffset timestamp, string milestone, string extension)
    {
        var safeMilestone = SanitizeFileNameSegment(milestone);
        var filename = string.Create(
            CultureInfo.InvariantCulture,
            $"{timestamp:yyyyMMddTHHmmssfffZ}_{safeMilestone}{extension}");
        return Path.Combine(outputDirectory, SanitizeFileNameSegment(correlationId), filename);
    }

    internal static string BuildDiagnosticArtifactBlobPath(string correlationId, string artifactPath)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{SanitizeFileNameSegment(correlationId)}/{Path.GetFileName(artifactPath)}");

    internal static string ResolveDiagnosticsOutputPath(string outputPath, string contentRootPath)
        => Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.GetFullPath(Path.Combine(contentRootPath, outputPath));

    internal static string SanitizeFileNameSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = new char[value.Length];

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            sanitizedCharacters[index] = invalidCharacters.Contains(character)
                || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
                || char.IsControl(character)
                ? '-'
                : character;
        }

        var sanitized = new string(sanitizedCharacters);
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
