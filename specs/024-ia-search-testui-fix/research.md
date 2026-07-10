# Research Phase: Fix Iowa Search and Test UI

**Feature**: 024-ia-search-testui-fix  
**Phase**: Phase 0 - Research & Discovery  
**Date**: 2026-04-01  
**Status**: Complete

---

## Research Summary

Root cause confirmed as stub definition files. All clarifications resolved via spec Q&A. Existing 022 contracts validated as adoption source. Model extension strategy finalized for both C# and TypeScript. No outstanding unknowns.

---

## 1. Root Cause Analysis

**Decision**: Stub definitions are the sole root cause  
**Rationale**: Both deployed Iowa definitions (`us-ia-business-search.json`, `us-ia-entity-details.json`) contain only a single placeholder `screenshot` action. The complete v2 definitions were authored in `specs/022-iowa-entity-search/contracts/` but never deployed to `src/BizScraper.Api/Infrastructure/Scraping/Definitions/`.  
**Alternatives considered**:
- Initially suspected `detailsUrl` parameter naming was a bug — code inspection revealed `ExecuteScriptHandler` pre-decodes `uniqueKey` and injects `detailsUrl` before definition execution. This is the established pattern, not a defect.
- Considered mapper bugs — `SearchResultMapper.MapIaResults` and `EntityDetailMapper.MapIaDetails` are functionally correct for the data shapes the 022 contracts produce.

---

## 2. Definition Adoption Strategy

**Decision**: Rewrite Iowa definitions in v2 engine format using live-site-validated structure  
**Rationale**: The 022 contracts use action types (`extract-table`, `extract-group`, `decode`, `check-selector`, `goto-action`, `set-value`, `assemble-object`) that **do not exist in the v2 scraping engine**. The engine only supports: `navigate`, `fill`, `click`, `wait-for-load`, `wait-for-condition`, `extract` (JS), `check-text`, `screenshot`, `download`, `loop`, `call-service`. The contracts must be rewritten to use these v2 action types with inline JavaScript extraction — matching the pattern used by CO, WY, and DE definitions.  
**Alternatives considered**:
- Adopt 022 contracts directly — **rejected** (engine will reject unknown action types at load time via `FlowDefinitionLoader` validation)
- Extend the engine to support 022 action types — **rejected** (YAGNI; the 022 format was a draft spec, not an engine-compatible format)
- Write only CSS selector updates — **rejected** (the entire contract structure is incompatible, not just selectors)

### Selector Validation Plan

The 022 contracts reference the following key selectors that must be validated against the live Iowa SOS site during implementation:

**Search page** (`us-ia-business-search.json`):
| Selector | Purpose | Confidence |
|----------|---------|------------|
| `input#BusinessName` | Search input field | 0.95 |
| `button#btnSearch` | Submit button | 0.95 |
| `table#gridSearchResults` | Results table | 0.90 |
| `table#gridSearchResults tbody tr` | Result rows | 0.90 |
| `a.next-page` | Pagination link | 0.90 |

**Detail page** (`us-ia-entity-details.json`):
| Selector | Purpose | Confidence |
|----------|---------|------------|
| `div.entity-header, div.detail-body` | Detail content container | 0.80 |
| `div.entity-name h1, span.legal-name` | Legal name | 0.80 |
| `span.business-number` | Identifier | 0.80 |
| `table.names-list tbody tr` | Names list rows | 0.80 |
| `div.registered-agent-name` | Agent name | 0.80 |

**Validation approach**: During implementation, use Playwright MCP to navigate the live Iowa SOS site, verify each selector exists, and update/add fallback selectors where needed.

**Live Validation (2026-04-01)**: Successfully validated via native Playwright MCP (`@playwright/mcp`). The Docker-based Playwright was blocked by Akamai CDN (reference `18.95623017`), but the native `npx` MCP server bypassed it using the host machine's network fingerprint.

### Selector Validation Results — CRITICAL UPDATES NEEDED

The Iowa SOS site has been redesigned since the 022 contracts were authored. **All CSS ID-based selectors are stale.** The page uses role-based accessible elements without explicit IDs.

**Search page** (`Search.aspx`):

| 022 Contract Selector | Live Site Reality | Status | Fix |
|---|---|---|---|
| `input#BusinessName` | `textbox "Business Name"` (placeholder: "Enter business name") | **STALE** — no `#BusinessName` ID | Use role: `textbox[name="Business Name"]` |
| `button#btnSearch` | `button "Submit search"` with text "Search" | **STALE** — no `#btnSearch` ID | Use role: `button[name="Submit search"]` |
| `table#gridSearchResults` | Generic `table` inside `article` (no ID) | **STALE** — no `#gridSearchResults` ID | Use table within `article` context |
| `a.next-page` | Numbered page links: `link "2"`, `link "3"`, etc. | **STALE** — no `.next-page` class | Use sequential numbered links |

**Results page** structure (confirmed):
- URL pattern: `results.aspx?q=<encrypted-token>`
- Heading: "Business Entities Results"
- Count text: "Results 1 - 25 of 153"
- Table columns: Business No. (link to `summary.aspx`), Name, Status, Type, Legal Name
- Pagination: Simple numbered links (1, 2, 3, 4, 5, 6, 7)
- Drill-down URL: `summary.aspx?q=<encrypted-token>` (NOT a clean URL with business number)

**Detail page** (`summary.aspx`) — **major structural difference from 022 assumptions**:

| 022 Contract Selector | Live Site Reality | Status | Fix |
|---|---|---|---|
| `div.entity-header, div.detail-body` | Table-based layout with header/value row pairs | **STALE** — no CSS class divs | Use `table` plus `columnheader` text matching |
| `div.entity-name h1, span.legal-name` | Cell in first table, second column | **STALE** | Extract from table row by column header text |
| `span.business-number` | Cell in first table, first column | **STALE** | Extract from table row by "Business No." header |
| `span.status` | Cell in first table, third column | **STALE** | Extract from table row by "Status" header |
| `table.names-list tbody tr` | Second table with heading "Names (Viewing N of M)" | **STALE** | Use table after `heading "Names"` |
| `div.registered-agent-name` | Third table with heading "Registered Agent or Reserving Party" | **STALE** | Use table after heading |
| `div.principal-office-address` | Fourth table with heading "Principal Office" | **STALE** | Use table after heading |

**Detail page confirmed data layout** (table-based, header/value row pairs):

```
Table 1:  [Business No.] [Legal Name] [Status]
          [635318]        [AACME LLC]  [Active]
          [Type]          [State of Inc.] [Modified]
          [Legal]         [IA]            [No]
          [Expiration Date] [Effective Date] [Filing Date]
          [PERPETUAL]       [6/23/2020]     [6/23/2020]
          [Chapter]
          [CODE 489 DOMESTIC LIMITED LIABILITY COMPANY]

Heading: "Names (Viewing 1 of 1)"
Table 2:  [Type] [Status] [Modified] [Name]
          [Legal] [Active] [No]      [AACME LLC]

Heading: "Registered Agent or Reserving Party"
Table 3:  [Full Name]  →  [MARK LEHR]
          [Address] [Address 2]  →  [3304 SOUTH HENNEPIN STREET] []
          [City, State, Zip]  →  [SIOUX CITY, IA, 51106]

Heading: "Principal Office"
Table 4:  [Full Name]  →  []
          [Address] [Address 2]  →  [3304 S. HENNEPIN ST.] []
          [City, State, Zip]  →  [SIOUX CITY, IA, 51106]
```

**Navigation tabs**: Address, Agent, Filings, Names, Officers, Protected Series, Stock, Summary
**Certificate**: "Print Certificate of Existence" link available (goes to `/business/cert/AddCertToCart.aspx?...` — paywall)

---

## 3. Model Extension Strategy

**Decision**: Add explicit Iowa-specific nullable fields to both C# and TypeScript models  
**Rationale**: Iowa detail responses include data (chapterCode, certificateNote, names list with type/status/modified) that doesn't map cleanly to existing generic fields. Overloading `jurisdiction` for `chapterCode` or `CertificateSection.error` for `certificateNote` loses semantic clarity and complicates UI rendering.  
**Alternatives considered**:
- Overload existing generic fields (jurisdiction, CertificateSection.error, PartyEntry) — rejected (loses semantic meaning, makes UI rendering depend on implicit field overloading logic)
- Add fields to TypeScript only — rejected (inconsistent API contract; C# serializes the wrong shape)
- Create separate Iowa-specific response model — rejected (violates unified entity pattern; YAGNI for a few extra fields)

### New C# Fields

**On `DetailSection`**:
```csharp
public string? ChapterCode { get; init; }
public string? CertificateNote { get; init; }
public IReadOnlyList<IowaNameEntry>? IowaNames { get; init; }
```

**New record `IowaNameEntry`**:
```csharp
public sealed record IowaNameEntry
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Status { get; init; }
    public bool Modified { get; init; }
}
```

**On `AppJsonSerializerContext`**:
```csharp
[JsonSerializable(typeof(IowaNameEntry))]
[JsonSerializable(typeof(IReadOnlyList<IowaNameEntry>))]
```

### New TypeScript Fields

**On `DetailSection`**:
```typescript
chapterCode?: string;
certificateNote?: string;
iowaNames?: IowaNameEntry[];
```

**New interface `IowaNameEntry`**:
```typescript
export interface IowaNameEntry {
  name: string;
  type: string;
  status: string;
  modified: boolean;
}
```

---

## 4. MapIaDetails Update Strategy

**Decision**: Update `MapIaDetails` to populate new explicit fields instead of overloading generic fields  
**Rationale**: Current mapper maps `chapterCode` → `Jurisdiction`, `certificateNote` → `CertificateSection.Error`, and names → `PartyEntry[]` with `Role=type`, `Address=status`. After model extension, these should populate the dedicated fields.  

### Current Mapping (to be updated)

| Source Field | Current Target | New Target |
|-------------|---------------|------------|
| `chapterCode` | `Details.Jurisdiction` | `Details.ChapterCode` |
| `certificateNote` | `Certificate.Error` | `Details.CertificateNote` |
| `names[].type` | `PartyEntry.Role` | `IowaNameEntry.Type` |
| `names[].status` | `PartyEntry.Address` | `IowaNameEntry.Status` |
| `names[].modified` | (lost) | `IowaNameEntry.Modified` |
| `names[].name` | `PartyEntry.Name` | `IowaNameEntry.Name` |

### Post-Update Mapping

- `Details.Jurisdiction` — set to `null` (Iowa doesn't have a traditional jurisdiction field)
- `Details.ChapterCode` — populated from `output.chapterCode`
- `Details.CertificateNote` — populated from `output.certificateNote`
- `Details.IowaNames` — populated from `output.names[]` as `IowaNameEntry[]`
- `Certificate` — set to `new CertificateSection { Available = false }` (paywall)
- `Parties` — set to empty (names moved to `IowaNames` dedicated field)

---

## 5. Test UI Rendering Strategy

**Decision**: Extend entity-details component template with Iowa-specific section  
**Rationale**: Iowa fields (`chapterCode`, `certificateNote`, `iowaNames`) need dedicated display sections with proper labels. The component already conditionally renders sections based on null checks.

### Template Additions

1. **Chapter/Code row** in detail section — rendered when `detail.details.chapterCode` is non-null
2. **Certificate Note** row — rendered when `detail.details.certificateNote` is non-null, displayed as informational notice
3. **Iowa Names List** table — rendered when `detail.details.iowaNames` is non-null and non-empty, columns: Name, Type, Status, Modified

---

## 6. Current-Specs Update Scope

**Decision**: Update `specs/current-specs/data-model.md` and `specs/current-specs/spec.md`  
**Rationale**: Constitution mandates global specification snapshot stays in sync (Design Standards > Global Specification Snapshot)  

### Changes to `specs/current-specs/data-model.md`

- Add `ChapterCode`, `CertificateNote`, `IowaNames` to `EntityDetailResult` table
- Add new `IowaNameEntry` model table
- Note that these are Iowa-specific nullable fields

### Changes to `specs/current-specs/spec.md`

- Update Iowa definition entries to reflect "complete" status (no longer stubs)
- Note explicit Iowa model fields in the unified entity detail response description

---

## 7. Existing Infrastructure Reuse

All required infrastructure exists from prior features:

- ✅ **v2 Execute-Script Handler** — `ExecuteScriptHandler.HandleAsync` with uniqueKey pre-processing
- ✅ **UniqueKey Encoder** — `US-IA` prefix registered in `KnownStates`
- ✅ **Search Result Mapper** — `MapIaResults` functional, encodes uniqueKey with `detailsUrl` param
- ✅ **Entity Detail Mapper** — `MapIaDetails` exists, needs update for new fields
- ✅ **Script Definition Engine** — v2 JSON-driven action orchestration
- ✅ **Diagnostic Screenshots** — Milestone-based capture throughout actions
- ✅ **Concurrency Management** — Per-state semaphore (1 concurrent IA request)
- ✅ **Playwright Browser Pool** — Pooled IBrowser instances with per-request BrowserContext
- ✅ **Test UI State Wiring** — IA in `StateCode` type, `STATE_SLUG_MAP`, `STATE_DETAIL_SLUG`
- ✅ **AppJsonSerializerContext** — Source-generated; needs `IowaNameEntry` registration
