# Research: Generic Script Execution Endpoint

**Feature**: 016-generic-script-execution  
**Date**: 2026-03-28

## R1: Definition Slug Resolution Strategy

**Decision**: Add `metadata.definitionSlug` field to `FlowMetadata` and each JSON definition file.  
**Rationale**: The current `FlowDefinitionLoader` keys definitions by `"{state}:{endpoint}"` (internal). The consumer-facing slug (e.g., `"co-business-search"`) is a separate identifier that maps cleanly to the internal key. Adding an explicit `definitionSlug` to metadata avoids deriving it from convention and allows future definitions that don't follow the `{state}-{endpoint}` pattern.  
**Alternatives considered**:
- Derive slug as `{state.ToLower()}-{endpoint}` — rejected because it couples the consumer API to internal metadata conventions and prevents non-state-based definitions.
- Use `metadata.id` (UUID) as identifier — rejected because UUIDs are not human-friendly for API consumers.

## R2: Response Envelope Design

**Decision**: Uniform JSON envelope: `{ "definition": "<slug>", "correlationId": "<id>", "truncated": <bool>, "data": <raw flow output> }`.  
**Rationale**: A predictable outer schema lets consumers parse metadata without knowing the definition type. The `data` field contains whatever the scraping flow outputs (arrays, objects, etc.), shaped by the definition's extract scripts and post-processors.  
**Alternatives considered**:
- Raw output with no envelope — rejected because there's no standard way to return correlationId or truncated flag.
- Definition-typed response objects — rejected because it requires per-definition mapping code, violating the "zero code changes for new definitions" goal.

## R3: Concurrency Management

**Decision**: One `SemaphoreSlim` per definition slug, keyed by slug string, with configurable max-concurrency defaulting to 1.  
**Rationale**: Per-slug semaphores prevent one definition type from starving another. The existing pattern (keyed `SemaphoreSlim` per state) is similar but coarser-grained.  
**Implementation**: Register a `ConcurrentDictionary<string, SemaphoreSlim>` as a singleton. On first request for a slug, lazily create a semaphore with the configured limit from `appsettings.json` section `ExecuteScript:ConcurrencyLimits:{slug}`, defaulting to 1.

## R4: Audit Trail Table Design

**Decision**: New table `dbo.ScriptExecutionAuditEntries` with common columns plus an `InputParameters` NVARCHAR(MAX) column for definition-specific JSON data.  
**Rationale**: A single generic table avoids coupling the audit schema to specific definition types. The JSON column accommodates any definition's parameters without schema changes.  
**Liquibase version**: `2.0.2603.6-create-script-execution-audit-table.sql`

## R5: Parameter Mapping from Request to Engine

**Decision**: The handler reads the `parameters` object from the request body and maps each key-value pair into the `ActionContext.Variables` dictionary. The `correlationId` variable is system-generated (from `HttpContext.TraceIdentifier`) and injected automatically — it is NOT expected from the consumer. Config-source variables (e.g., `searchEntryUrl`, `maxPages`) are populated from `PlaywrightOptions` as they are today.  
**Rationale**: The existing scrapers (`JsonDrivenSearchScraper`, `JsonDrivenEntityDetailScraper`) do exactly this mapping manually. The generic handler generalizes it by reading the definition's `variables` array and populating from the request `parameters` object for runtime-source variables and from configuration for config-source variables.

## R6: Deprecation Strategy for Existing Endpoints

**Decision**: Mark existing endpoints as deprecated in Swagger using `.WithMetadata(new ObsoleteAttribute("Use POST /api/v1/execute-script instead."))` and `.WithOpenApi(op => { op.Deprecated = true; return op; })`. Endpoints remain functional during the transition period.  
**Rationale**: Swagger's `deprecated: true` flag is the standard way to communicate deprecation to API consumers. The endpoints continue to work, giving consumers time to migrate.  
**Implementation**: Add `Swashbuckle.AspNetCore.Annotations` is not needed — `.WithOpenApi()` extension already available via `Microsoft.AspNetCore.OpenApi`.

## R7: Test UI Migration Strategy

**Decision**: Add a new `ExecuteScriptService` in the Angular test-ui that calls `POST /api/v1/execute-script`. Update all existing feature components (`search`, `entity-details`, `germany-search`) to use this new service instead of their dedicated services. The dedicated services remain but are no longer used by the components.  
**Rationale**: The test UI should exercise the same API path that production consumers will use. By switching the UI, we get immediate integration testing of the new endpoint.  
**Approach**: The `ExecuteScriptService` will wrap the response envelope, extracting the `data` field and passing it through to the existing components. This minimizes changes to the component templates.

## R8: FlowDefinitionLoader Extension for Slug Index

**Decision**: Add a second index `Dictionary<string, FlowDefinitionV2>` keyed by `definitionSlug` (case-insensitive). Expose a `GetDefinitionBySlug(string slug)` method alongside existing `GetDefinition(state, endpoint)`. Validate slug uniqueness at load time (throw on duplicate slugs).  
**Rationale**: Keeps backward compatibility with existing callers that use state+endpoint while adding slug-based lookup for the generic endpoint.

## R9: Browser Instance Sharing

**Decision**: The generic handler will create a new `JsonDrivenGenericScraper` (or similar) that mirrors the browser management pattern from `JsonDrivenSearchScraper` but is definition-agnostic. It uses the same browser pool and diagnostics infrastructure.  
**Rationale**: Each definition execution needs its own `BrowserContext` (isolation) but can share `IBrowser` instances. The existing scrapers are state-keyed singletons; the generic scraper can use a pooled browser approach keyed by the need for concurrency isolation.  
**Alternative**: Reuse existing keyed scrapers — rejected because it would still require per-definition registered services, defeating the purpose of genericity. Instead, a single generic scraper service handles all definitions.
