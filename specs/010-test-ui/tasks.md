# Tasks: Test UI for BizScraper API

**Feature**: 010-test-ui | **Branch**: `010-test-ui` | **Date**: 2026-03-26
**Input**: Design documents from `specs/010-test-ui/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label — US1 = Search, US2 = Entity Details, US3 = Download Documents, US4 = Navigation
- File paths are relative to the workspace root

---

## Phase 1: Setup — Angular App Scaffold & Deployment Skeleton

**Purpose**: Create the Angular 17 project structure, Dockerfile, and Helm chart skeleton. All are independent of each other and can be created first without any feature logic.

- [X] T001 Scaffold Angular 17 standalone app at `src/test-ui/` using `ng new test-ui --standalone --routing --style=css --skip-git`
- [X] T002 Configure `src/test-ui/package.json` to add `@wk/components-v3-angular17` dependency and ensure `@angular/common`, `@angular/router`, `@angular/forms` are present (depends on T001 — `ng new` creates this file)
- [X] T003 Create `src/test-ui/tsconfig.json` and `src/test-ui/tsconfig.app.json` with strict mode, `target: ES2022`, and `useDefineForClassFields: false` for Angular 17 compatibility (depends on T001 — `ng new` creates these files)
- [X] T004 [P] Create `src/bizscraper-ui-dockerfile` — multi-stage: `node:22-alpine` build stage (`npm ci`, `ng build --configuration production`); `nginx:alpine` runtime stage (copy dist, copy nginx.conf, expose port 80, non-root `nginx` user)
- [X] T005 [P] Create `src/test-ui/nginx.conf` with SPA fallback (`try_files $uri $uri/ /index.html`), all security headers (HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, CSP), and temp paths under `/tmp` for read-only filesystem compatibility
- [X] T006 [P] Create `deploy/helm/bizscraper-ui/Chart.yaml` with `name: bizscraper-ui`, `version: 1.0.2603.0`, `appVersion: 1.0.2603.0`
- [X] T007 [P] Create `deploy/helm/bizscraper-ui/values.yaml` with image repo/tag, service port 80, resource requests/limits, HPA config (min 1, max 3, CPU target 70%), and `cors.testUiOrigin` placeholder
- [X] T008 [P] Create `deploy/helm/bizscraper-ui/templates/deployment.yaml` with securityContext (`readOnlyRootFilesystem: true`, drop all capabilities, `allowPrivilegeEscalation: false`), `emptyDir` volume for `/tmp`
- [X] T009 [P] Create `deploy/helm/bizscraper-ui/templates/service.yaml` — ClusterIP, port 80
- [X] T010 [P] Create `deploy/helm/bizscraper-ui/templates/hpa.yaml` — CPU/memory autoscaling with stabilisation windows from `values.yaml`
- [X] T011 [P] Create `deploy/helm/bizscraper-ui/templates/ingress.yaml` — nginx ingress class, `ssl-redirect: true`, path `/` for the UI on the same cluster host as the API, backend HTTPS annotation matching API ingress pattern

**Checkpoint**: Scaffold complete — `ng build` produces a dist folder; Docker image builds; Helm chart lints. No feature logic yet.

---

## Phase 1.5: Test Specifications — Write Tests First (Red Phase)

**Purpose**: Author all unit and E2E test specs BEFORE any implementation code. Each spec MUST fail (Red) when run. This phase satisfies Constitution Principle I (Test-First, NON-NEGOTIABLE). Requires Phase 1 complete so the Angular test runner (`ng test`) can be invoked.

**⚠️ CRITICAL**: All tasks in this phase MUST be completed and confirmed failing before their corresponding implementation tasks begin. Run `ng test` after each spec task — the test MUST fail (Red) before implementation starts.

- [X] T043 Write unit test spec for `ApiConfigService` in `src/test-ui/src/app/core/services/api-config.service.spec.ts` — test: (a) `baseUrl` signal defaults to `https://localhost:8443` when localStorage is empty; (b) updating the signal writes to `localStorage`; (c) initialising with a pre-set `localStorage` key restores the saved URL. Run `ng test` — MUST FAIL (Red). **Unblocks T014.**
- [X] T044 [P] Write unit test spec for `SearchService` in `src/test-ui/src/app/core/services/search.service.spec.ts` — use `HttpClientTestingModule`; test: (a) `search('Wendy', 'WY')` issues GET to `{baseUrl}/api/v1/business-search?name=Wendy&state=WY`; (b) a new search cancels the in-flight request via `switchMap`; (c) HTTP error propagates as Observable error. Run `ng test` — MUST FAIL (Red). **Unblocks T015.**
- [X] T045 [P] Write unit test spec for `EntityDetailsService` in `src/test-ui/src/app/core/services/entity-details.service.spec.ts` — use `HttpClientTestingModule`; test: (a) `getDetails(url, 'CO')` issues GET with `detailsUrl` correctly URL-encoded in query string; (b) `getDetails(url, 'WY')` passes `state=WY`; (c) HTTP 500 propagates as Observable error. Run `ng test` — MUST FAIL (Red). **Unblocks T016.**
- [X] T046 [P] Write unit test spec for `SearchComponent` in `src/test-ui/src/app/features/search/search.component.spec.ts` — test: (a) empty `term` field blocks form submission (FR-014); (b) whitespace-only `term` is invalid; (c) valid submission sets `loading()` to `true`; (d) successful API response populates `results()` and clears `loading()`; (e) API error sets `error()` signal. Run `ng test` — MUST FAIL (Red). **Unblocks T023.**
- [X] T047 [P] Write unit test spec for `EntityDetailsComponent` in `src/test-ui/src/app/features/entity-details/entity-details.component.spec.ts` — test: (a) WY-specific fields (`standingTax`, `subStatus`, `termOfDuration`, etc.) are rendered only when non-null; (b) `certificate === null` renders "No certificate of good standing"; (c) `certificate.available === false` renders "Not available"; (d) `historyDocuments` with `storageUrl` renders anchor `target="_blank"`; (e) `historyDocuments` with `error` renders error text and no anchor. Run `ng test` — MUST FAIL (Red). **Unblocks T026.**
- [ ] T048 Write Playwright E2E spec (DEFERRED — Playwright not installed) in `src/test-ui/e2e/search-flow.spec.ts` — scenario: load app, enter "Wendy" / state "WY", click Search, assert results grid visible **within 5 seconds** of click (SC-001), click first result row, assert entity details rendered, assert at least one history document link visible, click "Back to Results", assert results grid restored. Configure `playwright.config.ts` to run in `chromium`, `firefox`, and `webkit` browsers (SC-005). Run — MUST FAIL until app is built. **Unblocks T042.**

**Checkpoint**: All 6 test specs exist and are confirmed failing (Red). Implementation phases may now begin.

---

## Phase 2: Foundational — App Shell, Routing & Core Services

**Purpose**: App shell with Jumpstart Chrome pattern, Angular router, and three core services that all user stories depend on. This phase MUST be complete before any user story feature work begins.

**⚠️ CRITICAL**: Phase 1.5 test specs (T043–T048) MUST exist and be failing before implementing services here. US1–US4 all depend on `ApiConfigService` and the Angular HTTP client. US2 depends on `EntityDetailsService`. US3 depends on document URLs from US2.

- [X] T012 Configure `src/test-ui/src/app/app.config.ts` — `provideRouter(routes)`, `provideHttpClient()`, import WK Angular modules from `@wk/components-v3-angular17`
- [X] T013 Create `src/test-ui/src/app/app.routes.ts` — `{ path: '', redirectTo: 'search', pathMatch: 'full' }`, `{ path: 'search', component: SearchComponent }`, `{ path: 'entity-details', component: EntityDetailsComponent }`
- [X] T014 Implement `src/test-ui/src/app/core/services/api-config.service.ts` — reads/writes `localStorage` key `bizscraper.apiBaseUrl`; exposes `baseUrl` as a `WritableSignal<string>`; default `https://localhost:8443`
- [X] T015 [P] Implement `src/test-ui/src/app/core/services/search.service.ts` — injects `HttpClient` and `ApiConfigService`; `search(term: string, state: 'CO' | 'WY'): Observable<BusinessSearchResult[]>` using `switchMap` on `apiConfig.baseUrl`; calls `GET {baseUrl}/api/v1/business-search?name={term}&state={state}`
- [X] T016 [P] Implement `src/test-ui/src/app/core/services/entity-details.service.ts` — injects `HttpClient` and `ApiConfigService`; `getDetails(detailsUrl: string, state: 'CO' | 'WY'): Observable<EntityDetailResponse>` calls `GET {baseUrl}/api/v1/entity-details?detailsUrl={encodeURIComponent(detailsUrl)}&state={state}`
- [X] T017 [P] Create TypeScript model `src/test-ui/src/app/shared/models/business-search-result.model.ts` — interface `BusinessSearchResult` with fields: `name`, `identifier`, `status`, `entityType`, `formationDate`, `state`, `detailsUrl`
- [X] T018 [P] Create TypeScript model `src/test-ui/src/app/shared/models/entity-detail-response.model.ts` — interfaces `EntityDetailResponse`, `EntityDetailResult`, `RegisteredAgentResult`, `CertificateResult`, `PartyResult`, `HistoryDocumentResult` per data-model.md
- [X] T019 Implement `src/test-ui/src/app/app.component.ts` — Jumpstart Chrome pattern host using `banner-pds3`, `spine-pds3`, `brand-architecture-pds3`; includes `<app-api-config-bar>` in the banner area; `<router-outlet>` in content area
- [X] T020 [P] Implement `src/test-ui/src/app/shared/components/api-config-bar/api-config-bar.component.ts` — Jumpstart `text-field-pds3` bound to `ApiConfigService.baseUrl`; writes updates to localStorage on blur/enter (FR-016)
- [X] T021 [P] Implement `src/test-ui/src/app/shared/components/loading-indicator/loading-indicator.component.ts` — Jumpstart `spinner-pds3`; shown when `@Input() loading` signal is `true` (FR-012)
- [X] T022 [P] Implement `src/test-ui/src/app/shared/components/error-banner/error-banner.component.ts` — Jumpstart `banner-pds3` error variant; shown when `@Input() errorMessage` is non-null (FR-013)

**Checkpoint**: App shell renders with Jumpstart Chrome pattern. Routes are configured. Services compile. No user-facing feature functionality yet.

---

## Phase 3: User Story 1 — Business Entity Search (Priority: P1) 🎯 MVP

**Goal**: A tester can enter a business name and state, submit a search, and see results in a data grid.

**Independent Test**: Open the UI, enter "Wendy" with state "WY", click Search. A grid appears with matching entities showing Name, Identifier, Status, Entity Type, Formation Date, State columns. Empty search term shows validation message.

- [X] T023 [US1] Implement `src/test-ui/src/app/features/search/search.component.ts` — reactive form with `term` (required, trim validation — FR-014) and `state` (CO/WY dropdown, default CO); signals: `loading = signal(false)`, `results = signal<BusinessSearchResult[]>([])`, `error = signal<string | null>(null)`; injects `SearchService` and `Router`; `onSearch()` sets loading, calls `SearchService.search()` with `switchMap` (cancels previous), populates results or error signal
- [X] T024 [US1] Implement `src/test-ui/src/app/features/search/search.component.html` — Jumpstart `text-field-pds3` for name input, `dropdown-pds3` for state selector (CO/WY options), `button-field-pds3` for Search; `<app-loading-indicator>` bound to `loading()`; `<app-error-banner>` bound to `error()`; `<table>` or `table-pds3` showing result rows (Name, Identifier, Status, Entity Type, Formation Date, State); each row clickable calling `onSelectResult(result)`; "No results found" message when `results().length === 0` and not loading (FR-004)
- [X] T025 [US1] Wire row-click navigation in `SearchComponent.onSelectResult(result: BusinessSearchResult)` — calls `this.router.navigate(['/entity-details'], { state: { detailsUrl: result.detailsUrl, state: result.state, results: this.results() } })` to pass search state through router state for back-navigation (FR-005, FR-015)

**Checkpoint**: User Story 1 fully functional. Tester can search for CO and WY entities and see the grid. Validation prevents empty-term submission.

---

## Phase 4: User Story 2 — View Entity Details (Priority: P2)

**Goal**: Selecting an entity from the search grid navigates to a detail view showing entity info, registered agent, certificate status, and history documents list.

**Independent Test**: From search results for "Wendy" (WY), click "Wendy's Technology, LLC". Detail view shows name, status, formation date, ID number, form, jurisdiction, standing fields, registered agent name and address, certificate section, and a list of history document file names with dates.

- [X] T026 [US2] Implement `src/test-ui/src/app/features/entity-details/entity-details.component.ts` — reads `detailsUrl` and `state` from router state (`history.state`); signals: `loading = signal(true)`, `detail = signal<EntityDetailResponse | null>(null)`, `error = signal<string | null>(null)`; on init calls `EntityDetailsService.getDetails()` and populates signal; preserves `results` from router state for back-navigation
- [X] T027 [US2] Implement `src/test-ui/src/app/features/entity-details/entity-details.component.html` — `<app-loading-indicator>` during load; `<app-error-banner>` on error; `card-pds3` sections for: Entity Details (name, status, formationDate, idNumber, form, jurisdiction; WY-only fields guarded with `@if`), Registered Agent (name, streetAddress, mailingAddress), Certificate of Good Standing section (FR-010), History Documents list (FR-008); `breadcrumb-pds3` with "Back to Results" link calling `onBackToResults()` (FR-015)
- [X] T028 [US2] Implement WY-specific conditional rendering in entity-details template — `@if (detail()?.details?.standingTax)` guards for `standingTax`, `standingRA`, `standingOther`, `subStatus`, `inactiveDate`, `termOfDuration`, `formedIn`, `latestAnnualReportYear`, `annualReportExempt`, `licenseTaxPaid` fields per data-model.md null-safety rules

**Checkpoint**: User Story 2 functional. Tester can navigate to entity details and see all entity information with correct CO/WY field rendering.

---

## Phase 5: User Story 3 — Download Documents (Priority: P3)

**Goal**: Document file names in the detail view are clickable links that open or download the PDF from the cloud storage URL in a new browser tab.

**Independent Test**: From detail view for "Wendy's Technology, LLC" (WY), click "2025-original-annual-report-11347360.pdf". PDF opens in a new browser tab. Certificate link (if available) opens the certificate PDF in a new tab. Documents with errors show the error message instead of a link.

- [X] T029 [US3] Implement History Documents section in entity-details template — for each `doc` in `detail()?.historyDocuments`: `@if (doc.storageUrl && doc.fileName)` renders `<a [href]="doc.storageUrl" target="_blank" rel="noopener noreferrer">{{ doc.fileName }}</a>` alongside `doc.date` and `doc.eventTitle`; `@else if (doc.error)` renders error message text; never renders both (FR-008, FR-009) per data-model.md display rules
- [X] T030 [US3] Implement Certificate of Good Standing section in entity-details template — `@if (detail()?.certificate?.available && detail()?.certificate?.storageUrl)` renders download anchor (`target="_blank" rel="noopener noreferrer"`) with `fileName` as link text; `@else if (detail()?.certificate?.error)` renders error text; `@else if (detail()?.certificate != null && !detail()?.certificate?.available)` renders "Not available" text; `@else if (!detail()?.certificate)` renders "No certificate of good standing"; uses Jumpstart `button-field-pds3` anchor variant for the download link (FR-010) per data-model.md display rules

**Checkpoint**: User Story 3 functional. All document links open in new tabs. Error states render correctly. No crashes on missing URLs.

---

## Phase 6: User Story 4 — Navigation Between Search and Details (Priority: P4)

**Goal**: Tester can navigate back from entity details to the previous search results without losing state, and can initiate a new search at any time.

**Independent Test**: Search for "Wendy" (WY), click a result, then click "Back to Results". Previous search results grid re-displays. Click "New Search" — search form appears with empty inputs.

- [X] T031 [US4] Implement `EntityDetailsComponent.onBackToResults()` — navigates to `/search` passing the preserved `results` signal value through router state; `SearchComponent` reads results from router state on activation and restores `results` signal if present (FR-015)
- [X] T032 [US4] Implement `SearchComponent` restoration logic — in `ngOnInit`, check `history.state.results`; if present, restore `results` signal without triggering a new API call; show results grid immediately
- [X] T033 [US4] Add "New Search" button in `EntityDetailsComponent` — navigates to `/search` without passing prior results; `SearchComponent` detects no prior state and shows empty search form (FR-015 + US4 acceptance scenario 2)

**Checkpoint**: Full navigation flow works. Back-to-results preserves grid. New Search resets form. No state leaks between sessions.

---

## Phase 7: API CORS Configuration (BizScraper API Change)

**Purpose**: Enable the Angular UI to call the BizScraper API cross-origin. This is a modification to the existing .NET API, not the Angular app.

- [X] T034 Add `"Cors": { "TestUiOrigin": "http://localhost:4200" }` to `src/BizScraper.Api/appsettings.json`
- [X] T035 Add CORS policy registration in `src/BizScraper.Api/Program.cs` — `builder.Services.AddCors(opts => opts.AddPolicy("TestUiPolicy", policy => policy.WithOrigins(builder.Configuration["Cors:TestUiOrigin"]!).WithMethods("GET").WithHeaders("Content-Type", "Accept")))` before `builder.Build()`
- [X] T036 Add `app.UseCors("TestUiPolicy")` in `src/BizScraper.Api/Program.cs` **before** `app.UseRouting()` and endpoint registration (per R4 middleware placement rule from research.md)
- [X] T049 Wire CORS origin into deployed API pods — add `cors.testUiOrigin: ""` to `deploy/helm/bizscraper/values.yaml`; update `deploy/helm/bizscraper/templates/deployment.yaml` to inject env var `ASPNETCORE_Cors__TestUiOrigin` from `values.cors.testUiOrigin` into the BizScraper API container; set the production value in per-environment `values-prod.yaml`. This ensures `Cors:TestUiOrigin` is resolved from the cluster config — not the hardcoded `localhost:4200` in `appsettings.json` — so CORS works correctly in AKS without code changes. *(Resolves H1: CORS production URL gap.)*

**Checkpoint**: BizScraper API returns correct CORS headers. Angular UI at `http://localhost:4200` can call `https://localhost:8443/api/v1/...` without CORS errors. In AKS, the CORS origin is set per-environment from Helm values.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Wire up remaining quality attributes: request cancellation on new searches, global Angular error handler, accessibility, and final validation pass.

- [X] T037 Ensure `switchMap` is used in `SearchComponent` to cancel in-flight search requests when a new search is submitted before the previous one completes (edge case from spec.md)
- [X] T038 [P] Add Angular `ErrorHandler` in `src/test-ui/src/app/app.config.ts` — catches uncaught errors and sets a global error signal visible in `AppComponent`; prevents blank-screen failures
- [X] T039 [P] Validate all Jumpstart component usages conform to `@wk/components-v3-angular17` API — ensure `text-field-pds3`, `dropdown-pds3`, `button-field-pds3`, `spinner-pds3`, `banner-pds3`, `card-pds3`, `breadcrumb-pds3` are imported correctly in each standalone component's `imports` array
- [X] T040 [P] Add ARIA labels and `role` attributes to the results grid and document list for screen-reader accessibility (WK compliance requirement)
- [X] T041 [P] Add `<title>` updates per route — "BizScraper — Search" on `/search`, "BizScraper — Entity Details" on `/entity-details` using Angular `Title` service
- [ ] T042 Run Playwright E2E test suite (DEFERRED — Playwright not installed): execute `npx playwright test` targeting `chromium`, `firefox`, and `webkit` — validate SC-001 (results grid visible within 5 seconds of Search click) and SC-005 (cross-browser compatibility); all scenarios must pass before this task is complete
- [X] T050 Update `deploy/pipelines/ci.yaml` to add Angular UI build stage: (a) set up Node.js 22; (b) `npm ci` in `src/test-ui/`; (c) `ng build --configuration production`; (d) `ng test --watch=false --browsers=ChromeHeadless` with code coverage output; (e) Docker build and push of `src/bizscraper-ui-dockerfile` — feature branch pushes to `devacr`, main pushes to `stagingacr`, PRs build but do not push. Stage runs in parallel with the existing .NET build stage. *(Resolves M2: CI does not currently build the UI image.)*

---

## Dependencies

```
Phase 1 (Setup) ──────────────────────────────────────► no dependencies
  T001 ─────────────────────────────────────────────────► no dependencies
  T002, T003 ───────────────────────────────────────────► T001 (ng new creates package.json + tsconfig)
  T004–T011 [P] ────────────────────────────────────────► T001 (independent of T002/T003)
Phase 1.5 (Test Specs — Red Phase) ────────────────────► Phase 1 complete
  T043 (ApiConfigService spec) ────────────────────────► T001; must be FAILING before T014
  T044 (SearchService spec) ───────────────────────────► T001; must be FAILING before T015
  T045 (EntityDetailsService spec) ────────────────────► T001; must be FAILING before T016
  T046 (SearchComponent spec) ─────────────────────────► T001; must be FAILING before T023
  T047 (EntityDetailsComponent spec) ──────────────────► T001; must be FAILING before T026
  T048 (E2E search-flow spec) ─────────────────────────► T001; must be FAILING before T042
Phase 2 (Foundational) ────────────────────────────────► Phase 1 + Phase 1.5 complete
  T014 (ApiConfigService) ──────────────────────────────► T043 failing; Phase 2 can start with T012/T013
  T015 (SearchService) ─────────────────────────────────► T044 failing; T014
  T016 (EntityDetailsService) ──────────────────────────► T045 failing; T014
  T019 (AppComponent) ──────────────────────────────────► T012, T013, T020
Phase 3 (US1 Search) ──────────────────────────────────► Phase 2 complete; T046 failing
  T023 (SearchComponent.ts) ────────────────────────────► T046 failing; T015, T017, T021, T022
  T024 (SearchComponent.html) ──────────────────────────► T023
  T025 (row-click nav) ─────────────────────────────────► T024
Phase 4 (US2 Entity Details) ──────────────────────────► Phase 2 complete, T025; T047 failing
  T026 (EntityDetailsComponent.ts) ────────────────────► T047 failing; T016, T018, T021, T022
  T027 (EntityDetailsComponent.html base) ─────────────► T026
  T028 (WY conditional rendering) ─────────────────────► T027
Phase 5 (US3 Download) ────────────────────────────────► T027, T026 (uses same template)
  T029 (history documents) ────────────────────────────► T027
  T030 (certificate section) ──────────────────────────► T027
Phase 6 (US4 Navigation) ──────────────────────────────► T025, T026
  T031 (back to results) ───────────────────────────────► T025, T026
  T032 (search restore) ────────────────────────────────► T031
  T033 (new search button) ────────────────────────────► T031
Phase 7 (CORS) ────────────────────────────────────────► can be done in parallel with Phase 3+
  T034, T035, T036 ────────────────────────────────────► independent of Angular app phases
  T049 (CORS Helm env var wiring) ─────────────────────► T007 (values.yaml must exist); T036
Phase 8 (Polish) ──────────────────────────────────────► all phases complete
  T042 (E2E test run) ─────────────────────────────────► T048 spec; full app built (Phases 1–7)
  T050 (CI pipeline update) ───────────────────────────► T004 (Dockerfile must exist)
```

---

## Parallel Execution Examples

### Phase 1 Parallelisation (Setup)
T001 → then sequential: T002, T003 → then parallel: [T004, T005, T006, T007, T008, T009, T010, T011]

### Phase 1.5 Parallelisation (Test Specs)
T043 → then parallel: [T044, T045, T046, T047, T048]

### Phase 2 Parallelisation (Foundation)
T012 + T013 in parallel → then T014 → then parallel: [T015, T016, T017, T018] → T019 → parallel: [T020, T021, T022]

### Phase 3+4 Parallelisation (US1 + US2 + CORS)
Once Phase 2 is done: US1 (T023→T024→T025) and US2 (T026→T027→T028) can be worked by two developers in parallel. Phase 7 CORS tasks (T034, T035, T036) can be done concurrently.

### Phase 5+6 Parallelisation (US3 + US4)
T029 and T030 (US3) can be done in parallel after T027. T031, T032, T033 (US4) can be done in parallel after T025 and T026.

---

## Implementation Strategy

**MVP Scope (Phases 1–3 + Phase 7)**:
- Angular app scaffold → Foundation → US1 (Search) → CORS config
- Deliverable: Tester can enter name/state, call the API, and see a results grid

**Incremental Delivery**:
1. ✅ Phases 1–2: App shell + services (no visible features yet, but verifiable by `ng serve`)
2. ✅ Phase 3: Search grid visible and functional (US1 MVP)
3. ✅ Phase 4: Entity details view (US2 — most complex view)
4. ✅ Phase 5: Document and certificate download links (US3 — completes the happy path)
5. ✅ Phase 6: Back-navigation preserves state (US4 — UX polish)
6. ✅ Phase 7: CORS (unblocks cross-origin API calls — do this before Phase 3 if API is remote)
7. ✅ Phase 8: Polish, CI pipeline integration (T050), and final E2E validation (T042)
