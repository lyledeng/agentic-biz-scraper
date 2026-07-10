# Tasks: Microsoft Entra ID Authentication

**Input**: Design documents from `/specs/025-entra-id-auth/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/authentication.md ✓, quickstart.md ✓

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add authentication packages, configuration plumbing, and database schema changes shared by all user stories.

- [X] T001 Add `Microsoft.Identity.Web` PackageVersion to `Directory.Packages.props`
- [X] T002 Add `Microsoft.Identity.Web` PackageReference to `src/BizScraper.Api/BizScraper.Api.csproj`
- [X] T003 [P] Add `AzureAd` configuration section with placeholder values to `src/BizScraper.Api/appsettings.json`
- [X] T004 [P] Create Liquibase changelog `deploy/liquibase/changelog/ddl/2.0.2604.0-add-caller-identity-to-search-audit.sql` — ALTER TABLE SearchAuditEntries ADD COLUMN CallerIdentity TEXT NULL, with rollback. Changeset MUST include a `comment` attribute per Constitution §VI
- [X] T005 [P] Create Liquibase changelog `deploy/liquibase/changelog/ddl/2.0.2604.1-add-caller-identity-to-script-execution-audit.sql` — ALTER TABLE ScriptExecutionAuditEntries ADD COLUMN CallerIdentity TEXT NULL, with rollback. Changeset MUST include a `comment` attribute per Constitution §VI
- [X] T006 Register new changelogs in `deploy/liquibase/masterfiles/master-1.0.2603.0.yaml`
- [X] T007 [P] Add `CallerIdentity` property to `src/BizScraper.Api/Domain/Entities/SearchAuditEntry.cs`
- [X] T008 [P] Add `CallerIdentity` property to `src/BizScraper.Api/Domain/Entities/ScriptExecutionAuditEntry.cs`
- [X] T009 Update `src/BizScraper.Api/Infrastructure/Persistence/AuditTrailRepository.cs` — add CallerIdentity to INSERT SQL and ConfigureSaveCommand
- [X] T010 Update `src/BizScraper.Api/Infrastructure/Persistence/ScriptExecutionAuditRepository.cs` — add CallerIdentity to INSERT SQL and ConfigureSaveCommand
- [X] T010a [P] Write unit tests for `AuditTrailRepository.ConfigureSaveCommand` verifying CallerIdentity parameter is bound — `tests/BizScraper.UnitTests/Infrastructure/AuditTrailRepositoryTests.cs` (extend existing test class)
- [X] T010b [P] Write unit tests for `ScriptExecutionAuditRepository.ConfigureSaveCommand` verifying CallerIdentity parameter is bound — `tests/BizScraper.UnitTests/Infrastructure/ScriptExecutionAuditRepositoryTests.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire up ASP.NET Core authentication/authorization middleware. All user stories depend on this.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T011 Register authentication and authorization services in `src/BizScraper.Api/Program.cs` — `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))` and `AddAuthorization()`
- [X] T012 Add `UseAuthentication()` and `UseAuthorization()` to the middleware pipeline in `src/BizScraper.Api/Program.cs` — after `UseCors()`, before `UseMiddleware<CorrelationIdMiddleware>()`
- [X] T013 Apply `.RequireAuthorization()` to `MapExecuteScriptEndpoints()` and `MapDocumentEndpoints()` in `src/BizScraper.Api/Program.cs`
- [X] T013a Configure an authorization policy in `AddAuthorization()` that accepts requests with **either** the `access_as_user` delegated scope (via `RequireScope`) **or** the `BizScraper.Execute` application role (via `RequireRole`). Apply this policy to protected endpoints so that valid tokens without the correct scope/role receive 403 Forbidden (FR-011)
- [X] T014 Apply `.AllowAnonymous()` to `MapHealthCheckEndpoints()`, Swagger routes, and root redirect in `src/BizScraper.Api/Program.cs`
- [X] T015 Update CORS policy in `src/BizScraper.Api/Program.cs` — add `"Authorization"` to `.WithHeaders()` call
- [X] T016 Configure Swagger OAuth2 security definition and requirement in `src/BizScraper.Api/Program.cs` — `AddSecurityDefinition("oauth2", ...)` and `AddSecurityRequirement(...)` in SwaggerGen options, plus `OAuthUsePkce(true)` in SwaggerUI options

**Checkpoint**: API now rejects unauthenticated requests on protected endpoints (401) and requests with valid tokens but wrong scope/role (403). Health/Swagger remain public.

---

## Phase 3: User Story 3 — Backend API Token Validation (Priority: P1) 🎯 MVP

**Goal**: API validates both delegated and application tokens; returns 401/403 correctly; logs auth failures.

**Independent Test**: Send requests with valid/invalid/missing tokens and verify correct HTTP status codes.

### Implementation for US3

- [X] T017a [US3] Write unit tests (Red) for `ClaimsPrincipalExtensions.GetCallerIdentity()` — test delegated token returns `preferred_username`, application token returns `azp`, anonymous returns null — `tests/BizScraper.UnitTests/Common/Extensions/ClaimsPrincipalExtensionsTests.cs`
- [X] T017 [US3] Create caller identity extraction helper (static method or extension) that reads `preferred_username` for delegated tokens and `azp` for application tokens — add to `src/BizScraper.Api/Common/Extensions/ClaimsPrincipalExtensions.cs` (Green — make T017a tests pass)
- [X] T018a [US3] Write unit tests (Red) for caller identity wiring in `ExecuteScriptHandler` — verify `CallerIdentity` is set on `ScriptExecutionAuditEntry` from `HttpContext.User` — `tests/BizScraper.UnitTests/Features/Authentication/CallerIdentityWiringTests.cs`
- [X] T018 [US3] Wire caller identity into `src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs` — inject `IHttpContextAccessor`, extract identity via `ClaimsPrincipalExtensions.GetCallerIdentity()`, and pass to `WriteAuditAsync` for `ScriptExecutionAuditEntry` creation (Green — make T018a tests pass). Note: `AuditTrailRepository` (`SearchAuditEntry`) is currently unused in production code — no handler calls `AuditTrailRepository.SaveAsync`. The CallerIdentity column is added to both tables for schema consistency (T004/T009) but only `ScriptExecutionAuditEntry` is actively written.
- [X] T019 [US3] Add authentication failure logging in `src/BizScraper.Api/Program.cs` — configure `JwtBearerEvents.OnAuthenticationFailed` to log with correlation ID. Additionally, detect Entra ID connectivity failures (e.g., `HttpRequestException`, `SocketException`) and return 503 Service Unavailable instead of generic 401, per spec edge case

- [X] T019a [US3] Write integration tests for protected and anonymous endpoints — `tests/BizScraper.IntegrationTests/Features/Authentication/AuthenticatedEndpointTests.cs`: verify 401 for missing token, 403 for token without required scope/role, 200 for valid delegated token, 200 for valid application token, and that `/healthz` + `/swagger` return 200 without auth

**Checkpoint**: API validates tokens from both flows, records caller identity, logs failures. All auth behaviours covered by tests.

---

## Phase 4: User Story 1 — Interactive User Login via Entra ID (Priority: P1)

**Goal**: Users open the test-ui and are redirected to Entra ID to sign in. After login, API calls include a bearer token.

**Independent Test**: Open test-ui → redirect to Entra ID → sign in → see display name → perform a search successfully.

### Implementation for US1

- [X] T020 [US1] Install `@azure/msal-browser` and `@azure/msal-angular` npm packages in `src/test-ui/`
- [X] T021 [P] [US1] Create `src/test-ui/src/environments/environment.ts` with production MSAL config (assembly redirect URI, API base URL, clientId, authority, scopes as placeholders)
- [X] T022 [P] [US1] Create `src/test-ui/src/environments/environment.development.ts` with dev MSAL config (localhost redirect URI, localhost API base URL, placeholder clientId/authority/scopes)
- [X] T023 [US1] Create `src/test-ui/src/app/core/auth/auth.config.ts` — export MSAL `PublicClientApplication` instance, `MsalGuardConfiguration`, and `MsalInterceptorConfiguration` using values from `environment.ts`
- [X] T024 [US1] Update `src/test-ui/src/app/app.config.ts` — add MSAL providers (`MsalModule.forRoot`, `MsalGuard`, `MsalInterceptor`, `MsalRedirectComponent`), switch to `provideHttpClient(withInterceptorsFromDi())`
- [X] T025 [US1] Update `src/test-ui/src/app/app.routes.ts` — add `MsalGuard` to search and entity-details routes, add `MsalRedirectComponent` route for redirect handling
- [X] T026 [US1] Update `src/test-ui/src/app/app.component.ts` and template — display signed-in user's display name in the application header bar (read from `MsalService.instance.getActiveAccount()`)
- [X] T027 [US1] Update `src/test-ui/angular.json` — add `fileReplacements` for environment files in production build configuration
- [X] T028 [US1] Update `src/bizscraper-ui-dockerfile` — add build args for MSAL config values (MSAL_CLIENT_ID, MSAL_AUTHORITY, MSAL_REDIRECT_URI, MSAL_SCOPES) and inject into environment files during build

**Checkpoint**: Users can sign in via Entra ID and make authenticated API calls from the test-ui.

---

## Phase 5: User Story 2 — System-to-System Authentication via Client Credentials (Priority: P1)

**Goal**: External systems call the API using client credentials tokens. No code changes beyond Phase 2/3 — this is validated by the token validation already in place.

**Independent Test**: Obtain a client credentials token using `curl`, call the API, verify success.

### Implementation for US2

- [X] T029 [US2] Verify that application tokens with `roles: ["BizScraper.Execute"]` are accepted by the existing authorization pipeline — covered by integration test in T019a (application token → 200); no additional code change expected; document verification in quickstart.md verification section

**Checkpoint**: System clients can authenticate via client credentials and call all protected endpoints.

---

## Phase 6: User Story 4 — User Sign-Out (Priority: P2)

**Goal**: Authenticated users can sign out, clearing local tokens and optionally ending the SSO session.

**Independent Test**: Sign in → click sign-out → verify tokens cleared → verify redirect to login.

### Implementation for US4

- [X] T030 [US4] Add sign-out button and logic to `src/test-ui/src/app/app.component.ts` — call `MsalService.logoutRedirect()` with `postLogoutRedirectUri` from environment config

**Checkpoint**: Users can sign out and are redirected to re-authenticate on next visit.

---

## Phase 7: User Story 5 — Health and Swagger Endpoints Remain Public (Priority: P2)

**Goal**: Health check and Swagger endpoints remain accessible without authentication.

**Independent Test**: Send unauthenticated GET to `/healthz` and `/swagger` — both return 200.

### Implementation for US5

- [X] T031 [US5] Verify that `.AllowAnonymous()` applied in T014 covers `/healthz`, `/ready`, `/swagger/**`, and root redirect — covered by integration test in T019a (anonymous → 200 for healthz/swagger); no additional code change expected; this task is a verification checkpoint

**Checkpoint**: K8s probes and developer tooling continue to function without tokens.

---

## Phase 8: Deployment & Configuration

**Purpose**: Helm chart updates and CI/CD configuration for deployed environments.

- [X] T032 [P] Update `deploy/helm/bizscraper/values.yaml` — add `azureAd` section with `instance`, `tenantId`, `clientId`, `audience` keys
- [X] T033 [P] Update `deploy/helm/bizscraper-ui/values.yaml` — add `msalConfig` section with `clientId`, `authority`, `redirectUri`, `postLogoutRedirectUri`, `scopes` keys
- [X] T034 Update Helm deployment template for bizscraper to inject `AzureAd__*` environment variables from values
- [X] T035 Update Helm deployment template for bizscraper-ui to pass MSAL build args during Docker build
- [X] T036 Update `deploy/pipelines/ci.yaml` — pass MSAL build args to the Angular Docker image build step

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, current-specs update, and final validation.

- [X] T037 [P] Update `src/test-ui/src/app/core/services/api-config.service.ts` — integrate with environment-based API base URL (fallback to environment.apiBaseUrl instead of hardcoded default)
- [X] T038 [P] Update `specs/current-specs/spec.md` — add Section for authentication describing Entra ID integration, protected/anonymous endpoints, and supported token flows
- [X] T039 [P] Update `specs/current-specs/contracts/` — add `authentication.md` contract from this feature
- [X] T040 [P] Update `specs/current-specs/data-model.md` — add CallerIdentity column to audit table definitions
- [X] T041 Update `README.md` — add authentication section with link to quickstart.md for app registration setup

---

## Dependencies

```
Phase 1 (Setup) ──────────▶ Phase 2 (Foundational) ──▶ Phase 3 (US3: Token Validation)
                                                        │
                                                        ├──▶ Phase 4 (US1: Interactive Login)
                                                        │                │
                                                        │                └──▶ Phase 6 (US4: Sign-Out)
                                                        │
                                                        ├──▶ Phase 5 (US2: Client Credentials — verification only)
                                                        │
                                                        └──▶ Phase 7 (US5: Public Endpoints — verification only)

Phase 4, 5, 7 ─────────────▶ Phase 8 (Deployment)
Phase 8 ────────────────────▶ Phase 9 (Polish)
```

### Parallel Execution Opportunities

| Tasks | Why Parallel |
|-------|-------------|
| T003, T004, T005 | Different files (appsettings, two SQL changelogs) |
| T007, T008 | Different entity files |
| T010a, T010b | Different test files |
| T017a, T018a | Different test files |
| T021, T022 | Different environment files |
| T032, T033 | Different Helm charts |
| T037, T038, T039, T040 | Different documentation files |

---

## Implementation Strategy

**MVP Scope**: Phases 1–3 (Setup + Foundation + Token Validation). After these phases, the API is secured and rejects unauthenticated requests. This is the minimum viable security increment.

**Incremental Delivery**:
1. **Increment 1** (Phases 1–3): API auth works — can be tested with `curl` + manually obtained tokens
2. **Increment 2** (Phase 4): UI auth works — interactive login flow complete
3. **Increment 3** (Phases 5–7): Client credentials verified, sign-out added, public endpoints confirmed
4. **Increment 4** (Phases 8–9): Deployed to AKS with Helm config, documentation updated

**Suggested MVP**: Complete Phases 1–4 to deliver a fully authenticated API + UI experience.
