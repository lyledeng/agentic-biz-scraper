# Implementation Plan: Document Proxy Authenticated Download

**Branch**: `028-doc-proxy-auth` | **Date**: 2026-04-03 | **Spec**: `specs/028-doc-proxy-auth/spec.md`
**Input**: Feature specification from `/specs/028-doc-proxy-auth/spec.md`

## Summary

The test-ui document links (certificates, filing history PDFs, translated documents) currently use `<a [href]="dl.proxyUrl" target="_blank">` which opens a direct browser navigation to the API's document proxy endpoint. After Entra ID authentication was added (feature 025), these links return HTTP 401 because direct browser navigation does not include the bearer token — only Angular `HttpClient` requests pass through the MSAL interceptor.

**Fix**: Replace all direct `<a href>` document links with `HttpClient`-based PDF fetching. The component fetches the PDF as a blob via `HttpClient` (which triggers the MSAL interceptor to attach the token), creates a blob URL via `URL.createObjectURL()`, and renders it inline using an `<object>` tag on the entity-details page. Blob URLs are revoked via `OnDestroy` to prevent memory leaks. Errors show inline with a Retry button.

**Scope**: Angular test-ui only. No API changes required.

## Technical Context

**Language/Version**: TypeScript 5.x / Angular 17 (standalone components, signals)  
**Primary Dependencies**: `@angular/common/http` (HttpClient), `@azure/msal-angular` (MsalInterceptor), `@wk/components-v3-angular17` (Jumpstart PDS3)  
**Storage**: N/A (no backend data changes)  
**Testing**: Jasmine + Karma for unit tests  
**Target Platform**: Modern Chrome/Edge browsers  
**Project Type**: Frontend web application (Angular SPA)  
**Performance Goals**: PDF renders inline within 5 seconds of click (excluding network latency)  
**Constraints**: Must not break existing search/details flows; inline-only presentation (no new tab)  
**Scale/Scope**: 3 document link locations in entity-details template (certificates, document table, document table downloads)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Unit tests for DocumentProxyService and component behaviour |
| II. SOLID Design | ✅ PASS | Single-responsibility DocumentProxyService; component delegates fetching |
| III. Quality Attributes | ✅ PASS | Error handling with inline messages; blob URL cleanup prevents leaks |
| IV. Versioning | ✅ PASS | No version bump needed — UI-only fix, no API contract change |
| V. Hands-Free Deployment | ✅ PASS | Existing CI/CD pipeline handles Angular container |
| VI. Documentation | ✅ PASS | Service and component JSDoc; current-specs update included |

**Gate Result**: PASS — proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/028-doc-proxy-auth/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (no new contracts — existing document-proxy contract unchanged)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/test-ui/src/app/
├── core/
│   ├── auth/
│   │   └── auth.config.ts                    # Existing — MSAL config (no changes)
│   └── services/
│       ├── execute-script.service.ts          # Existing — no changes
│       └── document-proxy.service.ts          # NEW — HttpClient PDF blob fetcher
├── features/
│   └── entity-details/
│       ├── entity-details.component.ts        # MODIFY — add inline PDF viewer state, OnDestroy cleanup
│       ├── entity-details.component.html      # MODIFY — replace <a href> with click handlers + inline <object>
│       └── entity-details.component.css       # MODIFY — add inline viewer and error styles

tests/
└── BizScraper.UnitTests/                      # No backend test changes needed
```

**Structure Decision**: This is a UI-only change within the existing Angular test-ui. A new `DocumentProxyService` is added under `core/services/` following the existing `ExecuteScriptService` pattern. All template changes are in the existing `entity-details` component.

## Complexity Tracking

No constitution violations — no complexity justification needed.

## Post-Design Constitution Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Unit tests for DocumentProxyService (blob fetch, error mapping). Component tests for click → inline viewer → destroy cleanup. |
| II. SOLID Design | ✅ PASS | DocumentProxyService has single responsibility (PDF blob fetching). Component delegates to service. No inheritance abuse. |
| III. Quality Attributes | ✅ PASS | Error categorisation (401/404/502/504), memory cleanup (blob URL revocation), structured inline error display with retry. |
| IV. Versioning | ✅ PASS | No API version change. Current-specs updated to v1.4.2604.0. |
| V. Hands-Free Deployment | ✅ PASS | No pipeline changes. Existing Angular build/containerize flow handles the new service file. |
| VI. Documentation | ✅ PASS | Service JSDoc, component JSDoc, quickstart.md, current-specs snapshot updated. |

**Post-Design Gate Result**: PASS — proceed to `/speckit.tasks`.
