# Research: Washington Business Entity Search Integration

**Feature**: 032-wa-entity-search  
**Phase**: 0 — Outline & Research  
**Date**: 2026-04-05

## Research Tasks

### R1: WA CCFS Site Architecture & Navigation

**Decision**: Use Playwright browser automation interacting with the AngularJS SPA UI elements for search and navigation.

**Rationale**: The WA CCFS site at `https://ccfs.sos.wa.gov/#/` is a single-page AngularJS application. The frontend communicates with a REST API at `ccfs-api.prod.sos.wa.gov`. All routing is hash-based (`/#/`, `/#/AdvancedSearch`, `/#/BusinessSearch/{id}`). The search form uses AngularJS data binding — typing in the search input and clicking the search button triggers an API call to `api/BusinessSearch/GetBusinessSearchList`. Results render client-side after the JSON API response. This requires full browser automation via Playwright to interact with the SPA's UI elements, wait for data rendering, and then extract results from the DOM.

**Alternatives considered**:
- Direct REST API calls: Rejected — spec FR-009 explicitly requires browser UI interaction; direct API calls would bypass Cloudflare Turnstile protection and be inconsistent with the project's browser-automation approach.
- Selenium: Rejected — Playwright is the project standard and supports Azure Playwright Workspaces.

### R2: Search Page Structure

**Decision**: Search by business name using the CCFS Corporation Search input field, then extract results from the rendered table.

**Rationale**: Discovered via Playwright MCP browser. The search interface has:
- **Search input**: Text field for entering business name (AngularJS-bound)
- **Search button**: Triggers the `GetBusinessSearchList` API call
- **Results table**: Columns: Business Name, UBI Number, Business Type, Principal Office, Registered Agent, Status
- **Pagination**: Results paginated with 25 per page; numbered page buttons at bottom
- **Navigation**: Clicking a business name navigates to `/#/BusinessSearch/{businessID}` with `showBusineInfo(businessID, businessType)` function

The search input accepts the business name directly. No dropdown selection or match-type option is required.

### R3: Entity Detail Page Structure

**Decision**: Scrape entity detail sections from the single detail page including general info, registered agent, governors/officers, and filing history.

**Rationale**: Discovered via Playwright MCP browser. The detail page URL pattern is:
```
https://ccfs.sos.wa.gov/#/BusinessSearch/{businessID}
```

The detail page has these sections:
- **General Information**: Business Name, UBI Number, Business Type, Status, Principal Office (Street/Mailing), Expiration Date, Jurisdiction, Formation/Registration Date, Period of Duration, Inactive Date, Nature of Business
- **Nonprofit Fields** (when applicable): Charitable Corporation, EIN, Gross Revenue <$500K, Has Members, Public Benefit Designation, Host Home
- **Registered Agent**: Name, Street Address, Mailing Address
- **Governors/Officers**: Table with Title, Governors Type (Individual/Entity), Entity Name, First Name, Last Name
- **Filing History**: Table with Filing Number, Filing Date/Time, Effective Date, Filing Type

All data is loaded via REST API calls:
- General info + registered agent: `api/BusinessSearch/BusinessInformation?businessID={id}`
- Governors: Included in the BusinessInformation response or a separate API call
- Filing history: `api/BusinessSearch/GetBusinessFilingList?IsOnline=true&businessId={id}`

### R4: Filing Document Download Pattern

**Decision**: Use page-level `fetch()` to call `GetTransactionDocumentsList` API for each filing, then download documents via `DownloadOnlineFilesByNumber` endpoint using page-level `fetch()`.

**Rationale**: Per spec clarification Q4, the approach uses page-level JavaScript `fetch()` within the browser context rather than clicking modal dialogs. This:
1. Preserves session cookies (avoids the cookie-sharing issue found with MO's `browserContext.APIRequest.GetAsync()`)
2. Avoids fragile modal dialog interaction
3. Matches the MO filing download pattern established in the `ProcessMoFilingDocumentsAsync` rewrite

**Flow for each filing (up to N=10 most recent)**:
1. Call `api/Common/GetTransactionDocumentsList` via `page.EvaluateAsync()` with `fetch()` to get document list for the filing
2. For each document in the list, call `Common/DownloadOnlineFilesByNumber` via `page.EvaluateAsync()` with `fetch()` to download the file as base64
3. Upload to Azure Blob Storage under `{correlationId}/wa/{businessId}/`
4. Return proxy URL via `storageUrl` field

**Per-document error handling**: Each filing document result includes `storageUrl`, `fileName`, and `error` fields. Individual download failures are recorded per-document without failing the entire request.

**Alternatives considered**:
- Click "Action" button to open modal, then download: Rejected — fragile modal interaction, inconsistent with Q4 clarification
- `browserContext.APIRequest.GetAsync()`: Rejected — doesn't share session cookies with remote browsers (learned from MO fix)

### R5: Pagination Handling

**Decision**: Click numbered page buttons in the SPA pagination controls to navigate through result pages.

**Rationale**: The CCFS search results show 25 results per page with numbered pagination buttons. The scraping definition will:
1. Extract results from the current page
2. Check if a "next page" button exists
3. Click it and wait for the SPA to re-render with new data
4. Repeat until no more pages or maxPages limit reached

The `maxPages` variable defaults to 25 (625 results cap), configurable per definition.

### R6: UniqueKey Encoding

**Decision**: Base64url-encoded JSON with `{ "state": "US-WA", "params": { "businessId": "{businessID}", "businessType": "{businessType}" } }`.

**Rationale**: The WA CCFS detail navigation requires both `businessID` (numeric) and `businessType` (string like "WA PROFIT CORPORATION") to construct the `showBusineInfo(businessID, businessType)` call. Following the existing uniqueKey convention from CO/WY/IA/MO, the uniqueKey will be a base64url-encoded JSON string. Both fields are included because the detail page routing may need the business type for proper rendering.

**Alternatives considered**:
- Business ID only: Rejected — the `showBusineInfo()` function and API endpoint both require businessType.
- Full URL encoding: Rejected — unnecessarily large; only businessID and businessType are needed.

### R7: Browser Mode & Anti-Bot Protection

**Decision**: Remote (Azure Playwright Workspaces) with Cloudflare Turnstile awareness.

**Rationale**: Per spec clarification Q2, remote mode is used consistent with MO. Azure Playwright Workspaces IPs reduce Cloudflare fingerprinting risk. The CCFS site uses Cloudflare Turnstile for bot protection. The system's existing browser stealth configuration (realistic viewport, user agent) should be sufficient to pass Turnstile challenges in most cases. If Turnstile triggers an interactive challenge, the system should detect it and return an appropriate error with diagnostics rather than silently failing.

**Detection strategy**: After navigation, check for Cloudflare challenge elements (e.g., `iframe[src*='challenges.cloudflare.com']`, `#turnstile-wrapper`). If detected, flag as blocked and return error.

### R8: SPA Wait Strategy

**Decision**: Use `wait-for-condition` actions with JavaScript checks for AngularJS rendering completion.

**Rationale**: The CCFS site is an AngularJS SPA that renders data client-side after API calls. Standard `networkidle` waits may not be sufficient because AngularJS digest cycles can complete after network requests. The definition will use `wait-for-condition` actions that check for specific DOM elements indicating data has rendered:
- After search: Wait for result table rows or "no results" message
- After detail navigation: Wait for business name element to appear
- After pagination: Wait for result table to re-render with new data

### R9: No-Records-Found Handling

**Decision**: Detect empty results and return empty data array with `totalCount: 0`.

**Rationale**: When a WA CCFS search returns no results, the SPA displays a message indicating no matching businesses were found. The extraction script should detect this state and return an empty JSON array. The engine's existing `no-records-found` handling (feature 027) will produce the normalized empty response.

### R10: Nonprofit-Specific Fields

**Decision**: Include 6 nonprofit-specific boolean/string fields in entity details when present.

**Rationale**: Per spec clarification Q3, nonprofit fields (Charitable Corporation, EIN, Gross Revenue <$500K, Has Members, Public Benefit Designation, Host Home) are valuable for nonprofit due diligence. These fields appear on the detail page only for nonprofit entities. The extraction script should check for their presence and include them in the output. For non-nonprofit entities, these fields will be null.
