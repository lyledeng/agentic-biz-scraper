# Data Model: Test-UI And Spec Consistency Alignment

**Feature**: 023-test-ui-spec-alignment  
**Date**: 2026-04-01  
**Phase**: 1 - Design

## 1. ExecuteScriptCanonicalRequest

Represents the canonical payload for v2 script execution.

| Field | Type | Required | Notes |
|---|---|---|---|
| definition | string | Yes | Script slug (for example `us-ia-business-search`) |
| parameters | object | Yes | Runtime values consumed by script variables |

Validation rules:

- `definition` must be non-empty
- `parameters` must contain required runtime keys for the selected definition
- Unknown extra keys are allowed but ignored unless used by script

## 2. ExecuteScriptCanonicalEnvelope

Unified outer response from `/api/v2/execute-script`.

| Field | Type | Required | Notes |
|---|---|---|---|
| definition | string | Yes | Echoed script slug |
| correlationId | string | Yes | Request trace identifier |
| truncated | boolean | Yes | Indicates pagination/engine truncation |
| data | object/array/null | Yes | Normalized payload for selected definition |

Search metadata model (when present in `data` payload):

| Field | Type | Required | Notes |
|---|---|---|---|
| results | array | Yes (search flows) | Normalized search rows |
| totalCount | number | Yes (search flows) | Returned row count |
| siteTotalCount | number | Optional | Source-site reported count |
| pagesScraped | number | Optional | Pagination depth |
| searchTerm | string | Optional | Echoed normalized term |
| state | string | Optional | State code |
| executionTime | number | Optional | Operation duration |

## 3. IowaSearchResult

Normalized Iowa search row.

| Field | Type | Required | Notes |
|---|---|---|---|
| identifier | string | Yes | Iowa business number |
| name | string | Yes | Display name |
| status | string | Yes | Active/Inactive mapped status |
| entityType | string | Yes | Legal/Fictitious class |
| formationDate | string/null | Optional | Date when available |
| uniqueKey | string | Yes | Encoded detail reference |
| state | string | Yes | `IA` |
| legalName | string/null | Optional | Iowa-specific extension |
| searchResultType | string/null | Optional | Iowa-specific extension |

## 4. IowaEntityDetail

Extended Iowa detail payload.

| Field | Type | Required | Notes |
|---|---|---|---|
| identifier | string | Yes | Business identifier |
| legalName | string | Yes | Legal business name |
| status | string | Yes | Registration status |
| entityType | string | Yes | Entity class |
| stateOfIncorporation | string | Yes | Usually `IA` |
| modified | boolean | Yes | Modified flag |
| filingDate | string/null | Optional | Filing date |
| effectiveDate | string/null | Optional | Effective date |
| expirationDate | string/null | Optional | Expiration date |
| chapterCode | string/null | Optional | Chapter/code value |
| names | array | Yes | Names list entries |
| registeredAgent | object/null | Optional | Agent contact block |
| principalOffice | object/null | Optional | Principal office block |
| certificateNote | string | Yes | Paywall note |

## 5. CurrentSpecsSnapshot

Project-level consolidated documentation object under `specs/current-specs/`.

| Artifact | Purpose |
|---|---|
| `spec.md` | Canonical merged behavior snapshot |
| `data-model.md` | Canonical merged model snapshot |
| `contracts/*` | Current authoritative API/interface contracts |

Lifecycle rule:

- Snapshot artifacts must be updated when this feature changes canonical contract/model behavior.
