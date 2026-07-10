# Implementation Plan: PDF Proxy Streaming Endpoint

**Branch**: `011-pdf-proxy-streaming` | **Date**: 2026-03-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/011-pdf-proxy-streaming/spec.md`

## Summary

Add a server-side streaming proxy endpoint (`GET /api/v1/documents/{**blobPath}`) to the BizScraper API that streams PDF files from Azure Blob Storage on behalf of the caller. This eliminates the requirement for test UI users to have direct network access to the storage account. The entity-details handler is modified to rewrite `storageUrl` values server-side so the Angular test UI renders proxy URLs as-is. The proxy streams blobs directly to the HTTP response without in-memory buffering, includes HTTP caching (ETag + Cache-Control), enforces a 30-second timeout, and validates blob paths against a pattern allowlist to prevent traversal attacks.

## Technical Context

**Language/Version**: .NET 10 / C# 14 (`net10.0`, language version `latest`)
**Primary Dependencies**: Azure.Storage.Blobs, Polly (resilience), LiteBus (CQRS), OpenTelemetry, System.Text.Json (source-generated)
**Storage**: Azure Blob Storage (container `diagnostics`) for PDF files; IBlobStorageClient / AzureBlobStorageClient
**Testing**: xUnit + NSubstitute (unit); Testcontainers + Azure SQL Edge (integration); k6 (performance)
**Target Platform**: Alpine-based container on AKS; Kestrel HTTPS on port 8443
**Project Type**: Web service (ASP.NET Core minimal API)
**Performance Goals**: Streaming throughput limited only by blob storage egress; no full-file buffering; p95 latency < storage round-trip + 500 ms
**Constraints**: 30-second max stream timeout (FR-014); max file size ~50 MB; no authentication (test UI); read-only container filesystem
**Scale/Scope**: Single new endpoint + modification to entity-details handler; ~6 new files + 3 modified files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First | **PASS** | Unit tests for blob path validation, handler logic, and URL rewriting; integration tests for end-to-end streaming with Testcontainers (Azurite); error scenario coverage for 400/404/502/504 |
| II. SOLID Design | **PASS** | New `DownloadBlobAsync` added to existing `IBlobStorageClient` (ISP — single method, cohesive with existing upload); dedicated `StreamDocumentHandler` for proxy concern (SRP); endpoint class follows existing static extension method pattern |
| III. Architectural Quality | **PASS** | Streaming-first (no memory buffering) per FR-004; Polly resilience pipeline on download; structured logging + OpenTelemetry metrics per FR-012; blob path validation at system boundary per FR-010; RFC 9457 error responses |
| IV. Hybrid SemVer+CalVer | **PASS** | No new versioned artifacts required (extends existing API version `v1`) |
| V. Hands-Free Deployment | **PASS** | No new infrastructure; uses existing blob storage configuration; no database changes; endpoint auto-registered via `MapDocumentEndpoints()` in Program.cs |

**Gate Result**: PASS — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/011-pdf-proxy-streaming/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output — OpenAPI + internal contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Common/
│   └── Interfaces/
│       └── IBlobStorageClient.cs                          # MODIFY: add DownloadBlobAsync method
├── Infrastructure/
│   └── Storage/
│       ├── AzureBlobStorageClient.cs                      # MODIFY: implement DownloadBlobAsync
│       └── NullBlobStorageClient.cs                       # MODIFY: implement DownloadBlobAsync (no-op)
├── Features/
│   ├── Documents/                                          # NEW — feature vertical slice
│   │   ├── Endpoints/
│   │   │   └── DocumentEndpoints.cs                       # NEW: MapGet /api/v1/documents/{**blobPath}
│   │   ├── Handlers/
│   │   │   └── StreamDocumentHandler.cs                   # NEW: IQueryHandler — blob streaming logic
│   │   ├── Models/
│   │   │   └── StreamDocumentQuery.cs                     # NEW: CQRS query record
│   │   ├── Validation/
│   │   │   └── BlobPathValidator.cs                       # NEW: pattern allowlist validation (FR-010)
│   │   └── Logging/
│   │       └── DocumentLogMessages.cs                     # NEW: [LoggerMessage] partial class
│   └── EntityDetails/
│       └── Handlers/
│           └── GetEntityDetailsHandler.cs                 # MODIFY: rewrite storageUrl to proxy URLs
├── Program.cs                                              # MODIFY: register DocumentEndpoints
└── appsettings.json                                        # NO CHANGE: reuses existing CloudStorage config

src/test-ui/
└── src/app/
    └── features/entity-details/
        └── entity-details.component.html                  # NO CHANGE: renders storageUrl as-is (server rewrites)

tests/
├── BizScraper.UnitTests/
│   └── Features/
│       └── Documents/                                      # NEW
│           ├── BlobPathValidatorTests.cs                   # NEW: validation tests
│           └── StreamDocumentHandlerTests.cs               # NEW: handler tests
└── BizScraper.IntegrationTests/
    └── Features/
        └── Documents/                                      # NEW
            └── DocumentStreamingTests.cs                   # NEW: end-to-end streaming tests
```

**Structure Decision**: Follows the existing vertical-slice pattern. The `Documents` feature folder is a new slice under `Features/`, consistent with `EntityDetails/` and `BusinessSearch/`. The proxy is an API-side concern with no Angular changes (server-side URL rewriting per FR-008).

## Complexity Tracking

No Constitution Check violations — no complexity justification required.

---

## Phase 0: Research Findings

*See [research.md](research.md) for full findings.*

### Key Decisions

| Decision | Rationale |
|----------|-----------|
| `BlobClient.DownloadStreamingAsync()` for blob downloads | Returns a `Stream` that pipes directly to `HttpResponse.Body`; no full-memory buffering per FR-004 |
| `Results.Stream()` for HTTP response | ASP.NET Core minimal API streaming pattern; handles chunked transfer, disposal, and headers |
| `[GeneratedRegex]` for blob path validation | Compile-time validated, AOT-safe; matches project convention from SSRF protection (R-002 in feature 004) |
| Server-side URL rewriting in `GetEntityDetailsHandler` | Per clarification: handler constructs proxy URLs using `HttpContext.Request` base URL; Angular renders as-is |
| Dedicated `ResiliencePipeline` for downloads | 3 retries + 30s timeout; matches existing upload pipeline parameters; independent tuning |
| `Cache-Control: public, max-age=86400, immutable` + ETag | PDFs are immutable once uploaded; `GetPropertiesAsync()` for lightweight conditional request checks |
| EventId range 3000–3099 for log messages | Next available range after entity-details (2000–2099); follows existing convention |

---

## Phase 1: Design

### Endpoint Design

**Route**: `GET /api/v1/documents/{**blobPath}`

The catch-all route segment (`**`) captures the `/` in the blob path (e.g., `correlationId/filename.pdf`), so the path binds as a single string. The endpoint delegates to `StreamDocumentHandler` via LiteBus CQRS.

**Request flow**:
1. `DocumentEndpoints.MapGet()` receives the request
2. Validates blob path via `BlobPathValidator.IsValid()` → 400 if invalid
3. Extracts `If-None-Match` header from `HttpContext.Request.Headers`
4. Dispatches `StreamDocumentQuery` via `IQueryMediator`
5. Handler interacts with `IBlobStorageClient` and returns `IResult`

### IBlobStorageClient Extension

Two new methods added to `IBlobStorageClient`:
- `DownloadBlobAsync(blobPath, ct)` → streams blob content via `DownloadStreamingAsync()` with Polly resilience
- `GetBlobETagAsync(blobPath, ct)` → lightweight metadata check via `GetPropertiesAsync()` for conditional requests

Both return `null` when the blob does not exist (404 from Azure SDK is caught and mapped).

`NullBlobStorageClient` returns `null` for both methods — when cloud storage is disabled, all proxy requests return 404.

### URL Rewriting in GetEntityDetailsHandler

The handler currently constructs `storageUrl` values using `GetFolderUrl(correlationId)` which returns the direct Azure Blob Storage URL. The modification:

```
BEFORE: storageUrl = $"{GetFolderUrl(correlationId)}/{filename}"
         → https://account.blob.core.windows.net/diagnostics/{correlationId}/{filename}

AFTER:  storageUrl = $"{requestBaseUrl}/api/v1/documents/{correlationId}/{filename}"
         → https://localhost:8443/api/v1/documents/{correlationId}/{filename}
```

`requestBaseUrl` is derived from `HttpContext.Request` (`{Scheme}://{Host}`), making it environment-agnostic.

**Audit trail note**: The audit entry `DiagnosticsUrl` and `CertificateUrl` continue to store the direct blob URL for operational tracing. Only the API response values are rewritten.

### Streaming Architecture

```text
Client → [GET /api/v1/documents/abc/file.pdf]
  │
  ▼
DocumentEndpoints
  │ validate blobPath
  │ extract If-None-Match header
  │
  ▼
StreamDocumentHandler
  │ conditional check (If-None-Match → GetBlobETagAsync)
  │ if match → return 304
  │
  │ download (DownloadBlobAsync)
  │ if null → return 404
  │
  │ set Cache-Control, ETag headers
  │ return Results.Stream(content, contentType, fileName)
  │
  ▼
AzureBlobStorageClient
  │ ResiliencePipeline (3 retries, 30s timeout)
  │ BlobClient.DownloadStreamingAsync()
  │
  ▼
Azure Blob Storage (diagnostics container)
```

### Observability

**Logging** (`DocumentLogMessages`, EventId 3000–3099):

| EventId | Level | Message |
|---------|-------|---------|
| 3000 | Information | `Document stream started for '{blobPath}'.` |
| 3001 | Information | `Document stream completed for '{blobPath}' — {bytesStreamed} bytes in {durationMs} ms.` |
| 3002 | Warning | `Document not found: '{blobPath}'.` |
| 3003 | Error | `Document stream failed for '{blobPath}'.` |
| 3004 | Warning | `Document stream timed out for '{blobPath}'.` |
| 3005 | Information | `Document not modified (304) for '{blobPath}'.` |

**Metrics** (via `System.Diagnostics.Metrics.Meter`):

| Metric | Type | Tags |
|--------|------|------|
| `bizscraper.documents.requests` | Counter | `status` (200/304/400/404/502/504) |
| `bizscraper.documents.duration_ms` | Histogram | `status` |

### Constitution Check (Post-Design)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | **PASS** | Unit tests for `BlobPathValidator`, `StreamDocumentHandler` (mock `IBlobStorageClient`); integration tests for end-to-end streaming; error scenario tests for each status code path |
| II. SOLID Design | **PASS** | `IBlobStorageClient` extended with cohesive download methods (ISP); `StreamDocumentHandler` has single concern (SRP); `BlobPathValidator` is a pure static utility; `DocumentEndpoints` follows existing extension method pattern |
| III. Architectural Quality | **PASS** | Streaming-first (no buffering); Polly resilience; structured logging with `[LoggerMessage]`; OTel metrics; blob path validation at system boundary; RFC 9457 error responses |
| IV. Hybrid SemVer+CalVer | **PASS** | Extends API `v1`; no new versioned artifacts |
| V. Hands-Free Deployment | **PASS** | No infra changes; reuses existing cloud storage config; `MapDocumentEndpoints()` in `Program.cs`; zero manual steps |

**Gate Result**: PASS — Design is constitution-compliant.

---

## Artifacts Generated

| Artifact | Path |
|----------|------|
| This plan | `specs/011-pdf-proxy-streaming/plan.md` |
| Research | `specs/011-pdf-proxy-streaming/research.md` |
| Data model | `specs/011-pdf-proxy-streaming/data-model.md` |
| Quickstart | `specs/011-pdf-proxy-streaming/quickstart.md` |
| OpenAPI contract | `specs/011-pdf-proxy-streaming/contracts/openapi.yaml` |
| Internal contracts | `specs/011-pdf-proxy-streaming/contracts/internal-contracts.md` |
| Tasks | `specs/011-pdf-proxy-streaming/tasks.md` — created by `/speckit.tasks` |
