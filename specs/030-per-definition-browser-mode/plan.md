# Implementation Plan: Per-Definition Browser Mode Configuration

**Branch**: `030-per-definition-browser-mode` | **Date**: 2026-04-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/030-per-definition-browser-mode/spec.md`

## Summary

Extend the scraping engine to support per-definition browser mode overrides via an optional `browser` section in each definition JSON file. This enables definitions targeting anti-bot sites (Iowa) to use a local Chrome browser while all others continue using the remote Azure Playwright workspace. The single shared `IBrowser` instance in `GenericScriptScraper` is replaced with a dual-pool architecture — one pool for local-mode executions and one for remote-mode executions — each with independent concurrency limits (3 per pool, 6 total). The readiness health check is updated to report per-mode status.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS), `net10.0`  
**Primary Dependencies**: Microsoft.Playwright (v1.58.0), System.Text.Json (source-generated)  
**Storage**: N/A (no database changes for this feature)  
**Testing**: xUnit + NSubstitute for unit tests; end-to-end validation via manual Iowa search  
**Target Platform**: Linux container (Playwright noble base image with Google Chrome stable installed)  
**Project Type**: Web service (ASP.NET Core minimal API)  
**Performance Goals**: Concurrent local + remote executions without cross-mode interference; max 3 per pool  
**Constraints**: Chrome channel already installed in container; no additional base image changes required  
**Scale/Scope**: 8 existing definition JSON files; 2 (Iowa) get local mode override; 6 unchanged

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | **PASS** | Unit tests for BrowserConfig parsing, pool selection logic, health check breakdown |
| II. SOLID Design | **PASS** | Browser pool abstraction follows SRP; definition model extension is OCP-compliant |
| III. Quality Attributes | **PASS** | Observability: mode logged per execution (FR-014); Reliability: independent pools prevent cross-mode failures |
| IV. Versioning | **PASS** | No version bump needed (additive JSON schema change, backward-compatible) |
| V. Hands-Free Deployment | **PASS** | Per-definition config requires no code deployment to change browser mode |
| VI. Documentation | **PASS** | New `BrowserConfig` type documented; definition JSON schema updated; current-specs updated |
| Design Standards | **PASS** | Configuration follows twelve-factor (JSON config with sensible defaults); API contracts updated |
| Global Spec Snapshot | **PASS** | current-specs updated with §13 Per-Definition Browser Mode and BrowserConfig data model |

**Pre-Phase 0 Gate**: PASS — no violations.

### Post-Phase 1 Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | **PASS** | Test plan defined: BrowserConfig parsing, pool selection, health check breakdown |
| II. SOLID Design | **PASS** | `BrowserPool` extracted following SRP; `FlowDefinitionV2` extended via additive property (OCP) |
| III. Quality Attributes | **PASS** | Per-mode health check (Observability); dual pools (Reliability); mode logging (Supportability) |
| IV. Versioning | **PASS** | current-specs bumped to 1.6.2604.0; data-model to 1.2.2604.0 |
| V. Hands-Free Deployment | **PASS** | Operators switch browser mode via JSON file edits; no code deployment |
| VI. Documentation | **PASS** | BrowserConfig schema in contracts/; quickstart.md with usage examples; current-specs §13 added |
| Design Standards | **PASS** | Twelve-factor config; JSON Schema contract for browser config; API contract for health response |
| Global Spec Snapshot | **PASS** | current-specs/spec.md §13 added; data-model.md BrowserConfig/BrowserPool/ReadinessHealthResponse added |

**Post-Phase 1 Gate**: PASS — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/030-per-definition-browser-mode/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── browser-config-schema.json
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Common/Configuration/
│   └── PlaywrightOptions.cs                    # No changes (global defaults remain)
├── Infrastructure/Scraping/Engine/
│   ├── Models/
│   │   ├── FlowDefinitionV2.cs                 # Add BrowserConfig? property
│   │   └── FlowDefinitionV2JsonContext.cs       # Register BrowserConfig type
│   ├── GenericScriptScraper.cs                  # Dual-pool architecture
│   └── BrowserPool.cs                           # NEW: extracted browser pool lifecycle
├── Infrastructure/Scraping/Definitions/
│   ├── us-ia-business-search.json               # Add browser: { mode: "local", channel: "chrome" }
│   └── us-ia-entity-details.json                # Add browser: { mode: "local", channel: "chrome" }
├── Features/HealthCheck/
│   ├── HealthCheckEndpoints.cs                  # Per-mode health breakdown
│   └── HealthModels.cs                          # NEW: ReadinessHealthResponse, ModeStatus

tests/BizScraper.UnitTests/
├── Infrastructure/Scraping/Engine/
│   ├── BrowserConfigTests.cs                    # NEW: BrowserConfig parsing & validation
│   ├── BrowserPoolSelectionTests.cs             # NEW: pool routing by definition config
│   └── GenericScriptScraperTests.cs             # Updated: dual-pool scenarios
├── Features/HealthCheck/
│   └── HealthCheckEndpointsTests.cs             # Updated: per-mode response
```

**Structure Decision**: Existing vertical slice architecture. New `BrowserPool` class extracted from `GenericScriptScraper` to encapsulate browser lifecycle per pool. No new projects; all changes within `BizScraper.Api` and `BizScraper.UnitTests`.

## Complexity Tracking

No constitution violations. No justified complexity entries needed.
