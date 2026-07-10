# Research: WY Filing History Document Download Fix

**Feature**: 029-wy-history-doc-fix  
**Date**: 2026-04-03

## R-001: Where should the blob upload step be placed?

**Decision**: Inside `JsonDrivenEntityDetailScraper.ScrapeAsync()`, immediately after the existing per-document local download succeeds (after `File.WriteAllBytesAsync`), within the same `foreach` loop.

**Rationale**: The scraper already has `_blobStorageClient` injected and the downloaded bytes in memory (or on local disk). Adding the upload step here keeps the download→upload→replace-URL pipeline in one place, avoids touching the handler or mapper, and makes it generic for all states.

**Alternatives considered**:
- Post-processing in `ExecuteScriptHandler` (like `DeEntityDetailsProcessor`): Rejected — requires per-state wiring, duplicates download logic, and the handler doesn't have access to the local file bytes.
- Upload in `EntityDetailMapper`: Rejected — the mapper is a pure data transformer; adding I/O would violate single responsibility.

## R-002: How to build the proxy URL from within the scraper?

**Decision**: Inject `IHttpContextAccessor` and `IConfiguration` into `JsonDrivenEntityDetailScraper` (same pattern as `DownloadActionHandler`) to build the proxy URL: `{scheme}://{host}{pathBase}/api/v1/documents/{blobPath}`.

**Rationale**: The `DownloadActionHandler.UploadAndGetProxyUrlAsync()` method already uses this exact pattern. Replicating it in the scraper (or extracting a shared helper) maintains consistency.

**Alternatives considered**:
- Building a relative proxy URL (`/api/v1/documents/{blobPath}`) without `IHttpContextAccessor`: Rejected — `EntityDetailMapper.MapHistoryDocuments()` passes `storageUrl` through as-is to `ProxyUrl`, and the test-ui `DocumentProxyService.fetchDocument()` handles both absolute and relative URLs. A relative URL would work BUT the existing pattern (certificates, DE documents) uses absolute URLs, so consistency favors absolute.
- Extracting a shared `ProxyUrlBuilder` utility: Feasible improvement but YAGNI for this fix — only two call sites. Can be refactored in the future if a third emerges.

## R-003: What blob path format to use for history documents?

**Decision**: `{correlationId}/{filename}` — matching the existing pattern used by `DownloadActionHandler` when `__blobPrefix` falls back to `correlationId`.

**Rationale**: The `JsonDrivenEntityDetailScraper` currently uses `correlationId` as the blob prefix for its `ActionContext`. History document file names are already sanitized by the JSON extraction script (e.g., `2025-original-annual-report-11347360.pdf`). The resulting blob path is compatible with `BlobPathValidator` (alphanumeric + hyphen/period, max 1024 chars).

**Alternatives considered**:
- Using `BuildBlobPrefix()` for a richer path (`{date}/{slug}/{label}/{correlationId}`): The scraper doesn't have runtime parameters to call `BuildBlobPrefix()` directly, and modifying the constructor to accept the full blob prefix would require changes up the call chain. The simpler `correlationId`-based path is sufficient and matches current behavior for the v1 entity detail path.

## R-004: Should `IHttpContextAccessor` be added to the scraper constructor?

**Decision**: Yes — add `IHttpContextAccessor` and `IConfiguration` as constructor parameters.

**Rationale**: The scraper is resolved from DI via the entity detail scraper factory. Adding two more constructor parameters is a minimal change. `IHttpContextAccessor` is already registered in the DI container (used by `DownloadActionHandler`, `DocumentEndpoints`, etc.).

**Alternatives considered**:
- Passing proxy URL builder as a delegate: Over-engineering for a single use case.
- Computing the proxy URL in the handler after scraping: Rejected — the handler receives the scrape result as a record; modifying `storageUrl` fields after the fact would require re-serializing and is fragile.

## R-005: What if the blob upload fails after successful download?

**Decision**: Log a warning and set the `Error` field on the `HistoryDocumentResult` record, leaving `StorageUrl` empty. The document will display an error in the UI rather than a broken link.

**Rationale**: This follows the same failure-isolation pattern already used for download failures (content type mismatch, empty body, timeout). Each document fails independently without affecting others (FR-004).

## R-006: Test-UI document button upgrade approach?

**Decision**: Replace custom `doc-open-btn` HTML buttons with Jumpstart `button-field-pds3 type="secondary" size="small"` containing an `icon-pds3 name="wk-icon-file-pdf"` icon. This gives document buttons a clear, branded appearance that stands out from surrounding text.

**Rationale**: The current `doc-open-btn` uses custom CSS that doesn't match Jumpstart PDS3 styling. Using `button-field-pds3` with `type="secondary"` provides a visible border and hover state out of the box. The `wk-icon-file-pdf` icon clearly communicates "PDF document" to users.

**Alternatives considered**:
- `type="tertiary"`: Too subtle — no border, won't stand out in a data table.
- `type="primary"`: Too prominent — every row would have a bold blue button.
- `type="text"` with icon: Still too subtle for discoverability in a dense table.
