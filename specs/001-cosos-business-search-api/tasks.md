# Tasks: CO SOS Business Entity Search API

**Input**: Design documents from `/specs/001-cosos-business-search-api/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: REQUIRED — Constitution Principle I (Test-First) mandates TDD. Tests MUST be written and FAIL before implementation.

**Organization**: Tasks grouped by user story. Each story is independently testable after its phase completes.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story (US1, US2, US3) — only in user story phases
- Include exact file paths in descriptions

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create the .NET solution structure, build infrastructure, and configuration files

- [x] T001 Create solution file, Directory.Build.props, Directory.Packages.props, and .editorconfig at repository root
- [x] T002 Create API project in src/CososWebScraping.Api/CososWebScraping.Api.csproj with required NuGet package references (Microsoft.Playwright, LiteBus, Polly, Flurl.Http, OpenTelemetry, System.Text.Json)
- [x] T003 [P] Create unit test project in tests/CososWebScraping.UnitTests/CososWebScraping.UnitTests.csproj with xUnit and NSubstitute
- [x] T004 [P] Create integration test project in tests/CososWebScraping.IntegrationTests/CososWebScraping.IntegrationTests.csproj with xUnit, NSubstitute, and Testcontainers
- [x] T005 [P] Create end-to-end test project in tests/CososWebScraping.EndToEndTests/CososWebScraping.EndToEndTests.csproj with xUnit and Microsoft.Playwright
- [x] T006 Create appsettings.json with PlaywrightOptions section (BrowserEndpoint, AccessToken placeholder, RequestTimeoutSeconds, MaxPages) and ConnectionStrings:AuditTrail in src/CososWebScraping.Api/appsettings.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T007 Create PlaywrightOptions configuration record in src/CososWebScraping.Api/Common/Configuration/PlaywrightOptions.cs
- [x] T008 [P] Create IBusinessEntityScraper interface in src/CososWebScraping.Api/Common/Interfaces/IBusinessEntityScraper.cs
- [x] T009 [P] Create BusinessEntityResult model record in src/CososWebScraping.Api/Features/BusinessSearch/Models/BusinessEntityResult.cs
- [x] T010 [P] Create SearchResponse model record in src/CososWebScraping.Api/Features/BusinessSearch/Models/SearchResponse.cs
- [x] T011 [P] Create SearchRequest model record in src/CososWebScraping.Api/Features/BusinessSearch/Models/SearchRequest.cs
- [x] T012 [P] Create AppJsonSerializerContext with source-generated serialisation for all models in src/CososWebScraping.Api/Common/Serialization/AppJsonSerializerContext.cs
- [x] T013 [P] Create SearchAuditEntry domain entity in src/CososWebScraping.Api/Domain/Entities/SearchAuditEntry.cs
- [x] T014 Create BusinessSearchLogMessages static partial class with [LoggerMessage] source-generated methods in src/CososWebScraping.Api/Features/BusinessSearch/Logging/BusinessSearchLogMessages.cs
- [x] T015 Create global IExceptionHandler mapping exceptions to RFC 9457 Problem Details in src/CososWebScraping.Api/Middleware/GlobalExceptionHandler.cs
- [x] T016 [P] Create CorrelationIdMiddleware in src/CososWebScraping.Api/Middleware/CorrelationIdMiddleware.cs
- [x] T017 [P] Create SecurityHeadersMiddleware (HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, CSP) in src/CososWebScraping.Api/Middleware/SecurityHeadersMiddleware.cs
- [x] T018 [P] Create RequestTimeoutMiddleware in src/CososWebScraping.Api/Middleware/RequestTimeoutMiddleware.cs
- [x] T019 Create Program.cs with DI registration (Litebus, Playwright browser connection, Polly, OpenTelemetry, middleware pipeline, Kestrel HTTPS on 8443) in src/CososWebScraping.Api/Program.cs
- [x] T020 Create Liquibase master file in deploy/liquibase/masterfiles/master-1.0.2603.0.yaml
- [x] T021 [P] Create Liquibase DDL changelog for SearchAuditEntry table in deploy/liquibase/changelog/ddl/1.0.2603.0-create-search-audit-table.sql
- [x] T053 Create health check endpoints (/healthz for liveness, /ready for readiness checking Playwright workspace connectivity) in src/CososWebScraping.Api/Features/HealthCheck/HealthCheckEndpoints.cs

**Checkpoint**: Foundation ready — models, interfaces, middleware, DI, database schema, and health endpoints in place. User story implementation can now begin.

---

## Phase 3: User Story 1 — Single-Page Business Name Search (Priority: P1) 🎯 MVP

**Goal**: A caller sends a business name and receives a JSON array of scraped results from the first page of the CO SOS site.

**Independent Test**: `curl -k "https://localhost:8443/api/v1/business-search?name=Acme+Construction+Colorado"` returns JSON with 7 fields per record.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [x] T022 [P] [US1] Unit test for SearchBusinessEntitiesHandler with mocked IBusinessEntityScraper (single-page, no results, validation error) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/SearchBusinessEntitiesHandlerTests.cs
- [x] T023 [P] [US1] Unit test for search term validation (empty, whitespace, valid) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/SearchRequestValidationTests.cs
- [x] T024 [P] [US1] Unit test for GlobalExceptionHandler mapping custom exceptions to Problem Details in tests/CososWebScraping.UnitTests/Middleware/GlobalExceptionHandlerTests.cs
- [x] T025 [P] [US1] Integration test for GET /api/v1/business-search endpoint (WebApplicationFactory, mocked scraper) in tests/CososWebScraping.IntegrationTests/Features/BusinessSearch/BusinessSearchEndpointTests.cs
- [x] T054 [P] [US1] Contract test validating 200/400/422/502/503 responses match OpenAPI schema (contracts/openapi.yaml) in tests/CososWebScraping.IntegrationTests/Features/BusinessSearch/OpenApiContractTests.cs
- [x] T055 [P] [US1] Unit test for search terms with special characters (&, ", accented chars) verifying correct URL encoding and form submission in tests/CososWebScraping.UnitTests/Features/BusinessSearch/SpecialCharacterSearchTests.cs
- [x] T056 [P] [US1] Unit test asserting scraper returns empty strings (not nulls) for missing table cells (FR-011) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/EmptyCellHandlingTests.cs
- [x] T057 [P] [US1] Unit test for AuditTrailRepository verifying audit records are constructed correctly in tests/CososWebScraping.UnitTests/Infrastructure/AuditTrailRepositoryTests.cs
- [x] T058 [P] [US1] Integration test for AuditTrailRepository persisting SearchAuditEntry to Azure SQL Edge via Testcontainers in tests/CososWebScraping.IntegrationTests/Infrastructure/AuditTrailRepositoryIntegrationTests.cs
- [x] T059 [P] [US1] E2E test for single-page search using Playwright against running API (WebApplicationFactory with real scraper mock) in tests/CososWebScraping.EndToEndTests/Scenarios/BusinessSearch/SinglePageSearchTests.cs

### Implementation for User Story 1

- [x] T026 [US1] Create SearchBusinessEntitiesQuery record implementing IQuery<SearchResponse> in src/CososWebScraping.Api/Features/BusinessSearch/Queries/SearchBusinessEntitiesQuery.cs
- [x] T027 [US1] Implement PlaywrightBusinessEntityScraper (connect to Azure Playwright workspace, navigate to CO SOS search page, fill form, submit, extract single-page table results via EvaluateAsync, parse pagination metadata) in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs
- [x] T028 [US1] Implement SearchBusinessEntitiesHandler with SemaphoreSlim(1,1) for sequential processing, IBusinessEntityScraper invocation, and SearchResponse assembly in src/CososWebScraping.Api/Features/BusinessSearch/Handlers/SearchBusinessEntitiesHandler.cs
- [x] T029 [US1] Create AuditTrailRepository for persisting SearchAuditEntry to Azure SQL MI in src/CososWebScraping.Api/Infrastructure/Persistence/AuditTrailRepository.cs
- [x] T030 [US1] Register GET /api/v1/business-search endpoint with input validation, Litebus query dispatch, and JSON response in src/CososWebScraping.Api/Features/BusinessSearch/Endpoints/BusinessSearchEndpoints.cs

**Checkpoint**: User Story 1 complete — single-page search works end-to-end. API returns correct 200, 400, and 503 responses.

---

## Phase 4: User Story 2 — Multi-Page Result Pagination (Priority: P2)

**Goal**: Searches returning multiple pages automatically aggregate all records (up to 10 pages) into a single response.

**Independent Test**: Search for a broad-but-valid term that spans multiple pages; verify totalCount matches siteTotalCount and truncated flag is correct.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T031 [P] [US2] Unit test for PlaywrightBusinessEntityScraper pagination logic (multi-page, last page detection, 10-page cap with truncation) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/PaginationScrapingTests.cs
- [x] T032 [P] [US2] Unit test for SearchBusinessEntitiesHandler with multi-page scraper responses (truncated=true, truncated=false) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/SearchHandlerPaginationTests.cs
- [x] T060 [P] [US2] E2E test for multi-page search verifying aggregation across pages and truncation indicator in tests/CososWebScraping.EndToEndTests/Scenarios/BusinessSearch/MultiPageSearchTests.cs

### Implementation for User Story 2

- [x] T033 [US2] Extend PlaywrightBusinessEntityScraper to detect "Viewing page X of Y" text, navigate to next page links, aggregate results across pages, and stop at 10-page cap with truncation flag in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs
- [x] T034 [US2] Update SearchBusinessEntitiesHandler to propagate Truncated, PagesScraped, and SiteTotalCount metadata from scraper to SearchResponse in src/CososWebScraping.Api/Features/BusinessSearch/Handlers/SearchBusinessEntitiesHandler.cs

**Checkpoint**: User Stories 1 AND 2 complete — single-page and multi-page searches both work. Pagination cap at 10 pages with truncation indicator.

---

## Phase 5: User Story 3 — Search Error and Edge Case Handling (Priority: P3)

**Goal**: All error conditions (exceeded record count, site unavailable, pagination failure) produce structured RFC 9457 Problem Details responses.

**Independent Test**: Search with a very broad term (e.g., "a") and verify 422 response with `exceeded-record-count` type.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T035 [P] [US3] Unit test for PlaywrightBusinessEntityScraper detection of "Exceeded Record Count" error page in tests/CososWebScraping.UnitTests/Features/BusinessSearch/ExceededRecordCountTests.cs
- [x] T036 [P] [US3] Unit test for scraper handling of site timeout and connection errors (Polly resilience) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/ScraperResilienceTests.cs
- [x] T037 [P] [US3] Unit test for mid-pagination failure (page 2 throws after page 1 succeeds → zero results, error response) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/PaginationFailureTests.cs
- [x] T038 [P] [US3] Integration test for error response contracts (400, 422, 502, 503 all return application/problem+json) in tests/CososWebScraping.IntegrationTests/Features/BusinessSearch/ErrorResponseContractTests.cs
- [x] T061 [P] [US3] E2E test for error scenarios (exceeded record count, invalid input, service busy) returning correct Problem Details in tests/CososWebScraping.EndToEndTests/Scenarios/BusinessSearch/ErrorHandlingTests.cs

### Implementation for User Story 3

- [x] T039 [US3] Create custom exception types (ExceededRecordCountException, UpstreamException, ScrapingException, ServiceBusyException) in src/CososWebScraping.Api/Features/BusinessSearch/Exceptions/
- [x] T040 [US3] Extend PlaywrightBusinessEntityScraper to detect "Exceeded Record Count" error text and throw ExceededRecordCountException in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs
- [x] T041 [US3] Add Polly retry/timeout policy to PlaywrightBusinessEntityScraper for transient failures, wrapping unrecoverable failures as UpstreamException in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs
- [x] T042 [US3] Extend pagination logic to discard partial results on mid-pagination failure and throw ScrapingException (FR-013) in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs
- [x] T043 [US3] Update GlobalExceptionHandler to map all custom exception types to correct HTTP status codes and Problem Details types in src/CososWebScraping.Api/Middleware/GlobalExceptionHandler.cs

**Checkpoint**: All user stories complete. All error paths return structured Problem Details. Full test pyramid passes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Deployment, observability, documentation, and hardening across all stories

- [x] T044 [P] Create multi-stage Dockerfile (build, publish, runtime with Playwright deps) in src/cososwebscraping-dockerfile
- [x] T045 [P] Create Helm chart (Chart.yaml, values.yaml with HPA config, templates) in deploy/helm/cososwebscraping/
- [x] T046 [P] Create CI pipeline (build, Liquibase validate, test with coverage, SonarQube, Docker build/push) in deploy/pipelines/ci.yaml
- [x] T047 [P] Create CD pipeline (deploy to AKS, run k6 performance scenarios, enforce latency gates) in deploy/pipelines/cd.yaml
- [x] T048 [P] Create SBOM scan pipeline in deploy/pipelines/sbom.yaml
- [x] T049 [P] Create Liquibase Dockerfile in deploy/liquibase/liquibase_dockerfile
- [x] T050 [P] Add OpenTelemetry custom metrics (search request count, success/failure rate, duration histogram, active searches gauge, pages scraped distribution) in src/CososWebScraping.Api/Features/BusinessSearch/Metrics/BusinessSearchMetrics.cs
- [x] T051 [P] Create README.md with project overview, quickstart reference, and architecture summary at repository root
- [x] T062 [P] Create k6 performance test scripts (single-page search, multi-page search, concurrent rejection scenarios) with p95/p99 latency gates in tests/k6/
- [ ] T052 Run quickstart.md validation — follow all steps and verify expected responses match

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion — delivers MVP
- **User Story 2 (Phase 4)**: Depends on Phase 2 completion — can start in parallel with US1 if staffed, but logically extends US1's scraper
- **User Story 3 (Phase 5)**: Depends on Phase 2 completion — can start in parallel with US1/US2 if staffed, but builds on scraper from US1/US2
- **Polish (Phase 6)**: Can start after Phase 2 for infra tasks (T044-T049); metrics (T050) after Phase 3; quickstart validation (T052) after Phase 5

### User Story Dependencies

- **User Story 1 (P1)**: No dependencies on other stories. Creates the scraper, handler, and endpoint used by US2/US3.
- **User Story 2 (P2)**: Extends the scraper from US1 with pagination. Can be developed independently if scaffolded, but most efficient after US1.
- **User Story 3 (P3)**: Extends the scraper and handler from US1/US2 with error detection. Can be developed independently if scaffolded, but most efficient after US1/US2.

### Within Each User Story

1. Tests MUST be written and FAIL before implementation (Constitution Principle I)
2. Models/interfaces before services
3. Services (scraper) before handlers
4. Handlers before endpoints
5. Story complete before moving to next priority

### Parallel Opportunities

**Phase 1**:
- T003, T004, T005 can run in parallel (test projects are independent)

**Phase 2**:
- T008, T009, T010, T011, T012, T013 can run in parallel (independent model/interface files)
- T016, T017, T018 can run in parallel (independent middleware files)
- T020, T021 can run in parallel (Liquibase files)
- T053 depends on T019 (needs Program.cs and DI container)

**Phase 3 (US1)**:
- T022, T023, T024, T025, T054, T055, T056, T057, T058, T059 can run in parallel (independent test files)

**Phase 4 (US2)**:
- T031, T032, T060 can run in parallel (independent test files)

**Phase 5 (US3)**:
- T035, T036, T037, T038, T061 can run in parallel (independent test files)

**Phase 6**:
- T044, T045, T046, T047, T048, T049, T050, T051, T062 can ALL run in parallel (independent files)

---

## Parallel Example: User Story 1

```text
Step 1 (parallel):  T022 + T023 + T024 + T025 + T054 + T055 + T056 + T057 + T058 + T059  (write all tests — they FAIL)
Step 2:             T026                         (query record)
Step 3:             T027                         (scraper implementation)
Step 4:             T028                         (handler — depends on T026, T027)
Step 5:             T029                         (audit repository)
Step 6:             T030                         (endpoint — depends on T028)
Step 7:             Run all US1 tests — they PASS ✅
```

## Implementation Strategy

- **MVP Scope**: Phase 1 + Phase 2 + Phase 3 (User Story 1) delivers a fully functional single-page search API
- **Incremental Delivery**: Each subsequent phase adds capability without modifying the core contract
- **Test Coverage**: Every phase starts with failing tests, ensuring no code ships without verification
- **Deployment Readiness**: Phase 6 can begin infrastructure tasks (Docker, Helm, CI/CD) in parallel with any user story phase after Phase 2
