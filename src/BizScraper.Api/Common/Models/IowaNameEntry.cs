namespace BizScraper.Api.Common.Models;

/// <summary>
/// Represents an Iowa business entity name variant (legal, fictitious, trade name).
/// Extracted from the Iowa SOS entity detail page names table.
/// </summary>
public sealed record IowaNameEntry
{
    /// <summary>The name text (e.g., "ACME CORPORATION").</summary>
    public required string Name { get; init; }

    /// <summary>Name classification: Legal, Fictitious, Trade, etc.</summary>
    public required string Type { get; init; }

    /// <summary>Name active status: Active or Inactive.</summary>
    public required string Status { get; init; }

    /// <summary>Whether this name has been modified since original filing.</summary>
    public bool Modified { get; init; }
}
