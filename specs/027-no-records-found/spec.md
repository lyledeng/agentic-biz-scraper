# Feature Specification: Show No Records Found in Both UI and API

**Feature Branch**: `027-no-records-found`  
**Created**: 2026-04-03  
**Status**: Draft  
**Input**: User description: "Show No Records Found in both UI and API"

## Clarifications

### Session 2026-04-03

- Q: What exact wording should the "no records found" message use? → A: `No records found for "{searchTerm}" in {stateName}` using the full display label from STATE_DISPLAY_LABEL (e.g., "No records found for "Acme" in Colorado (US)")
- Q: Should `resultCount` be omitted or set to null for non-search requests? → A: Omit the field entirely from the JSON response
- Q: What visual treatment should the "no records found" message use? → A: Informational notification (blue/neutral tone), visually distinct from the initial dashed-border prompt and warning/error banners
- Q: When `truncated: true` and zero results after mapping, show "no records" or truncation warning? → A: Show a truncation warning instead ("Search may have incomplete results. Please try again or refine your search.")

## User Scenarios & Testing *(mandatory)*

### User Story 1 - No Records Found Message in UI (Priority: P1)

A user performs a business entity search that returns zero results. Instead of seeing the initial prompt message ("Enter a business name above and click Search to find registered entities"), the user sees a clear, distinct "No records found" message so they know the search executed successfully but yielded no matches.

**Why this priority**: This is the most visible gap — users currently cannot distinguish between "I haven't searched yet" and "my search returned nothing," leading to confusion and repeated searches.

**Independent Test**: Search for a nonexistent business name (e.g., "ZZZZNONEXISTENT99999") in any state and verify the UI displays a "No records found" message rather than the initial prompt.

**Acceptance Scenarios**:

1. **Given** the user is on the search page and has not yet searched, **When** the page loads, **Then** the initial prompt message is displayed ("Enter a business name above and click Search to find registered entities").
2. **Given** the user has entered a search term, **When** the search completes with zero results, **Then** the message `No records found for "{searchTerm}" in {stateName}` is displayed using the full display label (e.g., "No records found for "Acme" in Colorado (US)").
3. **Given** the user previously saw the "No records found" message, **When** the user performs a new search that returns results, **Then** the results table is displayed and the "No records found" message is hidden.
4. **Given** the user previously saw search results, **When** they perform a new search that returns zero results, **Then** the "No records found" message replaces the previous results table.

---

### User Story 2 - No Records Indicator in API Response (Priority: P2)

An API consumer receives a clear signal in the response when a business search returns no matching records. The response includes a `resultCount` field so callers can programmatically distinguish between "search returned empty" and "search returned data" without inspecting the data array.

**Why this priority**: API consumers (including the UI) benefit from an explicit count field, but the UI can already derive this from an empty array. This is an ergonomic improvement for external consumers.

**Independent Test**: Call the `/api/v2/execute-script` endpoint with a search term that matches no entities and verify the response body includes `resultCount: 0` alongside the empty `data` array.

**Acceptance Scenarios**:

1. **Given** an API consumer sends a business search request, **When** no matching entities are found, **Then** the response includes `"resultCount": 0` and `"data": []` with HTTP status 200.
2. **Given** an API consumer sends a business search request, **When** matching entities are found, **Then** the response includes `"resultCount": N` (where N is the count of results) and `"data": [...]` with HTTP status 200.
3. **Given** an API consumer sends a non-search request (e.g., entity-details), **When** the request completes, **Then** the `resultCount` field is not present in the JSON response body.

---

### Edge Cases

- What happens when the search returns results but all are filtered out by the mapper (e.g., unrecognized state slug)? The system treats this as zero results and displays "No records found."
- What happens when the API returns `data: null` instead of an empty array? The UI treats null data the same as an empty array and shows the "No records found" message.
- What happens on a network error or timeout? The error message takes precedence — "No records found" does not display alongside an error.
- What happens when `truncated: true` but zero results are produced by the mapper? The UI shows a truncation warning ("Search may have incomplete results. Please try again or refine your search.") instead of "No records found," since the empty result may be due to incomplete data extraction rather than a genuine absence of matches.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: After a search completes with zero results, the UI MUST display the "no records found" message using an informational notification (blue/neutral tone), visually distinct from both the initial dashed-border prompt and warning/error banners.
- **FR-002**: The UI MUST distinguish between four states: (a) initial state (no search performed), (b) search returned no results, (c) search returned results, and (d) search returned truncated/incomplete results with zero mapped records (shows truncation warning instead of "no records found").
- **FR-003**: The API response for business search requests MUST include a `resultCount` integer field indicating the number of matching records returned.
- **FR-004**: The `resultCount` field MUST be omitted entirely from the JSON response for non-search requests (e.g., entity-details, certificate-of-good-standing). The key must not appear in the serialized output.
- **FR-005**: The "No records found" UI state MUST display the message `No records found for "{searchTerm}" in {stateName}` using the full state display label from `STATE_DISPLAY_LABEL` (e.g., "Colorado (US)", not "CO").

### Key Entities

- **ExecuteScriptResponse**: Extended with an optional `resultCount` field (integer, nullable). Present only for business-search definitions.
- **Search Component UI State**: Gains a new "searched with no results" state, tracked separately from the initial "not yet searched" state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can distinguish between "no search performed" and "search returned no results" on every search attempt.
- **SC-002**: The "No records found" message appears within the same response time as a successful search (no added latency).
- **SC-003**: API consumers can determine the result count from the response without parsing the data array.
- **SC-004**: All existing search flows (CO, WY, IA, DE) correctly report zero results when no entities match.

## Assumptions

- The existing search response shape (`ExecuteScriptResponse`) can be extended with an optional field without breaking existing API consumers (additive change).
- The "No records found" message uses existing UI styling conventions — no new design system components are needed.
- The initial empty state prompt remains unchanged for users who have not yet performed a search.
- The `resultCount` field is derived from the post-mapper result array length, not from the raw scraping output.
