# Research: WY Certificate of Good Standing Fix

**Feature**: 020-wy-cert-good-standing-fix  
**Date**: 2026-03-31

---

## R1: WY Certificate Flow — Missing `element-exists` Condition

**Decision**: Add `element-exists` condition on action-017 (download) to check for `#MainContent_ContinueButton` before attempting download.

**Rationale**: The CO entity details definition (`us-co-entity-details.json`) uses an `element-exists` condition on its download action (action-004). The `ConditionEvaluator` already supports this condition type. The `ScrapingFlowEngine.ExecuteActionsAsync` already evaluates conditions before each action — if the condition returns false, the action is skipped via `continue`. This is the established pattern.

**Alternatives considered**:
- Relying on `DownloadActionHandler`'s built-in "link not found" fallback (returns silently when `link.CountAsync() == 0`): This works but provides no distinction between "element not found" and "explicitly skipped due to ineligibility." Using `condition` is more explicit and enables the engine to log `ActionSkipped`.

**Codebase evidence**:
- `ConditionEvaluator.cs` — `ElementExistsAsync()` method checks `context.Page.Locator(selector).CountAsync() > 0`
- `ScrapingFlowEngine.cs` lines 118-122 — condition evaluation and skip logging
- `us-co-entity-details.json` action-004 — `"condition": { "type": "element-exists", "selector": "a:has-text('certificate of good standing')" }`

---

## R2: Ineligibility Alert Detection

**Decision**: Add a `check-text` action after the certificate search click (action-016) with `onMatch: "skip-group"` behavior. Since `check-text` currently only supports `onMatch: "throw"`, use an `extract` action to detect the alert text and store an eligibility flag, combined with a `variable-truthy` condition on the download action.

**Rationale**: The WY SOS site displays *"Certificates may only be printed when the entity Standing is 'Good'"* when the entity is not eligible. The ConditionEvaluator supports `variable-truthy` and `variable-falsy` conditions. We can extract a boolean eligibility flag from the page and use it as the download condition.

**Alternatives considered**:
- Adding a new `onMatch: "skip-group"` mode to `check-text`: This would require engine changes (violates Open/Closed). The extract + condition approach uses existing engine capabilities.
- Using `element-exists` condition alone: This catches the missing Print button but doesn't capture the ineligibility reason for the API response.

**Final approach**: Use a combined condition:
1. Add extract action after search click to detect the alert text and store as `certificateEligible` variable
2. Add `element-exists` condition on the download action for `#MainContent_ContinueButton`
3. The `JsonDrivenEntityDetailScraper` already handles certificate flow errors gracefully (try/catch with 15s timeout)

---

## R3: Duplicate Order Values (action-009 and action-009b)

**Decision**: Renumber action-009b to a distinct order value that executes after the main details extraction.

**Rationale**: Both `action-009` (detail extraction) and `action-009b` (idNumber extraction) have `"order": 9`. The `ScrapingFlowEngine` sorts by `Order` then by original array index, so in practice they execute in array order. However, this is fragile and relies on implementation details. Sequential, distinct order values are the established convention.

**Alternatives considered**:
- Leave as-is: Would work in current engine implementation, but violates the explicit ordering contract.

---

## R4: V1 Entity-Details Endpoint Status

**Decision**: No action needed — v1 entity-details endpoint was already removed in spec 018.

**Rationale**: Codebase search confirms:
- No `MapEntityDetailsEndpoints` call in `Program.cs`
- No `EntityDetailsEndpoints.cs` file exists
- No `/api/v1/entity-details` route registration exists
- The `Features/EntityDetails/` folder contains only shared Models/, Logging/, Metrics/, Exceptions/ used by the v2 `JsonDrivenEntityDetailScraper`
- Spec 018 tasks T032-T034 (delete EntityDetails endpoints/handlers) are all marked `[x]` done

**Alternatives considered**: N/A — already resolved.

---

## R5: V1 Business-Search Endpoint Removal

**Decision**: Remove `BusinessSearchEndpoints.cs` and its `MapBusinessSearchEndpoints()` call from `Program.cs`. Retain all other BusinessSearch subfolders (Models/, Exceptions/, Logging/, Metrics/).

**Rationale**: The v1 `/api/v1/business-search` endpoint is the only remaining v1 endpoint with a live route (besides the 410 Gone tombstones for v1 execute-script and v1 definitions). Spec 018 FR-018 required its removal. The BusinessSearch Models, Exceptions, Logging, and Metrics namespaces are extensively shared with the v2 execute-script flow:
- `ScrapingException` → used by `ExecuteScriptHandler`, `GlobalExceptionHandler`, `JsonDrivenEntityDetailScraper`
- `ServiceBusyException` → used by `GlobalExceptionHandler`
- `BusinessSearchLogMessages` → used by `PlaywrightBrowserManager`, `AzureBlobStorageClient`
- `NormalizedSearchResult` → used by `SearchResultMapper`
- `IStateSearchScraper` → used by DI registrations for search scrapers

Only the endpoint file is v1-specific.

**Test migration required**:
- `tests/BizScraper.IntegrationTests/Features/BusinessSearch/BusinessSearchEndpointTests.cs` → v2
- `tests/BizScraper.IntegrationTests/Features/BusinessSearch/MultiStateEndpointTests.cs` → v2
- `tests/BizScraper.IntegrationTests/Features/BusinessSearch/ErrorResponseContractTests.cs` → v2
- `tests/BizScraper.IntegrationTests/Features/BusinessSearch/OpenApiContractTests.cs` → v2
- `tests/k6/business-search.js` → v2

---

## R6: Certificate Error Reporting in Response

**Decision**: Use the existing `CertificateResult` record (`Available`, `StorageUrl`, `FileName`, `Error`) to report eligibility status. When the entity is not eligible, set `Available = false` and `Error = "Entity is not in good standing. Certificates may only be printed when the entity Standing is 'Good'."`.

**Rationale**: The `JsonDrivenEntityDetailScraper.MapToResult()` method already constructs `EntityDetailScrapeResult` from action context variables. When `certificateLocalPath` is null/empty, `certificateAvailable` is set to `false`. The `EntityDetailMapper` in the v2 flow translates this to the response. Adding an explicit error message requires storing a `certificateError` variable in the action context, which the mapper can pick up.

**Alternatives considered**:
- Adding a new `CertificateEligibility` enum: Over-engineering for a string error message.

---

## R7: WY Certificate Mapping Missing in EntityDetailMapper (NEW FINDING)

**Decision**: Add certificate mapping to `EntityDetailMapper.MapWyDetails()` and add `certificateError` to the JSON definition's output compose block.

**Rationale**: Code review of `EntityDetailMapper.cs` revealed that `MapWyDetails()` never reads the `certificateUrl` property from the composed output. The CO mapper (`MapCoDetails`) correctly maps `certificateUrl` to a `CertificateSection`, but the WY mapper was originally written without it. This means **even if the JSON definition download succeeded, the v2 API response would still return `certificate: null` for WY**. This is a second bug on top of the download condition issue.

**Impact**: The mapper fix is mandatory for the certificate to appear in v2 responses. Without it, fixing the JSON definition alone would be insufficient — the download would succeed, the file would be uploaded to storage, but the API consumer would never receive the URL.

**Implementation**: Mirror the CO mapper pattern — read `certificateUrl` and `certificateError` from the output, construct `CertificateSection` accordingly.

**Alternatives considered**: None — this is a straightforward bug fix required for functional correctness.
