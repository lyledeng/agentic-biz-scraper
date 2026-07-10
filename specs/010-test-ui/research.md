# Research: Test UI for BizScraper API

**Feature**: 010-test-ui
**Date**: 2026-03-26

## R1: Angular Version & Framework Strategy

**Decision**: Angular 17 with standalone components, signals API, and no NgModule wrappers.

**Rationale**: The user directive specifies Angular 17 and `@wk/components-v3-angular17`. Angular 17 introduced standalone components as the default and the signals API for fine-grained reactivity. Standalone components reduce boilerplate and align with Wolters Kluwer's current Angular 17 guidance. No NgModule usage means the `app.config.ts` file provides all framework features (`provideRouter`, `provideHttpClient`, WK module imports).

**Alternatives Considered**:
- Angular 16 with NgModules: Rejected — user explicitly requested Angular 17; older pattern adds unnecessary boilerplate.
- React with `@wk/components-v3-react16`: Rejected — user directive specifies Angular.
- Vue.js or Svelte: Rejected — no WK Jumpstart library exists for these.

---

## R2: Wolters Kluwer Design System — Jumpstart Component Selection

**Decision**: Use the Chrome pattern (`banner-pds3` + `spine-pds3` + `brand-architecture-pds3`) as the application shell. Use fluid layout variant (data-heavy content). Key components: `text-field-pds3`, `dropdown-pds3`, `button-field-pds3`, `spinner-pds3`, `card-pds3`, `breadcrumb-pds3`, `accordion-pds3`.

**Rationale**: The Chrome pattern provides out-of-the-box WK-compliant navigation shell without custom layout work. Fluid layout is specified for data-heavy applications (search results grid fits this category). All selected components are in the `@wk/components-v3-angular17` library.

**Component mapping confirmed**:
- `text-field-pds3` → search name input, API base URL input
- `dropdown-pds3` → state selector (CO / WY)
- `button-field-pds3` → Search button, Back button, document download link
- `spinner-pds3` → loading indicator during API calls
- `banner-pds3` (error variant inline) → error message display
- `card-pds3` → entity detail sections
- `breadcrumb-pds3` → "Back to Results" navigation crumb
- `accordion-pds3` → optional: collapsible history documents list

**Alternatives Considered**:
- Custom CSS grid without Jumpstart: Rejected — user directive requires WK-compliant site.
- PrimeNG or Angular Material: Rejected — not the WK standard.

---

## R3: API Base URL Configuration Strategy

**Decision**: `localStorage` key `bizscraper.apiBaseUrl` with a `ApiConfigService` Angular service. Default value: `https://localhost:8443`. Configurable at runtime via the `ApiConfigBarComponent` rendered in the banner area.

**Rationale**: No build-time environment files need to change per-environment. A tester switches the base URL without redeployment. `localStorage` persists across page refreshes within the same browser session. This satisfies FR-016 with zero backend dependencies.

**Alternatives Considered**:
- Angular environment files (`environment.ts`): Rejected — requires rebuild per environment; testers cannot override at runtime.
- URL query parameter: Rejected — ugly UX; URL sharing reveals internal API addresses.
- Cookie-based: Rejected — unnecessary complexity; no cross-tab sharing needed.

---

## R4: CORS Configuration on BizScraper API

**Decision**: Add a named `"TestUiPolicy"` CORS policy in `Program.cs`. The allowed origin is loaded from `appsettings.json → Cors:TestUiOrigin`. In development the default is `http://localhost:4200`. In production the value is the AKS ingress hostname of the UI (set via Helm values → K8s ConfigMap or environment variable injection).

**Rationale**: A named, origin-restricted policy is more secure than a wildcard (`AllowAnyOrigin()`). The value is externalised to configuration so no code changes are needed when the hostname changes. The policy only allows GET requests and the headers used by Angular HTTP client (`Content-Type`, `Accept`).

**CORS middleware placement**: `app.UseCors("TestUiPolicy")` must be placed **before** `app.UseRouting()` and the minimal API endpoint mapping to take effect on all responses including 4xx errors.

**Alternatives Considered**:
- Wildcard CORS (`AllowAnyOrigin`): Rejected — OWASP violation; unnecessarily broad for a known consumer.
- Separate reverse proxy to avoid CORS: Over-engineered for an internal test tool.

---

## R5: Docker Multi-Stage Build Strategy

**Decision**: Two stages in `src/bizscraper-ui-dockerfile`:
1. **`build`** stage: `node:22-alpine` — runs `npm ci` and `ng build --configuration production`. Output: `/dist/test-ui/browser/`.
2. **`runtime`** stage: `nginx:alpine` — copies dist output, copies `src/test-ui/nginx.conf`, exposes port 80.

**Rationale**: Follows the platform standard for Alpine-based, multi-stage Docker builds. The build stage includes all Node.js and Angular toolchain dependencies but none are copied to the runtime image. The nginx Alpine image is small (~5 MB) and production-grade.

**nginx.conf design**:
- `listen 80 default_server` — TLS termination at the AKS ingress, not at the container
- `root /usr/share/nginx/html` — Angular dist output location
- `try_files $uri $uri/ /index.html` — SPA fallback for Angular Router deep links
- Security headers added via `add_header` directives (see plan.md)
- `client_body_temp_path /tmp/nginx` and `proxy_temp_path /tmp/nginx` — enables read-only root filesystem

**Non-root user**: The nginx process runs as `nginx` user (UID 101, GID 101). The `USER nginx` directive is added before `CMD`.

**Alternatives Considered**:
- Node.js Express server at runtime: Rejected — heavier image, unnecessary for a static SPA.
- `httpd:alpine` (Apache): Viable but nginx is more widely used and documented for SPA hosting.
- Inline environment variable substitution via `envsubst`: Considered for API URL injection at container start — rejected in favour of `localStorage` approach (R3) which requires no per-environment image rebuild.

---

## R6: Helm Chart Design for AKS

**Decision**: Mirror the structure of `deploy/helm/bizscraper/` exactly. Add an `ingress.yaml` template (the API chart does not have one — traffic reaches the API via internal service-to-service; the UI needs an external-facing ingress).

**Ingress pattern from AKS cluster**:
- Annotation: `kubernetes.io/ingress.class: nginx`
- Annotation: `nginx.ingress.kubernetes.io/ssl-redirect: "true"`
- The UI is served at path prefix `/` on the cluster ingress host
- The API is served at path prefix `/api/` on the same host
- AKS Ingress uses HTTPS backend annotation for the API (`nginx.ingress.kubernetes.io/backend-protocol: HTTPS`); the UI backend uses plain HTTP (port 80, nginx terminates TLS at ingress)

**Resource sizing**: Minimal — `100m` CPU request, `128Mi` memory request. HPA enabled (1–3 replicas, 70% CPU threshold) matching API chart convention.

**Alternatives Considered**:
- Embedding the UI in the API Helm chart: Rejected — violates FR-011 (separate application); complicates rollback.
- Using a separate K8s namespace: Deferred — decision is infra/ops scope; not required for the plan.

---

## R7: Security Headers via nginx

**Decision**: Include the full OWASP-recommended header set in `nginx.conf`:
- `Strict-Transport-Security`: `max-age=31536000; includeSubDomains`
- `X-Content-Type-Options`: `nosniff`
- `X-Frame-Options`: `DENY`
- `Referrer-Policy`: `strict-origin-when-cross-origin`
- `Permissions-Policy`: camera, microphone, geolocation all denied
- `Content-Security-Policy`: `default-src 'self'`; `connect-src` includes API origin and Azure Blob Storage origin (for PDF direct-link from browser)

**CSP `connect-src` consideration**: History document PDFs are served directly from Azure Blob Storage URLs (the `storageUrl` field). When the browser opens these in a new tab (`window.open(url, '_blank')`), no `connect-src` directive is triggered (navigation, not XHR). However, if the UI were to do a pre-flight HEAD request, Blob Storage would need to be in `connect-src`. For safety, include `https://*.blob.core.windows.net` in `connect-src`.

**Alternatives Considered**:
- Setting headers in AKS Ingress annotations: Viable but couples security configuration to infra; nginx.conf approach keeps it in the container and is testable locally.

---

## R8: Angular HTTP Client Error Handling

**Decision**: Each service method returns an `Observable` and maps HTTP errors to a typed `ServiceError` interface via `catchError`. Components subscribe and populate an error signal for display in `ErrorBannerComponent`.

**Pattern**:
```typescript
// In service:
return this.http.get<SearchResult[]>(url).pipe(
  catchError((err: HttpErrorResponse) => {
    const message = err.status === 0
      ? 'Unable to reach the API. Check the base URL and try again.'
      : `API error ${err.status}: ${err.error?.detail ?? err.statusText}`;
    return throwError(() => ({ message }));
  })
);

// In component:
this.searchService.search(term, state).subscribe({
  next: (results) => { this.results.set(results); this.loading.set(false); },
  error: (err) => { this.error.set(err.message); this.loading.set(false); }
});
```

**Rationale**: User-friendly messages (FR-013) without exposing raw API error JSON. The `err.status === 0` case covers network failure / unreachable API.

**Alternatives Considered**:
- Global HTTP interceptor: More complex; interceptor would need access to component signal state. Per-service `catchError` is simpler and YAGNI-compliant.

---

## R9: Previous Search Request Cancellation

**Decision**: Use `switchMap` in the search stream for type-ahead or rapid re-submissions, and an `AbortController`-backed pattern for the search button click via `switchMap` on a `Subject<{ term, state }>`.

**Rationale**: Edge case from spec — "When the user searches while a previous search is still in progress, the previous request is superseded by the new one." `switchMap` automatically unsubscribes from the previous inner observable when a new value arrives.

**Alternatives Considered**:
- Manual `Subscription.unsubscribe()`: Fragile, error-prone in async contexts.
- `exhaustMap`: Ignores new emissions while current is in-flight — wrong behaviour for this requirement.
