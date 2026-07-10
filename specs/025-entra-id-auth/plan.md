# Implementation Plan: Microsoft Entra ID Authentication

**Branch**: `025-entra-id-auth` | **Date**: 2026-04-02 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/025-entra-id-auth/spec.md`

## Summary

Add Microsoft Entra ID authentication to both the Angular test-ui (interactive login via MSAL with redirect) and the .NET backend API (JWT bearer token validation supporting both delegated and application/client-credentials flows). Single-tenant mode only. Record caller identity in audit trail. Keep health and Swagger endpoints public.

## Technical Context

**Language/Version**: .NET 10 / C# 14 (API), Angular 17 / TypeScript 5.4 (UI)
**Primary Dependencies**: Microsoft.Identity.Web (API), @azure/msal-browser + @azure/msal-angular (UI)
**Storage**: SQLite via Microsoft.Data.Sqlite (audit trail — add CallerIdentity column)
**Testing**: xUnit + NSubstitute (unit), Testcontainers (integration)
**Target Platform**: Azure AKS (Linux containers)
**Project Type**: Web API + SPA
**Performance Goals**: Token validation adds < 5ms overhead per request (cached signing keys)
**Constraints**: Single-tenant only; no auth bypass in any environment
**Scale/Scope**: All existing endpoints protected; 2 new NuGet packages, 2 new npm packages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Unit tests for auth middleware, integration tests for protected endpoints |
| II. SOLID Design | ✅ PASS | Auth is a cross-cutting concern via ASP.NET Core middleware pipeline; no SRP violations |
| III. Quality Attributes | ✅ PASS | Security (OWASP token validation), Observability (auth failure logging), Auditability (caller identity in audit trail) |
| IV. Hybrid SemVer+CalVer | ✅ PASS | Liquibase changelog `2.0.2604.0` for schema change |
| V. Hands-Free Deployment | ✅ PASS | Entra config via appsettings + Key Vault; no manual steps |
| VI. Documentation | ✅ PASS | App registration guide, Swagger OAuth config, API doc updates |

## Project Structure

### Documentation (this feature)

```text
specs/025-entra-id-auth/
├── plan.md                           # This file
├── research.md                       # Phase 0 output
├── data-model.md                     # Phase 1 output
├── quickstart.md                     # Phase 1 output — app registration guide
├── contracts/                        # Phase 1 output
│   └── authentication.md             # Auth contract documentation
└── tasks.md                          # Phase 2 output
```

### Source Code (repository root)

```text
src/
├── BizScraper.Api/
│   ├── BizScraper.Api.csproj         # + Microsoft.Identity.Web
│   ├── Program.cs                    # + AddAuthentication, AddAuthorization, UseAuthentication, UseAuthorization
│   ├── appsettings.json              # + AzureAd section
│   ├── Common/
│   │   └── Configuration/
│   │       └── AzureAdOptions.cs     # Typed config for Entra ID settings (REMOVED — Microsoft.Identity.Web provides its own)
│   ├── Features/
│   │   └── ExecuteScript/
│   │       └── Handlers/
│   │           └── ExecuteScriptHandler.cs  # Extract caller identity from ClaimsPrincipal
│   ├── Infrastructure/
│   │   └── Persistence/
│   │       ├── AuditTrailRepository.cs      # + CallerIdentity column
│   │       └── ScriptExecutionAuditRepository.cs  # + CallerIdentity column
│   └── Domain/
│       └── Entities/
│           ├── SearchAuditEntry.cs           # + CallerIdentity property
│           └── ScriptExecutionAuditEntry.cs  # + CallerIdentity property
│
├── test-ui/
│   ├── package.json                  # + @azure/msal-browser, @azure/msal-angular
│   ├── src/
│   │   ├── app/
│   │   │   ├── app.config.ts         # + MSAL providers, MsalGuard, MsalInterceptor
│   │   │   ├── app.routes.ts         # + MsalGuard on routes
│   │   │   ├── app.component.ts      # + user display name, sign-out button
│   │   │   └── core/
│   │   │       └── auth/
│   │   │           └── auth.config.ts   # MSAL configuration (clientId, tenantId, scopes)
│   │   └── environments/
│   │       ├── environment.ts              # Production (placeholder values)
│   │       └── environment.development.ts  # Dev (localhost redirect)

deploy/
├── helm/
│   ├── bizscraper/
│   │   └── values.yaml              # + azureAd section
│   └── bizscraper-ui/
│       └── values.yaml              # + MSAL config vars
├── liquibase/
│   ├── masterfiles/
│   │   └── master-1.0.2603.0.yaml   # + new changelog entry
│   └── changelog/
│       └── ddl/
│           ├── 2.0.2604.0-add-caller-identity-to-search-audit.sql
│           └── 2.0.2604.1-add-caller-identity-to-script-execution-audit.sql

tests/
├── BizScraper.UnitTests/
│   └── Features/
│       └── Authentication/
│           └── TokenValidationTests.cs
└── BizScraper.IntegrationTests/
    └── Features/
        └── Authentication/
            └── AuthenticatedEndpointTests.cs
```

**Structure Decision**: Auth is a cross-cutting concern; no new feature folder needed. Configuration goes in `appsettings.json` (API) and `environments/` (UI). MSAL config is isolated in `core/auth/`. DB schema gets two new ALTER TABLE changesets.

## Complexity Tracking

No constitution violations. No complexity justifications needed.

## Constitution Re-Check (Post-Design)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Plan includes unit tests for caller identity extraction, integration tests for auth middleware |
| II. SOLID | ✅ PASS | Auth via built-in ASP.NET Core middleware pipeline; no new abstractions needed |
| III. Quality Attributes | ✅ PASS | Security (JWT validation, single-tenant), Observability (auth failure logging), Auditability (CallerIdentity column) |
| IV. Versioning | ✅ PASS | Liquibase changelogs follow `2.0.2604.x` pattern |
| V. Hands-Free Deployment | ✅ PASS | Config via appsettings + Key Vault + Helm; no manual deployment steps |
| VI. Documentation | ✅ PASS | App registration guide (quickstart.md), contract doc, Swagger OAuth |
