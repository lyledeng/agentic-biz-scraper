# Data Model: German Hardcopy Printout Retrieval with FAB Translation

**Feature**: 015-de-hardcopy-printout
**Date**: 2026-03-27

## Search Model Enhancement

The existing `GermanyBusinessResult` record must be enhanced to include the registration header.

### GermanyBusinessResult (MODIFIED)

**File**: `Features/GermanySearch/Models/GermanyBusinessResult.cs`

| Field | C# Type | Required | Source | Description |
|-------|---------|----------|--------|-------------|
| `CompanyName` | `string` | ✅ | `td.paddingBottom20Px` | Company name |
| `RegisteredOffice` | `string` | ✅ | `td.sitzSuchErgebnisse` | City of registered office |
| `Status` | `string` | ✅ | Next cell after office | Registration status (e.g., "currently registered") |
| `RegistrationId` | `string` | ✅ | `span.fontWeightBold` | Court + register type + number (e.g., "District court München HRB 73851") |

```csharp
public sealed record GermanyBusinessResult(
    string CompanyName,
    string RegisteredOffice,
    string Status,
    string RegistrationId);
```

**Extraction source**: Each result row's first `tr` contains a `td.fontTableNameSize` with a `span.fontWeightBold` that holds the registration header (e.g., "District court München HRB 73851"). The state name (e.g., "Bavaria") precedes the bold span but is NOT included in the `RegistrationId` field.

**Handler mapping change**: In `SearchGermanyBusinessesHandler`, the `NormalizedSearchResult.DocumentNumber` field (already extracted as `registeredOffice` in the flow JSON but more accurately the registration header) maps to `RegistrationId`. The extraction JS in `de-business-search.json` must be updated to separately extract the bold header text.

---

## API Models

These C# records represent the request/response types for the hardcopy printout endpoint. They live in `src/BizScraper.Api/Features/GermanySearch/Models/`.

---

### HardcopyPrintoutRequest

The API request body for `POST /api/v1/germany-search/current-hardcopy-printout`. Identifies a specific business entity on the Handelsregister.

**File**: `Features/GermanySearch/Models/HardcopyPrintoutRequest.cs`

| Field | C# Type | Required | Validation | Description |
|-------|---------|----------|------------|-------------|
| `SearchTerm` | `string` | ✅ | Non-empty | Company name to search for on the Handelsregister (e.g., "Claes und Rohde") |
| `RegistrationId` | `string` | ✅ | Non-empty | Court and registration number (e.g., "Paderborn HRA 3059") used to locate the specific result row |

```csharp
public sealed record HardcopyPrintoutRequest(string SearchTerm, string RegistrationId);
```

**JSON (camelCase via source-gen)**:
```json
{ "searchTerm": "Claes und Rohde", "registrationId": "Paderborn HRA 3059" }
```

---

### HardcopyPrintoutResult

Internal result record returned from the handler. Not serialized to JSON directly — the endpoint streams the PDF bytes and sets response headers from this record.

**File**: `Features/GermanySearch/Models/HardcopyPrintoutResult.cs`

| Field | C# Type | Required | Description |
|-------|---------|----------|-------------|
| `TranslatedPdf` | `byte[]` | ✅ | The English-translated PDF bytes, ready for streaming to the caller |
| `TranslatedDocumentUrl` | `string` | ✅ | Blob storage URL of the translated PDF |
| `OriginalDocumentUrl` | `string` | ✅ | Blob storage URL of the original German PDF |
| `CorrelationId` | `string` | ✅ | Correlation ID for audit/diagnostics traceability |

```csharp
public sealed record HardcopyPrintoutResult(
    byte[] TranslatedPdf,
    string TranslatedDocumentUrl,
    string OriginalDocumentUrl,
    string CorrelationId);
```

---

## Configuration Models

### FabDocumentTranslatorOptions

Independent configuration class for the FAB Document Translator service. **NOT** nested inside `FabAgentOptions` — lives at its own configuration section level.

**File**: `Common/Configuration/FabDocumentTranslatorOptions.cs`

| Property | C# Type | Default | Description |
|----------|---------|---------|-------------|
| `EndpointUrl` | `string` | `""` | FAB agent HTTP endpoint URL |
| `AuthToken` | `string` | `""` | FAB API key (sourced from Key Vault in deployed environments) |
| `TimeoutSeconds` | `int` | `60` | HTTP timeout for the translation request (higher than CAPTCHA's 30s default) |

```csharp
public sealed class FabDocumentTranslatorOptions
{
    public string EndpointUrl { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;
}
```

**appsettings.json section** (same level as `FabAgent`, not nested):
```json
{
  "FabDocumentTranslator": {
    "EndpointUrl": "",
    "AuthToken": "",
    "TimeoutSeconds": 60
  }
}
```

---

## Infrastructure Models

These records define the FAB HTTP contract. They live in `src/BizScraper.Api/Infrastructure/DocumentTranslation/`.

### FabTranslationRequest

Outbound request to FAB Generic Document Processor agent. Mirrors the `FabCaptchaSolverRequest` structure.

**File**: `Infrastructure/DocumentTranslation/FabTranslationRequest.cs`

| Field | JSON Name | C# Type | Description |
|-------|-----------|---------|-------------|
| `Input` | `input` | `FabTranslationInput` | Wrapper for the document payload |
| `Input.Format` | `format` | `string` | Always `"base64"` |
| `Input.Data` | `data` | `string` | Base64-encoded German PDF content |

```csharp
public sealed record FabTranslationRequest(
    [property: JsonPropertyName("input")] FabTranslationInput Input);

public sealed record FabTranslationInput(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("data")] string Data);
```

**Example JSON**:
```json
{
  "input": {
    "format": "base64",
    "data": "JVBERi0xLjQKMSAw..."
  }
}
```

---

### FabTranslationResponse

Inbound response from FAB Generic Document Processor agent.

**File**: `Infrastructure/DocumentTranslation/FabTranslationResponse.cs`

| Field | JSON Name | C# Type | Description |
|-------|-----------|---------|-------------|
| `Output` | `output` | `FabTranslationOutput?` | Nullable wrapper for the translated content |
| `Output.TranslatedMarkdown` | `translatedMarkdown` | `string` | English translation of the German PDF content, formatted as Markdown |
| `Output.SourceLanguage` | `sourceLanguage` | `string` | Detected source language (expected: `"de"`) |

```csharp
public sealed record FabTranslationResponse(
    [property: JsonPropertyName("output")] FabTranslationOutput? Output);

public sealed record FabTranslationOutput(
    [property: JsonPropertyName("translatedMarkdown")] string TranslatedMarkdown,
    [property: JsonPropertyName("sourceLanguage")] string SourceLanguage);
```

**Example JSON**:
```json
{
  "output": {
    "translatedMarkdown": "# Company Registration Extract\n\n## District Court Leipzig, HRB 30226\n\n| Field | Value |\n|-------|-------|\n| Company Name | Rohde & Schwarz Group Services GmbH |\n| Registered Office | Leipzig |\n| Status | Active |\n\n## Officers\n\n- Managing Director: John Smith\n- Managing Director: Jane Doe",
    "sourceLanguage": "de"
  }
}
```

---

## Interface Contracts

### IDocumentTranslator

Abstraction for document translation, enabling testing and future alternative implementations.

**File**: `Common/Interfaces/IDocumentTranslator.cs`

```csharp
public interface IDocumentTranslator
{
    Task<DocumentTranslationResult> TranslateAsync(
        byte[] documentBytes,
        CancellationToken cancellationToken);
}

public sealed record DocumentTranslationResult(
    string TranslatedMarkdown,
    string SourceLanguage);
```

**Implementations**: `FabDocumentTranslator` (production).

---

## Command Model

### GetHardcopyPrintoutCommand

LiteBus command record. Follows the existing `SearchGermanyBusinessesQuery` pattern but as a command (side effects: downloads, stores, audits).

**File**: `Features/GermanySearch/Commands/GetHardcopyPrintoutCommand.cs`

```csharp
public sealed record GetHardcopyPrintoutCommand(
    string SearchTerm,
    string RegistrationId) : ICommand<HardcopyPrintoutResult>;
```

---

## Serialization Registration

The following types must be registered in `AppJsonSerializerContext.cs`:

```csharp
[JsonSerializable(typeof(HardcopyPrintoutRequest))]
[JsonSerializable(typeof(FabTranslationRequest))]
[JsonSerializable(typeof(FabTranslationResponse))]
```

> `HardcopyPrintoutResult` is NOT registered — it is never serialized to JSON (PDF bytes are streamed directly).

---

## Relationships

```text
┌─────────────────────────┐
│ HardcopyPrintoutRequest │─── API input (JSON body)
└──────────┬──────────────┘
           │ maps to
           ▼
┌─────────────────────────────┐
│ GetHardcopyPrintoutCommand  │─── LiteBus command
└──────────┬──────────────────┘
           │ handled by
           ▼
┌──────────────────────────────┐
│ GetHardcopyPrintoutHandler   │
│  ├─ Playwright: search + AD  │─── downloads German PDF
│  ├─ IDocumentTranslator      │─── sends base64 PDF, gets Markdown
│  ├─ MarkdownToPdfConverter   │─── Markdown → HTML → PDF
│  ├─ IBlobStorageClient       │─── stores original + translated PDF
│  └─ AuditTrailRepository     │─── persists audit entry
└──────────┬──────────────────-┘
           │ returns
           ▼
┌─────────────────────────┐
│ HardcopyPrintoutResult  │─── internal result
│  ├ TranslatedPdf        │─── streamed as response body
│  ├ TranslatedDocumentUrl│─── X-Document-Url header
│  ├ OriginalDocumentUrl  │─── X-Original-Document-Url header
│  └ CorrelationId        │─── X-Correlation-Id header
└─────────────────────────┘
```

## State Transitions

The handler follows a linear pipeline with no branching state machine. The diagnostic state flow:

```text
Start → SearchPage → ResultsFound → RowMatched → AdLinkClicked → PdfDownloaded
  → PdfUploadedOriginal → FabTranslationSent → MarkdownReceived
  → HtmlConverted → TranslatedPdfGenerated → TranslatedPdfUploaded
  → AuditWritten → Complete
```

Error exits at any stage → audit with error status, cleanup, return appropriate HTTP error.
