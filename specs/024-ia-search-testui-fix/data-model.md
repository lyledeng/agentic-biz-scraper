# Data Model: Fix Iowa Search and Test UI

**Feature**: 024-ia-search-testui-fix  
**Phase**: Phase 1 - Design  
**Date**: 2026-04-01  
**Alignment**: Extend unified v2 models with explicit Iowa fields; replace stub definitions

---

## Model Changes

This feature extends existing models rather than introducing new domain entities. The primary change is adding Iowa-specific nullable fields to `DetailSection` and `UnifiedEntityDetailResponse`, plus a new `IowaNameEntry` record for the structured names list.

---

### 1. DetailSection — Extended Fields

Add three nullable properties to the existing `DetailSection` record.

**File**: `src/BizScraper.Api/Common/Models/DetailSection.cs`

```csharp
// Existing 19 properties unchanged. Add:

/// <summary>
/// Iowa SOS legal code/chapter designation
/// (e.g., "CODE 489 DOMESTIC LIMITED LIABILITY COMPANY").
/// Null for non-Iowa entities.
/// </summary>
public string? ChapterCode { get; init; }

/// <summary>
/// Iowa certificate of existence paywall note.
/// Always set to the standard paywall message for Iowa entities; null for others.
/// </summary>
public string? CertificateNote { get; init; }

/// <summary>
/// Iowa-specific names list (legal, fictitious, trade names with type/status/modified).
/// Null for non-Iowa entities. Iowa entities use this instead of the generic Parties list.
/// </summary>
public IReadOnlyList<IowaNameEntry>? IowaNames { get; init; }
```

---

### 2. IowaNameEntry — New Record

New record for structured Iowa names list entries.

**File**: `src/BizScraper.Api/Common/Models/IowaNameEntry.cs`

```csharp
namespace BizScraper.Api.Common.Models;

/// <summary>
/// Represents an Iowa business entity name variant (legal, fictitious, trade name).
/// Extracted from the Iowa SOS entity detail page names table.
/// </summary>
public sealed record IowaNameEntry
{
    /// <summary>
    /// The name text (e.g., "ACME CORPORATION").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Name classification: Legal, Fictitious, Trade, etc.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Name active status: Active or Inactive.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Whether this name has been modified since original filing.
    /// </summary>
    public bool Modified { get; init; }
}
```

---

### 3. AppJsonSerializerContext — Registration

Add source-generated serialization for the new record.

**File**: `src/BizScraper.Api/Common/Serialization/AppJsonSerializerContext.cs`

```csharp
// Add to existing attributes:
[JsonSerializable(typeof(IowaNameEntry))]
[JsonSerializable(typeof(IReadOnlyList<IowaNameEntry>))]
```

---

### 4. EntityDetailMapper.MapIaDetails — Updated Mapping

**File**: `src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs`

Update the `MapIaDetails` method to populate new explicit fields instead of overloading generic fields.

| Source (definition output) | Previous Target | New Target |
|---------------------------|----------------|------------|
| `output.chapterCode` | `Details.Jurisdiction` | `Details.ChapterCode` |
| `output.certificateNote` | `Certificate.Error` | `Details.CertificateNote` |
| `output.names[].name` | `PartyEntry.Name` | `IowaNameEntry.Name` |
| `output.names[].type` | `PartyEntry.Role` | `IowaNameEntry.Type` |
| `output.names[].status` | `PartyEntry.Address` (!) | `IowaNameEntry.Status` |
| `output.names[].modified` | (lost) | `IowaNameEntry.Modified` |

Post-update behavior:
- `Details.Jurisdiction` → `null` (Iowa has no traditional jurisdiction field)
- `Details.ChapterCode` → populated from `output.chapterCode`
- `Details.CertificateNote` → populated from `output.certificateNote`
- `Details.IowaNames` → populated from `output.names[]` as `IowaNameEntry[]`
- `Certificate` → `new CertificateSection { Available = false }` (paywall)
- `Parties` → empty list (names moved to dedicated `IowaNames` field)

---

### 5. TypeScript Model Extensions

**File**: `src/test-ui/src/app/shared/models/unified-entity.model.ts`

```typescript
// Add to DetailSection interface:
chapterCode?: string;
certificateNote?: string;
iowaNames?: IowaNameEntry[];

// New interface:
export interface IowaNameEntry {
  name: string;
  type: string;
  status: string;
  modified: boolean;
}
```

---

## Definition File Changes

### 6. us-ia-business-search.json — Replace Stub

**Source**: `specs/024-ia-search-testui-fix/contracts/us-ia-business-search.json` (newly authored in v2 format)  
**Target**: `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json`

Replace the current stub (1 screenshot action) with the new v2 definition containing 10 actions: navigate → wait-for-condition → fill → click → extract (JS) → extract pagination → loop (extract next page info → click → extract page results). Selectors validated against live Iowa SOS site via native Playwright MCP.

**Note**: The 022 contract definitions could NOT be adopted directly — they use action types (`extract-table`, `extract-group`, `decode`, `check-selector`, `goto-action`, `set-value`, `assemble-object`) not supported by the v2 engine. These definitions were rewritten from scratch in the v2 format.

### 7. us-ia-entity-details.json — Replace Stub

**Source**: `specs/024-ia-search-testui-fix/contracts/us-ia-entity-details.json` (newly authored in v2 format)  
**Target**: `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-entity-details.json`

Replace the current stub (1 screenshot action) with the new v2 definition containing 5 actions: navigate → wait-for-condition → screenshot → extract (single JS function extracts all fields from table-based layout) → screenshot. The extraction JS handles the Iowa SOS header/value row-pair table layout and extracts names, registered agent, principal office from separate tables identified by h2 headings.

---

## Test UI Component Changes

### 8. entity-details.component — Iowa Rendering

**File**: `src/test-ui/src/app/features/entity-details/entity-details.component.ts` and `.html`

Add conditional rendering for Iowa-specific detail fields:

| Field | Display Condition | Rendering |
|-------|------------------|-----------|
| `chapterCode` | non-null | Row in detail section: "Chapter/Code" label |
| `certificateNote` | non-null | Informational notice banner (warn style) |
| `iowaNames` | non-null, non-empty | Table: Name, Type, Status, Modified columns |

---

## Current-Specs Updates

### 9. specs/current-specs/data-model.md

Add to `EntityDetailResult` table:

| Field | Type | Description |
|-------|------|-------------|
| ChapterCode | string? | Iowa legal code/chapter (Iowa-specific) |
| CertificateNote | string? | Certificate paywall note (Iowa-specific) |
| IowaNames | IowaNameEntry[]? | Iowa names list (Iowa-specific) |

Add new `IowaNameEntry` model table:

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Name text |
| Type | string | Name classification |
| Status | string | Active status |
| Modified | bool | Whether modified since filing |

### 10. specs/current-specs/spec.md

Update Iowa definition entries to note "complete" status (definitions fully deployed, not stubs).

---

## Validation Rules

### IowaNameEntry Validation

- **Name**: Non-null, non-empty, max 255 characters
- **Type**: Non-null, non-empty (expected: "Legal", "Fictitious", "Trade")
- **Status**: Non-null, non-empty (expected: "Active", "Inactive")
- **Modified**: Boolean, defaults to `false`

### DetailSection Iowa Fields

- **ChapterCode**: Nullable; when present, non-empty string
- **CertificateNote**: Nullable; when present, contains standard paywall message
- **IowaNames**: Nullable; when present, non-empty list of valid `IowaNameEntry` items
