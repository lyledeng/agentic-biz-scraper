# Contract: Entity Details API

**Feature**: 010-test-ui (consumed by Angular UI)
**Source**: `GET /api/v1/entity-details`
**Version**: Existing endpoint (no changes required for this feature)

## Request

```
GET /api/v1/entity-details?detailsUrl={url}&state={state}
```

| Parameter | Type | Required | Notes |
|-----------|------|----------|-------|
| `detailsUrl` | `string` | ✅ | URL-encoded entity details page URL (from search result) |
| `state` | `string` | ✅ | `CO` or `WY` |

## Response

**200 OK** — `application/json`

```json
{
  "details": {
    "name": "Wendy's Technology, LLC",
    "status": "Active",
    "formationDate": "2019-03-15",
    "idNumber": "2019-000123456",
    "form": "Limited Liability Company - Domestic",
    "jurisdiction": "Wyoming",
    "periodicReportMonth": null,
    "principalOfficeStreetAddress": "123 Main St, Cheyenne WY 82001",
    "principalOfficeMailingAddress": null,
    "subStatus": "Current",
    "standingTax": "Good",
    "standingRA": "Good",
    "standingOther": "Good",
    "inactiveDate": null,
    "termOfDuration": "Perpetual",
    "formedIn": "Wyoming",
    "latestAnnualReportYear": "2025",
    "annualReportExempt": "No",
    "licenseTaxPaid": "Yes"
  },
  "registeredAgent": {
    "name": "Registered Agents Inc.",
    "streetAddress": "30 N Gould St, Sheridan WY 82801",
    "mailingAddress": null
  },
  "certificate": {
    "available": true,
    "storageUrl": "https://devacr.blob.core.windows.net/certs/abc123.pdf",
    "fileName": "certificate-good-standing.pdf",
    "error": null
  },
  "parties": [
    {
      "name": "Jane Doe",
      "role": "Organizer",
      "organization": null,
      "address": "123 Main St, Cheyenne WY 82001"
    }
  ],
  "historyDocuments": [
    {
      "eventTitle": "2025 Original Annual Report - 14843526",
      "date": "2025-01-10",
      "storageUrl": "https://devacr.blob.core.windows.net/docs/2025-annual-report.pdf",
      "fileName": "2025-original-annual-report-14843526.pdf",
      "error": null
    }
  ]
}
```

## Response Field Reference

### `details` (EntityDetailResult)

| Field | Type | CO | WY |
|-------|------|----|----|
| `name` | `string` | ✅ | ✅ |
| `status` | `string` | ✅ | ✅ |
| `formationDate` | `string` | ✅ | ✅ |
| `idNumber` | `string` | ✅ | ✅ |
| `form` | `string` | ✅ | ✅ |
| `jurisdiction` | `string` | ✅ | ✅ |
| `periodicReportMonth` | `string \| null` | ✅ | ❌ null |
| `principalOfficeStreetAddress` | `string \| null` | ✅ | ✅ |
| `principalOfficeMailingAddress` | `string \| null` | ✅ | ✅ |
| `subStatus` | `string \| null` | ❌ null | ✅ |
| `standingTax` | `string \| null` | ❌ null | ✅ |
| `standingRA` | `string \| null` | ❌ null | ✅ |
| `standingOther` | `string \| null` | ❌ null | ✅ |
| `inactiveDate` | `string \| null` | ❌ null | ✅ (if inactive) |
| `termOfDuration` | `string \| null` | ❌ null | ✅ |
| `formedIn` | `string \| null` | ❌ null | ✅ |
| `latestAnnualReportYear` | `string \| null` | ❌ null | ✅ |
| `annualReportExempt` | `string \| null` | ❌ null | ✅ |
| `licenseTaxPaid` | `string \| null` | ❌ null | ✅ |

### `registeredAgent` (RegisteredAgentResult | null)

| Field | Type |
|-------|------|
| `name` | `string \| null` |
| `streetAddress` | `string \| null` |
| `mailingAddress` | `string \| null` |

### `certificate` (CertificateResult | null)

| Field | Type | Notes |
|-------|------|-------|
| `available` | `boolean` | If false, show "Not available" |
| `storageUrl` | `string \| null` | Blob Storage URL |
| `fileName` | `string \| null` | Link display text |
| `error` | `string \| null` | Show if non-null |

### `parties` (PartyResult[] | null — WY only)

| Field | Type |
|-------|------|
| `name` | `string` |
| `role` | `string` |
| `organization` | `string \| null` |
| `address` | `string \| null` |

### `historyDocuments` (HistoryDocumentResult[] | null — WY only)

| Field | Type | Notes |
|-------|------|-------|
| `eventTitle` | `string` | Row title |
| `date` | `string` | Event date |
| `storageUrl` | `string \| null` | Open in new tab |
| `fileName` | `string \| null` | Clickable link text |
| `error` | `string \| null` | Show if non-null |

## TypeScript Interface (UI consumer)

```typescript
// src/test-ui/src/app/shared/models/entity-detail-response.model.ts

export interface EntityDetailResponse {
  details: EntityDetailResult;
  registeredAgent: RegisteredAgentResult | null;
  certificate: CertificateResult | null;
  parties: PartyResult[] | null;
  historyDocuments: HistoryDocumentResult[] | null;
}

export interface EntityDetailResult {
  name: string;
  status: string;
  formationDate: string;
  idNumber: string;
  form: string;
  jurisdiction: string;
  periodicReportMonth: string | null;
  principalOfficeStreetAddress: string | null;
  principalOfficeMailingAddress: string | null;
  subStatus: string | null;
  standingTax: string | null;
  standingRA: string | null;
  standingOther: string | null;
  inactiveDate: string | null;
  termOfDuration: string | null;
  formedIn: string | null;
  latestAnnualReportYear: string | null;
  annualReportExempt: string | null;
  licenseTaxPaid: string | null;
}

export interface RegisteredAgentResult {
  name: string | null;
  streetAddress: string | null;
  mailingAddress: string | null;
}

export interface CertificateResult {
  available: boolean;
  storageUrl: string | null;
  fileName: string | null;
  error: string | null;
}

export interface PartyResult {
  name: string;
  role: string;
  organization: string | null;
  address: string | null;
}

export interface HistoryDocumentResult {
  eventTitle: string;
  date: string;
  storageUrl: string | null;
  fileName: string | null;
  error: string | null;
}
```
