# Data Model: Test UI for BizScraper API

**Feature**: 010-test-ui
**Date**: 2026-03-26

## UI Data Models

These TypeScript interfaces represent the shapes consumed from the BizScraper API and used throughout the Angular application. They live in `src/test-ui/src/app/shared/models/`.

---

## Model: BusinessSearchResult

Represents a single row in the search results grid. Maps to the `GET /api/v1/business-search` response array items.

**File**: `business-search-result.model.ts`

| Field | TypeScript Type | Required | Source (API field) | Display Column |
|-------|----------------|----------|--------------------|----------------|
| `name` | `string` | ✅ | `name` | Name |
| `identifier` | `string` | ✅ | `identifier` | Identifier |
| `status` | `string` | ✅ | `status` | Status |
| `entityType` | `string` | ✅ | `entityType` | Entity Type |
| `formationDate` | `string \| null` | ❌ | `formationDate` | Formation Date |
| `state` | `string` | ✅ | `state` | State |
| `detailsUrl` | `string` | ✅ | `detailsUrl` | (internal — click target) |

**Usage**: SearchComponent renders rows; `detailsUrl` and `state` are passed to the router when navigating to `/entity-details`.

---

## Model: EntityDetailResponse

Root response shape from `GET /api/v1/entity-details`. Maps directly to the serialized JSON returned by the BizScraper API.

**File**: `entity-detail-response.model.ts`

```typescript
export interface EntityDetailResponse {
  details: EntityDetailResult;
  registeredAgent: RegisteredAgentResult | null;
  certificate: CertificateResult | null;
  parties: PartyResult[] | null;           // WY only
  historyDocuments: HistoryDocumentResult[] | null; // WY only
}
```

### EntityDetailResult

| Field | TypeScript Type | Required | Notes |
|-------|----------------|----------|-------|
| `name` | `string` | ✅ | Entity name |
| `status` | `string` | ✅ | Active / Inactive |
| `formationDate` | `string` | ✅ | ISO date string or formatted |
| `idNumber` | `string` | ✅ | CO filing ID or WY filing ID |
| `form` | `string` | ✅ | CO form / WY entity type |
| `jurisdiction` | `string` | ✅ | Jurisdiction / Formed In |
| `periodicReportMonth` | `string \| null` | ❌ | CO only |
| `principalOfficeStreetAddress` | `string \| null` | ❌ | Street address |
| `principalOfficeMailingAddress` | `string \| null` | ❌ | Mailing address |
| `subStatus` | `string \| null` | ❌ | WY only |
| `standingTax` | `string \| null` | ❌ | WY only |
| `standingRA` | `string \| null` | ❌ | WY only |
| `standingOther` | `string \| null` | ❌ | WY only |
| `inactiveDate` | `string \| null` | ❌ | WY only |
| `termOfDuration` | `string \| null` | ❌ | WY only |
| `formedIn` | `string \| null` | ❌ | WY only |
| `latestAnnualReportYear` | `string \| null` | ❌ | WY only |
| `annualReportExempt` | `string \| null` | ❌ | WY only |
| `licenseTaxPaid` | `string \| null` | ❌ | WY only |

**Null-safety rule**: Fields with `| null` are rendered only when non-null. The template uses `@if (detail.subStatus)` guards to conditionally show WY-specific rows.

### RegisteredAgentResult

| Field | TypeScript Type | Required |
|-------|----------------|----------|
| `name` | `string \| null` | ❌ |
| `streetAddress` | `string \| null` | ❌ |
| `mailingAddress` | `string \| null` | ❌ |

### CertificateResult

| Field | TypeScript Type | Required | Notes |
|-------|----------------|----------|-------|
| `available` | `boolean` | ✅ | When `false`, no download link is shown |
| `storageUrl` | `string \| null` | ❌ | Blob Storage URL |
| `fileName` | `string \| null` | ❌ | Display name for download link |
| `error` | `string \| null` | ❌ | Error message if retrieval failed |

**Display rule**: If `available === true` and `storageUrl !== null`, render anchor with `target="_blank"`. If `error !== null`, render error text. If `available === false`, render "Not available".

### PartyResult (WY only)

| Field | TypeScript Type | Required |
|-------|----------------|----------|
| `name` | `string` | ✅ |
| `role` | `string` | ✅ |
| `organization` | `string \| null` | ❌ |
| `address` | `string \| null` | ❌ |

### HistoryDocumentResult

| Field | TypeScript Type | Required | Notes |
|-------|----------------|----------|-------|
| `eventTitle` | `string` | ✅ | Row header |
| `date` | `string` | ✅ | Event date |
| `storageUrl` | `string \| null` | ❌ | Blob Storage URL — open in new tab (FR-009) |
| `fileName` | `string \| null` | ❌ | Clickable display name |
| `error` | `string \| null` | ❌ | Shown if download failed |

**Display rule**: If `storageUrl !== null` and `fileName !== null`, render `<a [href]="doc.storageUrl" target="_blank" rel="noopener noreferrer">{{ doc.fileName }}</a>`. If `error !== null`, show error message. Never render both.

---

## UI-Only Models

### ApiConfig

Held in `ApiConfigService`. Not serialised — persisted to `localStorage`.

| Field | TypeScript Type | Default |
|-------|----------------|---------|
| `baseUrl` | `string` | `https://localhost:8443` |

---

## Service Input Models

### SearchQuery

Parameters passed to `SearchService.search()`.

| Field | TypeScript Type | Validation |
|-------|----------------|------------|
| `term` | `string` | Trim; must not be empty (FR-014) |
| `state` | `'CO' \| 'WY'` | Required; default `'CO'` |

### EntityDetailsQuery

Parameters passed to `EntityDetailsService.getDetails()`.

| Field | TypeScript Type | Notes |
|-------|----------------|-------|
| `detailsUrl` | `string` | URL-encoded when appended to query string |
| `state` | `'CO' \| 'WY'` | Passed through from search result row |

---

## State Transitions

### Search View State Machine

```text
IDLE ──[Search submitted]──> LOADING
LOADING ──[API success]──> RESULTS
LOADING ──[API error]──> ERROR
RESULTS ──[New search]──> LOADING
RESULTS ──[Row clicked]──> navigate to /entity-details
ERROR ──[Retry / New search]──> LOADING
```

### Entity Detail View State Machine

```text
LOADING ──[API success]──> DETAIL
LOADING ──[API error]──> ERROR
DETAIL ──[Back clicked]──> navigate back to /search (preserving results signal)
```
