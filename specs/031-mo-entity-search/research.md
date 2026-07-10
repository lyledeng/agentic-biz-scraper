# Research: Missouri Business Entity Search Integration

**Feature**: 031-mo-entity-search  
**Phase**: 0 — Outline & Research  
**Date**: 2026-04-04

## Research Tasks

### R1: MO SOS Site Navigation & Postback Mechanics

**Decision**: Use Playwright browser automation with ASP.NET Web Forms postback handling.

**Rationale**: The MO SOS site at `https://bsd.sos.mo.gov/BusinessEntity/BESearch.aspx?SearchType=0` is built on ASP.NET Web Forms with Telerik UI components. All form submissions use `__doPostBack()` JavaScript calls with ViewState and EventValidation hidden fields. Search, pagination, and detail-page tab switching all use postback-based navigation — not standard URL-based routing. This requires full browser automation via Playwright; simple HTTP requests cannot replicate the postback lifecycle.

**Alternatives considered**:
- Direct HTTP POST with ViewState extraction: Rejected — fragile, requires maintaining session cookies and parsing encrypted ViewState tokens that change per page load.
- Selenium: Rejected — Playwright is already the project standard and supports Azure Playwright Workspaces.

### R2: Search Page Structure

**Decision**: Search by "Business Name" with "Starting With" match type using the postback SEARCH button.

**Rationale**: Discovered via Playwright MCP browser. The search form has:
- **Search type dropdown**: Business Name (default), Registered Agent, Name Availability, Charter No.
- **Business Name textbox**: Accepts the search term
- **Match type dropdown**: Starting With (default), All Words, Any Word, Exact Match
- **Only Active Corporations checkbox**: Unchecked by default
- **SEARCH button**: Triggers `WebForm_DoPostBackWithOptions()` postback

Results render as an HTML table with columns: Business Name, Charter No., Type, Status, Created, Registered Agent Name. Pagination uses numbered submit buttons at top and bottom (20 results per page, up to 1,051 cap).

Previous/former names appear as separate rows with "(previous name)" suffix, linked to the same entity `ID`.

### R3: Entity Detail Page Structure

**Decision**: Scrape all three tabs (General Information, Filings, Principal Office Address) on the detail page.

**Rationale**: Discovered via Playwright MCP browser. The detail page URL pattern is:
```
https://bsd.sos.mo.gov/BusinessEntity/BusinessEntityDetail.aspx?page=beSearch&ID={entityId}
```

**General Information tab** (default):
- Name, Type, Charter No., Domesticity, Home State
- Registered Agent (name + full address with link to agent detail)
- Status, Date Formed, Duration

**Filings tab** (postback-loaded):
- Table with columns: Action (View Document button), Type (Modify/Creation), Create Filing (document type description), Date Filed, Effective Date
- "Show Registration Reports" checkbox (checked by default)
- Each filing has a "View Document" button that triggers a postback to load/download the PDF

**Principal Office Address tab** (postback-loaded):
- Table with columns: Type (Reg. Office/Mailing), Address, Since, To
- "Show Previous Principal Office Address" checkbox

### R4: Filing PDF Download Pattern

**Decision**: Click each "View Document" button to trigger PDF download, upload to Azure Blob Storage, return proxy URL.

**Rationale**: The "View Document" buttons use `__doPostBack()` to trigger server-side PDF generation. The PDF is delivered as a response to the postback request. This follows the same pattern as WY history document downloads — Playwright intercepts the download event, saves the PDF locally, uploads to Azure Blob Storage under the correlation-ID folder, and returns the proxy URL.

**Per-document error handling**: Each filing document result includes `storageUrl`, `fileName`, and `error` fields. If a single download fails, the error is recorded on that document and processing continues for remaining filings.

### R5: Previous Names Aggregation

**Decision**: Collapse previous-name rows into a `previousNames` string array on the parent entity.

**Rationale**: The MO SOS search results interleave previous/former names as separate table rows. These rows have "(previous name)" suffixed to the name, empty Charter No./Type/Status/Created/Agent cells, and link to the same entity `ID` URL as the parent. The extraction script must:
1. Identify rows with "(previous name)" in the name cell
2. Extract the entity ID from the link URL
3. Group them under the parent entity with matching ID
4. Strip the "(previous name)" suffix before adding to the array

### R6: Truncation Detection

**Decision**: Detect the "More than 1051 records found" warning message and set `truncated: true`.

**Rationale**: When results exceed 1,051, the MO SOS site displays: "More than 1051 records found, only 1051 are shown. Please refine your search criteria." This text appears in a warning table above the search form. The definition should use a `check-text` action to detect this pattern and flag the response as truncated.

### R7: Browser Mode Configuration

**Decision**: Remote (Azure Playwright Workspaces), no anti-bot measures.

**Rationale**: Anti-bot scan performed via Playwright MCP headless Chrome confirmed:
- No CAPTCHA (reCAPTCHA, hCaptcha, Turnstile)
- No Cloudflare challenge pages (`#challenge-form`, `.cf-challenge`)
- No bot-detection SDKs (DataDome, PerimeterX, Distil, Imperva, Akamai, Kasada, Shape)
- Cloudflare Insights beacon present but blocked by site's own CSP — analytics only, not bot management
- Site scripts: jQuery 3.0, Telerik Web UI, Google Analytics, SmartyStreets (address autocomplete)

The `us-mo-business-search.json` and `us-mo-entity-details.json` definitions will set `browser.mode: "remote"` in their JSON files, consistent with CO, WY, and DE.

### R8: UniqueKey Encoding

**Decision**: Base64url-encoded JSON with `{ "state": "MO", "id": "{entityId}" }`.

**Rationale**: The MO SOS detail page URL uses an `ID` query parameter (numeric, e.g., `3021929`). Following the existing uniqueKey convention from CO/WY/IA, the uniqueKey will be a base64url-encoded JSON string containing the state code and entity ID. This allows the `us-mo-entity-details` definition to decode the key and construct the detail page URL.

**Alternatives considered**:
- Raw entity ID: Rejected — inconsistent with multi-state uniqueKey pattern; lacks state context.
- Full URL encoding: Rejected — unnecessarily large; only the ID parameter is needed.
