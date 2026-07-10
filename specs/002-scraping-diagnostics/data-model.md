# Data Model: Scraping Diagnostics — Traces & Screenshots

**Feature**: 002-scraping-diagnostics  
**Date**: 2026-03-24

## Entities

### DiagnosticsOptions (configuration)

Configuration entity bound from `appsettings.json` → `Playwright:Diagnostics` section.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| TracingMode | `TracingMode` enum | `Always` | Controls when Playwright traces are captured: `Off`, `OnFailure`, `Always` |
| ScreenshotsEnabled | `bool` | `true` | Whether to capture milestone PNG screenshots during scraping |
| OutputPath | `string` | `"diagnostics"` | Relative or absolute path for diagnostic artifact output. Relative paths resolve from content root. |

### TracingMode (enum)

| Value | Numeric | Behavior |
|-------|---------|----------|
| Off | 0 | No tracing. No performance overhead. |
| OnFailure | 1 | Start tracing on every request; save only if the scraping session fails. Discard on success. |
| Always | 2 | Start tracing on every request; always save the trace archive. |

### Diagnostic Artifact (filesystem — not a persisted entity)

Diagnostic artifacts are files written to the output directory. They are not database entities.

| Attribute | Format | Example |
|-----------|--------|---------|
| Correlation ID | From `HttpContext.TraceIdentifier` | `abc123def` |
| Timestamp | UTC, ISO 8601 compact (`yyyyMMddTHHmmssfffZ`) | `20260324T143025123Z` |
| Milestone | Workflow step identifier | `trace`, `01-page-load`, `02-search-results`, `03-page-2`, `error` |
| Extension | File type | `.zip` (traces), `.png` (screenshots) |
| Full filename | `{correlationId}_{timestamp}_{milestone}.{ext}` | `abc123def_20260324T143025123Z_trace.zip` |

## Relationships

```
PlaywrightOptions
└── DiagnosticsOptions (nested configuration section)
    ├── TracingMode (enum value)
    ├── ScreenshotsEnabled (bool)
    └── OutputPath (string)
```

- `PlaywrightOptions` owns `DiagnosticsOptions` as a nested property (bound from `Playwright:Diagnostics` in `appsettings.json`).
- `PlaywrightBusinessEntityScraper` reads `DiagnosticsOptions` from the injected `IOptions<PlaywrightOptions>`.
- Diagnostic artifacts are correlated to HTTP requests via the correlation ID (from `CorrelationIdMiddleware`).

## Validation Rules

- `TracingMode` must be a valid enum value. Invalid config values fail fast at startup via options validation.
- `OutputPath` must not be null or empty. If relative, it is resolved against `IWebHostEnvironment.ContentRootPath`.
- `ScreenshotsEnabled` has no constraints (boolean).

## State Transitions

### Tracing Lifecycle (per browser context)

```
[Context Created] → [Tracing Started] → [Scraping Actions] → [Tracing Stopped]
                                                                    │
                                              ┌─────────────────────┤
                                              │                     │
                                         [Success]            [Failure]
                                              │                     │
                                   ┌──────────┴──────────┐    [Always Save]
                                   │                      │
                              [Mode=Always]         [Mode=OnFailure]
                              [Save trace]          [Discard trace]
```

### Screenshot Milestones (per scraping session)

```
[Navigate to search page] → capture "01-page-load.png"
[Submit search form]      → capture "02-search-results.png"
[Navigate to page N]      → capture "03-page-{N}.png" (repeated)
[Exception caught]        → capture "error.png" (always, regardless of config)
```
