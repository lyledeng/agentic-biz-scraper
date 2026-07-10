# Research: Multi-State Business Entity Search

**Feature**: `005-multi-state-search`  
**Date**: 2026-03-25

## R1: Wyoming ASP.NET WebForms Postback Pagination

**Decision**: Use Playwright's click + network-idle wait to drive WY postback pagination.

**Rationale**: The Wyoming site uses ASP.NET WebForms with `__doPostBack('ctl00$MainContent$lbtnNextHeader','')` for pagination. Playwright naturally handles JavaScript postback links via standard click operations. The `WaitUntilState.NetworkIdle` strategy (already used for CO pagination) works for postback-triggered page updates since the full page reloads.

**Alternatives considered**:
- Direct HTTP POST with `__VIEWSTATE`/`__EVENTVALIDATION`: Rejected — requires extracting hidden form fields and managing session state manually; fragile and not worth the complexity given Playwright already handles this.
- Intercept network requests to call postback API directly: Rejected — same fragility concerns as direct HTTP POST.

**Key detail**: WY pages 20 results per page and shows "Results: 1-20 of 37" with page navigation links `>` (next) and `>>` (last). The page number textbox and `of N` text provide total page count.

## R2: FAB AI Agent HTTP Integration

**Decision**: Use `Flurl.Http` for the FAB CAPTCHA solver HTTP POST, wrapped in a Polly retry pipeline.

**Rationale**: Flurl.Http is already a declared dependency (4.0.2 in `Directory.Packages.props`) though not currently used in source code. It provides a fluent, testable HTTP client that fits the project's existing dependency choices. The FAB call is a simple JSON POST with base64 image payload and structured JSON response — Flurl excels at this pattern. Polly retry wraps transient failures (5xx, timeouts).

**Alternatives considered**:
- Raw `HttpClient` via `IHttpClientFactory`: Viable but more boilerplate; Flurl is already available and provides cleaner fluent syntax for one-off API calls.
- Custom `HttpMessageHandler` pipeline: Over-engineered for a single endpoint call.

**Integration contract** (user-specified format):
- POST `{FabAgent:CaptchaSolver:EndpointUrl}` with `Authorization: Bearer {token}` header
- Request body:
  ```json
  {
    "input": {
      "format": "base64",
      "data": "<base64-encoded-png-screenshot>"
    }
  }
  ```
- Response body:
  ```json
  {
    "output": {
      "captchaText": "Tdd4eVa",
      "confidence": "high"
    }
  }
  ```
- Timeout: 30 seconds (configurable via `FabAgent:CaptchaSolver:TimeoutSeconds`)

## R3: Semantic Scraper Interface Design (IStateSearchScraper)

**Decision**: Define `IStateSearchScraper` with a `StateCode` property and a `SearchAsync` method returning a common `SearchScrapeResult`. Register implementations as keyed singletons in DI using the state code as key. The handler resolves the correct scraper via `IServiceProvider.GetRequiredKeyedService<IStateSearchScraper>(stateCode)`.

**Rationale**: The keyed services pattern is already used in the codebase for the EntityDetail semaphore. Using state code as the key provides clean O(1) scraper resolution without a factory class or dictionary. Each scraper encapsulates all state-specific DOM logic. The existing `PlaywrightBusinessEntityScraper` becomes the CO implementation; a new `PlaywrightWyomingSearchScraper` handles WY.

**Alternatives considered**:
- Strategy pattern with explicit factory: Rejected — keyed DI services achieve the same dispatch without a manual factory class.
- Single scraper class with state-conditional branches: Rejected — violates SRP and OCP; adding a new state would require modifying existing code.
- Abstract base class with template method: Rejected — composition over inheritance per constitution; shared browser/diagnostics logic lives in the existing `PlaywrightBusinessEntityScraper` which is injected as a collaborator (same pattern as `PlaywrightEntityDetailScraper`).

**Interface shape**:
```
IStateSearchScraper
  - string StateCode { get; }   // "CO" or "WY"
  - Task<NormalizedSearchScrapeResult> SearchAsync(string searchTerm, string correlationId, CancellationToken)
```

Each scraper returns `NormalizedSearchScrapeResult` — a new wrapper containing `IReadOnlyList<NormalizedSearchResult>`, `SiteTotalCount`, `Truncated`, and `PagesScraped`. The CO scraper maps `BusinessEntityResult` → `NormalizedSearchResult` internally (DocumentNumber→identifier, Form→entityType, Event→event, standingTax/RA=null, state="CO"). The WY scraper creates `NormalizedSearchResult` directly from parsed page content (FilingNumber→identifier, parsed EntityType→entityType, event=null, standingTax/RA populated, state="WY"). This keeps normalization inside each scraper and avoids forcing WY-specific fields onto the CO-only `BusinessEntityResult` type. The handler assembles the `NormalizedSearchResponse` envelope without any field-mapping logic.

## R4: Normalized Response Schema with Source-Generated JSON

**Decision**: Create `NormalizedSearchResult` and `NormalizedSearchResponse` records. Use `string?` for optional state-specific fields (`event`, `standingTax`, `standingRA`). Register both in `AppJsonSerializerContext` with `[JsonSerializable]`.

**Rationale**: System.Text.Json source generation with `CamelCase` naming policy (already configured) handles nullable string properties correctly — they serialize as `null` in JSON when not set. This keeps the schema unified: every result has the same fields, with state-specific ones being `null` when inapplicable. No polymorphic serialization or JSON converters needed.

**Alternatives considered**:
- `JsonDerivedType` polymorphic serialization (one subtype per state): Rejected — adds complexity for discriminator handling; consumers would need type-switching logic, defeating normalization.
- Dictionary-based extensions property: Rejected — loses compile-time safety and AOT compatibility.
- Separate response types per state: Rejected — directly contradicts the normalization requirement.

**Schema**:
```
NormalizedSearchResult:
  name: string              // common
  identifier: string        // CO documentNumber / WY filingNumber
  status: string            // common
  entityType: string        // CO form / WY parsed entity type
  formationDate: string     // CO formationDate / WY filedOn
  detailsUrl: string        // common (absolute URL)
  state: string             // "CO" or "WY"
  event: string?            // CO-only
  standingTax: string?      // WY-only
  standingRA: string?       // WY-only

NormalizedSearchResponse:
  results: NormalizedSearchResult[]
  totalCount: int
  siteTotalCount: int
  truncated: bool
  pagesScraped: int
  searchTerm: string
  state: string             // "CO" or "WY"
```

## R5: Per-State Keyed Semaphore Pattern

**Decision**: Register two keyed `SemaphoreSlim(1,1)` singletons — one keyed `"CO"`, one keyed `"WY"`. The handler resolves the correct semaphore using the `state` parameter as key.

**Rationale**: Follows the exact pattern used by EntityDetails (`"EntityDetailSemaphore"`). Per-state semaphores allow CO and WY to run simultaneously while preventing two concurrent searches for the same state (which would compete for the same browser context and page interactions).

**DI registration pattern**:
```csharp
builder.Services.AddKeyedSingleton("CO", (_, _) => new SemaphoreSlim(1, 1));
builder.Services.AddKeyedSingleton("WY", (_, _) => new SemaphoreSlim(1, 1));
```

**Handler resolution**: The handler constructor can't use `[FromKeyedServices]` since the key is dynamic (from request). Instead, use `IServiceProvider.GetRequiredKeyedService<SemaphoreSlim>(state)` at runtime.

## R6: Wyoming CAPTCHA Detection and Solving Flow

**Decision**: After navigating to the WY search page, check if the page contains the CAPTCHA challenge form (identifiable by the presence of a textbox with "What code is in the image?" text and a submit button). If detected, screenshot the CAPTCHA image element, base64-encode it, POST to FAB agent using the `input` wrapper format, fill the answer from `output.captchaText`, submit, and wait for the search page to load.

**Rationale**: The Wyoming site uses Imperva/Incapsula CAPTCHA protection that presents an image CAPTCHA on first visit. The CAPTCHA page structure (discovered via Playwright MCP) is:
- Text: "This question is for testing whether you are a human visitor..."
- Image element (distorted alphanumeric characters, 5-7 chars)
- Textbox for answer entry
- Submit button
- Support ID text

**Detection heuristic**: Check for the presence of text "What code is in the image?" on the page. If found, the page is a CAPTCHA challenge, not the search form.

**Flow**:
1. Navigate to `https://wyobiz.wyo.gov/Business/FilingSearch.aspx`
2. Check for CAPTCHA presence
3. If CAPTCHA: screenshot the CAPTCHA image → POST to FAB (with `{ "input": { "format": "base64", "data": "..." } }`) → extract `output.captchaText` → fill answer → submit
4. Verify the search form loaded (check for "Filing Name" textbox)
5. If still on CAPTCHA page: fail with `CaptchaResolutionException`
6. Proceed with normal search flow

## R7: Audit Trail Schema Extension

**Decision**: Add a nullable `State` column (`NVARCHAR(10) NULL`) to the `SearchAuditEntries` table via Liquibase DDL. Existing rows get NULL (interpreted as "CO" by convention). New rows always populate `State`.

**Rationale**: A nullable column avoids a data migration backfill for existing rows. The application code writes "CO" or "WY" for all new entries. Historical entries with NULL are understood to be CO-only (pre-multi-state era). The rollback drops the column.

**Changelog**: `2.0.2603.0-add-state-column-to-search-audit.sql`

## R8: Existing CO Scraper Browser Pool Sharing

**Decision**: The WY scraper will inject `PlaywrightBusinessEntityScraper` and call its `EnsureBrowserAsync()` method to share the browser pool, following the same pattern used by `PlaywrightEntityDetailScraper`.

**Rationale**: `PlaywrightEntityDetailScraper` already demonstrates this pattern — it injects `PlaywrightBusinessEntityScraper` and calls `EnsureBrowserAsync()` and `ExecuteWithDiagnosticsAsync()`. The WY scraper follows the identical approach: shared browser lifecycle, per-request browser context isolation, shared diagnostics infrastructure.

**Key code path**:
```
WY Scraper → PlaywrightBusinessEntityScraper.EnsureBrowserAsync()
           → browser.NewContextAsync(IgnoreHTTPSErrors: true)
           → PlaywrightBusinessEntityScraper.ExecuteWithDiagnosticsAsync(context, page, ...)
```
