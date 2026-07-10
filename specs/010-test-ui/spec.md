# Feature Specification: Test UI for BizScraper API

**Feature Branch**: `010-test-ui`  
**Created**: 2026-03-26  
**Status**: Draft  
**Input**: User description: "I want a test User Interface to test these capabilities — Initial search page, where the user enters the search term and the state. Search performs the search and displays the results in a grid. Selecting one item in the grid performs a detail search and displays the documents available. Clicking on the document pulls the pdf from the storage account."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Search for Business Entities (Priority: P1)

A tester opens the test UI in a browser and is presented with a search form. They enter a business name (e.g., "Wendy") and select a state (CO or WY) from a dropdown, then click Search. The system calls the BizScraper API and displays matching results in a data grid showing entity name, identifier, status, entity type, formation date, and state.

**Why this priority**: This is the entry point for all testing — without search, no other capability can be exercised.

**Independent Test**: Open the UI, enter "Wendy" with state "WY", click Search. A grid appears with matching business entities. Each row shows the entity name, identifier, status, type, and formation date.

**Acceptance Scenarios**:

1. **Given** the test UI is loaded, **When** the user enters "Wendy" as the search term and selects "WY", **Then** a grid displays matching WY business entities with name, identifier, status, entity type, formation date, and state columns.
2. **Given** the search form is displayed, **When** the user clicks Search with an empty search term, **Then** the UI displays a validation message indicating the search term is required.
3. **Given** a search has been performed, **When** results are loading, **Then** a loading indicator is visible until results are displayed.
4. **Given** the API returns zero results, **When** the grid renders, **Then** the UI displays a "No results found" message.

---

### User Story 2 — View Entity Details (Priority: P2)

After searching, the tester clicks on a row in the results grid. The system calls the entity details API using the selected entity's details URL and state. The UI navigates to or displays a detail view showing entity information (name, status, formation date, ID number, jurisdiction, standing, registered agent), a certificate of good standing section (if available), and a list of history documents.

**Why this priority**: Viewing details is the primary use case after finding an entity — it validates the entity details endpoint and document retrieval pipeline.

**Independent Test**: Perform a search for "Wendy" in WY, click on "Wendy's Technology, LLC" in the grid. The detail view shows entity information, registered agent details, certificate status, and a list of history documents with file names.

**Acceptance Scenarios**:

1. **Given** search results are displayed, **When** the user clicks on an entity row, **Then** the detail view shows entity details including name, status, formation date, ID number, form, jurisdiction, and standing fields.
2. **Given** the detail view is displayed for an entity with a registered agent, **When** the page renders, **Then** the registered agent name and address are shown.
3. **Given** the detail view is displayed for an entity with a certificate of good standing, **When** the certificate section renders, **Then** it shows the certificate as available with a download link.
4. **Given** the detail view is displayed, **When** the entity has history documents, **Then** each document is listed with its event title, date, and a clickable file name.
5. **Given** the entity details API is loading, **When** the user waits, **Then** a loading indicator is displayed until the detail view renders.

---

### User Story 3 — Download Documents (Priority: P3)

From the entity detail view, the tester clicks on a history document file name or the certificate of good standing link. The browser opens or downloads the PDF directly from the cloud storage URL.

**Why this priority**: Document access is the final step in the testing workflow. It verifies that the storage URLs returned by the API are accessible and contain valid PDFs.

**Independent Test**: From the detail view for "Wendy's Technology, LLC" (WY), click on "2025-original-annual-report-11347360.pdf". The browser opens or downloads the PDF file.

**Acceptance Scenarios**:

1. **Given** the detail view shows history documents, **When** the user clicks a document file name, **Then** the PDF opens in a new browser tab or downloads.
2. **Given** the detail view shows a certificate of good standing as available, **When** the user clicks the certificate link, **Then** the certificate PDF opens in a new browser tab or downloads.
3. **Given** a document has an error instead of a storage URL, **When** the document row renders, **Then** the error message is displayed instead of a download link.

---

### User Story 4 — Navigation Between Search and Details (Priority: P4)

The tester can navigate back from the detail view to the search results without losing the previous search state. They can also perform a new search at any time.

**Why this priority**: Smooth navigation supports rapid testing of multiple entities within a single session.

**Independent Test**: Perform a search, click into entity details, click "Back to Results" or browser back. The previous search results are still displayed.

**Acceptance Scenarios**:

1. **Given** the user is viewing entity details, **When** they click a "Back to Results" button, **Then** the previous search results grid is displayed.
2. **Given** the user is viewing entity details, **When** they click "New Search", **Then** the search form is displayed with empty fields.

---

### Edge Cases

- What happens when the API is unreachable? The UI displays a connection error message.
- What happens when the entity details API returns a very long list of history documents? The document list scrolls within the detail view.
- What happens when a storage URL returns a 404? The browser shows a standard error; the UI does not crash.
- What happens when the user searches while a previous search is still in progress? The previous request is superseded by the new one.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The UI MUST provide a text input field for the business name search term.
- **FR-002**: The UI MUST provide a selector for choosing the state (CO or WY).
- **FR-003**: The UI MUST call `GET /api/v1/business-search?name={term}&state={state}` when the user submits a search.
- **FR-004**: The UI MUST display search results in a tabular grid with columns: Name, Identifier, Status, Entity Type, Formation Date, State.
- **FR-005**: The UI MUST allow the user to click on a search result row to view entity details.
- **FR-006**: The UI MUST call `GET /api/v1/entity-details?detailsUrl={url}&state={state}` when the user selects an entity.
- **FR-007**: The UI MUST display entity details including name, status, formation date, ID number, form, jurisdiction, standing fields, registered agent, and certificate status.
- **FR-008**: The UI MUST display history documents as a list with event title, date, and a clickable file name linking to the storage URL.
- **FR-009**: The UI MUST open document PDFs in a new browser tab when the user clicks a document link.
- **FR-010**: The UI MUST display a certificate of good standing section when available, with a download link to the certificate PDF.
- **FR-011**: The UI MUST be a separate application from the BizScraper API.
- **FR-012**: The UI MUST display loading indicators during API calls.
- **FR-013**: The UI MUST display user-friendly error messages when API calls fail.
- **FR-014**: The UI MUST validate that the search term is not empty before submitting.
- **FR-015**: The UI MUST allow navigation back from entity details to search results.
- **FR-016**: The UI MUST provide a configurable API base URL field so the tester can point it at any environment (local, dev, staging).

### Key Entities

- **Search Form**: The entry point — captures search term and state selection.
- **Search Result**: A business entity row in the results grid — displays name, identifier, status, type, formation date, state, and links to the entity's details URL.
- **Entity Detail View**: Displays comprehensive entity information, registered agent, certificate, and history documents.
- **History Document**: A downloadable item — has event title, date, storage URL, file name, and possible error state.
- **Certificate of Good Standing**: An optional downloadable PDF — has availability status, storage URL, and file name.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A tester can search for business entities by name and state and view results within 5 seconds of page load.
- **SC-002**: A tester can navigate from search results to entity details and see the full detail view including documents.
- **SC-003**: A tester can open any listed document PDF directly from the detail view.
- **SC-004**: The UI supports both CO and WY states without requiring code changes to switch.
- **SC-005**: The UI loads and functions correctly in modern browsers (Edge, Chrome, Firefox).
- **SC-006**: All API error responses are surfaced as readable messages, not raw JSON or stack traces.

## Clarifications

### Session 2026-03-26

- Q: Should the test UI be served from the existing BizScraper API application or be a separate app? → A: The test UI is a separate application.
- Q: How should the UI discover the BizScraper API base URL? → A: Configurable API base URL field in the UI (tester enters it).
- Q: How should the separate UI app be hosted and structured (single HTML, Node.js, .NET)? → A: Deferred to planning phase.

## Assumptions

- The test UI is an internal/developer tool, not a production-facing application. Visual polish is secondary to functionality.
- The UI is a separate application from the BizScraper API.
- The BizScraper API must be configured to allow cross-origin requests from the test UI.
- Users have network access to the Azure Blob Storage URLs returned by the API for document downloads.
- The UI does not require authentication — it relies on the same access controls as the API itself.
- The UI uses vanilla HTML, CSS, and JavaScript — no frontend framework or build toolchain is required for a test UI.
