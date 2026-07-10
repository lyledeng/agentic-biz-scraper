# Data Model: Iowa Business Entity Search

**Feature**: 022-iowa-entity-search  
**Phase**: Phase 1 - Design  
**Date**: 2026-04-01  
**Alignment**: Unified v2 execute-script schema across CO/WY/IA

---

## Entity Models

### 1. IowaBusinessSearchResult

Represents a single business entity from Iowa SOS search results.

```csharp
public record IowaBusinessSearchResult
{
    /// <summary>
    /// Iowa business registration number (e.g., "457975")
    /// Maps to: identifier (normalized across states)
    /// </summary>
    public string Identifier { get; init; }

    /// <summary>
    /// Business name from search results table
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Registration status: Active or Inactive
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Entity type: Legal or Fictitious name
    /// </summary>
    public string EntityType { get; init; }

    /// <summary>
    /// Legal entity name (may differ from Name in results)
    /// Iowa-specific field
    /// </summary>
    public string? LegalName { get; init; }

    /// <summary>
    /// Formation/registration date (extracted from Iowa detail page if available)
    /// Normalized to ISO 8601 format
    /// </summary>
    public string? FormationDate { get; init; }

    /// <summary>
    /// Base64URL-encoded drill-down reference
    /// Contains: state=IA, businessNumber=Identifier, encrypted URL
    /// Used to retrieve full entity details via execute-script
    /// </summary>
    public string UniqueKey { get; init; }

    /// <summary>
    /// State code: "IA"
    /// </summary>
    public string State { get; init; } = "IA";

    /// <summary>
    /// Iowa-specific classification
    /// Indicates whether search result row represents Legal or Fictitious name
    /// </summary>
    public string? SearchResultType { get; init; }
}
```

---

### 2. IowaEntityDetails

Represents comprehensive entity information retrieved via drill-down operation.

```csharp
public record IowaEntityDetails
{
    /// <summary>
    /// Iowa business registration number
    /// </summary>
    public string Identifier { get; init; }

    /// <summary>
    /// Legal entity name
    /// </summary>
    public string LegalName { get; init; }

    /// <summary>
    /// Registration status: Active or Inactive
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Entity type: Legal or Fictitious name
    /// </summary>
    public string EntityType { get; init; }

    /// <summary>
    /// State of incorporation (typically "IA")
    /// </summary>
    public string StateOfIncorporation { get; init; }

    /// <summary>
    /// Whether entity has been modified since filing
    /// </summary>
    public bool Modified { get; init; }

    /// <summary>
    /// Certificate/registration expiration date
    /// ISO 8601 format
    /// </summary>
    public string? ExpirationDate { get; init; }

    /// <summary>
    /// Date entity became effective
    /// ISO 8601 format
    /// </summary>
    public string EffectiveDate { get; init; }

    /// <summary>
    /// Original filing date
    /// ISO 8601 format
    /// </summary>
    public string FilingDate { get; init; }

    /// <summary>
    /// Legal code/chapter designation
    /// Iowa SOS classification (e.g., "CODE 489 DOMESTIC LIMITED LIABILITY COMPANY")
    /// </summary>
    public string ChapterCode { get; init; }

    /// <summary>
    /// List of all names associated with this entity
    /// Includes legal name, fictitious names, trade names
    /// </summary>
    public IReadOnlyList<IowaEntityName> Names { get; init; } = [];

    /// <summary>
    /// Registered agent information
    /// Primary contact for legal service of process
    /// </summary>
    public IowaRegisteredAgent RegisteredAgent { get; init; }

    /// <summary>
    /// Principal office address
    /// </summary>
    public IowaAddress PrincipalOffice { get; init; }

    /// <summary>
    /// Certificate of Existence availability note
    /// Explains paywall limitation and directs users to Web site
    /// Always set to: "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)"
    /// </summary>
    public string CertificateNote { get; init; } = "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)";

    /// <summary>
    /// State code: "IA"
    /// </summary>
    public string State { get; init; } = "IA";
}
```

---

### 3. IowaEntityName

Represents an entity name variant (legal, fictitious, trade name, etc.).

```csharp
public record IowaEntityName
{
    /// <summary>
    /// Name type: Legal, Fictitious, etc.
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// Name active status
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Whether this name has been modified
    /// </summary>
    public bool Modified { get; init; }

    /// <summary>
    /// The name text
    /// </summary>
    public string Name { get; init; }
}
```

---

### 4. IowaRegisteredAgent

Represents the registered agent (responsible party) for the entity.

```csharp
public record IowaRegisteredAgent
{
    /// <summary>
    /// Full name of registered agent individual or organization
    /// </summary>
    public string FullName { get; init; }

    /// <summary>
    /// Street address (first line)
    /// </summary>
    public string Address { get; init; }

    /// <summary>
    /// Street address (second line, if applicable)
    /// </summary>
    public string? Address2 { get; init; }

    /// <summary>
    /// City, state, and ZIP code combined
    /// Format: "CITY, STATE, ZIP"
    /// </summary>
    public string CityStateZip { get; init; }
}
```

---

### 5. IowaAddress

Represents an Iowa SOS entity address (principal office, business address, etc.).

```csharp
public record IowaAddress
{
    /// <summary>
    /// Street address (first line)
    /// </summary>
    public string Address { get; init; }

    /// <summary>
    /// Street address (second line, if applicable)
    /// </summary>
    public string? Address2 { get; init; }

    /// <summary>
    /// City, state, and ZIP code combined
    /// Format: "CITY, STATE, ZIP"
    /// </summary>
    public string CityStateZip { get; init; }
}
```

---

### 6. IowaSearchResponse

Response envelope for Iowa search operation via execute-script.

```csharp
public record IowaSearchResponse
{
    /// <summary>
    /// Correlation ID for tracing this operation
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Script definition name
    /// Always: "us-ia-business-search"
    /// </summary>
    public string Definition { get; init; }

    /// <summary>
    /// Array of business entity results
    /// </summary>
    public IReadOnlyList<IowaBusinessSearchResult> Data { get; init; } = [];

    /// <summary>
    /// Total number of entities returned in this response
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total count reported by Iowa SOS (may exceed page limit)
    /// </summary>
    public int SiteTotalCount { get; init; }

    /// <summary>
    /// Whether results were truncated due to page limit
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Number of pages scraped
    /// </summary>
    public int PagesScraped { get; init; }

    /// <summary>
    /// Original search term
    /// </summary>
    public string SearchTerm { get; init; }

    /// <summary>
    /// State code: "IA"
    /// </summary>
    public string State { get; init; } = "IA";

    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// URL to diagnostic artifacts (screenshots, logs)
    /// Stored in Azure Blob Storage
    /// </summary>
    public string? DiagnosticsUrl { get; init; }
}
```

---

### 7. IowaDetailResponse

Response envelope for Iowa entity detail operation via execute-script.

```csharp
public record IowaDetailResponse
{
    /// <summary>
    /// Correlation ID for tracing this operation
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Script definition name
    /// Always: "us-ia-business-search" with uniqueKey parameter
    /// </summary>
    public string Definition { get; init; }

    /// <summary>
    /// Complete entity detail object
    /// </summary>
    public IowaEntityDetails Data { get; init; }

    /// <summary>
    /// State code: "IA"
    /// </summary>
    public string State { get; init; } = "IA";

    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// URL to diagnostic artifacts (screenshots, logs)
    /// Stored in Azure Blob Storage
    /// </summary>
    public string? DiagnosticsUrl { get; init; }
}
```

---

## Validation Rules

### Search Result Validation

- **Identifier**: Non-null, non-empty, numeric string
- **Name**: Non-null, non-empty, max 255 characters
- **Status**: Must be "Active" or "Inactive"
- **EntityType**: Must be "Legal" or "Fictitious name"
- **LegalName**: Optional; if present, max 255 characters
- **UniqueKey**: Base64URL-encoded, non-empty

### Detail Validation

- **Identifier**: Must match search result identifier
- **LegalName**: Non-null, non-empty, max 255 characters
- **Status**: Must be "Active" or "Inactive"
- **EntityType**: Must be "Legal" or "Fictitious name"
- **StateOfIncorporation**: Must be "IA"
- **Names**: At least one name entry required
- **RegisteredAgent**: Must contain full name and address
- **PrincipalOffice**: Must contain address
- **CertificateNote**: Must contain paywall message

---

## Mapping to Normalized v2 Schema

| Iowa Field | Normalized Field | Mapping Rule |
|-----------|------------------|--------------|
| Business Number | `identifier` | Direct pass-through |
| Business Name | `name` | Direct pass-through |
| Registration Status | `status` | Direct pass-through |
| Entity Type | `entityType` | Direct pass-through |
| Legal Name | `legalName` | Optional; Iowa-specific extension |
| Filing Date | `formationDate` | ISO 8601 format |
| States of Inc. | `stateOfIncorporation` | Detail-only field |
| Registered Agent | `registeredAgent` | Detail-only field |
| Principal Office | `principalOffice` | Detail-only field |
| Certificate Note | `certificateNote` | Detail-only field |

---

## Notes

- All date fields use ISO 8601 format (YYYY-MM-DD)
- All collection fields default to empty collections (not null)
- Certificate limitation is explicitly documented (not omitted)
- Iowa-specific fields (`legalName`, `searchResultType`) are used only for Iowa results; not present in CO/WY responses
- Base64URL encoding used for `uniqueKey` to ensure URL-safe transport; decoded by execute-script handler for drill-down
