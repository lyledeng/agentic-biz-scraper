# Research: Germany Business Entity Search (Handelsregister)

**Feature**: 013-germany-business-search  
**Date**: 2026-03-27

## R1: Handelsregister Site Structure & Form Mechanics

**Task**: Research the German Handelsregister Normal Search page to understand form fields, submission mechanism, and result structure.

**Decision**: The site uses JSF 2.x with PrimeFaces UI components. Forms are submitted via standard POST with `javax.faces.ViewState` tokens.

**Rationale**: Direct Playwright exploration of `https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml` confirmed the following:

- **Search field**: `<textarea name="form:schlagwoerter" id="form:schlagwoerter">` — Company or keywords input
- **Submit button**: `<button type="submit" id="form:btnSuche" name="form:btnSuche">Find</button>`
- **Radio options** for search mode — default is "contain all keywords" (`form:schlagwortOptionen:0`, value `1`)
- **Cookie consent**: May show an "Okay" button on first visit; must be dismissed
- **Results URL**: `https://www.handelsregister.de/rp_web/sucheErgebnisse/welcome.xhtml?cid=N`

**Alternatives considered**: Direct HTTP POST (bypassing Playwright) — rejected because the site relies on JSF ViewState tokens, server-side sessions with `jsessionid` cookies, and PrimeFaces AJAX for pagination; stateless HTTP calls would break the flow.

---

## R2: Results Table DOM Structure

**Task**: Understand the HTML structure of search results for data extraction.

**Decision**: Results are rendered as PrimeFaces `ui-panelgrid` tables (one per result), not a single `<table>`. Each result row is a 3-row panelgrid.

**Rationale**: Playwright DOM inspection showed:

- **Result container**: Each result is a `<table class="ui-panelgrid ui-widget">` with an ID pattern `ergebnissForm:selectedSuchErgebnisFormTable:N:j_idt213`
- **Row 1** (header): State, Court, Register Type, Register Number (class `fontTableNameSize`)
- **Row 2** (data): CompanyName (class `paddingBottom20Px`, colspan 5), RegisteredOffice (class `sitzSuchErgebnisse`, colspan 1), Status (colspan 1), Document links (colspan 2)
- **Row 3** (history): Optional "History" sub-rows (class `borderBottom3`)
- **Branches**: Optional nested table with class `displayInlineTable` — to be excluded per spec assumptions

**Extraction strategy**: Use JavaScript to iterate all `table.ui-panelgrid.ui-widget` elements on the page, find the second row (`.ui-panelgrid-odd`), and extract cells by position/class.

---

## R3: AJAX Pagination Mechanics

**Task**: Determine how pagination works on the results page, since Playwright's `waitForNavigation` won't work if the page uses AJAX.

**Decision**: The Handelsregister uses PrimeFaces AJAX partial page updates for pagination. The URL does NOT change between pages. The scraping flow must wait for DOM content refresh after clicking a paginator link.

**Rationale**: Playwright observation showed:
- **Paginator**: `<div class="ui-paginator">` with `<a class="ui-paginator-page">` links numbered 1–10
- **Active page**: `ui-state-active` class on current page
- **Disabled nav**: First/Prev links have `ui-state-disabled` class when on page 1; Next/Last disabled on last page
- **Page indicator**: `<span class="ui-paginator-current">1-10 of 100 records</span>`
- **AJAX behavior**: Clicking a page number triggers PrimeFaces AJAX (partial page update) — only the results table refreshes; the URL stays as `sucheErgebnisse/welcome.xhtml?cid=N`

**Wait strategy**: After clicking a paginator link, wait for the paginator-current text to update (e.g., from "1-10 of 100" to "11-20 of 100") using `wait-for-condition` with a JavaScript check. This is more reliable than `networkidle` for AJAX because multiple async requests may fire.

**Alternatives considered**: 
- `waitForNavigation` — rejected, URL doesn't change
- `networkidle` — rejected as unreliable for partial AJAX updates
- `waitForSelector` on specific result table ID — rejected because IDs change per page load

---

## R4: Warning Detection ("100 hits exceeded")

**Task**: Determine how to detect the exceeded-hits warning message.

**Decision**: Check for a `div.warning.displayBlock` element or text matching "The maximum number of 100 hits has been exceeded".

**Rationale**: Playwright showed the warning is rendered as:
```html
<div class="ui-outputpanel ui-widget warning displayBlock">
  <span class="warningIcon"><i class="fa fa-fw fa-warning"></i></span>
  ×The maximum number of 100 hits has been exceeded. Please limit your request further.
</div>
```

**Implementation**: Use the existing `check-text` action type with `onMatch: "store"` (not `throw`) to capture the warning text into a variable. Unlike the CO exceeded-record-count (which returns no results), the Handelsregister still returns up to 100 results with the warning, so we must NOT throw — we store the warning and continue scraping.

---

## R5: No-Results Detection

**Task**: Determine how to detect when a search returns zero results.

**Decision**: Check the paginator text for "0-0 of 0 records" and/or the presence of "There are no results for this search."

**Rationale**: Playwright showed:
- Paginator: `<span class="ui-paginator-current">0-0 of 0 records</span>`  
- Body text includes: "There are no results for this search."
- Results table is still present but has no data rows

**Implementation**: The extraction JavaScript will return an empty array when no panelgrid results exist. The response naturally shows `totalCount: 0, results: []`.

---

## R6: TLS Certificate Handling

**Task**: Determine the approach for handling TLS certificate errors caused by corporate proxy.

**Decision**: `IgnoreHTTPSErrors = true` in `BrowserNewContextOptions` for dev/local; config-driven flag for environment-specific behavior.

**Rationale**: The existing `JsonDrivenSearchScraper.SearchCoreAsync` already uses `IgnoreHTTPSErrors = true` in the `BrowserNewContextOptions`. The Handelsregister site triggers `ERR_CERT_AUTHORITY_INVALID` behind the corporate proxy. Per spec clarification, this is acceptable in dev/local only.

**Implementation**: Reuse the existing `IgnoreHTTPSErrors` pattern already in `JsonDrivenSearchScraper`. For production, the corporate proxy CA will be in the container trust store, so HTTPS works without ignoring errors and the existing `IgnoreHTTPSErrors = true` default is acceptable since the production cert chain validates correctly.

---

## R7: Audit Trail Schema Extension

**Task**: Determine whether to create a new audit table or extend the existing `SearchAuditEntries` table.

**Decision**: Extend the existing `SearchAuditEntries` table with a `WarningMessage` NVARCHAR(500) NULL column. The existing `State` column will hold "DE" for Germany searches.

**Rationale**: The `SearchAuditEntries` table already has all required fields (SearchTerm, ResultCount, Truncated, PagesScraped, DurationMs, Status, ErrorDetail, CreatedAt, CorrelationId, DiagnosticsUrl, State). The only new field is the exceeded-hits warning message, which is specific to Germany but could apply to other future states.

**Alternatives considered**: New `GermanySearchAuditEntries` table — rejected because schema is nearly identical; a single nullable column addition is simpler and avoids audit query fragmentation.

---

## R8: JSON Flow Definition Design for AJAX Pagination

**Task**: Design the pagination loop in the JSON flow definition to handle PrimeFaces AJAX behavior.

**Decision**: Use a `loop` action with `loopType: "pagination"` and a `wait-for-condition` action inside the loop body that polls for the paginator text to change after clicking a page link.

**Rationale**: The existing engine supports `loop` with `terminateWhen` conditions and `wait-for-condition` with JavaScript. The pattern:

1. Extract current paginator text ("1-10 of 100 records")
2. Click next page link (`a.ui-paginator-page` with appropriate number)
3. `wait-for-condition`: poll until paginator text differs from the saved value
4. Extract results from the refreshed page
5. Terminate when the "Next" link (`a.ui-paginator-next`) has `ui-state-disabled`

**Alternatives considered**: Changing the "Hits per page" dropdown to 100 (showing all at once) — considered but rejected because: (a) it may not eliminate pagination for exactly 100 results, (b) it changes the site's default behavior which could trigger different server-side logic, and (c) the user explicitly requested multi-page scraping.

---

## R9: Endpoint Routing & Scraper Registration

**Task**: Determine how to register the Germany search endpoint and scraper in the DI container.

**Decision**: Create a new `Features/GermanySearch/` vertical slice with its own `GermanySearchEndpoints.cs` (registering `MapGet /api/v1/germany-search`), `SearchGermanyBusinessesHandler.cs`, and associated models. Register the "DE" scraper via the existing keyed `IStateSearchScraper` DI pattern in `Program.cs`.

**Rationale**: The existing `BusinessSearch` feature handles CO/WY via a parameterized `state` query string on a single endpoint. The Germany endpoint is separate (per clarification Q1) and has a different response schema (CompanyName/RegisteredOffice/Status vs. Name/Identifier/Status/EntityType/etc.), so a distinct vertical slice is cleaner.

**Implementation**: 
- `Program.cs`: Register `JsonDrivenSearchScraper` with `stateCode: "DE"` as a keyed service
- New `GermanySearchEndpoints.MapGermanySearchEndpoints()` extension method
- The handler dispatches to `IStateSearchScraper` keyed by "DE", but maps results to `GermanyBusinessResult` (CompanyName, RegisteredOffice, Status) instead of `NormalizedSearchResult`

---

## R10: Test-UI Angular Integration

**Task**: Determine how to add Germany search to the test-ui.

**Decision**: Add a new Germany search tab/section to the existing Angular test-ui, following the same patterns used for CO/WY business search.

**Rationale**: The test-ui already has search components for CO/WY (based on conversation history — search.component.ts with signal-based state, error-banner.component.ts for warnings). A Germany search component will follow the same patterns: input field, search button, results table, warning banner.

**Implementation**: Add a new "Germany" option to the UI (tab, route, or section) that:
- Calls `GET /api/v1/germany-search?name={term}`
- Displays a table with CompanyName, RegisteredOffice, Status columns
- Shows the exceeded-hits warning banner if `warning` field is present in the response
- Shows "no results" empty state when results array is empty
