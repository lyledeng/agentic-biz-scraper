# Implementation Plan: Test UI for BizScraper API

**Branch**: `010-test-ui` | **Date**: 2026-03-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/010-test-ui/spec.md`

## Summary

Build a standalone Angular 17 application (`src/test-ui/`) that provides a developer test harness for the BizScraper API. The UI uses the Wolters Kluwer Jumpstart design system (`@wk/components-v3-angular17`) and delivers three views: a configurable API settings panel, a business-entity search page, and an entity detail page with history documents and certificate of good standing. The app is containerised via a multi-stage Dockerfile (`src/bizscraper-ui-dockerfile`) using `node:22-alpine` build and `nginx:alpine` runtime, and is deployed to AKS via a dedicated Helm chart (`deploy/helm/bizscraper-ui/`). A CORS policy change is required on the BizScraper API.

## Technical Context

**Language/Version**: TypeScript 5 / Angular 17 (standalone components, signals)
**Primary Dependencies**: `@wk/components-v3-angular17`, `@angular/common/http`, Angular Router
**Storage**: None — stateless UI; API base URL stored in `localStorage` (user configurable)
**Testing**: Karma + Jasmine (unit); Cypress or Playwright (e2e, deferred to /speckit.tasks)
**Target Platform**: nginx:alpine container, AKS (Azure Kubernetes Service)
**Project Type**: Single-page application (Angular 17)
**Performance Goals**: Initial load <3 s on LAN; search results visible within API round-trip latency
**Constraints**: No authentication in UI; CORS must be resolved by API; read-only filesystem in container
**Scale/Scope**: Internal developer tool — single instance; ~15 Angular components, 3 services, 2 routes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First | **PASS** | Unit tests for services and components; Karma/Jasmine coverage for all service methods; E2E scenarios map 1:1 to spec acceptance criteria |
| II. SOLID Design | **PASS** | Single service per API resource (`SearchService`, `EntityDetailsService`); `ApiConfigService` isolates base-URL concern; Angular HTTP client injected, not instantiated directly |
| III. Architectural Quality | **PASS** | CSP and security headers in nginx.conf; non-root container user; structured client-side error handling; Angular ErrorHandler for observability |
| IV. Hybrid SemVer+CalVer | **PASS** | Helm chart `version: 1.0.2603.0`; Docker image tagged by CI pipeline |
| V. Hands-Free Deployment | **PASS** | Multi-stage Dockerfile; Helm chart with values-based config; CI builds and pushes image; no manual server steps |

**Gate Result**: PASS — No violations. Proceeding to Phase 0.

**Complexity Justified**:

| Item | Justification |
|------|--------------|
| Separate Angular project (not embedded in API) | FR-011 explicitly requires separation; embedding would couple release cycles and violate Single Responsibility |
| Jumpstart design system | User directive requires WK-compliant UI; WK Jumpstart is the approved component library for Angular projects |
| Separate Helm chart (`bizscraper-ui`) | Separate deployment lifecycle from API; independent scaling, rollback, and configuration |

## Project Structure

### Documentation (this feature)

```text
specs/010-test-ui/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output — consumed API shapes
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── bizscraper-dockerfile                    # EXISTING — API container
├── bizscraper-ui-dockerfile                 # NEW — Angular UI container
└── test-ui/                                 # NEW — Angular 17 application root
    ├── angular.json
    ├── package.json
    ├── tsconfig.json
    ├── tsconfig.app.json
    ├── nginx.conf                           # nginx runtime config with security headers
    └── src/
        ├── main.ts
        ├── app/
        │   ├── app.config.ts               # provideRouter, provideHttpClient, WK module imports
        │   ├── app.component.ts            # Chrome pattern host (banner-pds3, spine-pds3)
        │   ├── app.routes.ts               # /search (default), /entity-details
        │   ├── core/
        │   │   └── services/
        │   │       ├── api-config.service.ts        # reads/writes API base URL from localStorage
        │   │       ├── search.service.ts            # GET /api/v1/business-search
        │   │       └── entity-details.service.ts    # GET /api/v1/entity-details
        │   ├── features/
        │   │   ├── search/
        │   │   │   ├── search.component.ts          # search form + results grid
        │   │   │   └── search.component.html
        │   │   └── entity-details/
        │   │       ├── entity-details.component.ts  # detail view, history docs, certificate
        │   │       └── entity-details.component.html
        │   └── shared/
        │       ├── models/
        │       │   ├── business-search-result.model.ts
        │       │   └── entity-detail-response.model.ts
        │       └── components/
        │           ├── api-config-bar/              # configurable base URL input field (FR-016)
        │           │   ├── api-config-bar.component.ts
        │           │   └── api-config-bar.component.html
        │           ├── error-banner/                # user-friendly API error display (FR-013)
        │           │   ├── error-banner.component.ts
        │           │   └── error-banner.component.html
        │           └── loading-indicator/           # spinner/overlay during API calls (FR-012)
        │               ├── loading-indicator.component.ts
        │               └── loading-indicator.component.html
        └── assets/

deploy/
└── helm/
    ├── bizscraper/                          # EXISTING — API helm chart (no changes)
    └── bizscraper-ui/                       # NEW — Angular UI helm chart
        ├── Chart.yaml
        ├── values.yaml
        └── templates/
            ├── deployment.yaml
            ├── service.yaml
            ├── hpa.yaml
            └── ingress.yaml                 # NEW: UI needs ingress (API chart has none yet)

src/BizScraper.Api/
└── Program.cs                               # MODIFY: add CORS policy for test-ui origin
```

**Structure Decision**: The Angular SPA lives entirely under `src/test-ui/`, keeping it inside the existing monorepo workspace without polluting the .NET project tree. The naming convention `bizscraper-ui-dockerfile` follows the existing `bizscraper-dockerfile` pattern. The Helm chart is a separate chart under `deploy/helm/bizscraper-ui/` to allow independent deployments.

---

## Phase 0: Research Findings

*See [research.md](research.md) for full findings.*

### Key Decisions

| Decision | Rationale |
|----------|-----------|
| Angular 17 with standalone components and signals | Aligns with user directive; Angular 17 is the version targeted by `@wk/components-v3-angular17`; standalone removes NgModule boilerplate |
| Jumpstart Chrome pattern (fluid layout) | Data-heavy results grid suits fluid (not fixed/research) layout; `banner-pds3` + `spine-pds3` provide WK-compliant shell |
| `localStorage` for API base URL | Zero-setup persistence across page refreshes; no backend dependency; cleared by tester if needed |
| `nginx:alpine` runtime with custom `nginx.conf` | Security headers (CSP, HSTS, etc.) without a Node.js server in production image; read-only FS possible with tmpfs for nginx temp dirs |
| `node:22-alpine` build stage | LTS, small Alpine base; matches platform preference for Alpine images |
| CORS policy: named policy restricted to UI origin | `app.UseCors("TestUiPolicy")` on BizScraper API; origin value from `appsettings.json`; not wildcard |
| Single `bizscraper-ui` Helm chart | Independent release cycle from API; separate HPA, resource limits, ingress |

---

## Phase 1: Design

### Angular Application Architecture

The app has two primary routes:

| Route | Component | Purpose |
|-------|-----------|---------|
| `/` or `/search` | `SearchComponent` | Search form (FR-001/002/003/004/005) + results grid |
| `/entity-details` | `EntityDetailsComponent` | Detail view (FR-006/007/008/009/010) + back navigation (FR-015) |

**State management**: Signals (`signal()`, `computed()`) used for loading, error, and data state within each component. The search results are held in a `WritableSignal` on `SearchComponent` and passed to the router state when navigating to detail view — keeping the SPA stateless at the service layer.

**API configuration** (`ApiConfigService`) reads the base URL from `localStorage` on initialisation and exposes it as a signal. `ApiConfigBarComponent` (rendered in the banner) writes updates back via the service.

### Jumpstart Components Mapped to Features

| Feature | Jumpstart Component |
|---------|-------------------|
| App shell / layout | Chrome pattern: `banner-pds3`, `spine-pds3`, `brand-architecture-pds3` |
| Search form inputs | `text-field-pds3` (name), `dropdown-pds3` (state CO/WY) |
| Search button | `button-field-pds3` |
| Results grid | `table-pds3` (or data-table-pds3 if available; else native `<table>` with PDS classes) |
| Loading indicator | `spinner-pds3` / `loader-pds3` |
| Error messages | `banner-pds3` (inline error variant) |
| Entity detail fields | `card-pds3` sections |
| History document list | `accordion-pds3` or `<ul>` list items |
| Back navigation | `breadcrumb-pds3` |
| Certificate download link | `button-field-pds3` (anchor variant) |
| API base URL input | `text-field-pds3` rendered in banner area |

### CORS Configuration Change (BizScraper API)

**File**: `src/BizScraper.Api/Program.cs`

Add a named CORS policy before `app.Build()` with:
- `AllowedOrigin` loaded from `appsettings.json` → `Cors:TestUiOrigin`
- Default value: `http://localhost:4200` for local dev
- Production value: the AKS ingress URL of the UI (configured per environment via Helm/K8s config)

The policy allows: GET methods, the Content-Type and Accept headers. No credentials required (FR assumption: no auth).

**`appsettings.json` addition**:
```json
"Cors": {
  "TestUiOrigin": "http://localhost:4200"
}
```

### Docker: `src/bizscraper-ui-dockerfile`

Multi-stage build:

| Stage | Base | Purpose |
|-------|------|---------|
| `build` | `node:22-alpine` | `npm ci`, `ng build --configuration production` → `/dist/test-ui/browser` |
| `runtime` | `nginx:alpine` (latest stable) | Copy dist, copy `nginx.conf`, expose port 80 |

Security constraints applied at runtime stage:
- Non-root user (`nginx` user, UID 101)
- `nginx.conf` writes temp files to `/tmp` (mounted as `emptyDir` in K8s)
- `readOnlyRootFilesystem: true` enabled in Helm deployment template

### Helm Chart: `deploy/helm/bizscraper-ui/`

Mirrors the existing `bizscraper` chart structure:
- `Chart.yaml`: `name: bizscraper-ui`, `version: 1.0.2603.0`
- `values.yaml`: image repo, tag, service port 80, resource requests/limits, HPA config
- `templates/deployment.yaml`: standard deployment with security context
- `templates/service.yaml`: ClusterIP on port 80
- `templates/hpa.yaml`: CPU/memory-based autoscaling
- `templates/ingress.yaml`: nginx ingress matching API ingress pattern; TLS via cluster cert-manager

**Ingress matching logic**: The API is exposed under a path prefix (e.g. `/api/`) on the cluster ingress. The UI is served at the root path `/`. Both share the same host name with different path prefixes, matching the AKS ingress convention.

### Security headers (`nginx.conf`)

```
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header Permissions-Policy "geolocation=(), microphone=()" always;
add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' $API_ORIGIN $BLOB_ORIGIN;" always;
```

The `connect-src` directive must include the BizScraper API origin and the Azure Blob Storage origin (for PDF downloads opened in new tabs).

### Constitution Check (Post-Design)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | **PASS** | Karma/Jasmine unit tests for all services (mock HTTP); component tests for form validation; E2E defined in tasks |
| II. SOLID Design | **PASS** | Each service has a single responsibility; `ApiConfigService` isolated; no service knows about components |
| III. Architectural Quality | **PASS** | CSP, HSTS, non-root container, K8s securityContext enforced in Helm |
| IV. Hybrid SemVer+CalVer | **PASS** | Chart version `1.0.2603.0`, app version `1.0.2603.0` |
| V. Hands-Free Deployment | **PASS** | CI builds Docker image; Helm chart deployed by CD pipeline; no manual steps |

**Gate Result**: PASS — Design is constitution-compliant.

---

## Artifacts Generated

| Artifact | Path |
|----------|------|
| This plan | `specs/010-test-ui/plan.md` |
| Research | `specs/010-test-ui/research.md` |
| Data model | `specs/010-test-ui/data-model.md` |
| Quickstart | `specs/010-test-ui/quickstart.md` |
| API contracts | `specs/010-test-ui/contracts/` |
| Tasks | `specs/010-test-ui/tasks.md` — created by `/speckit.tasks` |
