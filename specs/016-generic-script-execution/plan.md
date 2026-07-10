# Implementation Plan: Generic Script Execution Endpoint

**Branch**: `016-generic-script-execution` | **Date**: 2026-03-28 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/016-generic-script-execution/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Introduce a single generic `POST /api/v1/execute-script` endpoint that resolves a definition slug to a loaded JSON flow definition, validates required parameters from the nested `parameters` object, executes the scraping flow via the existing `ScrapingFlowEngine`, and returns results in a uniform response envelope (`definition`, `correlationId`, `truncated`, `data`). A `GET /api/v1/definitions` discovery endpoint lists available definitions and their required parameters. All existing dedicated endpoints are marked as deprecated in Swagger. The test UI is migrated to use the new generic endpoint. A new generic audit table records all executions. Per-definition-slug concurrency limits prevent resource exhaustion.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0, Swashbuckle.AspNetCore  
**Storage**: Azure SQL MI (audit trail), Azure Blob Storage (diagnostics), local filesystem (JSON flow definitions as content files)  
**Testing**: xUnit 2.9.3, NSubstitute 5.3.0, Testcontainers.MsSql 4.11.0, Microsoft.AspNetCore.Mvc.Testing 10.0.5  
**Target Platform**: Linux container (Playwright .NET runtime image), AKS via Helm  
**Project Type**: Web service (ASP.NET Core Minimal API)  
**Performance Goals**: Same latency as existing dedicated endpoints; <10% overhead from envelope wrapping  
**Constraints**: AOT-ready (source-generated JSON contexts), no reflection-based serialization, per-slug concurrency limits  
**Scale/Scope**: 5 existing flow definitions, 1 new generic endpoint, 1 discovery endpoint, 1 new audit table, 3 deprecated endpoints, 3 test-UI services migrated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for slug resolution, parameter validation, response envelope construction, concurrency limiting. Integration tests for full endpoint execution via WebApplicationFactory. Test-UI component tests updated. |
| II. SOLID Design | PASS | SRP: Generic handler has one responsibility (orchestrate definition execution). OCP: New definitions added without code changes. DIP: Handler depends on `FlowDefinitionLoader` and `ScrapingFlowEngine` abstractions. KISS: No speculative features. YAGNI: Only what the spec requires. |
| III. Quality Attributes | PASS | Reliability: Startup validation catches duplicate slugs. Resilience: Per-slug concurrency limits + existing Polly retry. Security: Input parameters validated against definition schema. Supportability: Correlation ID in every response. Observability: Metrics, structured logs. Auditability: New audit table. |
| IV. Versioning | PASS | Liquibase changeset `2.0.2603.6`. API versioned at `/api/v1/`. |
| V. Deployment | PASS | Zero manual steps. Definitions auto-discovered at startup. Liquibase migration applied automatically. |
| Design Standards | PASS | Source-generated JSON contexts for all new types. Typed errors. Problem Details format for all errors. |
| Quality Gates | PASS | All gates apply: test, static analysis, security, review, build. |

**Gate result: PASS** — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/016-generic-script-execution/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── api-contract.md  # Execute-script + definitions endpoint contract
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Features/
│   ├── ExecuteScript/                    # NEW: Generic execution feature slice
│   │   ├── Endpoints/
│   │   │   └── ExecuteScriptEndpoints.cs # POST /api/v1/execute-script + GET /api/v1/definitions
│   │   ├── Commands/
│   │   │   └── ExecuteScriptCommand.cs   # LiteBus command
│   │   ├── Handlers/
│   │   │   └── ExecuteScriptHandler.cs   # Orchestrates definition lookup, validation, execution, audit
│   │   ├── Models/
│   │   │   ├── ExecuteScriptRequest.cs   # { definition, parameters }
│   │   │   ├── ExecuteScriptResponse.cs  # { definition, correlationId, truncated, data }
│   │   │   └── DefinitionInfo.cs         # Discovery response model
│   │   ├── Exceptions/
│   │   │   ├── UnknownDefinitionException.cs
│   │   │   └── MissingParametersException.cs
│   │   ├── Logging/
│   │   │   └── ExecuteScriptLogging.cs   # [LoggerMessage] source-generated
│   │   ├── Metrics/
│   │   │   └── ExecuteScriptMetrics.cs   # OpenTelemetry counters/histograms
│   │   └── Validation/
│   │       └── ExecuteScriptValidator.cs # Parameter validation against definition variables
│   │
│   ├── BusinessSearch/
│   │   └── Endpoints/
│   │       └── BusinessSearchEndpoints.cs  # MODIFIED: Add deprecation markers
│   ├── EntityDetails/
│   │   └── Endpoints/
│   │       └── EntityDetailsEndpoints.cs   # MODIFIED: Add deprecation markers
│   └── GermanySearch/
│       └── Endpoints/
│           └── GermanySearchEndpoints.cs    # MODIFIED: Add deprecation markers (search only)
│
├── Infrastructure/
│   ├── Scraping/
│   │   ├── Engine/
│   │   │   ├── FlowDefinitionLoader.cs    # MODIFIED: Add slug index + GetDefinitionBySlug()
│   │   │   ├── GenericScriptScraper.cs     # NEW: Definition-agnostic scraper (browser management)
│   │   │   └── Models/
│   │   │       ├── FlowDefinitionV2.cs     # MODIFIED: Add DefinitionSlug to FlowMetadata
│   │   │       └── FlowDefinitionV2JsonContext.cs  # MODIFIED: Register new model types
│   │   └── Definitions/
│   │       ├── co-business-search.json     # MODIFIED: Add definitionSlug to metadata
│   │       ├── co-entity-details.json      # MODIFIED: Add definitionSlug to metadata
│   │       ├── de-business-search.json     # MODIFIED: Add definitionSlug to metadata
│   │       ├── wy-business-search.json     # MODIFIED: Add definitionSlug to metadata
│   │       └── wy-entity-details.json      # MODIFIED: Add definitionSlug to metadata
│   └── Persistence/
│       └── ScriptExecutionAuditRepository.cs  # NEW: Audit repository for generic executions
│
├── Middleware/
│   └── GlobalExceptionHandler.cs          # MODIFIED: Handle new exception types
│
└── Program.cs                             # MODIFIED: Register new services, map new endpoints

src/test-ui/src/app/
├── core/services/
│   └── execute-script.service.ts          # NEW: Generic endpoint service
├── features/
│   ├── search/
│   │   └── search.component.ts            # MODIFIED: Use execute-script service
│   ├── entity-details/
│   │   └── entity-details.component.ts    # MODIFIED: Use execute-script service
│   └── germany-search/
│       └── germany-search.component.ts    # MODIFIED: Use execute-script service
└── shared/models/
    ├── execute-script.model.ts            # NEW: Request/response type models
    └── definition-info.model.ts           # NEW: Discovery response model

tests/
├── BizScraper.UnitTests/
│   └── Features/
│       └── ExecuteScript/
│           ├── ExecuteScriptValidatorTests.cs
│           ├── ExecuteScriptHandlerTests.cs
│           └── FlowDefinitionLoaderSlugTests.cs
├── BizScraper.IntegrationTests/
│   └── Features/
│       └── ExecuteScript/
│           ├── ExecuteScriptEndpointTests.cs
│           └── ScriptExecutionAuditRepositoryTests.cs
└── BizScraper.EndToEndTests/
    └── Scenarios/
        └── ExecuteScript/
            └── ExecuteScriptScenarios.cs

deploy/liquibase/
├── masterfiles/
│   └── master-1.0.2603.0.yaml            # MODIFIED: Include new changeset
└── changelog/
    └── ddl/
        └── 2.0.2603.6-create-script-execution-audit-table.sql  # NEW
```

**Structure Decision**: Follows existing vertical slice architecture. The new `ExecuteScript` feature is a self-contained slice under `Features/`. `GenericScriptScraper` lives in the shared `Infrastructure/Scraping/Engine/` because it's a definition-agnostic browser management component used by the feature handler.

## Complexity Tracking

No complexity violations. All additions follow existing patterns:
- One new feature slice (same as BusinessSearch, EntityDetails, GermanySearch)
- One new Infrastructure scraper (same pattern as `JsonDrivenSearchScraper`)
- One new audit repository (same pattern as `AuditTrailRepository`)
- One new Liquibase DDL (same pattern as existing changesets)

## Phase 0 Outputs

See [research.md](research.md) for detailed research decisions:
- R1: Definition slug resolution via `metadata.definitionSlug` field
- R2: Uniform response envelope design
- R3: Per-slug concurrency management via `ConcurrentDictionary<string, SemaphoreSlim>`
- R4: Generic audit table with JSON column
- R5: Parameter mapping from request `parameters` to `ActionContext.Variables`
- R6: Deprecation via `.WithOpenApi(op => { op.Deprecated = true; })`
- R7: Test UI migration to `ExecuteScriptService`
- R8: Dual-index `FlowDefinitionLoader` (slug + state:endpoint)
- R9: `GenericScriptScraper` for definition-agnostic browser management

## Phase 1 Outputs

- [data-model.md](data-model.md) — Entity definitions, request/response models, audit table schema
- [contracts/api-contract.md](contracts/api-contract.md) — Full API contract with request/response examples and error scenarios
- [quickstart.md](quickstart.md) — curl examples for all operations

## Post-Phase 1 Constitution Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Test files identified for each new component. |
| II. SOLID Design | PASS | SRP maintained across handler, validator, scraper, audit. DIP via FlowDefinitionLoader. OCP: new definitions require zero code changes. |
| III. Quality Attributes | PASS | Auditability: New audit table. Observability: Metrics + logging. Security: Parameter validation at boundary. |
| IV. Versioning | PASS | Liquibase `2.0.2603.6`. |
| V. Deployment | PASS | Fully automated. |
| Design Standards | PASS | AOT-safe JSON contexts. Typed errors. Problem Details. |
| Quality Gates | PASS | All gates applicable. |

**Gate result: PASS** — Design is constitution-compliant. Ready for `/speckit.tasks`.
