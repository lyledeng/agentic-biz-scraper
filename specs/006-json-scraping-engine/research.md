# Research: JSON-Driven Scraping Engine

**Feature**: 006-json-scraping-engine  
**Date**: 2026-03-25

## 1. Project Rename: CososWebScraping → BizScraper

### Decision
Rename all project artifacts from `CososWebScraping` to `BizScraper`.

### Rationale
- "CososWebScraping" was coined for Colorado-only SOS scraping. The API now supports multiple states (CO, WY) and is becoming a generic scraping engine.
- "BizScraper" is concise, descriptive (business entity scraping), and state-agnostic.
- Shorter names reduce verbosity in namespace chains (e.g., `BizScraper.Api.Infrastructure.Scraping.Engine` vs `CososWebScraping.Api.Infrastructure.Scraping.Engine`).

### Alternatives Considered
- **SosWebScraping**: Still ties to "Secretary of State" terminology; some states use different department names.
- **BusinessEntityScraper**: Accurate but verbose for namespace prefixes.
- **EntityScraping**: Ambiguous outside business context.

### Rename Scope (~500+ references)
| Category | Files | Pattern |
|----------|-------|---------|
| Solution file | `CososWebScraping.slnx` → `BizScraper.slnx` | File rename + content |
| API project | `src/CososWebScraping.Api/` → `src/BizScraper.Api/` | Directory + .csproj + AssemblyName + RootNamespace |
| Unit tests | `tests/CososWebScraping.UnitTests/` → `tests/BizScraper.UnitTests/` | Directory + .csproj + ProjectReference |
| Integration tests | `tests/CososWebScraping.IntegrationTests/` → `tests/BizScraper.IntegrationTests/` | Directory + .csproj + ProjectReference |
| E2E tests | `tests/CososWebScraping.EndToEndTests/` → `tests/BizScraper.EndToEndTests/` | Directory + .csproj + ProjectReference |
| Dockerfile | `src/cososwebscraping-dockerfile` → `src/bizscraper-dockerfile` | File rename + content |
| CI pipeline | `deploy/pipelines/ci.yaml` | Content (solution name, dockerfile ref, image tag) |
| Helm chart | `deploy/helm/cososwebscraping/` → `deploy/helm/bizscraper/` | Directory + Chart.yaml + values.yaml |
| appsettings | `src/*/appsettings.json` | OpenTelemetry ServiceName |
| C# namespaces | ~50 source files, ~20 test files | `namespace CososWebScraping.*` → `namespace BizScraper.*` |
| Using statements | Same files | `using CososWebScraping.*` → `using BizScraper.*` |
| Error type URIs | Middleware files + OpenAPI spec | `cososwebscraping.local` → `bizscraper.local` |
| Liquibase changesets | 4 SQL files | Author field `cososwebscraping` → `bizscraper` |
| InternalsVisibleTo | API .csproj | Assembly name references |
| OpenTelemetry meter | EntityDetailsMetrics.cs, BusinessSearchMetrics.cs | Meter name string literals |
| README.md | Root | All command examples and descriptions |
| Constitution | `.specify/memory/constitution.md` | Project name in governance section |
| Copilot instructions | `.github/agents/copilot-instructions.md` | Header |
| Spec markdown files | `specs/*/` | Project references in documentation |
| Swagger title | Program.cs | "CO SOS Business Entity Search API" → "BizScraper API" |

### Implementation Approach
Execute the rename as the first task (before engine work) so all subsequent code is written against the new names. Use automated find-and-replace with manual verification, then validate with a full build + test run.

---

## 2. JSON Flow Definition Schema Design

### Decision
Use a flat action-list schema with typed action discriminator, inline conditionals, and loop blocks. Schema version is a required top-level field.

### Rationale
- A flat list with action-type discriminator is the simplest model that covers all three existing flows.
- Loops and conditionals are represented as action blocks (not nested state machines) to keep the schema shallow and readable.
- Variable substitution uses `${variableName}` syntax—familiar from shell/template languages and unambiguous in JSON string values.
- The schema version field supports forward compatibility per FR-016.

### Alternatives Considered
- **State machine model** (nodes + transitions): More powerful but far more complex than needed for 3 linear-with-branches flows. Rejected per YAGNI.
- **YAML instead of JSON**: Better readability for humans but adds a dependency and doesn't integrate as naturally with C# `System.Text.Json`.
- **External DSL / scripting language**: Maximum flexibility but introduces a runtime interpreter, security concerns, and steep learning curve. Rejected per KISS.

### Schema Structure (Conceptual)
```
FlowDefinition
├── schemaVersion: integer (required)
├── name: string
├── state: string (e.g., "CO", "WY")
├── endpoint: string (e.g., "business-search", "entity-details")
├── variables: [{ name, source }]  // source: "runtime" | "config" | "extracted"
├── actions: Action[]
└── output: { variableName, type }

Action (discriminated by "type" field)
├── type: "navigate" | "fill" | "click" | "wait-for-load" | "wait-for-condition" | "extract" | "check-text" | "screenshot" | "download" | "loop" | "call-service"
├── [type-specific params]: selector, url, script, timeout, etc.
├── screenshot?: { milestone: string, force: boolean }
├── condition?: { type: "element-exists" | "text-contains" | "variable-truthy", ... }
├── storeAs?: string  // capture action output into named variable
└── (for loops):
    ├── loopType: "pagination" | "retry"
    ├── maxIterations: integer
    ├── terminateWhen: condition
    └── actions: Action[]  // nested action list
```

### Post-Processor Registry
- Extract actions may specify `postProcessor: "wyoming-result-parser"` (a named string).
- The engine resolves this at startup via a dictionary of `IPostProcessor<TInput, TOutput>` implementations registered in DI.
- This keeps complex parsing (e.g., Wyoming label-value extraction) in testable C# while allowing simple extractions to return structured JS output directly.

---

## 3. Action Type Inventory

### Decision
11 action types cover all three existing scraper flows. No additional types needed.

| Action Type | Parameters | Used By |
|------------|------------|---------|
| `navigate` | url, waitUntil | CO, WY, Details |
| `fill` | selector, value | CO, WY |
| `click` | selector, waitAfter | CO, WY, Details |
| `wait-for-load` | state (networkidle, domcontentloaded) | CO, WY, Details |
| `wait-for-condition` | javascript, timeoutMs | WY |
| `extract` | javascript, storeAs, postProcessor? | CO, WY, Details |
| `check-text` | text (or pattern), onMatch (throw/skip/branch) | CO |
| `screenshot` | milestone, force | CO, WY, Details |
| `download` | linkSelector, filenamePattern, validateExtension | Details |
| `loop` | loopType, maxIterations, terminateWhen, actions[] | CO, WY, Details |
| `call-service` | serviceName, inputVariable, outputVariable | WY |

### Rationale
Derived directly from the existing scraper code. Each Playwright method call or logical step maps to exactly one action type. No speculative types added (YAGNI).

---

## 4. Engine Architecture Pattern

### Decision
A single `ScrapingFlowEngine` class that accepts a `FlowDefinition` and an `IPage`, then walks the action list sequentially, dispatching each to a type-specific `IActionHandler`.

### Rationale
- Strategy pattern for action handlers keeps the engine open for extension (new action types) and closed for modification (Open/Closed principle).
- The engine owns flow-level concerns: variable bag, error capture, diagnostics wrapping.
- Action handlers own action-level concerns: Playwright API calls, wait strategies, extraction.

### Alternatives Considered
- **Visitor pattern**: Actions accept a visitor that executes them. More formal but adds indirection without benefit for a sequential walk.
- **Pipeline/middleware**: Actions as middleware in a pipeline. Overkill for a linear sequence; conditionals and loops don't fit the middleware model well.

---

## 5. Flow Definition File Location & Loading

### Decision
JSON files stored as embedded content files at `Infrastructure/Scraping/Definitions/*.json`, loaded at DI startup via `IConfiguration` or direct file read, and validated against a JSON schema.

### Rationale
- Content files are copied to output on build, making them available at runtime without embedding in the assembly.
- Startup validation (FR-014) catches errors before any request is served.
- Files are co-located with the scraping infrastructure for discoverability.

### Alternatives Considered
- **Embedded resources**: Harder to update post-build; conflicts with SC-002 (operator can change without recompilation).
- **External config directory**: More operational flexibility but out of scope (no hot-reload).
- **Database storage**: Adds unnecessary persistence dependency for 3 static definitions.

---

## 6. Migration & Cleanup Strategy

### Decision
Implement the engine first, then migrate each flow one at a time (CO search → WY search → CO details). After each migration, run comparison tests. After all three pass, remove old scraper classes.

### Rationale
- Sequential migration limits blast radius—each flow can be verified independently.
- Comparison tests (SC-001) validate output equivalence before removing the old code.
- The old scraper classes are not exposed via public API, so removal is safe after verification.

---

## 7. Diagnostics Integration

### Decision
The engine wraps each flow execution in `ExecuteWithDiagnosticsAsync` (existing method), and individual actions trigger `CaptureScreenshotAsync` when their definition includes a screenshot milestone.

### Rationale
- Reuses existing diagnostics infrastructure without changes (FR-009, FR-010).
- Screenshot milestones are declared in the JSON definition, matching the current milestone naming per flow.
- Trace capture is automatic at the flow level, not per-action.
