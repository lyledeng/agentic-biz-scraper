# Data Model: Constitution Compliance Audit

**Feature**: 017-constitution-compliance-audit
**Date**: 2026-03-29

This feature does not introduce new runtime data entities. It produces
documentation artifacts and CI validation scripts. The entities below
describe the structure of the deliverables.

## Entity: Global Specification Snapshot

The `specs/current-specs/` folder structure.

| Artifact | Type | Description |
|----------|------|-------------|
| `spec.md` | Markdown | Consolidated specification organized by capability |
| `data-model.md` | Markdown | Unified data model for all current entities |
| `contracts/` | Directory | API contract files for each endpoint |

### spec.md Capability Sections

| Section | Source Specs | Content |
|---------|------------|---------|
| Generic Script Execution | 016 | Execute-script endpoint, definition discovery |
| Business Search (CO/WY) | 001, 005, 016 | Search via execute-script, NormalizedSearchResult |
| Entity Details (CO/WY) | 004, 007, 008, 009, 016 | Details via execute-script, EntityDetailResponse |
| Germany Business Search | 013, 016 | Germany search via execute-script |
| Germany Hardcopy Printout | 015, 016 | Hardcopy retrieval + FAB translation |
| PDF Document Proxy | 011 | Blob storage streaming proxy |
| Scraping Engine | 006, 014 | JSON-driven flow engine v2 |
| Diagnostics & Storage | 002, 003 | Screenshot capture, cloud storage |
| Semantic Error Handling | 012 | Non-retryable error detection |
| Test UI | 010 | Angular 17 test application |
| Constitution Compliance | (this feature) | Living compliance assessment |

### Unified Data Model Entities

All C# records in `src/BizScraper.Api/` that form the public API surface:

| Entity | Namespace | Fields |
|--------|-----------|--------|
| ExecuteScriptRequest | Features.ExecuteScript.Models | Definition, Parameters |
| ExecuteScriptResponse | Features.ExecuteScript.Models | Definition, CorrelationId, Truncated, Data |
| DefinitionInfo | Features.ExecuteScript.Models | DefinitionSlug, Name, Description, State, RequiredParameters |
| NormalizedSearchResponse | Features.BusinessSearch.Models | Results, TotalCount, SiteTotalCount, Truncated, PagesScraped, SearchTerm, State |
| NormalizedSearchResult | Features.BusinessSearch.Models | Identifier, Name, Status, EntityType, FormationDate, DetailsUrl, State, Event, StandingTax, StandingRA, RegistrationId |
| EntityDetailResponse | Features.EntityDetails.Models | Details, RegisteredAgent, Certificate, Parties, HistoryDocuments |
| EntityDetailResult | Features.EntityDetails.Models | Name, Status, FormationDate, IdNumber, Form, Jurisdiction, ... |
| RegisteredAgentResult | Features.EntityDetails.Models | Name, StreetAddress, MailingAddress |
| CertificateResult | Features.EntityDetails.Models | StorageUrl, FileName |
| PartyResult | Features.EntityDetails.Models | Name, Role, Organization, Address |
| HistoryDocumentResult | Features.EntityDetails.Models | EventTitle, Date, StorageUrl, FileName, Error |
| GermanySearchResponse | Features.GermanySearch.Models | Results, TotalCount, PagesScraped, SearchTerm, Warning |
| GermanyBusinessResult | Features.GermanySearch.Models | CompanyName, RegisteredOffice, Status, RegistrationId |
| HardcopyPrintoutRequest | Features.GermanySearch.Models | SearchTerm, RegistrationId |

## Entity: CI Validation Script

PowerShell scripts in `tools/ci/`:

| Script | Purpose | Exit Code |
|--------|---------|-----------|
| `check-xml-doc-coverage.ps1` | Verify all public C# types have `/// <summary>` | 0 = pass, 1 = missing docs |
| `check-liquibase-comments.ps1` | Verify all changesets have `--comment:` | 0 = pass, 1 = missing comments |
| `check-global-snapshot.ps1` | Verify `specs/current-specs/` exists with required files | 0 = pass, 1 = missing files |

## Entity: Compliance Assessment Entry

Each principle/gate assessed in the "Constitution Compliance" section
of `specs/current-specs/spec.md`:

| Field | Type | Description |
|-------|------|-------------|
| Principle/Gate Name | string | e.g., "I. Test-First" |
| Status | enum | Compliant / Partial / Non-Compliant |
| Evidence | string | Brief description of supporting evidence |
| Remediation | string? | Action plan if not fully compliant |
