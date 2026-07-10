# Data Model: Wyoming Entity Details Support

**Feature**: 007-wy-entity-details  
**Date**: 2026-03-25

## Entity: EntityDetailResult (MODIFIED)

Extends the existing CO entity detail record with nullable WY-specific fields.

| Field | Type | CO | WY | Source |
|-------|------|----|----|--------|
| Name | string | ✅ | ✅ | Details table / header |
| Status | string | ✅ | ✅ | Details table |
| FormationDate | string | ✅ | ✅ (as InitialFiling) | Details table |
| IdNumber | string | ✅ | ✅ (as FilingId) | Details table |
| Form | string | ✅ | ✅ (as Type) | Details table |
| PeriodicReportMonth | string? | ✅ | ❌ null | CO-specific |
| Jurisdiction | string | ✅ | ✅ (as FormedIn) | Details table |
| PrincipalOfficeStreetAddress | string? | ✅ | ✅ | Address section |
| PrincipalOfficeMailingAddress | string? | ✅ | ✅ | Address section |
| SubStatus | string? | ❌ null | ✅ | WY details |
| StandingTax | string? | ❌ null | ✅ | WY details |
| StandingRA | string? | ❌ null | ✅ | WY details |
| StandingOther | string? | ❌ null | ✅ | WY details |
| InactiveDate | string? | ❌ null | ✅ (if inactive) | WY details |
| TermOfDuration | string? | ❌ null | ✅ | WY details |
| FormedIn | string? | ❌ null | ✅ | WY details |
| LatestAnnualReportYear | string? | ❌ null | ✅ | WY Additional Details |
| AnnualReportExempt | string? | ❌ null | ✅ | WY Additional Details |
| LicenseTaxPaid | string? | ❌ null | ✅ | WY Additional Details |

**Mapping Notes**:
- CO `Jurisdiction` maps to WY `FormedIn` (same semantic concept)
- CO `Form` maps to WY `Type` (e.g., "Limited Liability Company - Domestic")
- CO `FormationDate` maps to WY `Initial Filing` date
- CO `IdNumber` maps to WY `Filing ID`
- WY `SubStatus` (e.g., "Current", "Archived") has no CO equivalent

## Entity: RegisteredAgentResult (UNCHANGED)

| Field | Type | CO | WY |
|-------|------|----|----|
| Name | string? | ✅ | ✅ |
| StreetAddress | string? | ✅ | ✅ |
| MailingAddress | string? | ✅ | ✅ |

## Entity: CertificateResult (UNCHANGED)

| Field | Type | CO | WY |
|-------|------|----|----|
| Available | bool | ✅ | ❌ (always false) |
| StorageUrl | string? | ✅ | ❌ null |
| FileName | string? | ✅ | ❌ null |
| Error | string? | ✅ | ❌ null |

**WY Note**: Certificate section is always null for WY. WY certificates are obtained via a separate `ViewCertificate.aspx` page and are out of scope.

## Entity: PartyResult (NEW)

Represents a party associated with a WY entity.

| Field | Type | Required | Source |
|-------|------|----------|--------|
| Name | string | ✅ | Parties section |
| Role | string | ✅ | Parsed from name suffix, e.g., "(Organizer)" |
| Organization | string? | ❌ | "Organization:" label if present |
| Address | string? | ❌ | "Address:" label content |

**Validation Rules**:
- Name must not be empty
- Role extracted from parenthetical suffix; if absent, default to "Unknown"
- Organization and Address are optional (some parties have neither)

## Entity: HistoryDocumentResult (NEW)

Represents a downloaded WY history filing document.

| Field | Type | Required | Source |
|-------|------|----------|--------|
| EventTitle | string | ✅ | History entry title (e.g., "2026 Original Annual Report - 14843526") |
| Date | string | ✅ | History entry date |
| StorageUrl | string? | ❌ | Azure Blob URL after upload |
| FileName | string? | ❌ | Saved filename |
| Error | string? | ❌ | If download/upload failed |

**Constraint**: Maximum 3 most recent documents downloaded per request.

## Entity: EntityDetailResponse (MODIFIED)

| Field | Type | CO | WY |
|-------|------|----|----|
| Details | EntityDetailResult | ✅ | ✅ |
| RegisteredAgent | RegisteredAgentResult? | ✅ | ✅ |
| Certificate | CertificateResult? | ✅ | ❌ null |
| Parties | PartyResult[]? | ❌ null | ✅ |
| HistoryDocuments | HistoryDocumentResult[]? | ❌ null | ✅ |

## Entity: EntityDetailScrapeResult (MODIFIED)

Internal scrape result returned by the scraper adapter.

| Field | Type | Added |
|-------|------|-------|
| Details | EntityDetailResult | Existing |
| RegisteredAgent | RegisteredAgentResult? | Existing |
| CertificateAvailable | bool | Existing |
| CertificateLocalPath | string? | Existing |
| Parties | PartyResult[]? | NEW |
| HistoryDocuments | HistoryDocumentResult[]? | NEW |

## Entity: EntityDetailAuditEntry (MODIFIED)

| Field | Type | Nullable | Added |
|-------|------|----------|-------|
| StateCode | NVARCHAR(10) | ✅ | NEW |

All other fields remain unchanged.

## Entity: GetEntityDetailsQuery (MODIFIED)

| Field | Type | Added |
|-------|------|-------|
| DetailsUrl | string | Existing |
| State | string | NEW (required) |

## State Transitions

N/A — Entity details are read-only queries with no state machine.

## Relationships

```
EntityDetailResponse
├── EntityDetailResult (1:1, required)
├── RegisteredAgentResult (1:0..1)
├── CertificateResult (1:0..1, CO only)
├── PartyResult[] (1:0..*, WY only)
└── HistoryDocumentResult[] (1:0..3, WY only)
```
