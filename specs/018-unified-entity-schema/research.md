# Research: Unified Entity Schema

**Feature**: 018-unified-entity-schema  
**Date**: 2026-03-29

## Research Tasks

### 1. UniqueKey Encoding Strategy

**Decision**: Base64url-encoded JSON payload

**Rationale**: The `uniqueKey` must be opaque to consumers and self-contained (encoding all parameters for a standalone detail lookup). Base64url-encoded JSON is the simplest approach that satisfies these constraints:
- CO: encodes the full SOS URL query parameters (`quitButtonDestination`, `nameTyp`, `masterFileId`, `entityId2`, `fileId`, `srchTyp`)
- WY: encodes the `eFNum` parameter
- DE: encodes `searchTerm` + `registrationId`

Each encoded payload also carries a `state` field so the server can route to the correct entity-details handler without the consumer specifying it separately.

**Format**:
```json
// CO example (before base64url encoding)
{
  "state": "US-CO",
  "params": {
    "quitButtonDestination": "BusinessEntityResults",
    "nameTyp": "ENT",
    "masterFileId": "20181871974",
    "entityId2": "20181871974",
    "fileId": "20181871974",
    "srchTyp": "ENTITY"
  }
}

// WY example
{
  "state": "US-WY",
  "params": {
    "eFNum": "095211136004012222042099209147048067169248238077"
  }
}

// DE example
{
  "state": "DE-DE",
  "params": {
    "searchTerm": "Rohde & Schwarz",
    "registrationId": "Amtsgericht München HRB 73851"
  }
}
```

**Alternatives considered**:
- Encrypted token — rejected: adds key management complexity, no confidentiality need (these are public SOS URLs)
- URL-safe hex encoding — rejected: longer strings, no benefit over base64url
- Server-side lookup table — rejected: requires state persistence, complicates horizontal scaling

### 2. API v1→v2 Migration with 410 Gone

**Decision**: Register v2 routes alongside v1 stub routes in ASP.NET Core Minimal API

**Rationale**: ASP.NET Core Minimal API supports multiple route groups. The v1 execute-script and definitions routes will be replaced with stubs that return `410 Gone` with a Problem Details body directing consumers to v2. The v1 documents route remains unchanged.

**Implementation approach**:
```csharp
// v2 routes — full functionality
var v2 = app.MapGroup("/api/v2");
v2.MapPost("/execute-script", ExecuteScriptHandler);
v2.MapGet("/definitions", ListDefinitionsHandler);

// v1 stubs — 410 Gone
var v1 = app.MapGroup("/api/v1");
v1.MapPost("/execute-script", () => Results.Problem(
    detail: "This endpoint has moved to /api/v2/execute-script.",
    statusCode: 410, title: "Gone"));
v1.MapGet("/definitions", () => Results.Problem(
    detail: "This endpoint has moved to /api/v2/definitions.",
    statusCode: 410, title: "Gone"));

// v1 documents — unchanged
v1.MapGet("/documents/{**blobPath}", DocumentsHandler);
```

**Alternatives considered**:
- URL rewrite middleware — rejected: hides the version change from consumers, defeats the purpose of versioning
- Remove v1 routes entirely — rejected: 404 gives no migration guidance, 410 is more informative

### 3. Unified Model Mapping in ExecuteScript Handler

**Decision**: Post-processing mapping step based on definition classification (search vs. entity-details)

**Rationale**: The `ExecuteScriptHandler` currently passes through raw `FlowExecutionResult.Output` without transformation. A post-processing step will inspect the definition slug suffix (`business-search` → search mapping, `entity-details` → details mapping) and transform the raw output into the unified model before setting `ExecuteScriptResponse.Data`.

**Mapping flow**:
1. Execute definition → raw `FlowExecutionResult`
2. Classify definition by slug pattern: `*-business-search` or `*-entity-details`
3. For search definitions: map raw results → `UnifiedSearchResult[]` (adding `uniqueKey`, removing `detailsUrl`, renaming fields for DE)
4. For entity-details definitions: map raw results → `UnifiedEntityDetailResponse` (wrapping in unified envelope, mapping `historyDocuments` → `documents` for WY, adding `documents` for DE)
5. Return mapped data in `ExecuteScriptResponse.Data`

**Alternatives considered**:
- Transform in JSON definition scripts — rejected: JavaScript extraction scripts should remain output-format-agnostic; normalization is a server concern
- Separate mapper per definition — rejected: defeats the purpose of unification; a single mapper with per-state adapters is cleaner

### 4. DE-DE Entity Details Definition

**Decision**: New JSON definition + server-side post-processing handler

**Rationale**: The German hardcopy download requires multi-step server-side orchestration (navigate Handelsregister → click AD link → download PDF → translate via FAB → generate translated PDF → upload both to blob storage). This cannot be expressed purely in the JSON action DSL. The solution is:

1. A JSON definition (`de-de-entity-details.json`) that handles the browser navigation: navigate to search → enter term → find result row → click "AD" link → capture PDF download
2. A server-side post-processing step (similar to the existing `GetHardcopyPrintoutHandler` pattern) that: receives the raw PDF bytes → uploads original to blob → calls FAB translation → converts markdown to PDF → uploads translated to blob → returns `UnifiedEntityDetailResponse` with `documents` populated

**The `uniqueKey` for DE encodes both `searchTerm` and `registrationId`**, which the definition uses as runtime variables to locate the correct entity row and trigger the AD download.

**Alternatives considered**:
- Fully DSL-driven (no server-side processing) — rejected: FAB translation + PDF generation + blob upload cannot be expressed in action DSL
- Separate hardcopy definition slug (e.g., `de-de-hardcopy`) — rejected per spec: must use entity-details pattern for consistency

### 5. Definition Slug Rename Impact

**Decision**: Update `metadata.definitionSlug` in each JSON file + update `FlowDefinitionLoader` state index format

**Rationale**: The `FlowDefinitionLoader` indexes definitions by `metadata.definitionSlug` (not by filename). Renaming requires:
1. Rename files: `co-business-search.json` → `us-co-business-search.json` (cosmetic, not functionally required)
2. Update `metadata.definitionSlug` in each file: `co-business-search` → `us-co-business-search`
3. Update `metadata.state` if needed: `CO` → `US-CO` or keep as `CO` with country prefix only in slug
4. Update the secondary state index in `FlowDefinitionLoader` to match new state format

**Decision on state field**: Keep `metadata.state` as-is (`CO`, `WY`, `DE`) for backward compatibility with the engine. The `country-state` naming convention applies only to the definition slug exposed to consumers.

**Alternatives considered**:
- Change `metadata.state` to include country — rejected: affects engine internals, semaphore keys, logging; slug convention is sufficient
