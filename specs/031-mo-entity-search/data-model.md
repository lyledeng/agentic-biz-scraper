# Data Model: Missouri Business Entity Search

**Feature**: 031-mo-entity-search  
**Phase**: Phase 1 — Design  
**Date**: 2026-04-04  
**Alignment**: Unified v2 execute-script schema across CO/WY/IA/MO

---

## Entity Models

### 1. MissouriBusinessSearchResult

Represents a single business entity from the MO SOS search results table.

```csharp
public record MissouriBusinessSearchResult
{
    /// <summary>
    /// Business name from search results (primary/current name).
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Charter number assigned by Missouri SOS (e.g., "FL1088756", "00178791").
    /// Maps to: identifier (normalized across states).
    /// </summary>
    public string Identifier { get; init; }

    /// <summary>
    /// Entity type description (e.g., "Limited Liability Company (Foreign)",
    /// "Gen. Business - For Profit (Domestic)").
    /// </summary>
    public string EntityType { get; init; }

    /// <summary>
    /// Entity status (e.g., "Active", "Voluntarily Dissolved",
    /// "Admin Diss/Cancel - Report").
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Date the entity was created/registered, as displayed on the MO SOS site.
    /// Format: MM/DD/YYYY.
    /// </summary>
    public string? FormationDate { get; init; }

    /// <summary>
    /// Name of the registered agent associated with the entity.
    /// </summary>
    public string? RegisteredAgentName { get; init; }

    /// <summary>
    /// Former/previous business names associated with this entity.
    /// Collapsed from separate "(previous name)" rows in search results.
    /// Empty array if no previous names exist.
    /// </summary>
    public string[] PreviousNames { get; init; } = [];

    /// <summary>
    /// Base64URL-encoded drill-down reference.
    /// Contains: { "state": "MO", "id": "{entityId}" }
    /// Used to retrieve full entity details via us-mo-entity-details.
    /// </summary>
    public string UniqueKey { get; init; }

    /// <summary>
    /// State code: "MO".
    /// </summary>
    public string State { get; init; } = "MO";
}
```

---

### 2. MissouriEntityDetails

Represents the full entity profile from the MO SOS detail page (all three tabs).

```csharp
public record MissouriEntityDetails
{
    /// <summary>
    /// General information section from the detail page.
    /// </summary>
    public MissouriGeneralInfo Details { get; init; }

    /// <summary>
    /// Filed documents from the Filings tab.
    /// Each includes metadata and a downloaded PDF (storageUrl/error pattern).
    /// </summary>
    public MissouriFiledDocument[] Filings { get; init; } = [];

    /// <summary>
    /// Principal office addresses from the Principal Office Address tab.
    /// </summary>
    public MissouriPrincipalAddress[] Addresses { get; init; } = [];
}
```

---

### 3. MissouriGeneralInfo

General information from the detail page's first tab.

```csharp
public record MissouriGeneralInfo
{
    /// <summary>
    /// Current business entity name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Entity type (e.g., "Limited Liability Company").
    /// </summary>
    public string EntityType { get; init; }

    /// <summary>
    /// Charter number (e.g., "FL1088756").
    /// </summary>
    public string CharterNumber { get; init; }

    /// <summary>
    /// Domestic or Foreign.
    /// </summary>
    public string Domesticity { get; init; }

    /// <summary>
    /// Home state (e.g., "IL", "MO"). Only present for foreign entities.
    /// </summary>
    public string? HomeState { get; init; }

    /// <summary>
    /// Registered agent name (e.g., "SA Agent Missouri, Inc.").
    /// </summary>
    public string? RegisteredAgentName { get; init; }

    /// <summary>
    /// Registered agent full address (multi-line).
    /// </summary>
    public string? RegisteredAgentAddress { get; init; }

    /// <summary>
    /// Entity status (e.g., "Active").
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Date the entity was formed/registered (e.g., "9/29/2010").
    /// </summary>
    public string? DateFormed { get; init; }

    /// <summary>
    /// Duration of the entity (e.g., "Perpetual").
    /// </summary>
    public string? Duration { get; init; }
}
```

---

### 4. MissouriFiledDocument

A filing record from the Filings tab, with downloaded PDF.

```csharp
public record MissouriFiledDocument
{
    /// <summary>
    /// Filing action type (e.g., "Modify", "Creation").
    /// </summary>
    public string Action { get; init; }

    /// <summary>
    /// Document type description (e.g., "Change of Registered Agent Office Address",
    /// "Application for Registration of a Foreign Limited Liability Company").
    /// </summary>
    public string DocumentType { get; init; }

    /// <summary>
    /// Date filed (e.g., "07/02/2024").
    /// </summary>
    public string? DateFiled { get; init; }

    /// <summary>
    /// Effective date (e.g., "07/02/2024").
    /// </summary>
    public string? EffectiveDate { get; init; }

    /// <summary>
    /// Proxy URL to the downloaded PDF in cloud storage.
    /// Null if download failed.
    /// </summary>
    public string? StorageUrl { get; init; }

    /// <summary>
    /// Generated filename for the PDF (e.g., "change-of-registered-agent-07022024.pdf").
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Error description if PDF download or upload failed. Null on success.
    /// </summary>
    public string? Error { get; init; }
}
```

---

### 5. MissouriPrincipalAddress

A principal office address record.

```csharp
public record MissouriPrincipalAddress
{
    /// <summary>
    /// Address type (e.g., "Reg. Office", "Mailing").
    /// </summary>
    public string AddressType { get; init; }

    /// <summary>
    /// Full street address with city, state, ZIP.
    /// </summary>
    public string Address { get; init; }

    /// <summary>
    /// Date the address became effective (e.g., "7/2/2024").
    /// </summary>
    public string? Since { get; init; }
}
```

---

## Entity Relationships

```
MissouriBusinessSearchResult (search)
  └─ uniqueKey ──→ MissouriEntityDetails (detail drill-down)
                     ├── Details: MissouriGeneralInfo (1:1)
                     ├── Filings: MissouriFiledDocument[] (1:N)
                     └── Addresses: MissouriPrincipalAddress[] (1:N)
```

## Field Mapping: MO SOS → Normalized Model

| MO SOS Source | Model Field | Notes |
|---------------|-------------|-------|
| Search: Business Name column | `Name` | Primary/current name |
| Search: Charter No. column | `Identifier` | Normalized from `charterNumber` |
| Search: Type column | `EntityType` | Full description, e.g., "Limited Liability Company (Foreign)" |
| Search: Status column | `Status` | e.g., "Active", "Voluntarily Dissolved" |
| Search: Created column | `FormationDate` | MM/DD/YYYY format |
| Search: Registered Agent Name column | `RegisteredAgentName` | Agent name only (no address in search) |
| Search: (previous name) rows | `PreviousNames[]` | Collapsed from child rows sharing same entity ID |
| Search: detail link ID param | `UniqueKey` | Base64url({ state: "MO", id: "3021929" }) |
| Detail: General Info tab | `Details.*` | All general info fields |
| Detail: Filings tab rows | `Filings[]` | With PDF download → storageUrl/fileName/error |
| Detail: Principal Office tab rows | `Addresses[]` | Type + Address + Since date |
