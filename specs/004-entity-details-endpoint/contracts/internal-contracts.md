# Internal Contracts: Entity Details Feature

**Feature**: 004-entity-details-endpoint
**Date**: 2026-03-24

## CQRS Query Contract

### GetEntityDetailsQuery

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Queries;

public sealed record GetEntityDetailsQuery(string DetailsUrl) : IQuery<EntityDetailResponse>;
```

**Resolved by**: `GetEntityDetailsHandler` via LiteBus `IQueryHandler<GetEntityDetailsQuery, EntityDetailResponse>`

---

## Scraper Interface Contract

### IEntityDetailScraper

```csharp
namespace CososWebScraping.Api.Common.Interfaces;

public interface IEntityDetailScraper
{
    Task<EntityDetailScrapeResult> ScrapeAsync(
        string detailsUrl,
        string correlationId,
        CancellationToken cancellationToken);
}
```

**Responsibilities**:
1. Navigate to the validated `detailsUrl` using the shared Playwright browser pool
2. Scrape the "Details" table → `EntityDetailResult`
3. Scrape the "Registered Agent" table → `RegisteredAgentResult` (nullable if table missing)
4. Detect "Get a certificate of good standing" link → set `CertificateAvailable`
5. If certificate link present: click, await download, save to temp file → set `CertificateLocalPath`
6. Capture diagnostic screenshots at key milestones
7. Return `EntityDetailScrapeResult`

**Does NOT**:
- Validate the URL (caller's responsibility)
- Upload the certificate PDF to blob storage (handler's responsibility)
- Write audit entries (handler's responsibility)
- Enforce concurrency limits (handler's responsibility)

**Incremental implementation note**: The scraper is built progressively across user stories. US1 (T018) implements core scraping + certificate link detection. US2 (T024) adds PDF download. US3 (T027) adds diagnostic screenshots. The interface contract is stable across all stories; only the implementation depth grows.

---

## Handler Contract

### GetEntityDetailsHandler

```text
Input:  GetEntityDetailsQuery { DetailsUrl }
Output: EntityDetailResponse { Details, RegisteredAgent?, Certificate? }

Dependencies (constructor-injected):
  - IEntityDetailScraper              → scrapes the page
  - IBlobStorageClient                → uploads certificate PDF + diagnostics
  - EntityDetailAuditRepository       → persists audit entry
  - SemaphoreSlim (keyed)             → concurrency throttle
  - ILogger<GetEntityDetailsHandler>  → structured logging
  - IHttpContextAccessorAccessor      → correlation ID from TraceIdentifier

Processing flow:
  1. Extract correlationId from HttpContext.TraceIdentifier
  2. Compute diagnosticsUrl from blobStorageClient.GetFolderUrl(correlationId)
  3. Validate detailsUrl against SSRF allowlist → 400 on failure
  4. Acquire semaphore → 503 ServiceBusyException on timeout
  5. Start stopwatch
  6. Call scraper.ScrapeAsync(detailsUrl, correlationId, ct)
  7. If certificateLocalPath != null:
     a. Upload to blob: {correlationId}/{idNumber}-certificateofgoodstanding.pdf
     b. Get certificateUrl from upload result
  8. Build EntityDetailResponse
  9. Write audit entry (always, success or failure)
  10. Return response

Exception mapping (follows GlobalExceptionHandler):
  - InvalidDetailsUrlException → 400 Bad Request
  - ServiceBusyException → 503 Service Unavailable (Retry-After: 30)
  - ScrapingException → 502 Bad Gateway
  - UpstreamException → 502 Bad Gateway
```

---

## Repository Contract

### EntityDetailAuditRepository

```text
Method: SaveAsync(EntityDetailAuditEntry entry, CancellationToken ct)

Behavior (mirrors AuditTrailRepository):
  - Skip gracefully if AuditTrail connection string not configured
  - Parameterized INSERT into dbo.EntityDetailAuditEntries
  - DBNull.Value for nullable columns (EntityIdNumber, EntityName, 
    EntityStatus, CertificateUrl, DiagnosticsUrl, ErrorDetail)
  - Fire-and-forget semantics: log warning on failure, never throw
```

---

## Exception Contract

### InvalidDetailsUrlException

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Exceptions;

public sealed class InvalidDetailsUrlException(string message) : Exception(message);
```

**Mapped in GlobalExceptionHandler** → 400 Bad Request

---

## URL Validation Contract

### DetailsUrlValidator (static helper or inline in handler)

```text
Input:  string detailsUrl
Output: bool isValid

Rules (all must pass):
  1. Uri.TryCreate(detailsUrl, UriKind.Absolute, out uri) succeeds
  2. uri.Scheme == "https" (case-insensitive)
  3. uri.Host == "www.sos.state.co.us" (case-insensitive)
  4. uri.AbsolutePath starts with "/biz/BusinessEntityDetail.do"

On failure: throw InvalidDetailsUrlException with descriptive message
```

---

## DI Registration Contract

### Program.cs additions

```text
New registrations:
  - builder.Services.AddKeyedSingleton<SemaphoreSlim>("EntityDetailSemaphore", (_, _) => new SemaphoreSlim(3, 3))
  - builder.Services.AddSingleton<EntityDetailAuditRepository>()
  - builder.Services.AddSingleton<PlaywrightEntityDetailScraper>()
  - builder.Services.AddSingleton<IEntityDetailScraper>(sp => sp.GetRequiredService<PlaywrightEntityDetailScraper>())
  - builder.Services.AddScoped<GetEntityDetailsHandler>()

Endpoint mapping:
  - app.MapEntityDetailsEndpoints()

Metrics meter:
  - metrics.AddMeter(EntityDetailsMetrics.MeterName)

Serializer context:
  - [JsonSerializable] attributes for EntityDetailResponse, EntityDetailResult, RegisteredAgentResult, CertificateResult
```
