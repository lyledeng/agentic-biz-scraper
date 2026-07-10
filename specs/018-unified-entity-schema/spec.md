# Feature Specification: Unified Entity Schema

**Feature Branch**: `018-unified-entity-schema`  
**Created**: 2026-03-29  
**Status**: Draft  
**Input**: User description: "Standardize the shapes of business entity summary and details between CO, WY, and DE. Unify identifier fields, replace detailsUrl with a uniqueKey, remove deprecated APIs, standardize Germany document download into execute-script, and update the test UI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Unified Search Results Shape (Priority: P1)

A consumer of the `/api/v2/execute-script` endpoint performs a business search against any supported state (CO, WY, DE) and receives results in a single, consistent schema. Today, CO and WY share most fields but DE returns a different shape (`companyName`, `registeredOffice`, `registrationId` nested under `data.results`). After this feature, every search definition returns `data` as a flat array of objects with the same field names.

**Why this priority**: A common shape across all states is the foundation for every downstream consumer — the test UI, integrations, and any future state additions. Without this, every consumer must maintain per-state parsing logic.

**Independent Test**: Execute `us-co-business-search`, `us-wy-business-search`, and `de-de-business-search` via `/api/v2/execute-script` and verify that each result row contains the same set of top-level field names and types.

**Acceptance Scenarios**:

1. **Given** a consumer sends a `us-co-business-search` request, **When** results are returned, **Then** each result object contains at minimum: `name`, `identifier`, `status`, `entityType`, `formationDate`, `state`, `event`, `uniqueKey`.
2. **Given** a consumer sends a `us-wy-business-search` request, **When** results are returned, **Then** each result object contains the same base fields as CO, plus optional WY-specific fields (`standingTax`, `standingRA`).
3. **Given** a consumer sends a `de-de-business-search` request, **When** results are returned, **Then** each result object uses the same field names (`name` instead of `companyName`, `identifier` instead of `registrationId`, `status`, `state` = `"DE"`) and `data` is a flat array (not nested under `data.results`).
4. **Given** any search result, **When** the consumer inspects the `uniqueKey` field, **Then** it contains an opaque, state-specific token encoding all parameters needed to request entity details for that row (replacing the former `detailsUrl`).
5. **Given** any search result, **When** the consumer inspects the result, **Then** no `detailsUrl` field is present.

---

### User Story 2 - Unified Entity Details Shape (Priority: P1)

A consumer requests entity details for any state by passing the `uniqueKey` from a search result into the appropriate entity-details definition. The response shape is consistent across CO, WY, and (future) DE, with state-specific sections present only when applicable.

**Why this priority**: Entity details are the primary value output — standardizing the shape is essential for the test UI and downstream consumers to render details from any state without branching logic.

**Independent Test**: Call `us-co-entity-details`, `us-wy-entity-details`, and `de-de-entity-details` via `/api/v2/execute-script` using a `uniqueKey` from their respective search results and verify all return the same top-level sections.

**Acceptance Scenarios**:

1. **Given** a consumer sends a `us-co-entity-details` request with a `uniqueKey`, **When** details are returned, **Then** the response contains sections: `details`, `registeredAgent`, `certificate`, `parties`, `documents` — with `parties` and `documents` being `null` for CO.
2. **Given** a consumer sends a `us-wy-entity-details` request with a `uniqueKey`, **When** details are returned, **Then** the response contains the same top-level sections as CO, with `parties` and `documents` populated and `certificate` populated where applicable.
3. **Given** a consumer sends a `de-de-entity-details` request with a `uniqueKey`, **When** details are returned, **Then** the response contains the same top-level sections, with `documents` populated (original + translated hardcopy PDFs) and `registeredAgent`, `certificate`, `parties` as `null`.
4. **Given** any entity detail response, **When** inspecting the `details` section, **Then** it contains at minimum: `name`, `identifier`, `status`, `formationDate`, `entityType`, `jurisdiction`, `principalAddress`, `mailingAddress`.
5. **Given** any entity detail response with documents, **When** inspecting each document, **Then** it contains: `title`, `date`, `downloads` — where `downloads` is an array of download references (each with `label`, `proxyUrl`, `fileName`).

---

### User Story 3 - Germany Entity Details with Document Downloads (Priority: P2)

A consumer retrieves German entity details (including the hardcopy printout / Aktuelle Drucke) by calling the `de-de-entity-details` definition through the execute-script endpoint, instead of the current standalone `POST /api/v1/germany-search/current-hardcopy-printout`. The response follows the same unified entity-details shape as CO and WY, with the `documents` section populated with the original German PDF and FAB-translated English PDF (uploaded to cloud storage and returned as PDF proxy references). Sections not available for DE (`registeredAgent`, `certificate`, `parties`) are returned as `null`.

**Why this priority**: This aligns the DE document workflow with the unified entity-details model established in Story 2. By modeling DE documents as entity details rather than a separate concept, every state follows the same pattern: search → entity details (with documents).

**Independent Test**: Call `de-de-entity-details` via `/api/v2/execute-script` with the appropriate `uniqueKey` and verify the response conforms to the standard entity-details shape with `documents` populated.

**Acceptance Scenarios**:

1. **Given** a consumer sends a `de-de-entity-details` request with the entity's `uniqueKey` (encoding search term + registration ID), **When** the system completes, **Then** the response follows the standard entity-details shape with `details`, `registeredAgent`, `certificate`, `parties`, `documents` sections.
2. **Given** a successful `de-de-entity-details` response, **When** the consumer inspects the `documents` section, **Then** it contains an entry with a `downloads` array including both the original PDF (label: "Original") and the translated PDF (label: "Translated"), each with a `proxyUrl` and `fileName`.
3. **Given** a `de-de-entity-details` response, **When** the consumer follows a `proxyUrl`, **Then** the PDF is served through the existing `/api/v1/documents/{blobPath}` proxy endpoint.
4. **Given** a `de-de-entity-details` request fails during scraping, **When** the error is returned, **Then** the response follows the standard execute-script error envelope (502, etc.).
5. **Given** a `de-de-entity-details` response, **When** the consumer inspects `registeredAgent`, `certificate`, and `parties`, **Then** each is `null`.

---

### User Story 4 - Deprecated API Removal (Priority: P2)

The three deprecated endpoints (`GET /api/v1/business-search`, `GET /api/v1/entity-details`, `GET /api/v1/germany-search`) are fully removed from the codebase. All consumers now use the `/api/v2/execute-script` endpoint exclusively.

**Why this priority**: Removing deprecated code reduces maintenance burden and API surface area. The generic execute-script endpoint has been available since feature 016 and all functionality is accessible through it.

**Independent Test**: Verify the deprecated endpoints return 404 (or are absent from routing). Verify all scraping definitions remain functional through execute-script.

**Acceptance Scenarios**:

1. **Given** the deprecated endpoints are removed, **When** a consumer sends a request to `GET /api/v1/business-search`, **Then** the system returns a 404 Not Found.
2. **Given** the deprecated endpoints are removed, **When** a consumer sends a request to `GET /api/v1/entity-details`, **Then** the system returns a 404 Not Found.
3. **Given** the deprecated endpoints are removed, **When** a consumer sends a request to `GET /api/v1/germany-search`, **Then** the system returns a 404 Not Found.
4. **Given** the deprecated `POST /api/v1/germany-search/current-hardcopy-printout` is replaced by the `de-de-entity-details` definition, **When** a consumer calls the old endpoint, **Then** the system returns a 404 Not Found.
5. **Given** all deprecated endpoints are removed, **When** a consumer lists definitions via `GET /api/v2/definitions`, **Then** all definitions are available under the new naming convention: `us-co-business-search`, `us-wy-business-search`, `de-de-business-search`, `us-co-entity-details`, `us-wy-entity-details`, `de-de-entity-details`.
6. **Given** the v1 execute-script and definitions endpoints, **When** a consumer calls `POST /api/v1/execute-script` or `GET /api/v1/definitions`, **Then** the system returns `410 Gone`.

---

### User Story 5 - Test UI Updates (Priority: P3)

The test UI is updated to consume the unified search result shape, use the `uniqueKey` for entity detail navigation, display documents in a consistent pattern across states, and support the Germany document download workflow.

**Why this priority**: The test UI is the primary internal tool for verifying scraping output. It must reflect all schema changes to remain useful, but is dependent on Stories 1–3 being completed first.

**Independent Test**: Open the test UI, perform a search for each state, view entity details, and verify the UI renders correctly with the unified shapes and document downloads.

**Acceptance Scenarios**:

1. **Given** a user searches for a business in any state, **When** results are displayed, **Then** the results grid shows columns consistent across all states (Name, Identifier, Status, Entity Type, Formation Date, State).
2. **Given** a user clicks on a search result, **When** the entity details are loaded, **Then** the UI uses the `uniqueKey` (not `detailsUrl`) to request details.
3. **Given** a user views entity details with documents (WY history documents or DE document downloads), **When** the documents section is rendered, **Then** each document shows its title, date, and download links.
4. **Given** a user selects a DE search result and views entity details, **When** the `de-de-entity-details` response is rendered, **Then** the user sees the documents section with links for both the original and translated PDFs.
5. **Given** a user views DE search results, **When** the results grid is rendered, **Then** DE results appear in the same column layout as CO and WY (no `registeredOffice` column — mapped into the unified schema).

---

### Edge Cases

- What happens when a `uniqueKey` from one state is accidentally passed to a different state's entity-details definition? The system should return a clear validation error.
- What happens when the DE translation service is unavailable during a `de-de-entity-details`? The system MUST return a partial success: the original German PDF download is returned normally, and the translated download entry has `proxyUrl: null` with an `error` field explaining the failure.
- How does the system handle DE search results that have no document available for download? The `de-de-entity-details` should return an appropriate error indicating no document was found.
- What happens to existing stored `detailsUrl` references in external systems? Consumers must migrate to use `uniqueKey` — this is a breaking change to the search response shape.
- What happens when a definition returns fields not present in the unified schema (e.g., WY `standingTax`)? Extra state-specific fields are allowed as optional/nullable alongside the required base fields.

## Requirements *(mandatory)*

### Functional Requirements

#### Definition Naming Convention

- **FR-000**: All definition slugs MUST follow the `{country}-{state}-{action}` naming convention: `us-co-business-search`, `us-co-entity-details`, `us-wy-business-search`, `us-wy-entity-details`, `de-de-business-search`, `de-de-entity-details`. Former slugs (e.g., `co-business-search`) MUST be immediately retired with no alias period — this is a breaking change.

#### Search Result Standardization

- **FR-001**: System MUST return all business search results as a flat array under `data` in the execute-script response envelope — DE results MUST NOT be nested under `data.results`.
- **FR-002**: Every search result object MUST contain the following base fields: `name`, `identifier`, `status`, `entityType`, `formationDate`, `state`, `event`, `uniqueKey`.
- **FR-003**: The `identifier` field MUST be the canonical entity identifier for that state: the SOS ID for CO, the Filing ID for WY, and the court registration ID (e.g., "Amtsgericht München HRB 55558") for DE.
- **FR-004**: The `uniqueKey` field MUST be a self-contained, opaque string that encodes all parameters needed to perform a standalone entity detail or document lookup for that specific result row — the consumer MUST NOT need to pass additional context (e.g., search term or state) alongside it. For CO this includes the full SOS URL parameters, for WY the eFNum, and for DE the search term + registration ID. It replaces the former `detailsUrl`.
- **FR-005**: The `detailsUrl` field MUST be removed from all search result shapes.
- **FR-006**: DE search results MUST map existing fields to the unified names: `companyName` → `name`, `registrationId` → `identifier`, `registeredOffice` → `registeredOffice` (preserved as an optional field for display value).
- **FR-007**: State-specific optional fields (WY: `standingTax`, `standingRA`; DE: `registeredOffice`) MAY be included alongside the base fields but MUST be nullable.

#### Entity Details Standardization

- **FR-008**: All entity detail definitions MUST return a response with the same top-level sections: `details`, `registeredAgent`, `certificate`, `parties`, `documents`.
- **FR-009**: Each section that is not applicable for a given state MUST be `null` (e.g., CO returns `parties: null`, `documents: null`).
- **FR-010**: The `details` section MUST contain at minimum: `name`, `identifier`, `status`, `formationDate`, `entityType`, `jurisdiction`, `principalAddress`, `mailingAddress`.
- **FR-011**: The `details.identifier` field MUST use the same value and source as the corresponding search result `identifier`.
- **FR-012**: The `documents` section (when populated) MUST be an array of document objects, each containing: `title` (string), `date` (string, MM/DD/YYYY or null), `downloads` (array of download references). For WY, each existing `historyDocument` maps 1:1 to a `DocumentEntry`: `eventTitle` → `title`, `date` → `date`, and a single download with `label: "Filing Document"`, `proxyUrl` derived from the blob path, `fileName` preserved.
- **FR-013**: Each download reference MUST contain: `label` (string, e.g., "Original", "Translated", "Filing Document"), `proxyUrl` (string or null, relative path to the PDF proxy endpoint — null when the download failed), `fileName` (string), `error` (string or null, describes failure when `proxyUrl` is null).

#### Germany Entity Details Integration

- **FR-014**: A new `de-de-entity-details` definition MUST be created for the execute-script endpoint to replace the standalone hardcopy endpoint.
- **FR-015**: The `de-de-entity-details` definition MUST accept the entity's `uniqueKey` as its parameter (encoding search term and registration ID).
- **FR-016**: The `de-de-entity-details` execution MUST scrape the original German PDF and the FAB-translated English PDF, upload both to cloud storage (using existing blob infrastructure), and return download references via the PDF proxy pattern. If the FAB translation service fails, the response MUST still succeed with the original PDF returned normally and the translated download entry containing `proxyUrl: null` and an `error` message describing the failure.
- **FR-017**: The `de-de-entity-details` response MUST conform to the unified entity-details shape (FR-008) with `documents` populated per FR-012/FR-013, and `registeredAgent`, `certificate`, `parties` set to `null`.
- **FR-017a**: The `de-de-entity-details` `details` section MUST be populated with the entity's basic information from the search result: `name`, `identifier` (registration ID), `status`, and other available fields.

#### Deprecated API Removal

- **FR-018**: The `GET /api/v1/business-search` endpoint MUST be removed from the codebase.
- **FR-019**: The `GET /api/v1/entity-details` endpoint MUST be removed from the codebase.
- **FR-020**: The `GET /api/v1/germany-search` endpoint MUST be removed from the codebase.
- **FR-021**: The `POST /api/v1/germany-search/current-hardcopy-printout` endpoint MUST be removed from the codebase after `de-de-entity-details` is available.
- **FR-022**: The unified `execute-script` and `definitions` endpoints MUST be served under `/api/v2` (`POST /api/v2/execute-script`, `GET /api/v2/definitions`). The v1 routes for these endpoints (`/api/v1/execute-script`, `/api/v1/definitions`) MUST return `410 Gone` to signal consumers to migrate. The PDF proxy endpoint remains at `/api/v1/documents/{blobPath}` (no breaking change to its contract).

#### Test UI Updates

- **FR-023**: The test UI MUST be updated to use `uniqueKey` instead of `detailsUrl` when requesting entity details.
- **FR-024**: The test UI results grid MUST display a consistent set of columns across all states.
- **FR-025**: The test UI entity detail view MUST render a unified documents section that displays download links for any document type (WY filings, DE hardcopy).
- **FR-026**: The test UI MUST support viewing DE entity details (triggering `de-de-entity-details` via the execute-script endpoint) from a DE search result, using the same entity-details view as CO and WY.
- **FR-027**: The test UI MUST remove any references to the deprecated endpoints.

### Key Entities

- **SearchResult**: A single row from a business search. Unified shape across all states with: `name`, `identifier`, `status`, `entityType`, `formationDate`, `state`, `event`, `uniqueKey`, plus optional state-specific fields.
- **UniqueKey**: An opaque, state-specific token that encodes all parameters required to look up entity details or documents for a specific search result. Replaces `detailsUrl`.
- **EntityDetailResponse**: The top-level response for entity detail lookups. Contains five nullable sections: `details`, `registeredAgent`, `certificate`, `parties`, `documents`.
- **DocumentEntry**: A single document associated with an entity. Contains `title`, `date`, and a list of download references.
- **DownloadReference**: A single downloadable file within a document entry. Contains `label`, `proxyUrl` (path for the PDF proxy endpoint), and `fileName`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All three search definitions (`us-co-business-search`, `us-wy-business-search`, `de-de-business-search`) return results conforming to a single schema — validated by a single consumer parsing function with zero per-state branching.
- **SC-002**: All entity detail definitions return responses with the same five top-level sections — consumers can render any state's details using a single display template.
- **SC-003**: Germany entity details (including document downloads) are accessible through the `de-de-entity-details` definition via the execute-script endpoint, following the same entity-details shape as CO and WY, with no standalone hardcopy endpoint required.
- **SC-004**: The deprecated endpoints (business-search, entity-details, germany-search, hardcopy-printout) are fully removed and v1 execute-script/definitions return 410 Gone — the API surface is reduced to three v2 routes (`/api/v2/execute-script`, `/api/v2/definitions`) plus the PDF proxy (`/api/v1/documents/{blobPath}`).
- **SC-005**: The test UI successfully searches, views details, and downloads documents across all supported states (CO, WY, DE) without state-specific UI logic for basic display.
- **SC-006**: Existing end-to-end tests pass or are updated to reflect the new shapes with no regressions in scraping functionality.

## Clarifications

### Session 2026-03-29

- Q: Should DE document download be modeled as a separate definition or as entity details? → A: Model as `de-de-entity-details` — same entity-details shape with `documents` populated, other sections null.
- Q: What naming convention should definition slugs follow? → A: Country-state-action format: `us-co-business-search`, `us-co-entity-details`, `us-wy-business-search`, `us-wy-entity-details`, `de-de-business-search`, `de-de-entity-details`.
- Q: Should old definition slugs remain as aliases during transition or be immediately removed? → A: Immediate rename — old slugs are removed, only new `country-state-action` slugs exist (breaking change).
- Q: Should `uniqueKey` be self-contained or minimal (requiring extra context)? → A: Self-contained — encodes all parameters needed for a standalone detail/document lookup with no extra context required.
- Q: What happens when FAB translation fails during `de-de-entity-details`? → A: Partial success — return original PDF normally; translated entry has `proxyUrl: null` with an `error` message.
- Q: How should WY `historyDocuments` map into the unified `documents` shape? → A: 1:1 mapping — each history document becomes one `DocumentEntry` with one download (label: "Filing Document").
- Q: Should breaking changes be applied in-place on `/api/v1` or under a new version? → A: New `/api/v2` prefix — all modified endpoints move to v2; v1 routes return 410 Gone.
- Q: Confirm DE document download is modeled as entity-details and all Germany-specific APIs removed? → A: Confirmed — DE doc download is `de-de-entity-details` (entity-details shape with `documents` populated). All standalone Germany endpoints (`/api/v1/germany-search`, `/api/v1/germany-search/current-hardcopy-printout`) are removed entirely.

## Assumptions

- The `uniqueKey` encoding strategy is an implementation concern — the spec only requires it to be opaque to consumers and decodable by the server. A reasonable default is a base64-encoded JSON payload or a URL-safe token.
- Breaking changes are delivered under `/api/v2`. The v1 execute-script and definitions endpoints return `410 Gone`. The PDF proxy endpoint (`/api/v1/documents/{blobPath}`) has no breaking changes and remains at v1.
- The `de-de-entity-details` definition covers the document download (hardcopy printout) workflow. Full Handelsregister company page scraping for additional detail fields beyond documents is out of scope — the `details` section will be populated with basic entity info available from the search result.
- The `registeredOffice` field from DE search results will be preserved as an optional field on the unified search result shape since it has display value, but it is not a required base field.
- Existing blob storage infrastructure (from feature 003) is reused for DE document uploads — no new storage provisioning is needed.
- The FAB translation service integration (from feature 015) is reused as-is for the `de-de-entity-details` definition.
- The test UI uses the existing Angular 17 + Jumpstart PDS stack (from feature 010) — no framework changes.
- Error responses continue to follow the existing RFC 7807 Problem Details format used throughout the API.
- The `formationDate` field on DE search results will be `null` since the Handelsregister does not expose formation dates in search results.
- The `event` field on DE search results will be `null` since the Handelsregister does not expose event information in search results.
