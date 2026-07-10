# Feature Specification: Generic Script Execution Endpoint

**Feature Branch**: `016-generic-script-execution`  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "Make the API generic with a single POST /api/v1/execute-script endpoint that accepts different definitions (co-business-search, co-entity-details, de-business-search, etc.) so new definitions can be added and executed through the scraping engine without writing new endpoint code."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Execute Any Definition via a Single Endpoint (Priority: P1)

An API consumer sends a POST request to `/api/v1/execute-script` with a JSON body containing a `definition` field that identifies which scraping flow to run, plus any parameters required by that definition. The system resolves the definition, validates the supplied parameters, executes the corresponding scraping flow, and returns the results.

**Why this priority**: This is the core value of the feature — collapsing multiple bespoke endpoints into one generic entry point. Without this, the feature has no purpose.

**Independent Test**: Can be fully tested by sending a POST to `/api/v1/execute-script` with `{"definition": "co-business-search", "parameters": {"searchTerm": "Wendys"}}` and verifying normalized search results are returned, identical in structure to the existing `/api/v1/business-search?state=CO&name=Wendys` endpoint.

**Acceptance Scenarios**:

1. **Given** the system has a "co-business-search" definition loaded, **When** a consumer sends `POST /api/v1/execute-script` with `{"definition": "co-business-search", "parameters": {"searchTerm": "Wendys"}}`, **Then** the system executes the CO business search flow and returns normalized search results.
2. **Given** the system has a "co-entity-details" definition loaded, **When** a consumer sends `POST /api/v1/execute-script` with `{"definition": "co-entity-details", "parameters": {"detailsUrl": "https://..."}}`, **Then** the system executes the CO entity details flow and returns entity detail results.
3. **Given** the system has a "de-business-search" definition loaded, **When** a consumer sends `POST /api/v1/execute-script` with `{"definition": "de-business-search", "parameters": {"searchTerm": "Rhode"}}`, **Then** the system executes the Germany business search flow and returns search results.

---

### User Story 2 - Input Validation and Clear Error Reporting (Priority: P2)

An API consumer sends a request with a missing or unrecognized definition, or omits required parameters for a given definition. The system returns a structured error response describing what went wrong without exposing internal details.

**Why this priority**: Robust validation is essential for a usable API and prevents confusing failures deep in the scraping engine.

**Independent Test**: Can be tested by sending requests with an unknown definition, missing `definition` field, or missing required parameters and verifying appropriate error responses.

**Acceptance Scenarios**:

1. **Given** no "unknown-definition" exists, **When** a consumer sends `POST /api/v1/execute-script` with `{"definition": "unknown-definition", "parameters": {}}`, **Then** the system returns a 400 error indicating the definition is not recognized.
2. **Given** the "co-business-search" definition requires a `searchTerm` parameter, **When** a consumer sends `POST /api/v1/execute-script` with `{"definition": "co-business-search", "parameters": {}}` (missing `searchTerm`), **Then** the system returns a 400 error listing the missing required parameters.
3. **Given** a valid request, **When** the `definition` field itself is missing or empty, **Then** the system returns a 400 error indicating that `definition` is required.

---

### User Story 3 - Automatic Discovery of New Definitions (Priority: P3)

A developer adds a new JSON flow definition file (e.g., "ny-business-search.json") to the definitions directory. After the application restarts, the new definition becomes available through the `/api/v1/execute-script` endpoint without any code changes.

**Why this priority**: This is the key extensibility benefit — zero code changes to support a new scraping target. It builds on the execution capability from P1.

**Independent Test**: Can be tested by adding a new definition file, restarting the application, and verifying the new definition can be executed through `/api/v1/execute-script`.

**Acceptance Scenarios**:

1. **Given** a new definition file "ny-business-search.json" has been added to the definitions directory, **When** the application starts, **Then** "ny-business-search" is available for execution via the execute-script endpoint.
2. **Given** a definition has been loaded, **When** a consumer sends a valid request using that definition's identifier, **Then** the scraping engine executes it and returns results.

---

### User Story 4 - List Available Definitions (Priority: P3)

An API consumer wants to discover which definitions are currently available. They call a discovery endpoint and receive a list of available definition identifiers along with their required parameters.

**Why this priority**: Improves developer experience by making the API self-documenting, but the core execution flow works without it.

**Independent Test**: Can be tested by calling the discovery endpoint and verifying it returns a list that matches the loaded definitions.

**Acceptance Scenarios**:

1. **Given** the system has definitions loaded for "co-business-search", "co-entity-details", "de-business-search", "wy-business-search", and "wy-entity-details", **When** a consumer calls the discovery endpoint, **Then** the response lists all five definitions with their required parameters.

---

### Edge Cases

- What happens when a definition exists but the upstream website is unreachable? The system should return a 502 error with a meaningful message.
- What happens when two definitions have the same logical identifier? The system should reject duplicate definitions at startup.
- What happens when the request body is not valid JSON? The system should return a 400 error.
- What happens when extra, unrecognized parameters are supplied? The system should ignore them and proceed with the recognized parameters.
- What happens when a scraping flow exceeds the configured timeout? The system should return a 504 error.
- What happens when the system is already at concurrency capacity for a given definition? The system should return a 503 error with a retry-after hint.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose a single `POST /api/v1/execute-script` endpoint that accepts a JSON request body containing a `definition` identifier and a `parameters` object holding definition-specific input parameters (e.g., `{"definition": "co-business-search", "parameters": {"searchTerm": "Wendys"}}`).
- **FR-002**: System MUST resolve the `definition` identifier by matching it against the `metadata.definitionSlug` field declared in each loaded flow definition.
- **FR-003**: System MUST validate that the `definition` field is present and non-empty; if missing, return a structured error response.
- **FR-004**: System MUST validate that all required input parameters for the resolved definition are provided within the `parameters` object; if any are missing, return a structured error listing the missing parameters.
- **FR-005**: System MUST return a 400 error when the `definition` identifier does not match any loaded definition.
- **FR-006**: System MUST execute the resolved scraping flow with the supplied parameters and return the results in a uniform response envelope: `{ "definition": "<slug>", "correlationId": "<id>", "truncated": <bool>, "data": <raw flow output> }`.
- **FR-007**: System MUST support all existing definition types (business search, entity details) through the generic endpoint without requiring definition-specific handler code.
- **FR-008**: System MUST enforce concurrency limits per definition slug (e.g., `co-business-search` and `co-entity-details` each have independent limits) to prevent resource exhaustion.
- **FR-009**: System MUST return structured error responses (problem details format) for all error scenarios, including validation failures, upstream errors, timeouts, and concurrency limits.
- **FR-010**: System MUST include a correlation identifier in every response for traceability.
- **FR-011**: System MUST log the execution of each request including the definition used, duration, and outcome.
- **FR-012**: System MUST continue to support existing dedicated endpoints (`/api/v1/business-search`, `/api/v1/entity-details`, `/api/v1/germany-search`) during a transition period.
- **FR-013**: System MUST expose a discovery mechanism (e.g., `GET /api/v1/definitions`) that lists available definitions and their required input parameters.
- **FR-014**: System MUST automatically discover new definitions from the definitions directory at application startup without code changes.
- **FR-015**: System MUST record each generic execution in a dedicated audit table containing: definition slug, correlation ID, input parameters (as JSON), execution duration, outcome status, error detail (if any), and diagnostics URL.

### Key Entities

- **Definition**: A named scraping flow configuration that describes what a scraping run does. Identified by an explicit `metadata.definitionSlug` field in the JSON definition file (e.g., `"co-business-search"`). Contains metadata about required input parameters, the target state/jurisdiction, and the endpoint type.
- **Execution Request**: A consumer's request to run a specific definition. Contains a `definition` identifier at the top level and a nested `parameters` object holding definition-specific key-value input parameters.
- **Execution Result**: The output of a scraping flow run. Returned in a uniform envelope containing the `definition` slug, `correlationId`, `truncated` flag, and a `data` field holding the definition-specific scraped data.
- **Definition Parameter**: A named input that a definition requires or optionally accepts. Has a name, whether it is required, and an optional default value.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing scraping operations (CO business search, CO entity details, WY business search, WY entity details, DE business search) can be executed through the single generic endpoint, returning equivalent data (inside the uniform envelope's `data` field) to the existing dedicated endpoints.
- **SC-002**: Adding a new scraping definition requires zero application code changes — only a new JSON definition file and an application restart.
- **SC-003**: Consumers can discover available definitions and their required parameters through the discovery endpoint.
- **SC-004**: Invalid requests (bad definition, missing parameters) receive clear error responses within 1 second.
- **SC-005**: The generic endpoint enforces concurrency limits, preventing more simultaneous executions than the system can handle per definition.
- **SC-006**: Every request is traceable via a correlation identifier present in the response.

## Clarifications

### Session 2026-03-28

- Q: How should the generic endpoint resolve the `definition` field from the request body to an internal flow definition? → A: Add a new `metadata.definitionSlug` field to each JSON definition file that consumers use directly.
- Q: Should the generic endpoint return raw flow output or a uniform envelope? → A: Return a uniform response envelope with metadata fields (definition, correlationId, truncated) wrapping the raw data.
- Q: At what granularity should concurrency be limited for the generic endpoint? → A: Per definition slug — each definition (e.g., co-business-search, co-entity-details) gets its own concurrency limit.
- Q: How should audit trail recording work for the generic endpoint? → A: A single generic execution audit table with common columns (definition slug, correlation ID, duration, status, error detail, diagnostics URL) plus a JSON column for definition-specific data.
- Q: How should definition-specific parameters be structured in the request body? → A: Nested — parameters go inside a `parameters` object (e.g., `{"definition": "...", "parameters": {"name": "..."}}`).

## Assumptions

- The existing JSON scraping engine (`ScrapingFlowEngine`, `FlowDefinitionLoader`, action handlers) will be reused as-is to execute definitions.
- The existing dedicated endpoints will remain available during a transition period. All existing search/entity-details GET endpoints (`/api/v1/business-search`, `/api/v1/entity-details`, `/api/v1/germany-search`) are marked as deprecated in Swagger. `POST /api/v1/germany-search/current-hardcopy-printout` and `GET /api/v1/documents/{blobPath}` are NOT deprecated — they stream binary PDF responses that the generic endpoint does not support. The test UI is migrated to use the new generic endpoint.
- Definition identifiers are explicitly declared via a `metadata.definitionSlug` field in each JSON definition file. The convention is `{state-code}-{endpoint-type}` (e.g., `"co-business-search"`, `"wy-entity-details"`, `"de-business-search"`), but the system resolves by exact slug match, not by convention.
- Each definition's JSON file already declares its required input variables (via the `variables` section with `source: "runtime"` and `required: true`), so the system can derive required parameters from the definition metadata.
- Concurrency limits are enforced per definition slug (not per state code). Each definition gets its own semaphore, configurable via settings, defaulting to 1 concurrent execution if not specified.
- The generic endpoint wraps the raw scraping flow output inside a uniform response envelope (`definition`, `correlationId`, `truncated`, `data`). The `data` field contains definition-specific output shaped by the definition's post-processors or extract scripts; no endpoint-specific mapping code is required.
- Authentication and authorization are not in scope for this feature; the API remains internally accessible as it is today.
- The generic endpoint records audit data in a single new audit table with common columns (definition slug, correlation ID, duration, status, error detail, diagnostics URL) plus a JSON column for definition-specific data. It does not write to the existing feature-specific audit tables.
