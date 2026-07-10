# Research: Wyoming Entity Details Support

**Feature**: 007-wy-entity-details  
**Date**: 2026-03-25

## R1: WY Entity Details Page Structure

**Decision**: WY entity details are organized into distinct sections on the `FilingDetails.aspx` page, each extractable via targeted JavaScript.

**Rationale**: Playwright discovery of two sample entities (active and inactive) revealed a consistent DOM structure across entity types. All sections use deterministic IDs or structural patterns.

**Sections Identified**:
1. **Header/Detail Block** — Entity name, Filing ID, Type, Status, Sub Status, Initial Filing Date, Standing (Tax/RA/Other), Inactive Date, Term of Duration, Formed In
2. **Addresses** — Principal Office and Mailing Address (structured text blocks)
3. **Additional Details** — Registered Agent (name + address), Latest AR/Year, AR Exempt, License Tax Paid
4. **History** — Chronological list of events with dates; some have clickable "Image(s)" links to filing documents
5. **Parties** — List of individuals with roles (e.g., "Organizer"), organizations, and addresses
6. **Public Notes** — Text section (often empty: "No Public Notes Found...")

**Alternatives Considered**: Using a single monolithic extract script vs. multiple targeted extracts. Chose multiple extracts for modularity, matching CO pattern.

## R2: WY CAPTCHA Handling for Entity Details

**Decision**: Reuse the existing CAPTCHA loop pattern from `wy-business-search.json`. The WY site presents the same CAPTCHA challenge on entity details pages as on search pages.

**Rationale**: Confirmed via Playwright navigation — the same "What code is in the image?" challenge appears with identical DOM structure (`#ans` input, `#jar` submit button). The existing FAB captcha-solver service and `CallServiceActionHandler` handle this.

**Alternatives Considered**: Skipping CAPTCHA (impossible — page blocks access); implementing separate CAPTCHA logic (unnecessary — same mechanism).

## R3: History Document Downloads

**Decision**: Expand the History section in the Playwright page to reveal document links, then download up to the most recent 3 filing images via Playwright download handling.

**Rationale**: History entries with documents show a clickable "Image(s)" icon (a `<span>` or `<div>` element). Clicking expands the entry, revealing a download link. The `DownloadActionHandler` already supports conditional downloads with extension validation. A loop with max 3 iterations over the most recent entries extracts these documents.

**Alternatives Considered**: Direct HTTP fetch of document URLs (rejected — documents are behind authenticated session state); downloading all documents (rejected — spec limits to 3 most recent).

## R4: Endpoint Signature Change

**Decision**: Add a required `state` query parameter to `GET /api/v1/entity-details`. Existing callers must update to include `state=CO`.

**Rationale**: The spec explicitly requires `state` to be mandatory (not optional). This is a breaking change, but the API is internal and consumers can be updated simultaneously.

**Alternatives Considered**: Optional `state` with CO default (rejected by user in spec clarification); separate WY endpoint (rejected — normalization goal requires unified endpoint).

## R5: Model Extension Strategy

**Decision**: Add nullable WY-specific fields to the existing `EntityDetailResult` record. Add new `PartyResult` and `HistoryDocumentResult` models. Extend `EntityDetailResponse` with `Parties` and `HistoryDocuments` collections.

**Rationale**: Keeping a single model with nullable state-specific fields is simpler than polymorphic dispatch and aligns with the normalization goal (US2). CO responses return `null` for WY-specific fields. The `JsonSerializerContext` source generation handles nullable properties correctly with `JsonIgnoreCondition.WhenWritingNull`.

**Alternatives Considered**: Separate CO/WY response types (rejected — violates normalization requirement FR-006); inheritance hierarchy (rejected — records are sealed, adds unnecessary complexity).

## R6: State-Aware Scraper Registration

**Decision**: Make `JsonDrivenEntityDetailScraper` state-parameterized, registered as keyed services (`IEntityDetailScraper` keyed by "CO" and "WY"). The handler resolves the correct scraper based on the `state` parameter.

**Rationale**: This mirrors the existing `JsonDrivenSearchScraper` pattern which is already keyed by state code ("CO", "WY") in DI. The scraper loads the appropriate flow definition via `flowLoader.GetDefinition(stateCode, "entity-details")`.

**Alternatives Considered**: Single scraper with state parameter on ScrapeAsync (viable but less aligned with existing DI pattern); factory method (unnecessary when keyed services exist).

## R7: Audit Table Migration

**Decision**: Add a nullable `StateCode NVARCHAR(10)` column to the existing `EntityDetailAuditEntries` table via a Liquibase changelog at version `2.0.2603.3`.

**Rationale**: The column must be `NULL` for backward compatibility with existing CO records that lack a state code. New records populate the column. The next available Liquibase version is `2.0.2603.3` (after `2.0.2603.0` which added the state column to the search audit table).

**Alternatives Considered**: Creating a separate WY audit table (rejected — violates DRY, complicates queries); making column NOT NULL with default (rejected — existing records have no state value).
