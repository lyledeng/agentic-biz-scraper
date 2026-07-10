# Research: CO SOS Business Entity Search API

**Date**: 2026-03-24
**Feature**: 001-cosos-business-search-api

## R1: Azure Playwright Workspaces — Remote Browser Connection

**Decision**: Connect to Azure Playwright workspace via `BrowserType.ConnectAsync(wsEndpoint)` with Bearer token in headers.

**Rationale**: Azure Playwright workspaces provide managed remote browsers. The .NET Playwright client supports WebSocket connection with custom headers for authentication. This avoids managing local browser binaries in the container while still supporting `IgnoreHTTPSErrors` on the `BrowserContext` level.

**Alternatives considered**:
- **Local Playwright browsers in container**: Rejected — larger image size, more complex container setup, no benefit for this use case since Azure Playwright workspaces are already provisioned.
- **Direct HTTP scraping with Flurl**: Rejected — CO SOS site uses Struts-based server-rendered forms with POST submissions. A headless browser is required to reliably interact with the form workflow.

**Key findings**:
- `ConnectAsync` accepts `BrowserTypeConnectOptions.Headers` for auth: `["Authorization"] = $"Bearer {token}"`
- `IgnoreHTTPSErrors` is set on `BrowserNewContextOptions`, not on ConnectAsync
- Connection lifecycle: reuse `IBrowser` instance, create/dispose `IBrowserContext` per request
- Handle `browser.Disconnected` event for reconnection resilience
- WSS endpoint format: `wss://eastus.api.playwright.microsoft.com/playwrightworkspaces/{id}/browsers?os=linux&runId={guid}&api-version=2023-10-01-preview`

## R2: Playwright .NET — Form Submission & Table Scraping

**Decision**: Use Playwright Locator API for form interaction and `EvaluateAsync` for bulk table extraction.

**Rationale**: The Locator API provides auto-waiting and retry semantics for element interactions. Bulk `EvaluateAsync` for table extraction is more efficient than iterating individual locators (single JS evaluation vs N+1 round trips).

**Alternatives considered**:
- **Individual locator iteration per row**: Rejected — too many round trips for large result sets; `EvaluateAsync` extracts all rows in one call.
- **Direct DOM parsing with AngleSharp**: Rejected — would require fetching raw HTML separately; Playwright already has the page loaded.

**Key findings — CO SOS site specifics** (confirmed via live Playwright MCP exploration):
- Search form: `form[name='BusinessEntityCriteriaForm']`, field `input[name='searchName']`, submit `input[name='cmd'][value='Search']`
- Results table headers: `#`, `ID Number`, `Document Number`, `Name`, `Event`, `Status`, `Form`, `Formation Date`
- Detail links on ID Number column: `BusinessEntityDetail.do?...masterFileId={id}&entityId2={id}...`
- Pagination indicator: `"Found N matching record(s). Viewing page X of Y."`
- Pagination navigation: links with page numbers; detect "Viewing page X of Y" to know when to stop
- Error message: `"Error - Exceeded Record Count"` displayed inline when search is too broad
- The "Document Number" column (3rd column) may be empty — confirmed by live scraping

**Table extraction pattern**:
```csharp
// Single JS evaluation to extract all rows from the innermost result table
var results = await page.EvaluateAsync<List<BusinessEntityResult>>(@"
    () => {
        const table = document.querySelector('table with headers');
        // find the table containing th elements with expected headers
        // extract td cells per row, including href from ID Number column
    }
");
```

## R3: Litebus CQRS Pattern

**Decision**: Use Litebus `IQuery<TResult>` / `IQueryHandler<TQuery, TResult>` for the search query; `IQueryMediator` for dispatch.

**Rationale**: Litebus is MIT-licensed, lightweight, supports .NET DI natively, and provides assembly-based handler registration. It fits the vertical slice architecture without adding unnecessary complexity.

**Alternatives considered**:
- **MediatR**: Rejected by project constraint — closed/dual-commercial licensing model.
- **Manual dispatch**: Rejected — reinventing the wheel; Litebus provides clean separation with minimal overhead.

**Key findings**:
- NuGet: `LiteBus` (MIT license) — confirmed compatible with project licensing policy
- Registration: `builder.Services.AddLiteBus(cfg => cfg.AddQueryModule(m => m.RegisterFromAssembly(...)))`
- Query: `public sealed record SearchBusinessEntitiesQuery(string SearchTerm) : IQuery<SearchResponse>`
- Handler: implements `IQueryHandler<SearchBusinessEntitiesQuery, SearchResponse>`
- Dispatch: `IQueryMediator.QueryAsync(query, cancellationToken)`

## R4: Sequential Request Processing

**Decision**: Use `SemaphoreSlim(1, 1)` with non-blocking `WaitAsync(0)` to serialize requests; return `503 Service Unavailable` with `Retry-After` header when busy.

**Rationale**: `SemaphoreSlim` is the simplest concurrency primitive for single-request serialization. Non-blocking check (`WaitAsync(0)`) immediately rejects concurrent requests rather than queueing them, which aligns with FR-012 (reject with "busy" error).

**Alternatives considered**:
- **Channel<T> queue**: Rejected — queuing contradicts FR-012 which requires rejection, not queueing.
- **ASP.NET Core rate limiting middleware**: Rejected — too coarse; rate limiting is per-time-window, not per-concurrent-request. `SemaphoreSlim` precisely models "one at a time".
- **Custom middleware**: Rejected — the semaphore can live in the query handler or a service; no need for middleware-level complexity.

**Key findings**:
- Register as singleton: `SemaphoreSlim` must be shared across requests
- Non-blocking pattern: `if (!await _semaphore.WaitAsync(0))` returns immediately when occupied
- Return `Results.Problem(statusCode: 503)` with `Retry-After: 30` header
- Integrate into the Litebus query handler or wrap at the endpoint level
- Use `try/finally` to ensure release even on exceptions

## R5: CO SOS Site Behaviour Summary (from live exploration)

Confirmed via Playwright MCP browser:

| Behaviour | Details |
|-----------|---------|
| Search entry URL | `https://www.sos.state.co.us/biz/BusinessEntityCriteriaExt.do` |
| Results URL | Same URL (POST back to self) |
| Form method | POST |
| Search field | `input[name='searchName']` (id: `searchCriteria`) |
| Submit button | `input[name='cmd'][type='submit'][value='Search']` |
| Result count text | `"Found N matching record(s). Viewing page X of Y."` |
| Table columns | `#`, `ID Number`, `Document Number`, `Name`, `Event`, `Status`, `Form`, `Formation Date` |
| Detail URL pattern | `BusinessEntityDetail.do?quitButtonDestination=BusinessEntityResults&nameTyp=ENT&masterFileId={id}&entityId2={id}&fileId=&srchTyp=ENTITY` |
| Too-broad error | `"Error - Exceeded Record Count, please refine search"` |
| Empty Document Number | Confirmed — some rows have empty 3rd column |
| TLS | Site has cert issues through corporate proxy; requires `IgnoreHTTPSErrors = true` |
