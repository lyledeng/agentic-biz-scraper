# Research: German Hardcopy Printout Retrieval with FAB Translation

**Feature**: 015-de-hardcopy-printout | **Date**: 2026-03-27

## R1: Handelsregister AD Link Behavior

**Decision**: The AD link triggers a PrimeFaces form-based PDF download via `PrimeFaces.monitorDownload()`. The system captures using Playwright's `page.waitForEvent('download')`.

**Rationale**: Verified via Playwright MCP discovery on 2026-03-27. The AD link has `href="#"` with an `onclick` handler that calls `PrimeFaces.monitorDownload()` and submits the form. This triggers a file download (not page navigation or popup). The downloaded PDF filename follows the pattern `{State}-{Court}_{RegisterType}_{RegisterNumber}+AD-{timestamp}.pdf` (e.g., `SN-Leipzig_HRB_30226+AD-20260328035907.pdf`).

**Alternatives considered**:
- Page navigation capture: Rejected — AD link does not navigate to a new page
- Popup/new tab capture: Rejected — no popup or new tab is opened
- Direct HTTP download: Rejected — the download requires PrimeFaces form state (JSF ViewState)

**Implementation**: Use Playwright's download event API within the existing browser session:
```csharp
var downloadTask = page.WaitForEventAsync("download");
await adLink.ClickAsync();
var download = await downloadTask;
var filePath = await download.PathAsync();
```

## R2: Markdown-to-HTML Library Selection

**Decision**: Use Markdig 1.1.2 (BSD-2-Clause license, NuGet).

**Rationale**: Markdig is the most popular .NET Markdown library (57.6M downloads), fully CommonMark compliant, supports pipe tables (critical for Handelsregister tabular data), is AOT-friendly (no reflection), and has a permissive BSD-2-Clause license.

**Alternatives considered**:
- CommonMark.NET: Less actively maintained; fewer extensions (no pipe tables natively)
- Manual regex conversion: Rejected — fragile, doesn't handle edge cases, violates KISS
- HTML rendering by FAB agent: Rejected — user chose Markdown output from FAB

**Usage**: `Markdig.Markdown.ToHtml(markdownContent, pipeline)` with `UseAdvancedExtensions()` to enable pipe tables, GFM features, and auto-identifiers for headings.

## R3: FAB Document Translator Integration Pattern

**Decision**: Create a new `FabDocumentTranslator` service following the same pattern as `FabCaptchaSolver` but with its own independent `FabDocumentTranslatorOptions` configuration class.

**Rationale**: The spec requires a separate options class (clarification Q4). The FAB contract reuses the same `{ "input": { "format": "base64", "data": "..." } }` pattern. The translator timeout should be longer (default 60s vs 30s for CAPTCHA) since PDF translation is more complex.

**Alternatives considered**:
- Nesting inside `FabAgentOptions`: Rejected per spec clarification — user chose separate class
- Generic `IFabAgent<TRequest, TResponse>`: Rejected — YAGNI; only 2 FAB integrations; each has different semantics
- Shared base class: Rejected — FabCaptchaSolver and FabDocumentTranslator have different retry policies and response models

**Contract**:
- Input: `{ "input": { "format": "base64", "data": "<base64-encoded-pdf>" } }`
- Output: `{ "output": { "translatedMarkdown": "# ...", "sourceLanguage": "de" } }`
- Authentication: `x-authentication: api-key {token}` header (same as CAPTCHA solver)
- Resilience: Polly retry (2 attempts, 1s delay, 2x backoff) on 5xx and timeout

## R4: PDF Generation Pipeline

**Decision**: Markdown → HTML (Markdig) → Playwright `page.pdf()` on a data URI page.

**Rationale**: The system already has a Playwright browser pool. Rendering HTML to PDF via Playwright produces high-fidelity output matching browser layout. Loading the HTML as a data URI (`data:text/html;charset=utf-8,...`) avoids needing a temp file or HTTP server.

**Implementation**:
1. Convert Markdown to HTML via Markdig with pipe table + advanced extensions
2. Wrap HTML in a complete document with `<style>` for tables, headings, page margins
3. Load the HTML document into a new Playwright page via `page.SetContentAsync()`
4. Call `page.PdfAsync()` with appropriate margin/format settings
5. Return the PDF bytes as a stream

**Alternatives considered**:
- Direct HTML string → PDF via wkhtmltopdf: Rejected — adds a binary dependency; Playwright already available
- iTextSharp: Rejected — dual-license (AGPL/commercial); violates open-source constraint
- QuestPDF: Rejected — requires programmatic layout; can't render HTML/Markdown directly

## R5: Concurrency and Semaphore Design

**Decision**: Reuse the existing `DE`-keyed `SemaphoreSlim(1, 1)` for the hardcopy endpoint. The search and hardcopy share the same semaphore since both interact with the Handelsregister site.

**Rationale**: The hardcopy endpoint navigates the same Handelsregister search page, so concurrent requests from search and hardcopy could interfere. Sharing the semaphore ensures only one DE operation runs at a time.

**Alternatives considered**:
- Separate semaphore for hardcopy: Rejected — could allow concurrent DE search + hardcopy hitting the site simultaneously
- No semaphore: Rejected — Handelsregister could throttle or block concurrent sessions

## R6: Audit Trail Reuse

**Decision**: Reuse the existing `SearchAuditEntry` table and `AuditTrailRepository`. The hardcopy request is written as an audit entry with a distinct status prefix (`HardcopySuccess`, `HardcopyError`).

**Rationale**: The existing audit schema already captures: SearchTerm, ResultCount, Status, ErrorDetail, CorrelationId, DiagnosticsUrl, DurationMs. The hardcopy request fits this schema — RegistrationId goes into SearchTerm (combined as `searchTerm|registrationId`), and document URLs go into DiagnosticsUrl.

**Alternatives considered**:
- New audit table: Rejected — adds Liquibase complexity for minimal benefit; the existing table captures all needed fields
- Extending audit table with new columns: Rejected — avoids schema change; use existing fields with conventions

## R8: Registration ID Availability in Search Results

**Decision**: Enhance the Germany search extraction to capture the registration header (`span.fontWeightBold`) and add a `RegistrationId` field to `GermanyBusinessResult`. This is a prerequisite for the hardcopy endpoint — callers need this value to identify which row to fetch the AD document for.

**Rationale**: Verified via Playwright MCP on 2026-03-27 — each Handelsregister result row has a header row (`tr.ui-panelgrid-even`) containing a `td.fontTableNameSize` with a `span.fontWeightBold` that provides the court, register type, and register number (e.g., "District court München HRB 73851"). The current extraction JS only captures `CompanyName`, `RegisteredOffice`, and `Status` from the data row (`tr.ui-panelgrid-odd`).

**Changes required**:
1. **`de-business-search.json`** (actions 004 and 012): Update extraction JS to also extract `headerBold.textContent` from `span.fontWeightBold` and store as `registrationId` in each result object
2. **`GermanyBusinessResult.cs`**: Add `string RegistrationId` parameter
3. **`SearchGermanyBusinessesHandler.cs`**: Map `NormalizedSearchResult` field to new `RegistrationId`
4. **`AppJsonSerializerContext.cs`**: No change needed (type already registered, just gains a field)
5. **Test-ui**: Add `registrationId` column to results table; pass to hardcopy endpoint

**Alternatives considered**:
- Hardcopy endpoint performs its own search + fuzzy match by company name: Rejected — duplicates search, imprecise matching, unnecessary Handelsregister load
- Use row index: Rejected — fragile if results change between search and hardcopy calls

**Verified data (10 results from "Rohde & Schwarz" search)**:

| Registration Header | Company |
|---|---|
| District court Hannover HRB 100300 | HMF Smart Solutions GmbH |
| District court München HRB 73851 | RUSA - Rohde & Schwarz Anlagen GmbH |
| District court München HRB 189144 | Rohde & Schwarz Corporate Finance GmbH |
| District court München HRB 160333 | Rohde & Schwarz Cybersecurity GmbH |
| District court München HRB 44547 | Rohde & Schwarz Electronic GmbH |
| District court München HRA 16270 | Rohde & Schwarz GmbH & Co. KG |
| District court Leipzig HRB 30226 | Rohde & Schwarz Group Services GmbH |
| District court Dresden HRB 28693 | Rohde & Schwarz INRADIOS GmbH |
| District court München HRB 124467 | Rohde & Schwarz Immobilien GmbH |
| District court München HRB 103507 | Rohde & Schwarz International GmbH |

## R7: FAB Agent Configuration Markdown

**Decision**: Generate a `fab-document-translator-agent-config.md` in the specs directory documenting the required FAB dashboard configuration.

**Rationale**: Follows the precedent set by Feature 005 (`fab-captcha-agent-config.md`). The FAB agent is configured manually on the FAB dashboard; the markdown file documents what needs to be set up.

**Configuration required**:
- Agent Name: "German Document Translator"
- System Prompt: "You are a professional translator specializing in German legal and business registration documents. Translate the provided German PDF document into English. Return the translation as Markdown, preserving the document structure: headings, tables, lists, and paragraphs. Use pipe tables for tabular data."
- AI Parameters: temperature=0.1, top_p=0.9 (low creativity for accurate translation)
- Max Tokens: 8000 (large enough for multi-page document)
- Output Format: JSON with structured schema
- JSON Schema: `{ "output": { "translatedMarkdown": "string", "sourceLanguage": "string" } }`
