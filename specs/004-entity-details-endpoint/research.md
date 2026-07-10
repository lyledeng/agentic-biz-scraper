# Research: Entity Details Retrieval & Certificate of Good Standing Download

**Feature**: 004-entity-details-endpoint
**Date**: 2026-03-24

## R-001: Playwright PDF Download Interception Strategy

### Decision: Use `page.WaitForEvent("download")` + `download.SaveAs()` for PDF capture

### Rationale
The CO SOS "Get a certificate of good standing" link navigates to `BuildCertificate.do` which returns `application/pdf` directly. Playwright treats this as a download event, not a page navigation. The correct approach is:

1. Register a download event listener before clicking the link
2. Click the "Get a certificate of good standing" link
3. Await the `download` event
4. Save the downloaded file to a local temp path using `download.SaveAs()`
5. Upload the local file to blob storage using the existing `IBlobStorageClient.UploadAsync(blobPath, localFilePath, ct)`

### Alternatives Considered
- **Response interception via `page.RouteAsync`**: More complex, would need to intercept the response body as a stream. Rejected because the existing blob storage client expects a local file path, not a stream.
- **Direct HTTP request via `page.APIRequestContext`**: Would bypass the browser session cookies/state. Rejected because the certificate URL requires session state from having navigated to the entity detail page first.

## R-002: URL Allowlist Validation for SSRF Prevention

### Decision: Compile-time `[GeneratedRegex]` pattern matching against scheme, host, and path

### Rationale
The `detailsUrl` query parameter is user-supplied and will be navigated to by a headless browser — a textbook SSRF vector. Validation must:

1. Parse the URL with `Uri.TryCreate(url, UriKind.Absolute, out var uri)`
2. Verify `uri.Scheme` is `https` (case-insensitive)
3. Verify `uri.Host` is `www.sos.state.co.us` (case-insensitive)
4. Verify `uri.AbsolutePath` starts with `/biz/BusinessEntityDetail.do`
5. Reject any URL that fails any check with a 400 Bad Request

Use `[GeneratedRegex]` for the path pattern match per project conventions (AOT-safe, compile-time validated).

### Alternatives Considered
- **Construct the URL server-side from entity ID**: More secure but requires a different API contract. Rejected per clarification: user chose to accept the full URL with strict validation.
- **Runtime regex**: Violates project convention of compile-time source generation. Rejected.

## R-003: Concurrency Control for Entity Detail Requests

### Decision: Dedicated `SemaphoreSlim(3, 3)` registered as a named/keyed singleton

### Rationale
The search endpoint uses `SemaphoreSlim(1, 1)` for single-request concurrency. The entity detail endpoint needs a separate semaphore with configurable initial count (default 3). This allows multiple concurrent entity detail lookups without blocking search requests and vice versa.

Registration pattern:
```csharp
// In Program.cs — use a named wrapper or distinct type to avoid DI collision
builder.Services.AddKeyedSingleton("EntityDetailSemaphore", 
    (_, _) => new SemaphoreSlim(3, 3));
```

Or simpler — wrap in a dedicated options type:
```csharp
// EntityDetailsOptions with MaxConcurrentRequests property
// Handler reads from IOptions<EntityDetailsOptions> 
```

### Alternatives Considered
- **Reuse the search semaphore**: Would serialize entity detail requests behind search. Rejected per clarification.
- **No concurrency limit**: Risks browser pool exhaustion. Rejected.

## R-004: Dedicated Audit Table Design

### Decision: New `EntityDetailAuditEntries` table with entity-specific columns + certificate URL

### Rationale
The entity detail audit entry has fundamentally different columns than `SearchAuditEntries` (no SearchTerm, ResultCount, PagesScraped, Truncated; has EntityId, EntityName, Status, CertificateUrl, DetailsUrl). A shared table would require many nullable columns with confusing semantics.

Matches existing Liquibase conventions:
- `-- liquibase formatted sql` header
- `-- changeset cososwebscraping:version-description`
- `-- rollback` block for reversibility (user directive)
- `dbo.` schema prefix, UNIQUEIDENTIFIER PKs, DATETIMEOFFSET timestamps, NVARCHAR text fields

### Alternatives Considered
- **Reuse SearchAuditEntries**: Would require many nullable columns. Rejected per clarification.
- **Generic audit table with JSON payload**: Violates the explicit-column requirement from clarification. Rejected.

## R-005: Scraper Architecture — New Interface vs Extending Existing

### Decision: New `IEntityDetailScraper` interface + `PlaywrightEntityDetailScraper` implementation that reuses the existing `PlaywrightBusinessEntityScraper`'s browser pool

### Rationale
The entity detail scraper performs a fundamentally different operation (single-page detail extraction + optional PDF download) compared to the search scraper (multi-page paginated result extraction). Separate interfaces follow ISP.

However, both scrapers need the shared Playwright browser pool (`IBrowser` instance), diagnostics infrastructure, and screenshot capture. The new scraper should:
1. Accept the existing `PlaywrightBusinessEntityScraper` as a dependency (it owns the browser pool)
2. Call `EnsureBrowserAsync` via the existing scraper to get a browser instance
3. Reuse `ExecuteWithDiagnosticsAsync`, `CaptureScreenshotAsync` for diagnostic artifacts

Alternatively, extract browser pool management into a shared service. But per YAGNI/KISS, the simpler approach is to have `PlaywrightEntityDetailScraper` delegate browser access to the existing scraper since both are singletons.

### Alternatives Considered
- **Add a `GetDetailsAsync` method to `IBusinessEntityScraper`**: Violates SRP (search vs details are different concerns) and ISP. Rejected.
- **Extract a `IBrowserPool` service**: Over-engineering for two consumers. Can refactor later if a third consumer appears. Rejected per YAGNI.

## R-006: Blob Storage Path for Certificate PDFs

### Decision: Store certificates at `{correlationId}/{documentnumber}-certificateofgoodstanding.pdf`

### Rationale
Per clarification, certificates are stored in the same container as diagnostics, under the correlation-ID folder. This keeps all artifacts for a single request together for easy cleanup and discovery. The certificate file is distinguished by its filename pattern rather than a separate folder.

The existing upload pattern via `IBlobStorageClient.UploadAsync(blobPath, localFilePath, ct)` is reused directly. The `blobPath` is constructed as `{sanitizedCorrelationId}/{documentnumber}-certificateofgoodstanding.pdf`.

### Alternatives Considered
- **Separate `certificates/` container**: Rejected per clarification — same container.
- **Separate `certificates/` prefix within the container**: Rejected — keeps things simpler to co-locate with other request artifacts.

## R-007: Liquibase Rollback Strategy

### Decision: Include `-- rollback` blocks in all changelogs per user directive

### Rationale
The user explicitly requested rollback capabilities. For the DDL changelog creating the new audit table, the rollback is `DROP TABLE dbo.EntityDetailAuditEntries`. This follows Liquibase best practices for reversible migrations.

Note: The existing changelogs do NOT include rollback blocks. This is a new convention starting with this feature per user directive. We will not retroactively modify existing changelogs.
