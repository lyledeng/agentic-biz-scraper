# Data Model: Washington Business Entity Search

**Feature**: 032-wa-entity-search  
**Phase**: Phase 1 — Design  
**Date**: 2026-04-05  
**Alignment**: Unified v2 execute-script schema across CO/WY/IA/MO/WA

---

## Entity Models

### 1. WashingtonBusinessSearchResult

Represents a single business entity from the WA CCFS search results table.

```csharp
public record WashingtonBusinessSearchResult
{
    /// <summary>
    /// Business name from search results.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Unified Business Identifier (UBI) number assigned by WA SOS.
    /// Maps to: identifier (normalized across states).
    /// </summary>
    public string Identifier { get; init; }

    /// <summary>
    /// Business type (e.g., "WA PROFIT CORPORATION", "FOREIGN LIMITED LIABILITY COMPANY").
    /// </summary>
    public string EntityType { get; init; }

    /// <summary>
    /// Entity status (e.g., "ACTIVE", "TERMINATED", "ADMINISTRATIVELY DISSOLVED").
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Principal office street address as displayed in search results.
    /// </summary>
    public string? PrincipalOfficeAddress { get; init; }

    /// <summary>
    /// Registered agent name as displayed in search results.
    /// </summary>
    public string? RegisteredAgentName { get; init; }

    /// <summary>
    /// Base64URL-encoded drill-down reference.
    /// Contains: { "state": "US-WA", "params": { "businessId": "{id}", "businessType": "{type}" } }
    /// Used to retrieve full entity details via us-wa-entity-details.
    /// </summary>
    public string UniqueKey { get; init; }

    /// <summary>
    /// State code: "WA".
    /// </summary>
    public string State { get; init; } = "WA";

    /// <summary>
    /// Formation date. Null in search results (not displayed in WA CCFS search table).
    /// Populated in entity details.
    /// </summary>
    public string? FormationDate { get; init; }
}
```

---

### 2. WashingtonEntityDetails

Represents the full entity profile from the WA CCFS detail page.

```csharp
public record WashingtonEntityDetails
{
    /// <summary>
    /// General information section from the detail page.
    /// </summary>
    public WashingtonGeneralInfo Details { get; init; }

    /// <summary>
    /// Registered agent information.
    /// </summary>
    public WashingtonRegisteredAgent? RegisteredAgent { get; init; }

    /// <summary>
    /// Governors/officers associated with the entity.
    /// </summary>
    public WashingtonGovernor[] Governors { get; init; } = [];

    /// <summary>
    /// Filing history records with downloadable documents.
    /// </summary>
    public WashingtonFilingRecord[] Filings { get; init; } = [];
}
```

---

### 3. WashingtonGeneralInfo

General information from the detail page.

```csharp
public record WashingtonGeneralInfo
{
    /// <summary>
    /// Business entity name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Unified Business Identifier (UBI) number.
    /// </summary>
    public string UbiNumber { get; init; }

    /// <summary>
    /// Business type (e.g., "WA PROFIT CORPORATION").
    /// </summary>
    public string BusinessType { get; init; }

    /// <summary>
    /// Business status (e.g., "ACTIVE", "TERMINATED").
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Principal office street address.
    /// </summary>
    public string? PrincipalOfficeStreetAddress { get; init; }

    /// <summary>
    /// Principal office mailing address.
    /// </summary>
    public string? PrincipalOfficeMailingAddress { get; init; }

    /// <summary>
    /// Entity expiration date (if applicable).
    /// </summary>
    public string? ExpirationDate { get; init; }

    /// <summary>
    /// State/country of incorporation or organization.
    /// </summary>
    public string? Jurisdiction { get; init; }

    /// <summary>
    /// Date of formation or registration in Washington.
    /// </summary>
    public string? FormationDate { get; init; }

    /// <summary>
    /// Period of duration (e.g., "PERPETUAL").
    /// </summary>
    public string? PeriodOfDuration { get; init; }

    /// <summary>
    /// Date the entity became inactive (if applicable).
    /// </summary>
    public string? InactiveDate { get; init; }

    /// <summary>
    /// Nature of business description.
    /// </summary>
    public string? NatureOfBusiness { get; init; }

    // Nonprofit-specific fields (null for non-nonprofit entities)

    /// <summary>
    /// Whether the entity is a charitable corporation.
    /// </summary>
    public bool? CharitableCorporation { get; init; }

    /// <summary>
    /// Nonprofit Employer Identification Number.
    /// </summary>
    public string? NonprofitEin { get; init; }

    /// <summary>
    /// Whether most recent gross revenue is under $500K.
    /// </summary>
    public bool? GrossRevenueUnder500K { get; init; }

    /// <summary>
    /// Whether the entity has members.
    /// </summary>
    public bool? HasMembers { get; init; }

    /// <summary>
    /// Whether the entity has a public benefit designation.
    /// </summary>
    public bool? PublicBenefitDesignation { get; init; }

    /// <summary>
    /// Whether the entity is a host home.
    /// </summary>
    public bool? HostHome { get; init; }
}
```

---

### 4. WashingtonRegisteredAgent

Registered agent information.

```csharp
public record WashingtonRegisteredAgent
{
    /// <summary>
    /// Registered agent name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Registered agent street address.
    /// </summary>
    public string? StreetAddress { get; init; }

    /// <summary>
    /// Registered agent mailing address.
    /// </summary>
    public string? MailingAddress { get; init; }
}
```

---

### 5. WashingtonGovernor

A governor or officer associated with a business entity.

```csharp
public record WashingtonGovernor
{
    /// <summary>
    /// Governor title (e.g., "GOVERNOR", "PRESIDENT", "SECRETARY").
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Governor type: "INDIVIDUAL" or "ENTITY".
    /// </summary>
    public string GovernorType { get; init; }

    /// <summary>
    /// Entity name (populated when GovernorType is "ENTITY").
    /// </summary>
    public string? EntityName { get; init; }

    /// <summary>
    /// First name (populated when GovernorType is "INDIVIDUAL").
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Last name (populated when GovernorType is "INDIVIDUAL").
    /// </summary>
    public string? LastName { get; init; }
}
```

---

### 6. WashingtonFilingRecord

A filing history record with optional document downloads.

```csharp
public record WashingtonFilingRecord
{
    /// <summary>
    /// Filing number (e.g., "604975361").
    /// </summary>
    public string FilingNumber { get; init; }

    /// <summary>
    /// Filing date and time.
    /// </summary>
    public string? FilingDateTime { get; init; }

    /// <summary>
    /// Effective date of the filing.
    /// </summary>
    public string? EffectiveDate { get; init; }

    /// <summary>
    /// Filing type (e.g., "ANNUAL REPORT", "MERGER", "STATEMENT OF CHANGE").
    /// </summary>
    public string FilingType { get; init; }

    /// <summary>
    /// Documents associated with this filing.
    /// Empty if no documents or filing is beyond N-most-recent limit.
    /// </summary>
    public WashingtonFilingDocument[] Documents { get; init; } = [];
}
```

---

### 7. WashingtonFilingDocument

A downloadable document associated with a filing.

```csharp
public record WashingtonFilingDocument
{
    /// <summary>
    /// Document type (e.g., "ANNUAL REPORT - FULFILLED", "CONFIRMATION LETTER").
    /// </summary>
    public string DocumentType { get; init; }

    /// <summary>
    /// Document creation date.
    /// </summary>
    public string? CreatedDate { get; init; }

    /// <summary>
    /// Proxy URL to the downloaded document in cloud storage.
    /// Null if download failed.
    /// </summary>
    public string? StorageUrl { get; init; }

    /// <summary>
    /// Generated filename for the document.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Error description if document download or upload failed. Null on success.
    /// </summary>
    public string? Error { get; init; }
}
```

---

## Entity Relationships

```
WashingtonBusinessSearchResult (search)
  └─ uniqueKey ──→ WashingtonEntityDetails (detail drill-down)
                     ├── Details: WashingtonGeneralInfo (1:1)
                     ├── RegisteredAgent: WashingtonRegisteredAgent (1:1, nullable)
                     ├── Governors: WashingtonGovernor[] (1:N)
                     └── Filings: WashingtonFilingRecord[] (1:N)
                                    └── Documents: WashingtonFilingDocument[] (1:N per filing)
```

## Field Mapping: WA CCFS → Normalized Model

| WA CCFS Source | Model Field | Notes |
|----------------|-------------|-------|
| Search: Business Name column | `Name` | Business name |
| Search: UBI Number column | `Identifier` | Unified Business Identifier |
| Search: Business Type column | `EntityType` | Full description |
| Search: Status column | `Status` | e.g., "ACTIVE", "TERMINATED" |
| Search: Principal Office column | `PrincipalOfficeAddress` | Street address |
| Search: Registered Agent column | `RegisteredAgentName` | Agent name only |
| Search: businessID + businessType | `UniqueKey` | Base64url({ state: "US-WA", params: { businessId, businessType } }) |
| Detail: General info section | `Details.*` | All general info fields |
| Detail: Nonprofit section | `Details.CharitableCorporation`, etc. | 6 boolean/string fields, null for non-nonprofits |
| Detail: Registered Agent section | `RegisteredAgent.*` | Name, street, mailing |
| Detail: Governors table | `Governors[]` | Title, type, names |
| Detail: Filing History table | `Filings[]` | Filing number, dates, type |
| Detail: GetTransactionDocumentsList API | `Filings[].Documents[]` | Document type, created date |
| Detail: DownloadOnlineFilesByNumber API | `Documents[].StorageUrl` | Downloaded PDF → proxy URL |

## Mapping to Unified NormalizedSearchResult

| WashingtonBusinessSearchResult | NormalizedSearchResult | Notes |
|-------------------------------|----------------------|-------|
| `Name` | `Name` | Direct map |
| `Identifier` (UBI) | `Identifier` | Direct map |
| `Status` | `Status` | Direct map |
| `EntityType` | `EntityType` | Direct map |
| `FormationDate` | `FormationDate` | Null in search (not in WA table) |
| `UniqueKey` | `UniqueKey` | Direct map |
| `State` ("WA") | `State` | Direct map |
| N/A | `Event` | Null (WA search has no event column) |
| N/A | `StandingTax` | Null (WA-specific, not available) |
| N/A | `StandingRA` | Null (WA-specific, not available) |
| N/A | `RegistrationId` | Null |
