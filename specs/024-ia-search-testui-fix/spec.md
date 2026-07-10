# Feature Specification: Fix Iowa Search and Test UI

**Feature Branch**: `024-ia-search-testui-fix`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "IA is not working, Can you fix the search and also the testui?"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iowa Business Search Returns Results (Priority: P1)

As an API consumer, I want Iowa business search (`us-ia-business-search`) to successfully navigate the Iowa Secretary of State site, submit a search term, extract result rows, and return normalized results so that I can query Iowa entities through the same unified v2 execute-script API used for other states.

**Why this priority**: The Iowa search is currently non-functional. Until the search definition actions are complete and the extraction logic works end-to-end, no Iowa data can be retrieved — blocking all downstream functionality including drill-down and test UI display.

**Independent Test**: Can be fully tested by invoking `POST /api/v2/execute-script` with `{definition: "us-ia-business-search", parameters: {searchTerm: "Acme"}}` and verifying a non-empty normalized result set is returned with identifier, name, status, entityType, formationDate, uniqueKey, and state fields.

**Acceptance Scenarios**:

1. **Given** the API is running and Iowa SOS site is reachable, **When** a consumer invokes execute-script with definition `us-ia-business-search` and parameters `{searchTerm: "Acme"}`, **Then** the system returns a normalized response containing at least one Iowa business entity result with populated identifier, name, status, and uniqueKey fields.
2. **Given** an Iowa search returning multiple pages, **When** the search is executed, **Then** it paginates through available result pages and aggregates all entities into a single response without duplicates or missing rows.
3. **Given** an Iowa search returning zero results, **When** the search is executed, **Then** it returns an empty result list with totalCount of zero and no error.
4. **Given** the Iowa SOS site is unreachable or returns an error, **When** the search is attempted, **Then** the system returns a meaningful error response with diagnostic information including screenshots captured at the point of failure.

---

### User Story 2 - Iowa Entity Details Drill-Down Returns Full Profile (Priority: P1)

As an API consumer, I want Iowa entity detail drill-down (`us-ia-entity-details`) to accept a `uniqueKey` from search results, navigate to the corresponding entity page, extract the full profile, and return it in the unified detail format so that I can retrieve comprehensive Iowa entity information.

**Why this priority**: Without working drill-down, search results are incomplete — users cannot access agent details, addresses, filing dates, or status information that are essential for business decisions.

**Independent Test**: Can be verified by executing an Iowa search, taking the `uniqueKey` from a result, and invoking `POST /api/v2/execute-script` with `{definition: "us-ia-entity-details", parameters: {uniqueKey: "<key>"}}` to confirm the full entity detail is returned.

**Acceptance Scenarios**:

1. **Given** a valid Iowa `uniqueKey` from search results, **When** execute-script is invoked with definition `us-ia-entity-details` and `parameters.uniqueKey`, **Then** the system returns a full entity profile with identifier, legal name, status, entity type, filing/effective/expiration dates, registered agent, principal office, names list, and certificate note.
2. **Given** an Iowa entity detail with a `uniqueKey` that uses the `detailsUrl` parameter internally, **When** the detail definition processes the request, **Then** it correctly decodes the uniqueKey to derive the navigation URL and retrieves the correct entity's details.
3. **Given** an invalid or expired `uniqueKey`, **When** entity details are requested, **Then** the system returns a clear error indicating the entity could not be found, with diagnostic screenshots.

---

### User Story 3 - Test UI Supports Iowa Search and Drill-Down (Priority: P1)

As a QA user or developer, I want the test UI to correctly execute Iowa searches and display results, and to drill down into Iowa entity details, so that I can visually verify search and detail functionality without using direct API calls.

**Why this priority**: The test UI is the primary verification and troubleshooting surface. If it cannot execute or display Iowa operations correctly, it undermines the ability to validate fixes and demonstrate working functionality.

**Independent Test**: Can be verified by selecting "Iowa (US)" in the test UI state dropdown, entering a search term, confirming results appear in the grid, and clicking a result to view its detail page with all Iowa-specific fields rendered.

**Acceptance Scenarios**:

1. **Given** a user selects Iowa (IA) in the test UI and enters a search term, **When** the search is submitted, **Then** the test UI sends the correct canonical payload (`definition: "us-ia-business-search"`, `parameters: {searchTerm: ...}`) and displays normalized search rows in the result grid.
2. **Given** Iowa search results are displayed in the test UI, **When** a user clicks on a result row, **Then** the test UI navigates to the entity detail view and displays the full Iowa entity profile including registered agent, principal office, names list, and certificate note.
3. **Given** the Iowa search returns an error (400, 503, or network failure), **When** the test UI receives the error, **Then** it displays an actionable error message including retry guidance for 503 responses.
4. **Given** the Iowa entity detail response includes Iowa-specific fields not present for other states, **When** the detail view renders, **Then** it displays Iowa-specific fields (e.g., chapter/code, modified flag, certificate note) without layout errors or missing sections.

---

### Edge Cases

- What happens when the Iowa SOS page has changed its HTML structure since the scraping script was authored? The system should capture a diagnostic screenshot at the failure point and return an error with enough information for rapid troubleshooting.
- What happens when the Iowa entity details definition receives a `uniqueKey`? The handler pre-processes it by decoding and injecting `detailsUrl` before the definition runs — this is the established pattern across all states and is not a bug.
- What happens when the Iowa search encounters a CAPTCHA or JavaScript challenge page? The system should detect the unexpected page state, capture a screenshot, and return an error rather than silently returning empty results.
- What happens when the test UI receives an Iowa detail response with null sections (e.g., no registered agent, no names list)? The test UI should gracefully hide those sections instead of showing empty or broken UI elements.
- What happens when TLS certificate errors occur when connecting to the Iowa SOS site in local development? The system should tolerate TLS errors in local mode to avoid blocking development and testing.

## Clarifications

### Session 2026-04-01

- Q: The spec assumes `detailsUrl` in the entity details definition is a bug (should be `uniqueKey`). However, the handler already decodes `uniqueKey` and injects decoded params including `detailsUrl` before the definition runs. The actual root cause is that both Iowa definitions are stubs (single screenshot action). The `specs/022-iowa-entity-search/contracts/` folder has complete definitions. What is the fix strategy? → A: Adopt 022 contract definitions as source; validate selectors against live site; deploy to definitions folder. No parameter rename needed — `detailsUrl` in the definition is correct by design.
- Q: If live-site validation reveals that CSS selectors in the 022 contract definitions are stale or the Iowa SOS site has changed its HTML structure, what should the fix approach be? → A: Use Playwright MCP for live discovery, update selectors in the definitions to match current site structure, and add multiple fallback selectors per action for resilience (matching the CO pattern with confidence-ranked selectors).
- Q: The test UI's `UnifiedEntityDetailResponse` model maps Iowa details through existing fields (`DetailSection`, `AgentSection`, `CertificateSection`, `PartyEntry[]`). Iowa-specific data like `chapterCode` is mapped to `jurisdiction`, `certificateNote` to `CertificateSection.error`, and Iowa names list to `PartyEntry[]`. Should the test UI accept these mappings or add explicit Iowa fields? → A: Extend `DetailSection` and `UnifiedEntityDetailResponse` models with explicit Iowa fields (`chapterCode`, `certificateNote`, `namesList` with type/status/modified) so the UI can render them with proper labels and structure rather than overloading existing generic fields.
- Q: Since the TypeScript models are being extended with explicit Iowa fields, should the C# `UnifiedEntityDetailResponse` / `DetailSection` models also get explicit Iowa fields, or only the TypeScript side? → A: Extend the C# model too — add nullable Iowa-specific fields to `DetailSection` and `UnifiedEntityDetailResponse` (chapterCode, certificateNote, namesList) so the API returns them explicitly and both sides stay consistent. Update `MapIaDetails` to populate the new fields instead of overloading jurisdiction/CertificateSection.error/PartyEntry.
- Q: Should this fix feature (024) include updating `specs/current-specs/spec.md` and `specs/current-specs/data-model.md` to reflect the Iowa model extensions (new explicit fields on DetailSection/UnifiedEntityDetailResponse)? → A: Yes — include current-specs updates as part of this feature so the global specification snapshot stays in sync with the new Iowa fields and model changes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Iowa search definition (`us-ia-business-search`) MUST contain complete actions to navigate to the Iowa SOS search page, enter the search term, submit the form, wait for results, and extract result rows — not just a placeholder screenshot action.
- **FR-002**: The deployed Iowa definitions (`us-ia-business-search.json` and `us-ia-entity-details.json`) MUST be replaced with the complete v2-format definitions authored in `specs/024-ia-search-testui-fix/contracts/`. The original 022 contracts used action types (`extract-table`, `decode`, `assemble-object`) incompatible with the v2 engine and were rewritten from scratch using only supported v2 action types (`navigate`, `fill`, `click`, `wait-for-condition`, `extract`, `screenshot`, `loop`). The `detailsUrl` parameter name in the entity details definition is correct — the handler decodes `uniqueKey` and injects `detailsUrl` before definition execution.
- **FR-003**: The selectors and action sequences in the v2 definitions MUST be validated against the live Iowa SOS site. This validation was performed during definition authoring using native Playwright MCP (`@playwright/mcp`), confirming that all 022-era CSS ID selectors are stale (site redesigned) and replacing them with attribute-based and text-content-based selectors. Multiple fallback selectors with confidence rankings MUST be included per action (matching the CO definition pattern).
- **FR-004**: Iowa search execution MUST extract the following fields from each result row: business number (identifier), name, status, entity type, legal name, formation date, and a drill-down URL encoded as `uniqueKey`.
- **FR-005**: Iowa search results MUST be returned in the same normalized schema as CO and WY, including canonical envelope fields (`definition`, `correlationId`, `truncated`, `data`) and per-result fields (`identifier`, `name`, `status`, `entityType`, `formationDate`, `uniqueKey`, `state`).
- **FR-006**: Iowa entity detail responses MUST include explicit fields for: identifier, legal name, status, entity type, state of incorporation, filing date, effective date, expiration date, chapter/code, names list (each with name, type, status, modified flag), registered agent (name and address), principal office (name and address), and certificate note. These MUST be returned as dedicated nullable properties on the C# `DetailSection` / `UnifiedEntityDetailResponse` models (not overloaded into existing generic fields like `jurisdiction` or `CertificateSection.error`). The `EntityDetailMapper.MapIaDetails` MUST be updated to populate the new explicit fields.
- **FR-007**: Iowa search MUST handle multi-page results by paginating through available pages, aggregating all entities into a single response.
- **FR-008**: Diagnostic screenshots and tracing MUST be captured at key milestones during Iowa search and detail operations: page load, search submission, result extraction, pagination, detail page load, and any error state.
- **FR-009**: The test UI MUST correctly invoke Iowa search and detail operations through the execute-script service using canonical request fields.
- **FR-010**: The test UI entity detail view MUST render Iowa-specific fields using explicit model properties — `DetailSection` and `UnifiedEntityDetailResponse` MUST be extended with Iowa-specific fields (`chapterCode`, `certificateNote`, and a structured names list with type/status/modified flag) rather than overloading existing generic fields. The detail view MUST render these fields with proper Iowa-specific labels and gracefully handle null or missing sections.
- **FR-011**: The test UI MUST display actionable error messages for Iowa operation failures, including retry guidance when a 503 Retry-After response is received.
- **FR-012**: The system MUST tolerate TLS certificate errors when connecting to the Iowa SOS site in local development mode.
- **FR-013**: The `specs/current-specs/spec.md` and `specs/current-specs/data-model.md` MUST be updated to reflect the new Iowa-specific fields added to `DetailSection` and `UnifiedEntityDetailResponse`, keeping the global specification snapshot in sync with this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Iowa business search returns correct, non-empty results for common search terms (e.g., "Acme", "Farm") in under 30 seconds.
- **SC-002**: Iowa entity detail drill-down from search results returns the full entity profile for 100% of valid uniqueKeys from a preceding search.
- **SC-003**: Test UI successfully displays Iowa search results and allows drill-down to entity details without errors for any valid search scenario.
- **SC-004**: Zero regression in existing state searches (CO, WY, DE) after Iowa fixes are applied.
- **SC-005**: All Iowa error scenarios (site unreachable, no results, invalid uniqueKey) produce user-friendly error messages in both API responses and test UI display.

## Assumptions

- The Iowa Secretary of State search site (https://sos.iowa.gov/search/business/Search.aspx) is accessible and its current HTML structure is stable enough for scraping.
- The existing v2 execute-script infrastructure and search result mapper (`MapIaResults`) are functionally correct. The entity detail mapper (`MapIaDetails`) needs updating to populate the new explicit Iowa fields instead of overloading generic fields. The primary issue is that the deployed definition JSON files are stubs with only a placeholder screenshot action, not the complete definitions authored in `specs/022-iowa-entity-search/contracts/`.
- The test UI already has the IA state wired into the state dropdown and slug maps; fixes are limited to ensuring the end-to-end flow works correctly with the complete API definitions deployed.
- The existing `UniqueKeyEncoder` correctly encodes/decodes Iowa keys with prefix `US-IA`. The handler correctly decodes `uniqueKey` and injects `detailsUrl` before the definition runs — the `detailsUrl` param name in the entity details definition is correct by design.
- Concurrency limits (1 concurrent IA request, 503 Retry-After) are already enforced at the handler level and do not need changes in this fix.
