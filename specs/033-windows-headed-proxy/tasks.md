# Tasks: Windows VM Proxy for Chrome Headed Mode

**Input**: Design documents from `/specs/033-windows-headed-proxy/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/health-check-extension.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Exact file paths included in every task description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration model and DI registration used by all user stories

- [X] T001 Create `WindowsProxyOptions` config class in src/BizScraper.Api/Common/Configuration/WindowsProxyOptions.cs
- [X] T002 Register `WindowsProxyOptions` with Options pattern and validation in src/BizScraper.Api/Program.cs
- [X] T003 [P] Add `WindowsProxy` section to src/BizScraper.Api/appsettings.json with default empty endpoint and 180s timeout
- [X] T004 [P] Add `WindowsProxy` env var mappings to deploy/helm/bizscraper/templates/deployment.yaml
- [X] T005 [P] Add `windowsProxy` values to deploy/helm/bizscraper/values.yaml (default empty) and deploy/helm/bizscraper/values-assembly.yaml (with VM endpoint)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Proxy service interface and implementation — MUST complete before any user story

**⚠️ CRITICAL**: All user stories depend on the proxy service and mode resolution

### Tests for Foundation ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T006 [P] Unit test: `ResolveEffectiveMode` returns `"windows"` when `mode: "windows"` + proxy configured; falls back to `"local"` when proxy not configured — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperModeResolutionTests.cs

### Implementation for Foundation

- [X] T007 Create `IWindowsProxyService` interface in src/BizScraper.Api/Infrastructure/Scraping/Proxy/IWindowsProxyService.cs
- [X] T008 Create `WindowsProxyService` implementing `IWindowsProxyService` with Flurl.Http forwarding in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs
- [X] T009 Register `IWindowsProxyService`/`WindowsProxyService` as singleton in src/BizScraper.Api/Program.cs
- [X] T010 Extend `ResolveEffectiveMode` to handle `"windows"` mode value in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs
- [X] T011 Add Windows proxy startup logging (definitions routed to windows) in src/BizScraper.Api/Program.cs alongside existing per-definition mode logging

**Checkpoint**: Foundation ready — proxy service available for injection, mode resolution supports `"windows"`, T006 passes green

---

## Phase 3: User Story 1 — Transparent Proxy to Windows VM (Priority: P1) 🎯 MVP

**Goal**: When a definition has `"mode": "windows"` and the proxy endpoint is configured, the handler forwards the request to the Windows VM and returns the response directly — bypassing the local scraper, mappers, and post-processing.

**Independent Test**: Submit an execute-script request for `us-ia-business-search` (mode: windows) through the AKS API. Verify the request is forwarded to the Windows VM and the response is returned with correct data and correlation ID.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] [US1] Unit test: `ForwardAsync` POSTs to `{EndpointUrl}/api/v2/execute-script` with request body and forwards `X-Correlation-Id` header — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Proxy/WindowsProxyServiceTests.cs
- [X] T013 [P] [US1] Unit test: Handler routes to `IWindowsProxyService.ForwardAsync` when `mode: "windows"` + endpoint configured; bypasses scraper and post-processing — in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerProxyTests.cs
- [X] T014 [P] [US1] Unit test: Handler falls back to local pool when `mode: "windows"` but no endpoint configured — in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerProxyTests.cs

### Implementation for User Story 1

- [X] T015 [US1] Add windows-mode proxy branch in `ExecuteScriptHandler.HandleAsync` — intercept before calling scraper, call `IWindowsProxyService.ForwardAsync`, return response directly, skip post-processing in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs
- [X] T016 [US1] Implement `WindowsProxyService.ForwardAsync` — POST to `{EndpointUrl}/api/v2/execute-script` with request body, forward `X-Correlation-Id` header, configure timeout from `WindowsProxyOptions.TimeoutSeconds` in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs
- [X] T017 [US1] Add structured logging for each proxied request (definition slug, correlation ID, destination endpoint) in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs
- [X] T018 [P] [US1] Update Iowa business search definition to `"mode": "windows"` in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json
- [X] T019 [P] [US1] Update Iowa entity details definition to `"mode": "windows"` in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-entity-details.json
- [X] T020 [US1] Implement local-pool fallback when `mode: "windows"` but no endpoint configured (FR-010) with warning log in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs

**Checkpoint**: Iowa definitions proxy transparently through AKS to Windows VM; non-windows definitions unaffected; T012–T014 pass green

---

## Phase 4: User Story 2 — Token Forwarding for Authenticated Requests (Priority: P1)

**Goal**: The proxy forwards the caller's original bearer token to the Windows VM. Unauthenticated requests are rejected at the AKS middleware before any forwarding occurs.

**Independent Test**: Submit an authenticated request for a headed definition. Verify the Windows VM receives the bearer token. Submit an unauthenticated request and verify 401 is returned before forwarding.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T021 [P] [US2] Unit test: `ForwardAsync` includes `Authorization` header extracted from `HttpContext` — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Proxy/WindowsProxyServiceTests.cs

### Implementation for User Story 2

- [X] T022 [US2] Extract `Authorization` header from `HttpContext.Request.Headers` in handler and pass to `IWindowsProxyService.ForwardAsync` in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs
- [X] T023 [US2] Set `Authorization` header on the Flurl proxy request in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs

**Checkpoint**: Bearer tokens flow end-to-end through the proxy; unauthorized requests rejected at AKS before forwarding; T021 passes green

---

## Phase 5: User Story 3 — Graceful Fallback When Windows VM Is Unavailable (Priority: P2)

**Goal**: When the Windows VM is unreachable or times out, the AKS API returns clear error responses (503/504) with appropriate headers. Error responses from the VM are propagated faithfully.

**Independent Test**: Stop the Windows VM, submit a headed request, verify 503 with `Retry-After` header. Wait for timeout, verify 504.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T024 [P] [US3] Unit test: `ForwardAsync` returns 503 + `Retry-After: 30` on `HttpRequestException` / connection refused — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Proxy/WindowsProxyServiceTests.cs
- [X] T025 [P] [US3] Unit test: `ForwardAsync` returns 504 on `FlurlHttpTimeoutException` — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Proxy/WindowsProxyServiceTests.cs
- [X] T026 [P] [US3] Unit test: `ForwardAsync` propagates Windows VM 4xx/5xx status and body to caller — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Proxy/WindowsProxyServiceTests.cs

### Implementation for User Story 3

- [X] T027 [US3] Add connection-failure handling — catch `FlurlHttpTimeoutException` → 504, `FlurlHttpException`/`HttpRequestException` (connection refused, DNS) → 503 with `Retry-After: 30` in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs
- [X] T028 [US3] Add error-response propagation — forward Windows VM 4xx/5xx status and body to the caller in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs
- [X] T029 [US3] Add structured error logging for proxy failures (timeout, connection, VM error) with definition slug and correlation ID in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs

**Checkpoint**: Windows VM down → 503; timeout → 504; VM errors propagated faithfully; all failures logged; T024–T026 pass green

---

## Phase 6: User Story 4 — Health Check Reports Windows VM Status (Priority: P2)

**Goal**: The `/ready` endpoint includes a `windowsProxy` section with connectivity status and lists definitions routed to the Windows VM.

**Independent Test**: Call `/ready` and verify `windowsProxy` section with status, endpoint, and `definitionsRoutedToWindows` list.

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T030 [P] [US4] Unit test: `CheckWindowsProxyHealthAsync` returns `healthy` when VM responds 200, `unhealthy` when unreachable, `not-configured` when no endpoint — in tests/BizScraper.UnitTests/Features/HealthCheck/HealthCheckWindowsProxyTests.cs
- [X] T031 [P] [US4] Unit test: `DefinitionsRoutedToWindows` lists correct slugs from definitions with `mode: "windows"` — in tests/BizScraper.UnitTests/Features/HealthCheck/HealthCheckWindowsProxyTests.cs

### Implementation for User Story 4

- [X] T032 [P] [US4] Create `WindowsProxyStatus` record in src/BizScraper.Api/Features/HealthCheck/HealthModels.cs
- [X] T033 [US4] Extend `ReadinessHealthResponse` with `WindowsProxy` and `DefinitionsRoutedToWindows` properties in src/BizScraper.Api/Features/HealthCheck/HealthModels.cs
- [X] T034 [US4] Add `CheckWindowsProxyHealthAsync` method to `IWindowsProxyService` — GET `{EndpointUrl}/healthz` with short timeout in src/BizScraper.Api/Infrastructure/Scraping/Proxy/WindowsProxyService.cs
- [X] T035 [US4] Extend `CanConnectAsync` in `GenericScriptScraper` to include Windows proxy health and definitions list in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs
- [X] T036 [US4] Update health check endpoint to return extended response with `windowsProxy` section in src/BizScraper.Api/Features/HealthCheck/HealthCheckEndpoints.cs

**Checkpoint**: `/ready` shows `windowsProxy.status`, endpoint, and routed definitions; unhealthy proxy does NOT cause NotReady; T030–T031 pass green

---

## Phase 7: User Story 5 — Windows VM Handles Post-Processing Independently (Priority: P3)

**Goal**: The AKS API does not duplicate any post-processing for proxied requests. The Windows VM's response is returned as-is. Blob storage URLs in the response are valid through the AKS API.

**Independent Test**: Proxy a request that generates screenshots. Verify screenshots exist in blob storage once (not duplicated). Verify document proxy URLs in the response resolve through AKS.

### Tests for User Story 5 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T037 [P] [US5] Unit test: Handler skips mapper, audit write, and DE entity processing for proxied responses — in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerProxyTests.cs

### Implementation for User Story 5

- [X] T038 [US5] Add integration-level documentation comment on proxy bypass explaining post-processing responsibility in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs

**Checkpoint**: Proxied requests produce no duplicate screenshots, documents, or audit records on the AKS side; T037 passes green

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Configuration, deployment, documentation, and validation

- [X] T039 [P] Update specs/current-specs/ with consolidated snapshot reflecting Windows proxy additions
- [X] T040 [P] Add inline XML doc-comments to `WindowsProxyOptions`, `IWindowsProxyService`, `WindowsProxyService`, `WindowsProxyStatus` public types
- [X] T041 Run quickstart.md validation — verify local two-instance proxy setup works per specs/033-windows-headed-proxy/quickstart.md
- [X] T042 Verify zero regression — confirm non-windows definitions (CO, WY, DE, MO, WA) route and execute identically to pre-feature behavior

> **CI/CD Deferral**: Research R6 describes a full Windows VM deployment pipeline (`win-x64` publish, SQLite DB sync from AKS PVC, deploy to ZUSCUDILNAEGIS). This is deferred to a separate follow-up task — it involves operations infrastructure (IIS management, PowerShell remoting, deployment groups) outside the scope of this feature's application-level code changes. Helm chart values (T004–T005) cover AKS-side configuration.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — core forwarding
- **US2 (Phase 4)**: Depends on Phase 3 T016 — adds auth header to existing proxy call
- **US3 (Phase 5)**: Depends on Phase 3 T016 — adds error handling to existing proxy call
- **US4 (Phase 6)**: Depends on Phase 2 — independent of US1/US2/US3 (health check only)
- **US5 (Phase 7)**: Depends on Phase 3 T015 — verifies handler bypass
- **Polish (Phase 8)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: Foundational phase only — no other story dependencies
- **US2 (P1)**: Builds on US1's proxy call (T016) — adds Authorization header
- **US3 (P2)**: Builds on US1's proxy call (T016) — adds error handling
- **US4 (P2)**: Foundational phase only — can run in parallel with US1/US2/US3
- **US5 (P3)**: Builds on US1's handler bypass (T015) — verifies no duplicate post-processing

### Within Each User Story

- **Tests MUST be written FIRST and FAIL (Red) before implementation begins**
- Models/interfaces before services
- Services before endpoint integration
- Core implementation before error handling
- Story complete before moving to next priority

### Parallel Opportunities

- T003, T004, T005 can run in parallel (config files in different locations)
- T012, T013, T014 can run in parallel (test files, no interdependencies)
- T018, T019 can run in parallel (different definition JSON files)
- T024, T025, T026 can run in parallel (test cases, no interdependencies)
- T030, T031 can run in parallel (test cases, no interdependencies)
- T032 can run in parallel with T034 (model vs service method)
- US4 (health check) can run in parallel with US1/US2/US3 (independent feature)
- T039, T040 can run in parallel (different file sets)

---

## Parallel Example: Phase 1 Setup

```
# All config tasks in parallel:
Task T003: Add WindowsProxy section to appsettings.json
Task T004: Add env var mappings to deployment.yaml
Task T005: Add values to values.yaml / values-assembly.yaml
```

## Parallel Example: Phase 3 US1 Tests

```
# All US1 test tasks in parallel (different files, no dependencies):
Task T012: Unit test — ForwardAsync endpoint + correlation
Task T013: Unit test — Handler routes to proxy
Task T014: Unit test — Handler fallback to local
```

## Parallel Example: US4 alongside US1-US3

```
# US4 health check is independent — can run alongside any other story:
Worker A: US1 → US2 → US3 (sequential proxy work)
Worker B: US4 (health check extension)
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Complete Phase 1: Setup (T001–T005)
2. Complete Phase 2: Foundational — test first (T006), then impl (T007–T011)
3. Complete Phase 3: US1 — tests first (T012–T014), then impl (T015–T020)
4. Complete Phase 4: US2 — test first (T021), then impl (T022–T023)
5. **STOP and VALIDATE**: Test proxy end-to-end with authenticated Iowa search request
6. Deploy to Assembly environment

### Incremental Delivery

1. Setup + Foundational → config and proxy service wired (T006 green)
2. US1 + US2 → core proxy working end-to-end with auth (**MVP**, all tests green)
3. US3 → graceful error handling for production resilience (T024–T026 green)
4. US4 → health check visibility for operators (T030–T031 green)
5. US5 → verify no duplicate post-processing (T037 green)
6. Polish → docs, regression check, quickstart validation

---

## Notes

- All tasks follow TDD discipline per constitution — test tasks precede implementation in every phase
- Flurl.Http used for proxy calls (consistent with FabCaptchaSolver, FabDocumentTranslator patterns)
- No retry on proxy timeout — headed scraping is non-idempotent (R4)
- Windows VM IIS path: `/mvpoc/bizscrapper-api` (double 'p') — config stores full base URL
- HTTPS-only to Windows VM with default CA certificate validation (FR-013)
- 42 total tasks across 8 phases (11 test tasks + 27 implementation/setup tasks + 4 polish tasks)
