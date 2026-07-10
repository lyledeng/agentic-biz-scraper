# BizScraper — Global Specification Snapshot

**Version**: 1.8.2604.0 | **Last Updated**: 2026-04-12
**Constitution**: v1.2.2603.0

This is the canonical reference for the BizScraper project's current API
surface, data model, and behavioural contracts. Individual feature specs
under `specs/NNN-feature/` remain the audit trail of how the system evolved.

---

## 1. Generic Script Execution

**Authoritative Spec**: 016-generic-script-execution

The primary API entry point. All business search and entity detail
operations are invoked through a single generic endpoint that executes
JSON-driven scraping flow definitions.

- **Endpoint**: `POST /api/v2/execute-script`
- **Definitions Listing**: `GET /api/v2/definitions`
- **Deprecated v1**: `POST /api/v1/execute-script` and `GET /api/v1/definitions` return 410 Gone with migration guidance.
- **Removed v1**: `GET /api/v1/business-search` and `GET /api/v1/entity-details`
  have been deleted (features 020 and 018 respectively). See
  `specs/current-specs/contracts/deprecated-*.md` for migration guidance.
- **Flow Engine**: ScrapingFlowEngine executes FlowDefinitionV2 JSON
  files with configurable actions, variables, conditions, and output
  schemas.
- **Available Definitions**: `us-co-business-search`, `us-ia-business-search`, `us-mo-business-search`,
  `us-wa-business-search`, `us-wy-business-search`, `de-de-business-search`, `us-co-entity-details`, `us-ia-entity-details`,
  `us-mo-entity-details`, `us-wa-entity-details`, `us-wy-entity-details`, `de-de-entity-details`
- **Post-Processing Mappers**: v2 responses are piped through
  `SearchResultMapper` (for `*-business-search` slugs) or
  `EntityDetailMapper` (for `*-entity-details` slugs) to produce
  unified response shapes.

### Behaviour

1. Client sends `ExecuteScriptRequest` with a definition slug and
   optional parameters.
2. Engine loads the matching JSON flow definition.
3. Engine launches a Playwright browser (routed to the correct
   browser pool based on the definition's optional `browser`
   configuration — see [§13 Per-Definition Browser Mode](#13-per-definition-browser-mode)),
   executes actions in order
   (navigate, fill, click, extract, loop, etc.).
4. Engine captures screenshots at configured milestones.
5. Engine returns `ExecuteScriptResponse` with correlation ID and
   the extracted data payload.

### Error Handling

All errors return RFC 7807 ProblemDetails responses. Custom exception
types map to specific HTTP status codes via `GlobalExceptionHandler`.
See [Error Handling](#10-error-handling) for the full mapping.

---

## 2. Business Search (CO / IA / MO / WY / DE)

**Authoritative Spec**: 018 (supersedes 016, 001, 005, 006, 013), 031

Search state Secretary of State databases (US) or the German
Handelsregister (DE) for business entities.

- **Colorado**: Definition `us-co-business-search`, requires `searchTerm`
- **Iowa**: Definition `us-ia-business-search`, requires `searchTerm`
- **Missouri**: Definition `us-mo-business-search`, requires `searchTerm`;
  ASP.NET Web Forms with postback pagination (20 results/page, 1,051 cap).
  Previous-name rows collapsed into `previousNames` string array on parent entity.
  `truncated: true` when result cap exceeded.
- **Washington**: Definition `us-wa-business-search`, requires `searchTerm`;
  AngularJS SPA at `ccfs.sos.wa.gov` with Cloudflare Turnstile protection.
  Results paginated 25/page. Extracts name, UBI number, business type,
  principal office, registered agent, status. `formationDate` always null
  (not in search results). UniqueKey encodes businessId + businessType.
- **Wyoming**: Definition `us-wy-business-search`, requires `searchTerm`;
  includes CAPTCHA resolution with retry loop.
- **Germany**: Definition `de-de-business-search`, requires `searchTerm`;
  includes paginated result aggregation.
- **Response Model**: `UnifiedSearchResult[]` — flat array with fields:
  name, identifier, status, entityType, formationDate, state, event,
  uniqueKey, and optional standingTax, standingRA, registeredOffice.
- **UniqueKey**: Base64url-encoded JSON containing state prefix
  (US-CO, US-IA, US-MO, US-WA, US-WY, DE-DE) and state-specific params for entity detail
  lookup. Replaces the former `detailsUrl` field.
- **Pagination**: Engine scrapes multiple pages; `Truncated` flag
  indicates whether results exceeded maximum.
- **Result Count**: Response includes `resultCount` field (int?) for
  business-search definitions — the count of returned `UnifiedSearchResult`
  items. Omitted for non-search definitions.
- **UI States**: The test-ui search component displays four distinct states:
  (a) initial prompt before any search, (b) no-records-found informational
  notification when `resultCount` is 0 and `truncated` is false,
  (c) truncation warning when `truncated` is true with zero results,
  (d) results table when data is present.
- **Audit Trail**: Each search is recorded in `ScriptExecutionAuditEntries`.

---

## 3. Entity Details (CO / IA / MO / WA / WY / DE)

**Authoritative Spec**: 018 (supersedes 016, 004, 007, 008, 009, 015), 031, 032

Retrieve detailed entity information using a uniqueKey obtained via
business search.

- **Colorado**: Definition `us-co-entity-details`, requires `uniqueKey`
- **Missouri**: Definition `us-mo-entity-details`, requires `uniqueKey`;
  extracts General Info (name, charter, entity type, status, purpose,
  formation date, registered agent, organizer), Principal Office Address,
  and Filings (with PDF download to Azure Blob Storage).
  Per-document error handling — individual PDF download failures do not
  block overall extraction. Certificate of Good Standing excluded (paywall).
- **Washington**: Definition `us-wa-entity-details`, requires `uniqueKey`;
  extracts General Info (name, UBI, business type, status, addresses,
  expiration date, jurisdiction, formation date, duration, inactive date,
  nature of business, and 6 nonprofit-specific fields), Registered Agent,
  Governors/Officers, and Filing History with document downloads.
  Documents downloaded via page-level `fetch()` to CCFS API endpoints
  (`GetTransactionDocumentsList`, `DownloadOnlineFilesByNumber`) preserving
  session cookies. Per-document error handling. N=10 configurable filing
  document download limit. Name History out of scope for initial release.
- **Iowa**: Definition `us-ia-entity-details`, requires `uniqueKey`;
  extracts full entity summary from table-based layout including
  standard fields, registered agent, and Iowa-specific fields:
  `chapterCode` (chapter/code classification), `certificateNote`
  (certificate paywall note), and `iowaNames` (names list with type,
  status, and modified flag stored as `IowaNameEntry[]` on
  `DetailSection` rather than generic `PartyEntry[]`).
- **Wyoming**: Definition `us-wy-entity-details`, requires `uniqueKey`;
  includes CAPTCHA loops, party extraction, history documents with
  cloud storage upload (generic in `JsonDrivenEntityDetailScraper`;
  each successfully downloaded history document is uploaded to Azure
  Blob Storage and its URL replaced with a proxy URL — feature 029),
  and certificate of good standing retrieval.
  Certificate flow uses `element-exists` condition on the Print button
  to detect eligibility; ineligible entities receive `certificate.available = false`
  with descriptive error. Certificate download errors are isolated
  and never block entity details.
- **Germany**: Definition `de-de-entity-details`, requires `uniqueKey`;
  downloads Aktuelle Drucke (AD) PDF, uploads to blob storage,
  translates via FAB Document Translator, returns original + translated.
- **Response Model**: `UnifiedEntityDetailResponse` with five nullable
  sections: `DetailSection`, `AgentSection`, `CertificateSection`,
  `PartyEntry[]`, `DocumentEntry[]`.
- **DetailSection**: 22 fields covering name, status, identifier,
  entityType, formationDate, jurisdiction, addresses, standing,
  and Iowa-specific chapterCode, certificateNote, iowaNames.
- **DocumentEntry**: title, date, and `DownloadReference[]` (label,
  proxyUrl, fileName, error). WY has "Filing Document" per history
  event; DE has "Original" + "Translated" per entity.
- **CertificateSection**: available flag, error, and `DownloadReference[]`.
- **Document Storage**: WY history documents, DE PDFs, and certificates
  are uploaded to Azure Blob Storage; proxy URLs returned in response.
- **Audit Trail**: Each detail scrape is recorded in
  `ScriptExecutionAuditEntries`.

---

## 4. (Removed — Germany Business Search merged into §2)

---

## 5. (Removed — Germany Hardcopy Printout merged into §3)

---

## 6. PDF Document Proxy

**Authoritative Spec**: 011-pdf-proxy-streaming

Stream PDF documents from Azure Blob Storage with caching support.

- **Endpoint**: `GET /api/v1/documents/{**blobPath}`
- **Caching**: `public, max-age=86400, immutable` with ETag-based
  conditional requests (If-None-Match → 304 Not Modified).
- **Download Mode**: Optional `?download=true` query parameter forces
  `Content-Disposition: attachment`.
- **Security**: Blob path is validated; only expected paths are served.

---

## 7. JSON Scraping Engine v2

**Authoritative Spec**: 006-json-scraping-engine, 014-recorder-friendly-schema

The engine that powers all scraping operations. Flow definitions are
JSON files conforming to the `FlowDefinitionV2` schema.

### Action Types

| Action | Purpose |
|--------|---------|
| `navigate` | Navigate browser to URL |
| `fill` | Fill form field with value |
| `click` | Click element |
| `wait-for-load` | Wait for page load state |
| `wait-for-condition` | Wait for JavaScript condition |
| `extract` | Execute JavaScript to extract data |
| `check-text` | Validate page text content |
| `screenshot` | Capture milestone screenshot |
| `download` | Download file from link |
| `loop` | Iterate (pagination or retry) |
| `call-service` | Call external service (CAPTCHA solver) |

### Condition System

Conditions control action execution: `element-exists`,
`element-visible`, `text-contains`, `variable-truthy`,
`variable-falsy`, with boolean combinators `and`, `or`, `not`.

### Variable System

Variables have three sources: `runtime` (caller-provided),
`config` (definition-level defaults), and `extracted`
(populated by extract actions during execution).

---

## 8. Scraping Diagnostics & Cloud Screenshot Storage

**Authoritative Specs**: 002-scraping-diagnostics, 003-cloud-screenshot-storage

Infrastructure for debugging and auditing scraping operations.

- **Tracing Modes**: Off, OnFailure, Always (configurable via
  `DiagnosticsOptions`).
- **Screenshot Capture**: Automatic at configured milestones and on
  error. Stored locally or uploaded to Azure Blob Storage.
- **Cloud Storage**: `AzureBlobStorageClient` with Polly resilience
  (3 retries, exponential backoff). `NullBlobStorageClient` for
  local development.
- **Diagnostics URL**: Stored in audit trail entries for post-mortem
  analysis.

---

## 9. Test UI

**Authoritative Spec**: 018 (supersedes 010)

Angular 17 standalone application for testing API capabilities.

- **Route**: Served at `/` (separate container)
- **Features**: Unified business search form (CO/WY/IA/MO/DE) with consistent
  results grid, entity details viewer with unified sections (details,
  registered agent, certificate, parties, documents, Iowa names),
  inline authenticated PDF viewing for all document types (certificates,
  filing history, translated PDFs) with error handling and retry support.
  503 Retry-After handling on both search and entity details views.
  Document buttons use Jumpstart `button-field-pds3 type="secondary"
  size="small"` with `wk-icon-file-pdf` icon for visual prominence
  (feature 029).
- **Document Viewing**: Document links use `HttpClient` (MSAL-intercepted)
  to fetch PDFs as blobs, then render inline via `<object type="application/pdf">`
  with blob URLs. Blob URLs are revoked on component destroy. Error states
  display inline with categorised messages and a Retry button.
  (Authoritative Spec: 028-doc-proxy-auth)
- **API**: Consumes `/api/v2/execute-script` and `/api/v2/definitions`.
  Legacy v1 services and Germany-specific components removed.
- **Stack**: Angular 17, Jumpstart PDS components, standalone
  components with signals.

---

## 10. Error Handling

**Authoritative Spec**: 012-co-record-count-error

All API errors use RFC 7807 ProblemDetails format via
`GlobalExceptionHandler`.

| Exception | HTTP | Error Type |
|-----------|------|-----------|
| `UnknownDefinitionException` | 400 | unknown-definition |
| `MissingParametersException` | 400 | missing-parameters |
| `InvalidDetailsUrlException` | 400 | invalid-details-url |
| `ExceededRecordCountException` | 422 | exceeded-record-count |
| `ServiceBusyException` | 503 | service-busy (+Retry-After) |
| `ScrapingException` | 502 | scraping-error |
| `CaptchaResolutionException` | 502 | captcha-resolution-failed |
| `UpstreamException` | 502 | upstream-error |
| `TimeoutException` | 504 | execution-timeout |
| *(unhandled)* | 500 | unhandled-error |

---

## 11. Health Checks

- `GET /healthz` — Liveness probe. Returns `{ status: "Healthy" }`.
- `GET /ready` — Readiness probe. Returns per-mode browser health
  breakdown with `200 OK` if at least one mode is healthy, `503`
  if all modes are unhealthy. Remote mode reports "healthy" when the
  endpoint is configured without creating an Azure Playwright session
  (Authoritative Spec: 037-no-remote-browser-cache). Response includes:
  - `status`: "Ready" | "Degraded" | "NotReady"
  - `browserModes.local`: health status, channel
  - `browserModes.remote`: health status, endpoint
  - `definitionsRequiringLocal`: slugs with explicit local override
  - `definitionsRequiringRemote`: slugs with explicit remote override
  (Authoritative Spec: 030-per-definition-browser-mode, supersedes 021)

---

## 12. CI Pipeline

**Authoritative Spec**: 019-ci-pipeline-refinement

CI is implemented as a two-stage Azure DevOps pipeline in
`deploy/pipelines/ci.yaml`:

- **Validation** stage runs on all branch and PR triggers and includes:
  - .NET restore/build/test
  - Angular build/test
  - Helm lint for `bizscraper` and `bizscraper-ui`
  - Liquibase validate + update against disposable SQL Server Edge
  - Documentation validation scripts
- **ImageBuildAndPush** stage runs only when Validation succeeds and
  `Build.Reason` is not `PullRequest`.
- Registry routing:
  - `main` branch → `Azure Container Registry (Staging)-iLienMVP`
  - non-main branches → `Azure Container Registry (Assembly)-iLienMVP`
- Image tagging uses `$(Build.BuildNumber)`.
- UI image is built with:
  - `BASE_HREF=/mvpoc/bizscraper-ui/`
  - `API_BASE_URL=/mvpoc/bizscraper-api`
- Helm chart ingress for non-main deployment host
  `devcaas-az.ilienonline.com` with TLS secret
  `ingress-tls-cert-secret` is configured for both API and UI charts.
- UI image receives MSAL build args (`MSAL_CLIENT_ID`, `MSAL_AUTHORITY`,
  `MSAL_REDIRECT_URI`, `MSAL_POST_LOGOUT_REDIRECT_URI`, `MSAL_SCOPES`)
  in the CI pipeline and Dockerfile.

---

## 12. Authentication

**Authoritative Spec**: 025-entra-id-auth

Microsoft Entra ID (Azure AD) protects the API and Test-UI.

### Backend (API)

- **Library**: `Microsoft.Identity.Web` (JWT Bearer validation)
- **Middleware**: `UseAuthentication()` + `UseAuthorization()` in pipeline
- **Authorization Policy** (`BizScraperAccess`): Accepts either
  `access_as_user` delegated scope **or** `BizScraper.Execute` application role
- **Protected Endpoints**: `POST /api/v2/execute-script`, `GET /api/v2/definitions`,
  `GET /api/v1/documents/{**blobPath}`
- **Anonymous Endpoints**: `/healthz`, `/ready`, `/swagger/**`, `/` (redirect)
- **Token Types**: Delegated (Authorization Code + PKCE) and Application
  (Client Credentials)
- **Error Responses**: 401 (missing/invalid token), 403 (wrong scope/role),
  503 (Entra ID unreachable)
- **CORS**: `Authorization` header allowed
- **Swagger**: OAuth2 security definition with PKCE support
- **Contract**: See `contracts/authentication.md`

### Frontend (Test-UI)

- **Library**: `@azure/msal-browser` + `@azure/msal-angular`
- **Flow**: Authorization Code + PKCE with redirect interaction
- **MsalGuard**: Protects all routes except auth redirect
- **MsalInterceptor**: Attaches bearer tokens to API requests
- **User Display**: Shows authenticated user's display name in banner
- **Sign-Out**: Redirect-based logout via `MsalService.logoutRedirect()`

### Audit Trail

Caller identity is extracted from JWT claims and recorded in audit tables:
- Delegated tokens → `preferred_username` claim
- Application tokens → `azp` claim
- Stored in `SearchAuditEntries.CallerIdentity` and
  `ScriptExecutionAuditEntries.CallerIdentity`

---

## 13. Per-Definition Browser Mode

**Authoritative Spec**: 030-per-definition-browser-mode (supersedes 021)

Each scraping definition JSON can optionally override the global browser
mode via a top-level `browser` section.

- **Browser Config**: Optional `browser` property on `FlowDefinitionV2`
  with `mode` ("local" | "remote"), `channel` (e.g., "chrome"), and
  `headless` (boolean).
- **Mode Resolution**: Definition override → global default
  (`BrowserEndpoint` presence). Remote requested without endpoint → local
  fallback with warning.
- **Dual Browser Pools**: Separate `BrowserPool` instances for local and
  remote modes. Local pool reuses browsers across requests up to
  `ReuseBrowserForRequests` threshold with independent concurrency limits
  (default 3). Remote mode uses **per-request lifecycle**: each remote
  execution creates an ephemeral `IPlaywright` + `IBrowser`, executes the
  flow, and disposes both immediately — eliminating idle Azure Playwright
  session billing (Authoritative Spec: 037-no-remote-browser-cache).
  One retry on mid-scrape disconnect; 30-second `ConnectAsync` timeout.
- **Iowa Definitions**: `us-ia-business-search` and `us-ia-entity-details`
  configured with `{ "mode": "local", "channel": "chrome", "headless": true }`
  to bypass Iowa SOS anti-bot detection.
- **All Other Definitions**: No browser override — use global default.
- **Stealth Args**: `--disable-blink-features=AutomationControlled` applied
  by default for all local-mode launches.
- **Health Check**: `/ready` reports per-mode health breakdown;
  `200 OK` if at least one mode healthy, `503` only if all unhealthy.
- **Logging**: Active browser mode logged per execution (FR-014) with
  source (definition override vs. global default).

---

## Constitution Compliance

*This section is a living assessment against constitution v1.2.2603.0.
Updated as compliance status changes.*

### Principle Compliance

#### I. Test-First — ⚠️ Partial

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Test pyramid layers | ✅ | Unit (xUnit + NSubstitute), Integration (BizScraper.IntegrationTests), E2E (BizScraper.EndToEndTests), Performance (k6) |
| Tests run in CI | ✅ | `dotnet test` in DotNetBuild job; `ng test --code-coverage` in AngularBuild job |
| TDD discipline (Red-Green-Refactor) | ⚠️ | Practiced in features 017+ (documented in tasks.md Red/Green phases). Earlier features lack formal TDD evidence. |
| Coverage enforcement in CI | ❌ | No coverage threshold configured. Angular produces coverage reports but no gate prevents regression. |

**Remediation**: Add `--collect:"XPlat Code Coverage"` to `dotnet test` and `coverlet` threshold. Add Angular `--code-coverage-exclude` and minimum threshold via `karma.conf.js`.

#### II. SOLID Design & Engineering Discipline — ✅ Compliant

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Single Responsibility | ✅ | Vertical slice architecture — each feature (BusinessSearch, EntityDetails, ExecuteScript, etc.) is self-contained with its own endpoints, handlers, models, and exceptions. |
| Open/Closed | ✅ | JSON-driven scraping engine (FlowDefinitionV2) allows new scraping flows without code changes. Action handlers registered via `IActionHandler` interface. |
| Liskov Substitution | ✅ | `IEntityDetailScraper` implementations (PlaywrightEntityDetailScraper, JsonDrivenEntityDetailScraper) are interchangeable. |
| Interface Segregation | ✅ | Fine-grained interfaces: `IBlobStorageClient`, `ICaptchaSolver`, `IDocumentTranslator`, `IActionHandler`, `IPostProcessor`. |
| Dependency Inversion | ✅ | All infrastructure wired via DI. Handlers depend on abstractions (`IBlobStorageClient`, `ICaptchaSolver`), not concrete implementations. |
| DRY | ✅ | Shared `Common/` layer for cross-cutting interfaces and configuration. Engine reuse across all scraping flows. |
| YAGNI | ✅ | No speculative code. Features built to current requirements only. |
| Separation of Concerns | ✅ | Clear boundaries: Features/ (application), Infrastructure/ (adapters), Domain/ (entities), Common/ (shared contracts). |

#### III. Architectural Quality Attributes — ✅ Compliant

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Reliability | ✅ | Typed exceptions per feature (e.g., `BusinessSearchException`, `ScriptExecutionException`). `GlobalExceptionHandler` middleware for consistent error responses. |
| Resilience | ✅ | `RetryConfig` in FlowDefinitionV2 with exponential backoff. `RequestTimeoutMiddleware` prevents runaway requests. |
| Security | ✅ | `SecurityHeadersMiddleware` adds CSP, X-Frame-Options, etc. Input validation at API boundaries. Secrets via Azure Key Vault (Helm `values.yaml`). |
| Supportability | ✅ | Structured logging via `[LoggerMessage]` source generators across all features. `CorrelationIdMiddleware` propagates trace context. |
| Scalability | ✅ | Stateless API design. Concurrency managed via `SemaphoreSlim` (EntityDetails). Cloud blob storage for artifacts. |
| Auditability | ✅ | `SearchAuditEntry`, `EntityDetailAuditEntry`, `ScriptExecutionAuditEntry` tables with timestamps, correlation IDs, and outcomes. |
| Self-Documentability | ✅ | All 120 public types carry `/// <summary>` XML doc-comments. Swagger/OpenAPI descriptions on all endpoints. |
| Observability | ✅ | Health check endpoints (`/healthz`, `/readyz`). Feature-level metrics classes (`BusinessSearchMetrics`, `EntityDetailsMetrics`, `DocumentMetrics`). Diagnostics URL per audit entry. |

#### IV. Hybrid SemVer+CalVer Versioning — ✅ Compliant

| Criterion | Status | Evidence |
|-----------|--------|----------|
| MAJOR.MINOR.YYMM.PATCH format | ✅ | Liquibase changesets: `1.0.2603.0` through `2.0.2603.6`. Constitution: `1.2.2603.0`. |
| Calendar segment accuracy | ✅ | YYMM=2603 (March 2026) matches actual development period. |
| Consistent across artifacts | ✅ | Code (Docker tags via `Build.BuildId`), Liquibase (changeset IDs), Constitution (version header), Specs (feature numbering). |

#### V. Hands-Free Deployment & Rollback — ✅ Compliant

| Criterion | Status | Evidence |
|-----------|--------|----------|
| CI/CD pipelines | ✅ | `deploy/pipelines/ci.yaml` — automated build, test, Docker build. `cd.yaml` for deployment. |
| Infrastructure as Code | ✅ | Helm charts in `deploy/helm/bizscraper/` and `deploy/helm/bizscraper-ui/`. Liquibase for DB migrations. |
| Secrets management | ✅ | No plaintext secrets in source. Azure Key Vault integration via Helm values and `CloudStorageOptions`. |
| Docker builds | ✅ | Multi-stage Dockerfiles: `src/bizscraper-dockerfile`, `src/bizscraper-ui-dockerfile`, `deploy/liquibase/liquibase_dockerfile`. |

#### VI. Documentation — ✅ Compliant (post-remediation)

| Criterion | Status | Evidence |
|-----------|--------|----------|
| API Documentation (Swagger) | ✅ | All endpoints use `.WithName()`, `.WithTags()`, `.Produces()`, `.WithOpenApi()`. |
| Code Documentation (XML docs) | ✅ | 120/120 public types have `/// <summary>` doc-comments. `GenerateDocumentationFile=true` in Directory.Build.props. |
| Code Documentation (method-level) | ⚠️ | Type-level complete. Method-level deferred to follow-up (630 public members). CS1591 remains suppressed. |
| Script Documentation | ✅ | All JSON flow definitions include `metadata.description`. CI scripts include purpose headers. |
| Database Documentation | ✅ | All 8/8 Liquibase changesets have `--comment` attributes describing the migration purpose. |
| README & Quickstart | ✅ | `README.md` at repo root. Feature-level `quickstart.md` files for integration scenarios. |
| Global Specification Snapshot | ✅ | `specs/current-specs/` contains `spec.md`, `data-model.md`, and 8 contracts (5 current + 3 deprecated). |

### Quality Gate Compliance

| Gate | Status | Evidence |
|------|--------|----------|
| 1. Test Gate | ✅ Automated | `dotnet test` (xUnit) + `ng test --code-coverage` (Karma/Jasmine) run in CI. Unit, integration, and E2E test projects exist. |
| 2. Static Analysis Gate | ✅ Automated | `TreatWarningsAsErrors=true`, `CodeAnalysisTreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-all` in Directory.Build.props. Build fails on any warning. |
| 3. Security Gate | ⚠️ Partial | `SecurityHeadersMiddleware` enforces runtime headers. Black Duck SBOM pipeline (`deploy/pipelines/sbom.yaml`) for SCA. No SAST tool integrated into main CI pipeline. |
| 4. Review Gate | ✅ Manual | Azure DevOps branch policies require PR review. Constitution mandates ≥1 reviewer. |
| 5. Build Gate | ✅ Automated | `dotnet build -c Release` + Docker multi-stage builds produce versioned artifacts in CI. |

### Remediation Plans

| Finding | Severity | Remediation | Target |
|---------|----------|-------------|--------|
| No test coverage threshold in CI | Medium | Add `coverlet` minimum threshold (80%) to `dotnet test`. Add Angular coverage gate. | Next sprint |
| SAST not in main CI | Medium | Integrate SonarQube or similar SAST into `ci.yaml` as a new job. | Next sprint |
| Method-level XML docs (630 members) | Low | Add `/// <summary>` to all public methods/properties. Remove CS1591 from NoWarn. | Follow-up feature |
