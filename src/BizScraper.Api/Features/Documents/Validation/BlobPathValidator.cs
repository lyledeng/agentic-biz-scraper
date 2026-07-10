using System.Text.RegularExpressions;

namespace BizScraper.Api.Features.Documents.Validation;

public static partial class BlobPathValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9\-_.]+(/[a-zA-Z0-9\-_.]+){1,5}$")]
    private static partial Regex ValidBlobPathPattern();

    public static bool IsValid(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return false;
        }

        if (blobPath.Length > 1024)
        {
            return false;
        }

        if (blobPath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return ValidBlobPathPattern().IsMatch(blobPath);
    }
}
