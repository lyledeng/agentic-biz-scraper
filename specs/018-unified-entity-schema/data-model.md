# Data Model: Unified Entity Schema

**Feature**: 018-unified-entity-schema  
**Date**: 2026-03-29

## Entity Relationship Overview

```
UnifiedSearchResult        UnifiedEntityDetailResponse
┌────────────────┐         ┌──────────────────────────┐
│ name           │         │ details: DetailSection    │
│ identifier     │         │ registeredAgent: Agent?   │
│ status         │         │ certificate: Cert?        │
│ entityType     │         │ parties: Party[]?         │
│ formationDate  │         │ documents: DocEntry[]?    │
│ state          │         └──────────────────────────┘
│ event?         │                    │
│ uniqueKey ─────┼─── decodes to ────┤
│ standingTax?   │          ┌────────┴────────┐
│ standingRA?    │          │   DocEntry       │
│ registeredOffice? │       │ title            │
└────────────────┘          │ date?            │
                            │ downloads: DlRef[]│
                            └─────────┬────────┘
                                      │
                              ┌───────┴───────┐
                              │ DownloadRef    │
                              │ label          │
                              │ proxyUrl?      │
                              │ fileName       │
                              │ error?         │
                              └────────────────┘
```

## Entities

### UnifiedSearchResult

A single row from a business entity search. Common shape across all jurisdictions.

| Field | Type | Required | Source | Notes |
|-------|------|----------|--------|-------|
| `name` | string | Yes | All | Entity legal name. DE maps from `companyName`. |
| `identifier` | string | Yes | All | Canonical ID: SOS ID (CO), Filing ID (WY), Court Registration ID (DE). |
| `status` | string | Yes | All | Entity status. DE: `aktuell`/`gelöscht`. CO/WY: `Good Standing`, `Active`, etc. |
| `entityType` | string | No | CO, WY | Entity form (Corporation, LLC, etc.). DE: `null`. |
| `formationDate` | string? | No | CO, WY | MM/DD/YYYY. DE: `null` (not available from Handelsregister). |
| `state` | string | Yes | All | Two-letter jurisdiction code: `CO`, `WY`, `DE`. |
| `event` | string? | No | CO | CO: last event (e.g., "Report Filed"). WY/DE: `null`. |
| `uniqueKey` | string | Yes | Computed | Base64url-encoded JSON containing all parameters for entity detail lookup. Replaces `detailsUrl`. |
| `standingTax` | string? | No | WY | WY: tax standing ("Good", "Delinquent"). CO/DE: `null`. |
| `standingRA` | string? | No | WY | WY: registered agent standing. CO/DE: `null`. |
| `registeredOffice` | string? | No | DE | DE: city of registration (e.g., "München"). CO/WY: `null`. |

**Validation rules**:
- `name`, `identifier`, `status`, `state`, `uniqueKey` must be non-empty strings.
- `state` must be one of: `CO`, `WY`, `DE`.
- `uniqueKey` must be a valid base64url string decodable by the server.

### UniqueKey (encoded, not a standalone entity)

An opaque, self-contained token encoding all parameters for a standalone entity detail or document lookup.

| Encoded Field | Type | Notes |
|---------------|------|-------|
| `state` | string | Routing key: `US-CO`, `US-WY`, `DE-DE` |
| `params` | object | State-specific parameters (see research.md for per-state breakdown) |

### UnifiedEntityDetailResponse

Top-level response envelope for entity detail lookups. Contains five nullable sections.

| Section | Type | CO | WY | DE |
|---------|------|----|----|----|
| `details` | DetailSection | Populated | Populated | Populated (basic info from search) |
| `registeredAgent` | AgentSection? | Populated | Populated | `null` |
| `certificate` | CertificateSection? | Populated (if available) | Populated (if available) | `null` |
| `parties` | PartyEntry[]? | `null` | Populated | `null` |
| `documents` | DocumentEntry[]? | `null` | Populated (history filings) | Populated (original + translated hardcopy) |

### DetailSection

Core entity information. Present for all jurisdictions.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | Yes | Entity legal name |
| `identifier` | string | Yes | Same value as search result `identifier` |
| `status` | string | Yes | Current entity status |
| `formationDate` | string? | No | MM/DD/YYYY or null (DE) |
| `entityType` | string? | No | Entity form or null (DE) |
| `jurisdiction` | string? | No | Jurisdiction of formation |
| `principalAddress` | string? | No | Principal office street address |
| `mailingAddress` | string? | No | Principal office mailing address |
| `periodicReportMonth` | string? | No | CO-specific |
| `subStatus` | string? | No | WY-specific |
| `standingTax` | string? | No | WY-specific |
| `standingRA` | string? | No | WY-specific |
| `standingOther` | string? | No | WY-specific |
| `inactiveDate` | string? | No | WY-specific |
| `termOfDuration` | string? | No | WY-specific |
| `formedIn` | string? | No | WY-specific |
| `latestAnnualReportYear` | string? | No | WY-specific |
| `annualReportExempt` | string? | No | WY-specific |
| `licenseTaxPaid` | string? | No | WY-specific |
| `registeredOffice` | string? | No | DE-specific (city) |

### AgentSection

Registered agent information.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string? | No | Agent name |
| `streetAddress` | string? | No | Street address |
| `mailingAddress` | string? | No | Mailing address |

### CertificateSection

Certificate of good standing.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `available` | boolean | Yes | Whether a certificate is available |
| `downloads` | DownloadReference[]? | No | Download references when available |
| `error` | string? | No | Error message if retrieval failed |

### PartyEntry

A party (officer, director, agent) associated with the entity.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | Yes | Person or organization name |
| `role` | string | Yes | Role (Organizer, Manager, Director, etc.) |
| `organization` | string? | No | Organization name |
| `address` | string? | No | Address |

### DocumentEntry

A single document associated with an entity (WY filing or DE hardcopy).

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `title` | string | Yes | Document title. WY: `eventTitle`. DE: "Current Hardcopy Printout". |
| `date` | string? | No | Date string (MM/DD/YYYY or null). |
| `downloads` | DownloadReference[] | Yes | One or more downloadable files for this document. |

### DownloadReference

A single downloadable file within a document entry.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `label` | string | Yes | Human-readable label: "Filing Document", "Original", "Translated" |
| `proxyUrl` | string? | No | Relative path to PDF proxy (`/api/v1/documents/{blobPath}`). Null when download failed. |
| `fileName` | string | Yes | Original file name |
| `error` | string? | No | Error description when `proxyUrl` is null (e.g., translation failure) |

## Mapping from Current Models

### NormalizedSearchResult → UnifiedSearchResult

| Current Field | Unified Field | Transformation |
|---------------|---------------|----------------|
| `Name` | `name` | Direct |
| `Identifier` | `identifier` | Direct |
| `Status` | `status` | Direct |
| `EntityType` | `entityType` | Direct |
| `FormationDate` | `formationDate` | Direct |
| `DetailsUrl` | — | **Removed** |
| `State` | `state` | Direct |
| `Event` | `event` | Direct |
| `StandingTax` | `standingTax` | Direct |
| `StandingRA` | `standingRA` | Direct |
| `RegistrationId` | — | **Removed** (was WY-specific, now unified under `identifier`) |
| — | `uniqueKey` | **New**: computed from state-specific URL parameters |

### GermanyBusinessResult → UnifiedSearchResult

| Current Field | Unified Field | Transformation |
|---------------|---------------|----------------|
| `CompanyName` | `name` | Rename |
| `RegisteredOffice` | `registeredOffice` | Move to optional field |
| `Status` | `status` | Direct |
| `RegistrationId` | `identifier` | Rename |
| — | `entityType` | Set to `null` |
| — | `formationDate` | Set to `null` |
| — | `state` | Set to `"DE"` |
| — | `event` | Set to `null` |
| — | `uniqueKey` | **New**: encoded from searchTerm + registrationId |

### EntityDetailResponse → UnifiedEntityDetailResponse

| Current Field | Unified Field | Transformation |
|---------------|---------------|----------------|
| `Details` | `details` | Map `IdNumber` → `identifier`, rename address fields |
| `RegisteredAgent` | `registeredAgent` | Direct (no changes) |
| `Certificate` | `certificate` | Map `StorageUrl` → `downloads[0].proxyUrl` |
| `Parties` | `parties` | Direct (no changes) |
| `HistoryDocuments` | `documents` | Map each to `DocumentEntry` with single download |

### HistoryDocumentResult → DocumentEntry

| Current Field | Unified Field | Transformation |
|---------------|---------------|----------------|
| `EventTitle` | `title` | Direct |
| `Date` | `date` | Direct |
| `StorageUrl` | `downloads[0].proxyUrl` | Extract blob path from URL |
| `FileName` | `downloads[0].fileName` | Direct |
| `Error` | `downloads[0].error` | Direct |
| — | `downloads[0].label` | Set to `"Filing Document"` |

## State Transitions

No state machines in this feature. All transformations are stateless request/response mappings.
