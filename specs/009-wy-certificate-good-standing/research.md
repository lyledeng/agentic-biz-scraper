# Research: WY Certificate of Good Standing

**Feature**: 009-wy-certificate-good-standing
**Date**: 2026-03-26

## R-001: WY Certificate Page Flow (ViewCertificate.aspx)

### Decision: Multi-step navigate → fill → click → click → download flow

### Research

The WY Certificate of Good Standing page was explored via Playwright MCP server:

1. **URL**: `https://wyobiz.wyo.gov/Business/ViewCertificate.aspx`
2. **Page structure**: Two sections — "Generate" (Filing ID → Search → Print) and "Validate" (Certificate ID → Search)
3. **Generate flow**:
   - Enter Filing ID in textbox (ref `e106`, label "Filing ID", placeholder format `YYYY-NNNNNNNNN`)
   - Click "Search" button (ref `e109`, title "Get a Certificate of Good Standing")
   - **Verification page** shows entity details: Name, Filing ID, Type, Status, Sub Status, Standing-Tax, Standing-RA, Standing-Other, Formed In
   - Click "Print" button (ref `e210`) to trigger PDF download
   - Download event fires with filename `CertOfGoodStanding.pdf`
4. **No CAPTCHA** on this page (unlike the entity details page)

### Rationale

Unlike CO where the certificate is a link on the same details page, WY requires navigating to a separate page. The existing JSON flow engine action types (`navigate`, `fill`, `click`, `download`) support this flow without new action types.

### Alternatives Considered

- **Separate scraper class**: Rejected. The JSON flow engine already supports all needed actions. A separate class would violate DRY and the Open/Closed principle.
- **Playwright APIRequest for certificate**: Rejected. The certificate page is a multi-step form requiring browser interaction (ASP.NET WebForms postbacks), not a simple HTTP GET.

---

## R-002: Integration with Existing WY Entity Details Flow

### Decision: Bundle certificate actions into wy-entity-details.json with a `group` property for independent execution

### Research

The `MapToResult()` method in `JsonDrivenEntityDetailScraper` checks for `certificateLocalPath` in the action context variables:

```csharp
var certificateLocalPath = context.Variables.GetValueOrDefault("certificateLocalPath")?.ToString();
var certificateAvailable = !string.IsNullOrEmpty(certificateLocalPath);
```

The `GetEntityDetailsHandler` then processes this:
- If `CertificateAvailable` is true and `CertificateLocalPath` is not null → upload to blob storage
- Populates `CertificateResult(Available, StorageUrl, FileName, Error)`
- Persists certificate URL to audit trail

The `ScrapingFlowEngine.ExecuteActionsAsync(IReadOnlyList<FlowAction>, ActionContext, CancellationToken)` is `internal`, allowing the scraper to call it directly with a filtered subset of actions. Certificate actions are appended to `wy-entity-details.json` with `"group": "certificate"`. The scraper splits actions by group: main actions (no group) run via `ExecuteAsync`, then certificate-grouped actions run via `ExecuteActionsAsync` with an independent 15-second `CancellationTokenSource` in a try-catch.

**Handler code (`GetEntityDetailsHandler`) requires no changes.** The scraper requires minor orchestration changes to split and execute grouped actions. A `Group` property is added to the `FlowAction` model.

### Rationale

Bundling keeps the entire WY entity details + certificate workflow in one JSON file (per FR-012), while the `group` property enables the scraper to execute certificate actions with an independent timeout (per FR-013). The `certificateLocalPath` variable mechanism is the same as CO — the handler doesn't differentiate between states.

### Alternatives Considered

- **Handler-level WY certificate logic**: Rejected. Would require `if (state == "WY")` branching in the handler, violating the JSON-driven engine architecture (feature 006).
- **Separate JSON flow file (`wy-certificate.json`)**: Considered for independent timeout support. Rejected in favor of bundling with group-based action splitting — keeps the workflow in one file, avoids `FlowDefinitionLoader` registration, and the scraper can call `ExecuteActionsAsync` directly for the certificate group.

---

## R-003: Download Action for Button-Triggered Downloads

### Decision: Use the existing `download` action type with a button selector

### Research

The `DownloadActionHandler` uses `context.Page.Locator(selector).First.ClickAsync()` to trigger the download. This works with any clickable element (links, buttons, divs with click handlers). The WY "Print" button is a standard ASP.NET button that triggers a postback producing the PDF download.

The `linkSelector` property name is a misnomer — it's actually a general-purpose CSS selector. The "Print" button can be selected via `button:has-text('Print')` or by its ASP.NET ID.

### Rationale

No changes to `DownloadActionHandler` needed. The selector `button:has-text('Print')` or `#MainContent_PrintButton` will work.

### Alternatives Considered

- **New `button-download` action type**: Rejected. The existing `download` action already handles button clicks via its selector.

---

## R-004: Certificate Timeout Strategy

### Decision: Group-based action splitting with scoped CancellationToken at the scraper level

### Research

Per spec clarification, the certificate retrieval has its own independent 15-second timeout. Certificate actions are bundled in `wy-entity-details.json` with `"group": "certificate"` (see R-002).

The `ScrapingFlowEngine.ExecuteActionsAsync(IReadOnlyList<FlowAction>, ActionContext, CancellationToken)` is `internal` and accepts any action list. The scraper can:

1. Load the flow definition
2. Split `definition.Actions` into main actions (no group) and certificate actions (`group == "certificate"`)
3. Execute main actions via `ExecuteAsync()` with the request CancellationToken
4. After history document downloads, validate Filing ID format (R-005)
5. Execute certificate actions via `ExecuteActionsAsync(certActions, context, certCts.Token)` with `CancellationTokenSource(TimeSpan.FromSeconds(15))` in a try-catch
6. On any failure (`PlaywrightException`, `TimeoutException`, `OperationCanceledException`, generic `Exception`), `certificateLocalPath` remains null, a structured warning is logged, and entity details are returned normally

### Rationale

This keeps the workflow bundled in one JSON file while providing independent timeout semantics. The scraper orchestrates the split — no engine changes required. The `ExecuteActionsAsync` method already supports executing arbitrary action lists.

### Alternatives Considered

- **Single flow with action-level timeout**: Would require adding timeout support to the flow engine model and all action handlers. Over-engineering for one use case.
- **Inline flow timeout wrapper action**: Adding a new "timeout-group" action type to the flow engine. Too much machinery for this feature; can be added later if needed.
- **Separate JSON flow file (`wy-certificate.json`)**: Simpler timeout isolation, but requires `FlowDefinitionLoader` registration and a second file. The group-based splitting achieves the same result with less moving parts.

---

## R-005: Filing ID Validation

### Decision: Validate with regex pattern `^\d{4}-\d{9}$` before attempting certificate page

### Research

WY Filing IDs follow the format `YYYY-NNNNNNNNN` (4-digit year, hyphen, 9-digit number). Examples:
- `2017-000757675`
- `2018-000815365`

The certificate page's own placeholder text shows `Example: 2000-000123456`.

The validation occurs in the scraper before navigating to the certificate page. Invalid Filing IDs are skipped with a warning log and `certificateLocalPath` remains null.

### Rationale

Prevents wasting a browser navigation + 15-second timeout window on a Filing ID that will definitely be rejected by the WY SOS site.

### Alternatives Considered

- **No validation, let WY SOS reject it**: Viable per Q1 clarification, but wastes time and browser resources for obviously invalid IDs.
- **Alphanumeric check only**: Too loose — would allow IDs like `ABC-123` which the WY SOS site would reject.

---

## R-006: `CertificateResult.Available` Semantics for WY

### Decision: `Available = true` only when the PDF was successfully downloaded

### Research

Per spec clarification Q4, `Available` for WY means the PDF was actually obtained, not just that the entity exists on the certificate page. This differs from CO where `Available` means the link exists on the page.

The `MapToResult()` method already implements this correctly:
```csharp
var certificateAvailable = !string.IsNullOrEmpty(certificateLocalPath);
```

If the download fails or is skipped, `certificateLocalPath` stays null → `Available = false`. If the download succeeds, it's non-null → `Available = true`. No code change needed.

The handler then decides what to put in `CertificateResult`:
- Download succeeded + upload succeeded → `CertificateResult(true, url, filename, null)`
- Download succeeded + upload failed → `CertificateResult(true, null, filename, error.Message)`
- Download failed → no `CertificateResult` created (field stays null, or `Available = false`)

### Rationale

WY has no "link detection" step like CO. The only meaningful signal is whether the PDF was actually obtained.
