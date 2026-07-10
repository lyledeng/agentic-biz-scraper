# Feature Specification: JSON-Driven Scraping Engine

**Feature Branch**: `006-json-scraping-engine`  
**Created**: 2025-06-22  
**Status**: Draft  
**Input**: User description: "I want to refactor the semantic intent for playwright actions, which is currently encoded in c# code to json files. - /api/v1/business-search CO - /api/v1/business-search WY - /api/v1/entity-details CO. The backend execution engine should be generic and run through these json files for execution."

## Clarifications

### Session 2026-03-25

- Q: Should old hard-coded scraper classes be removed after migration or kept as fallback? → A: Remove old scraper classes after all three flows are migrated and verified.
- Q: Should extraction produce fully structured output via JavaScript only, or support a declared C# post-processing step? → A: Allow JSON definitions to declare a named C# post-processing step for raw extraction output.
- Q: Should JSON definitions include a schema version field for forward compatibility? → A: Yes, include a required schema version field; the engine validates compatibility at startup.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Colorado Business Search via JSON Definition (Priority: P1)

As a system maintainer, I want the Colorado business search scraping flow to be defined in a JSON file so that I can update selectors, URLs, and extraction logic without modifying compiled code.

The Colorado business search currently navigates to a search page, fills in a business name, submits the form, checks for "exceeded record count" errors, extracts results from an 8-column HTML table, and paginates through numbered page links. All of these steps—including the specific selectors, the JavaScript extraction script, and the pagination pattern—should be expressed as a sequence of declarative actions in a JSON file. A generic execution engine reads this JSON file and performs each action in order using the browser automation layer.

**Why this priority**: Colorado search is the simplest of the three flows (no CAPTCHA, no file downloads) and exercises all core action types: navigate, fill, click, wait, extract, check-text, and paginate. Implementing this first proves the engine works end-to-end.

**Independent Test**: Can be fully tested by issuing a Colorado business search request and verifying that the engine loads the JSON definition, executes each action, and returns identical results to the current hard-coded implementation.

**Acceptance Scenarios**:

1. **Given** a JSON scraping definition exists for the Colorado business search flow, **When** a search request is made for a business name, **Then** the engine navigates to the search page, fills the search term, submits the form, extracts results, and returns them in the same format as the current implementation.
2. **Given** the search term returns too many results and the site displays an "Exceeded Record Count" message, **When** the engine processes the check-text action, **Then** the engine raises the appropriate error and stops execution.
3. **Given** the search results span multiple pages, **When** the engine processes the pagination action, **Then** it iterates through pages up to the configured maximum and aggregates all results.
4. **Given** an operator updates a selector value in the JSON file and restarts the service, **When** a search request is made, **Then** the engine uses the updated selector without requiring recompilation.

---

### User Story 2 - Wyoming Business Search via JSON Definition (Priority: P2)

As a system maintainer, I want the Wyoming business search scraping flow—including its CAPTCHA solving sub-flow—to be defined in a JSON file so that CAPTCHA detection selectors, search form selectors, and extraction scripts can be updated independently of compiled code.

The Wyoming flow is more complex than Colorado: it begins with a CAPTCHA gateway (detect challenge, locate image, solve via external service, submit answer, retry on failure up to a maximum number of attempts), followed by filling and submitting a search form (with retry logic), extracting results from an unstructured banded-row layout using link-anchored text parsing, and paginating through server-rendered next-page controls. The JSON definition must support conditional branching (e.g., "if CAPTCHA is presented, execute the CAPTCHA sub-flow") and loops (e.g., "retry CAPTCHA up to N attempts").

**Why this priority**: Wyoming exercises advanced engine capabilities—conditional logic, loops, and external service integration (CAPTCHA solver)—and validates that the engine can handle complex, stateful flows beyond simple linear action sequences.

**Independent Test**: Can be fully tested by issuing a Wyoming business search request and verifying that the engine loads the JSON definition, handles the CAPTCHA challenge, submits the search, extracts results from the banded layout, and returns them in the same format as the current implementation.

**Acceptance Scenarios**:

1. **Given** a JSON definition exists for the Wyoming business search flow, **When** a search request is made, **Then** the engine detects the CAPTCHA challenge, invokes the CAPTCHA solving sub-flow, fills and submits the search form, and extracts results.
2. **Given** the CAPTCHA solver returns a suspicious answer (e.g., only whitespace or single character), **When** the engine evaluates the answer, **Then** it refreshes the CAPTCHA and retries without counting the suspicious answer as a failed attempt.
3. **Given** the CAPTCHA cannot be solved after the maximum number of attempts, **When** the engine exhausts retries, **Then** it raises a CAPTCHA resolution error.
4. **Given** results span multiple pages, **When** the engine processes pagination, **Then** it clicks the next-page control, waits for the page to load, and continues extraction.

---

### User Story 3 - Colorado Entity Details via JSON Definition (Priority: P3)

As a system maintainer, I want the Colorado entity details scraping flow—including table extraction and certificate download—to be defined in a JSON file so that field mappings, table selectors, and download detection can be updated without code changes.

The entity details flow navigates to a detail page URL, extracts structured data from two labeled HTML tables ("Details" and "Registered Agent") where field labels may vary in format, detects whether a "Certificate of Good Standing" download link is available, and conditionally downloads the PDF file. The JSON definition must support conditional actions (e.g., "if certificate link exists, download it") and the download action type.

**Why this priority**: Entity details exercises the download action type and conditional execution, completing the full set of action types needed by the engine. It also validates multi-table extraction with flexible field name mapping.

**Independent Test**: Can be fully tested by providing a known entity details URL and verifying the engine extracts entity details, registered agent information, and downloads the certificate PDF (if available), matching the current implementation's output.

**Acceptance Scenarios**:

1. **Given** a JSON definition exists for the Colorado entity details flow, **When** a details request is made with a valid URL, **Then** the engine navigates to the page, extracts the Details table and Registered Agent table, and returns the structured data.
2. **Given** the entity detail page includes a "Certificate of Good Standing" link, **When** the engine processes the conditional download action, **Then** it downloads the PDF and saves it locally.
3. **Given** the entity detail page does not include a certificate link, **When** the engine evaluates the conditional, **Then** it skips the download action and continues.
4. **Given** the Details table uses alternate field labels (e.g., "Entity Name" instead of "Name"), **When** the engine runs the extraction script from the JSON definition, **Then** it correctly resolves the field value using the label variants defined in the script.

---

### User Story 4 - Shared Diagnostics Integration (Priority: P2)

As a system maintainer, I want the JSON-driven engine to automatically capture screenshots and traces at defined milestones so that I retain the same diagnostic visibility as the current implementation.

Each JSON action definition can optionally specify a screenshot milestone name. The engine captures a screenshot after executing the action if the milestone is specified. Trace capture (start/stop) wraps the entire flow execution, matching the existing diagnostics behavior (configurable tracing modes, cloud upload).

**Why this priority**: Diagnostics are essential for debugging scraping failures in production. Without them, moving to JSON definitions would reduce operational visibility.

**Independent Test**: Can be tested by executing any scraping flow and verifying that screenshot files are created at each milestone and trace files are captured according to the configured tracing mode.

**Acceptance Scenarios**:

1. **Given** a JSON action specifies a screenshot milestone, **When** the engine completes that action, **Then** a screenshot is saved with the milestone name in the diagnostics output directory.
2. **Given** the tracing mode is configured to "always," **When** a scraping flow executes, **Then** a trace archive is captured and optionally uploaded to cloud storage.
3. **Given** an error occurs during flow execution, **When** the engine catches the exception, **Then** it captures a forced error screenshot before propagating the error.

---

### Edge Cases

- What happens when a JSON definition file is missing or cannot be parsed? The engine should fail fast with a clear error message identifying the missing or malformed definition.
- What happens when a selector defined in the JSON file no longer matches any element on the page? The engine should raise a descriptive error including the selector and the action that failed, then capture a diagnostic screenshot.
- What happens when the extraction JavaScript returns null or an empty result set? The engine should treat this as an empty result (not an error) and continue with pagination or flow completion.
- What happens when the page structure changes mid-pagination (e.g., session expires)? The engine should capture a screenshot, log the failure, and return partial results collected so far along with a truncation indicator.
- What happens when a JSON definition references a variable (e.g., search term) that was not provided at runtime? The engine should fail fast with a clear error message identifying the unresolved variable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST load scraping flow definitions from JSON files at application startup for each supported endpoint: Colorado business search, Wyoming business search, and Colorado entity details.
- **FR-002**: System MUST provide a generic execution engine that processes any valid JSON flow definition by interpreting a sequence of declared actions and executing them against the browser automation layer.
- **FR-003**: The execution engine MUST support the following action types: navigate (go to URL), fill (enter text in a form field), click (activate an element), wait-for-load (pause until page reaches a load state), wait-for-condition (pause until a custom condition evaluates to true), extract (run a script and capture structured output), check-text (detect text on page and branch on result), screenshot (capture page state), download (initiate and save a file download), loop (iterate a sub-flow for pagination or retry with termination condition and max iterations), and call-service (invoke a named external service and capture its response).
- **FR-004**: The execution engine MUST support variable substitution in action parameters so that runtime values (e.g., search term, correlation ID, details URL) can be injected into the flow at execution time.
- **FR-005**: The execution engine MUST support conditional actions that execute only when a specified condition is met (e.g., element exists, text is present, previous extraction yielded a value).
- **FR-006**: The execution engine MUST support loop constructs (the "loop" action type) for repeatable sub-flows with two subtypes: retry loops (e.g., CAPTCHA attempts up to N times) and pagination loops (e.g., navigate to next page, extract, repeat until no more pages). For pagination loops, the engine MUST accumulate extraction results across iterations into a single aggregate variable.
- **FR-007**: The execution engine MUST support invoking external services from within a flow (e.g., sending a CAPTCHA image to a solving service and receiving the answer) as a declared action type.
- **FR-008**: Each JSON flow definition MUST declare the output model (the shape of data produced by the flow) so that the engine can return typed results to the calling handler.
- **FR-009**: The execution engine MUST integrate with the existing diagnostics infrastructure—screenshots at declared milestones, trace capture wrapping the full flow, and cloud upload of artifacts—using the same configuration options as today.
- **FR-010**: The execution engine MUST integrate with the existing browser pool and concurrency controls (per-state semaphores, browser reuse limits) without requiring changes to how browsers are managed.
- **FR-011**: The execution engine MUST propagate structured errors (e.g., exceeded record count, CAPTCHA resolution failure, scraping timeout) so that calling code can handle them identically to the current implementation.
- **FR-012**: System MUST produce identical API response shapes and data for all three endpoints after migration—no changes to request or response contracts.
- **FR-013**: The execution engine MUST log each action as it executes, including action type, target selector, and outcome, at a configurable verbosity level to support debugging.
- **FR-014**: System MUST validate JSON flow definitions at startup and reject any definition with missing required fields, unknown action types, or malformed structure, reporting clear validation errors.
- **FR-015**: The execution engine MUST support an optional named post-processing step on extract actions, allowing a JSON definition to reference a registered C# post-processor that transforms raw extraction output into the final structured model. This keeps complex parsing logic (e.g., label-value text parsing) in testable compiled code rather than in JavaScript strings embedded in JSON files.
- **FR-016**: Each JSON flow definition MUST include a required schema version field. The engine MUST validate that the version is compatible with the current engine version at startup and reject definitions with unsupported versions, reporting a clear error.

### Key Entities

- **Scraping Flow Definition**: A complete flow for a single endpoint (e.g., "Colorado Business Search"). Contains a schema version, metadata (name, state, endpoint identifier), runtime variable declarations, an ordered list of actions, and an output model declaration.
- **Action**: A single step within a flow. Has a type (navigate, fill, click, extract, etc.), parameters specific to its type (selector, URL, script, timeout, etc.), optional diagnostic settings (screenshot milestone), optional conditions for execution, and for extract actions an optional named post-processor reference.
- **Loop**: A repeatable sub-flow containing its own ordered list of actions, a termination condition (e.g., next-page link not found, max iterations reached), and a type (pagination, retry).
- **Conditional Branch**: A decision point that evaluates a condition (element existence, text presence, variable value) and selects which actions to execute next.
- **Variable**: A named runtime value injected into the flow at execution time (e.g., searchTerm, correlationId, detailsUrl). Variables are referenced in action parameters using substitution syntax.
- **Extraction Output**: The structured data produced by an extract action, captured into a named variable for use by subsequent actions or as the final flow result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All three scraping endpoints (Colorado search, Wyoming search, Colorado entity details) produce identical results when driven by JSON definitions compared to the current hard-coded implementation, verified by running the same set of search terms through both and comparing outputs field-by-field.
- **SC-002**: An operator can change a selector, URL, or extraction script in a JSON file and have the change take effect after a service restart—without any code compilation.
- **SC-003**: Adding a new state's business search flow requires only creating a new JSON definition file and registering it, with no changes to the execution engine code.
- **SC-004**: Diagnostic artifacts (screenshots, traces) captured during JSON-driven execution match the milestone naming and completeness of the current implementation.
- **SC-005**: Flow definition errors (invalid JSON, missing fields, unknown action types) are detected at startup and reported with actionable error messages before any scraping request is served.
- **SC-006**: Execution time for each scraping flow does not degrade by more than 10% compared to the current implementation when processing the same requests.

## Assumptions

- The existing browser pool, concurrency controls, and diagnostics infrastructure will be reused by the new engine rather than replaced.
- JSON definition files will be bundled with the application and loaded at startup; hot-reloading of definitions while the service is running is out of scope.
- The existing API request/response models and endpoint routes remain unchanged; only the internal execution mechanism changes.
- The CAPTCHA solving service integration remains the same; the JSON definition will reference it as a named external service call rather than embedding solver logic.
- The three existing scraping flows (CO search, WY search, CO entity details) are the only flows to migrate in this feature; additional states or endpoints are future work.
- The existing retry and timeout behaviors (resilience pipeline, per-attempt limits) will be preserved in the engine's loop/retry action types.
- After all three flows are migrated and verified to produce identical outputs, the old hard-coded scraper classes will be removed from the codebase. No configuration toggle or fallback mode is needed.
