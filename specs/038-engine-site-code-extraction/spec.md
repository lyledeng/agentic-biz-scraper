# Feature Specification: Extract Site-Specific Code from Generic Scraping Engine

**Feature Branch**: `038-engine-site-code-extraction`  
**Created**: 2026-04-13  
**Status**: Draft  
**Input**: User description: "Audit codebase for violations of SOLID and other constitution failures. Remove all code encoded in the generic scraping engine which is specific to a specific web site / scenario. Web Scraping engine should be very generic, and all code for a specific web site / scenario should be ideally encoded into the json script. If not possible it should be isolated to its own codebase."

## Audit Summary

A comprehensive audit of the BizScraper generic scraping engine identified **20 site-specific code violations** across 8 files. The most affected file is `GenericScriptScraper.cs` (5 critical violations), followed by mapper classes, `JsonDrivenEntityDetailScraper.cs`, and `Program.cs`. The violations span all SOLID principles, with SRP and OCP being the most frequently violated.

### Violation Categories

| Category | Count | Severity | Primary Files |
|----------|-------|----------|---------------|
| Post-flow document processors in engine | 4 | Critical | GenericScriptScraper.cs |
| State-specific URL injection in engine | 2 | Critical | GenericScriptScraper.cs, JsonDrivenSearchScraper.cs |
| State-branching in mappers | 2 | Major | SearchResultMapper.cs, EntityDetailMapper.cs |
| State-specific ID validation/parsing | 3 | Major | JsonDrivenEntityDetailScraper.cs |
| Hardcoded factory registrations | 2 | Major | Program.cs |
| State-specific post-processors | 2 | Moderate | WyomingResultPostProcessor.cs, ExtractActionHandler.cs |
| State-specific exception handling | 1 | Critical | JsonDrivenSearchScraper.cs |
| No document processor abstraction | 1 | Moderate | Architecture gap |

---

## User Scenarios & Testing

### User Story 1 — Extract Post-Flow Document Processors from GenericScriptScraper (Priority: P1)

The generic scraping engine (`GenericScriptScraper.cs`) contains four site-specific static methods that download and upload documents after a scraping flow completes: `ProcessHistoryDocumentsAsync` (WY), `ProcessMoFilingDocumentsAsync` (MO), `ProcessWaFilingDocumentsAsync` (WA), and `ProcessCoCertifiedDocumentsAsync` (CO). Each method contains hardcoded knowledge of a specific state's document structure, API endpoints, content types, and field names. These methods must be extracted into isolated, per-state document processor classes behind a common abstraction. The generic engine should invoke document processors dynamically based on the flow definition, not via hardcoded slug-matching `if` statements.

**Why this priority**: These four methods represent the largest single-file SOLID violation. They make `GenericScriptScraper.cs` grow linearly with each new state, requiring modification of the generic engine for every new document download scenario.

**Independent Test**: After extraction, adding a hypothetical new state's document processor requires zero changes to `GenericScriptScraper.cs`. Existing document download behaviour for all six states continues to work identically.

**Acceptance Scenarios**:

1. **Given** a WY entity details scrape completes with `historyDocuments` in output, **When** the engine runs post-flow processing, **Then** the WY document processor downloads, uploads, and rewrites URLs identically to current behaviour — without any WY-specific code in `GenericScriptScraper.cs`.
2. **Given** a MO entity details scrape completes with `filings` in output, **When** the engine runs post-flow processing, **Then** the MO document processor handles page-level fetch, TIFF/PDF content types, and slug construction — without any MO-specific code in `GenericScriptScraper.cs`.
3. **Given** a WA entity details scrape completes with `filings` in output, **When** the engine runs post-flow processing, **Then** the WA document processor calls the WA SOS API endpoints — without any hardcoded WA URLs in the engine.
4. **Given** a CO entity details scrape completes with `certifiedDocumentsViewerUrl` in output, **When** the engine runs post-flow processing, **Then** the CO document processor handles the certified docs download — without any CO-specific code in `GenericScriptScraper.cs`.
5. **Given** a definition with no registered document processor, **When** the engine runs post-flow processing, **Then** no document processing occurs and the flow result passes through unchanged.

---

### User Story 2 — Extract State-Specific Mappers into Polymorphic Classes (Priority: P1)

`SearchResultMapper.cs` contains six state-specific mapping methods (`MapCoResults`, `MapWyResults`, `MapIaResults`, `MapMoResults`, `MapWaResults`, `MapDeResults`), and `EntityDetailMapper.cs` mirrors this pattern with six detail-mapping methods. Each method has unique field extraction logic specific to a single state. These must be refactored into per-state mapper classes behind a common interface, looked up by definition slug.

**Why this priority**: These mappers are modified every time a new state is added, violating OCP. Combined, they represent 12 state-specific branches that should be polymorphic.

**Independent Test**: After refactoring, the `SearchResultMapper` and `EntityDetailMapper` classes no longer contain any state-branching logic. A new state requires only a new mapper class registered in DI — no modification to existing classes.

**Acceptance Scenarios**:

1. **Given** a CO business search result, **When** the execute-script handler maps the output, **Then** the Colorado search result mapper produces the same normalized output as today.
2. **Given** a WY entity details result with history documents, parties, and certificate, **When** the handler maps the output, **Then** the Wyoming entity detail mapper produces the same normalized output as today.
3. **Given** all six states' search and detail definitions, **When** each is executed end-to-end, **Then** the API response JSON is byte-for-byte identical to the current output.
4. **Given** a definition slug with no registered mapper, **When** the handler maps the output, **Then** the raw flow output is returned without transformation (graceful fallback).

---

### User Story 3 — Remove State-Specific URL Injection from Engine (Priority: P2)

`GenericScriptScraper.cs` and `JsonDrivenSearchScraper.cs` inject state-specific search URLs (`wyomingSearchUrl`, `iowaSearchUrl`, `germanySearchUrl`) into the action context from `PlaywrightOptions`. These URLs should be defined in each flow definition's variables section (where `searchEntryUrl` already is for CO) and resolved by the engine from the definition — not hardcoded in the engine.

**Why this priority**: Removing these hardcoded URLs eliminates a DIP violation and makes the engine truly definition-driven. Lower priority than US1/US2 because the behaviour already works, but it blocks the engine from being fully generic.

**Independent Test**: After extraction, `PlaywrightOptions` no longer contains `WyomingSearchUrl`, `IowaSearchUrl`, or `GermanySearchUrl` properties. The engine resolves all URLs from definition variables.

**Acceptance Scenarios**:

1. **Given** the WY business search definition with `searchEntryUrl` in its variables section, **When** the engine executes it, **Then** the URL is resolved from the definition — not from `PlaywrightOptions.WyomingSearchUrl`.
2. **Given** the IA business search definition with `searchEntryUrl` in its variables section, **When** the engine executes it, **Then** the URL is resolved from the definition.
3. **Given** the DE business search definition with `searchEntryUrl` in its variables section, **When** the engine executes it, **Then** the URL is resolved from the definition.
4. **Given** `PlaywrightOptions` after refactoring, **When** inspected, **Then** it contains no state-specific URL properties.

---

### User Story 4 — Delete V1 Scrapers and Legacy Code (Priority: P2)

Both `JsonDrivenSearchScraper` and `JsonDrivenEntityDetailScraper` are deprecated v1 wrappers — all execution already flows through the V2 `GenericScriptScraper`. These files must be deleted entirely, along with their v1 interfaces (`IStateSearchScraper`, `IEntityDetailScraper`), DI registrations, and test files. The WY-specific logic they contain (filing ID validation, certificate flow orchestration, history document lifecycle) is already handled by V2 definitions and the new `WyDocumentProcessor` (US1). No extraction is needed — only deletion.

**Why this priority**: These are the second-most violated files. Deleting them removes dead code, simplifies the codebase, and eliminates confusion between v1 and v2 code paths.

**Independent Test**: After deletion, `JsonDrivenSearchScraper.cs` and `JsonDrivenEntityDetailScraper.cs` no longer exist. The build succeeds with zero errors. WY entity details continue to work end-to-end via V2 definitions.

**Acceptance Scenarios**:

1. **Given** a WY entity details request with a valid filing ID, **When** the engine executes the definition, **Then** filing ID resolution and certificate flow work identically to today — without any WY-specific code in the entity detail scraper.
2. **Given** a CO entity details request, **When** the engine executes the definition, **Then** the entity detail scraper does not execute any WY-specific code paths.

---

### User Story 5 — Replace Hardcoded Factory Registrations with Dynamic Discovery (Priority: P3)

`Program.cs` manually registers `JsonDrivenSearchScraper` and `JsonDrivenEntityDetailScraper` instances per state (CO, WY, IA, MO, WA, DE). Adding a new state requires modifying `Program.cs`. Additionally, `GetExpectedStatePrefix()` in `ExecuteScriptHandler.cs` has a hardcoded switch for six state prefixes. These should be replaced with dynamic discovery from `FlowDefinitionLoader`.

**Why this priority**: This is a convenience/maintainability improvement. Current code works but requires manual registration for each new state.

**Independent Test**: After refactoring, adding a new state's JSON definitions to the definitions folder automatically makes them available via the API without any code changes.

**Acceptance Scenarios**:

1. **Given** a new state's JSON definition files placed in the definitions folder, **When** the API starts, **Then** the definitions are automatically loaded and executable via `/api/v2/execute-script` — without modifying `Program.cs`.
2. **Given** `ExecuteScriptHandler.cs` after refactoring, **When** inspected, **Then** it contains no hardcoded state prefix switch.

---

### Edge Cases

- What happens when a post-flow document processor throws an unhandled exception? The engine must log the error and return the flow result with document processing errors noted — not fail the entire request.
- What happens when a definition has no registered mapper? The raw flow output should be returned without transformation.
- What happens when the `searchEntryUrl` variable is missing from a definition? The engine should fail with a clear validation error at definition load time.
- How does the engine handle a definition that specifies a document processor that doesn't exist? The engine should log a warning and skip document processing.

## Requirements

### Functional Requirements

- **FR-001**: System MUST extract all post-flow document processing code from `GenericScriptScraper.cs` into per-state document processor classes behind an `IPostFlowDocumentProcessor` interface.
- **FR-002**: System MUST provide a document processor registry that resolves processors by convention from the definition slug prefix (e.g., `us-co` → Colorado processor), auto-discovered via DI scan at startup.
- **FR-003**: System MUST extract state-specific search result mapping from `SearchResultMapper.cs` into per-state classes behind an `ISearchResultMapper` interface.
- **FR-004**: System MUST extract state-specific entity detail mapping from `EntityDetailMapper.cs` into per-state classes behind an `IEntityDetailMapper` interface.
- **FR-005**: System MUST provide mapper registries that resolve the correct mapper by convention from the definition slug prefix (same convention as FR-002), auto-discovered via DI scan.
- **FR-006**: System MUST move state-specific search URLs (`WyomingSearchUrl`, `IowaSearchUrl`, `GermanySearchUrl`) from `PlaywrightOptions` into each definition's variables section as `searchEntryUrl`.
- **FR-007**: System MUST remove all state-specific URL injection from `GenericScriptScraper.cs` and `JsonDrivenSearchScraper.cs`.
- **FR-008**: System MUST extract WY-specific filing ID validation, certificate flow orchestration, and history document lifecycle from `JsonDrivenEntityDetailScraper.cs`.
- **FR-009**: System MUST replace hardcoded scraper factory registrations in `Program.cs` with dynamic discovery from `FlowDefinitionLoader`.
- **FR-010**: System MUST replace the hardcoded `GetExpectedStatePrefix()` switch in `ExecuteScriptHandler.cs` with a data-driven approach (definition metadata or convention).
- **FR-011**: System MUST preserve byte-for-byte identical API response JSON for all existing definitions after refactoring (zero behavioural regression).
- **FR-012**: System MUST ensure that `GenericScriptScraper.cs` contains zero conditional logic based on definition slugs or state identifiers after refactoring.
- **FR-013**: System MUST ensure that adding a new state's scraping capability requires only: (a) new JSON definition files, (b) optionally a new document processor class, and (c) optionally a new mapper class — with zero modification to any existing source file.
- **FR-014**: System MUST remove `JsonDrivenSearchScraper` and `JsonDrivenEntityDetailScraper` entirely, along with any v1 code paths, handlers, and DI registrations that reference them. All execution flows through the V2 `GenericScriptScraper`.
- **FR-015**: System MUST consolidate `WyomingResultPostProcessor` logic into the WY search result mapper and remove the `IPostProcessor` interface, its registrations in `Program.cs`, and the post-processor lookup in `ExtractActionHandler` entirely.
- **FR-016**: System MUST refactor `DeEntityDetailsProcessor` into an `IPostFlowDocumentProcessor` implementation (slug prefix `de-de`) and remove all Germany-specific branching from the handler layer.

### Key Entities

- **IPostFlowDocumentProcessor**: Abstraction for state-specific document download, upload, and URL rewriting after a scraping flow completes. Each implementation declares a slug prefix (e.g., `us-co`) used for convention-based resolution. Receives both `IPage` and `IBrowserContext` — each implementation uses whichever it needs (e.g., MO/WA use page-level fetch, WY/CO use context-level API requests).
- **ISearchResultMapper**: Abstraction for normalizing raw search results from a state's specific format into the unified API response shape. Convention-keyed by slug prefix.
- **IEntityDetailMapper**: Abstraction for normalizing raw entity details from a state's specific format into the unified API response shape. Convention-keyed by slug prefix.
- **PostFlowDocumentProcessorRegistry**: Resolves the correct document processor(s) for a given definition slug by extracting the country-state prefix and matching registered processors.
- **MapperRegistry**: Resolves the correct search result or entity detail mapper by the same slug prefix convention. Returns a passthrough/no-op mapper when no match exists.

## Success Criteria

### Measurable Outcomes

- **SC-001**: `GenericScriptScraper.cs` contains zero string literals matching any state identifier, URL, or definition slug.
- **SC-002**: `JsonDrivenSearchScraper.cs` contains zero state-specific URL or exception handling code.
- **SC-003**: `JsonDrivenSearchScraper.cs` and `JsonDrivenEntityDetailScraper.cs` are deleted from the codebase. No v1 code paths remain.
- **SC-004**: `SearchResultMapper.cs` and `EntityDetailMapper.cs` contain zero state-specific mapping branches — only orchestration logic delegating to per-state mapper classes.
- **SC-005**: All 326+ existing unit tests and 45 integration tests continue to pass with zero modifications to test assertions (behaviour preservation).
- **SC-006**: Adding a new hypothetical state requires zero modifications to any existing source file in the engine or handler layer.
- **SC-007**: `PlaywrightOptions` contains zero state-specific URL properties.
- **SC-008**: `Program.cs` contains zero per-state scraper factory registrations.

## Assumptions

- The existing `IPostProcessor` interface (used by `WyomingResultPostProcessor`) will be consolidated into the new `ISearchResultMapper` abstraction and removed. The WY post-processor's result formatting logic belongs in the WY search result mapper — one abstraction for result normalization.
- The `CallServiceActionHandler` CAPTCHA service integration is properly generic and does not require extraction (confirmed in audit — it uses pluggable service configuration).
- The `DeEntityDetailsProcessor` (Germany PDF translation) will be refactored into an `IPostFlowDocumentProcessor` implementation (slug prefix `de`) so the handler layer has zero state-specific branching. The FAB translation workflow is just another post-flow document processor — the abstraction already supports it.
- JSON flow definitions already support the `searchEntryUrl` variable pattern (CO uses it). WY, IA, and DE definitions will adopt the same pattern.
- The `maxFilingDocuments` configuration currently in WA's definition variables section is an acceptable pattern for per-definition configuration that does not need engine-level changes.
- The deprecated v1 API endpoints are out of scope for this refactoring.

## Clarifications

### Session 2026-04-13

- Q: How should the engine resolve which processor/mapper to use for a given definition? → A: Convention-based — derive key from definition slug prefix (e.g., `us-co` → Colorado processor). Auto-discover implementations via DI scan at startup. No JSON schema changes or separate registry config needed.
- Q: Should `JsonDrivenSearchScraper` and `JsonDrivenEntityDetailScraper` be removed entirely or only cleaned? → A: Remove entirely — they are deprecated v1 wrappers. Delete both classes and all v1 code paths/handlers/registrations. All execution flows through V2 `GenericScriptScraper`.
- Q: What context should `IPostFlowDocumentProcessor` receive? → A: Both `IPage` and `IBrowserContext`. Each implementation picks what it needs (MO/WA use page-level fetch for session cookies; WY/CO use context-level API requests). Simple, future-proof.
- Q: Should `IPostProcessor` (result formatting) be consolidated into the new mapper or kept separate? → A: Consolidate. Merge `WyomingResultPostProcessor` logic into the WY search result mapper. Remove `IPostProcessor` interface entirely. One abstraction for result normalization.
- Q: Should `DeEntityDetailsProcessor` be unified as an `IPostFlowDocumentProcessor` or remain in the handler layer? → A: Unify — register as `IPostFlowDocumentProcessor` (slug prefix `de`). Handler layer gets zero state-specific branching. FAB translation is just another post-flow document processor.
