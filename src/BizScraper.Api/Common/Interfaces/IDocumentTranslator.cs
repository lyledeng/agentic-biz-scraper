namespace BizScraper.Api.Common.Interfaces;

/// <summary>
/// Abstraction for translating foreign-language documents to English via an external service.
/// </summary>
public interface IDocumentTranslator
{
    Task<DocumentTranslationResult> TranslateAsync(
        byte[] documentBytes,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a document translation containing the translated markdown and detected source language.
/// </summary>
public sealed record DocumentTranslationResult(
    string TranslatedMarkdown,
    string SourceLanguage);
