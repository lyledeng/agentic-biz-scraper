# Research Phase: Iowa Business Entity Search Integration

**Feature**: 022-iowa-entity-search  
**Phase**: Phase 0 - Research & Discovery  
**Date**: 2026-04-01  
**Status**: Complete

---

## Research Summary

All critical clarifications have been addressed via interactive Q&A session. No outstanding unknowns remain.

---

## Discovered & Verified

### 1. Iowa SOS Website Structure

**Status**: ✅ VERIFIED via Playwright discovery

- **Search Page**: https://sos.iowa.gov/search/business/Search.aspx
- **Input Field**: Business Name text box (placeholder: "Enter business name")
- **Submission**: Submit button (type=submit, name=btnNumber)
- **Results Page**: Table with columns: Business No., Name, Status, Type, Legal Name
- **Pagination**: Available via link elements (next page navigation)
- **Detail Page** (summary.aspx): Comprehensive multi-table layout
  - Business No., Legal Name, Status, Type, State of Inc., Modified, Expiration Date, Effective Date, Filing Date, Chapter/Code
  - Names section with type/status/modified/name
  - Registered Agent: name, address, address2, city/state/zip
  - Principal Office: name, address, address2, city/state/zip
  - Additional tabs: Address, Agent, Filings, Names, Officers, Protected Series, Stock

### 2. API Architecture Decision

**Status**: ✅ CLARIFIED - v2 Execute-Script Pattern

- **Decision**: All state searches use `/api/v2/execute-script` with state-specific script definitions
- **Script Naming**: country-state-action format
  - `us-ia-business-search` (Iowa search)
  - `us-co-business-search` (Colorado search)
  - `us-wy-entity-details` (Wyoming details)
- **v1 Deprecation**: `/api/v1/business-search` deprecated; CO/WY migration in scope or follow-on feature
- **Rationale**: Unified, schema-driven state management; future-proof for new states

### 3. Detail Response Scope

**Status**: ✅ CLARIFIED - Extended Set

**Summary Fields**:
- Business number, legal name, status, type, state of incorporation, modified flag

**Date Fields**:
- Expiration date, effective date, filing date

**Structural Fields**:
- Chapter/code

**Named Entities**:
- Names list (with type, status, modified flag for each)
- Registered agent contact (name, address lines, city/state/zip)
- Principal office (name, address lines, city/state/zip)

**Out-of-Scope for v1** (documented as future enhancement):
- Filings tab details
- Officers tab details
- Protection Series details
- Stock details

### 4. Certificate of Existence Handling

**Status**: ✅ CLARIFIED - Paywall Notation

- **Limitation**: Certificate behind paywall; not accessible via scraper
- **Documentation**: Include `certificateNote` field in detail response
- **Message**: "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)"
- **Marking**: Out-of-scope for v1; flagged for future enhancement

### 5. Local Development & TLS

**Status**: ✅ VERIFIED

- **Issue**: Iowa SOS uses self-signed certificate; TLS validation fails in corporate networks
- **Handling**: System MUST ignore TLS errors for local Playwright execution
- **Configuration**: Browser launch with `ignoreHTTPSErrors: true` flag

---

## Existing Infrastructure Reuse

All required infrastructure exists; no dependency gaps:

- ✅ **v2 Execute-Script Handler** — Already exists for CO/WY; extensible for IA
- ✅ **Playwright Browser Integration** — Already pooled and configured for Playwright workspace
- ✅ **Script Definition Engine** — JSON-driven action orchestration already implemented
- ✅ **Response Normalization** — Existing schema supports IA fields without modification
- ✅ **Audit Trail** — Existing AuditEntry model; just needs script name recording
- ✅ **Diagnostic Screenshots** — Existing infrastructure; reusable for IA
- ✅ **Concurrency Management** — Per-state semaphore pattern already implemented
- ✅ **Azure Blob Storage** — Already configured for diagnostic artifacts

---

## Design Decisions

Based on research findings and clarifications:

| Decision | Rationale | Alternative Rejected |
|----------|-----------|---------------------|
| **v2 Execute-Script API** | Unified, schema-driven, future-proof for new states | v1 endpoint (deprecated, lacks state abstraction) |
| **Extended Detail Fields** | Comprehensive entity data (names, agent, addresses, dates) | Minimal set (insufficient for business records) |
| **Certificate Paywall Notation** | Transparent documentation of limitation; enables future enhancement | Omit certificate (hides capability gap); attempt scraping (blocked by paywall) |
| **JSON Script Definitions** | Recorder-friendly, maintainable, version-controlled | Hard-coded C# logic (brittle with page changes) |
| **Per-State Concurrency Semaphore** | Prevents Iowa searches from blocking other states; enforces fairness | Global semaphore (unfair serialization) |

---

## No Clarifications Needed

All 3 interactive clarification questions resolved:

1. ✅ API Architecture: v2 execute-script (all states migrate)
2. ✅ Detail Fields: Extended set (13 fields + certificate note)
3. ✅ Certificate Limitation: Document as unavailable (paywall)

**Research Status**: COMPLETE — Ready for Phase 1 Design

---

## Next: Phase 1 - Design & Contracts

Proceed to generate:
- data-model.md (Iowa-specific entity models)
- contracts/ (script definitions & response schemas)
- quickstart.md (usage examples)
