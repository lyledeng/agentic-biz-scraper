# QuickStart: Iowa Business Entity Search API

**Feature**: 022-iowa-entity-search  
**API Version**: v2 (execute-script)  
**Date**: 2026-04-01  

---

## Overview

Iowa business entity search is integrated into the unified v2 `/api/v2/execute-script` endpoint. This endpoint accepts JSON-driven script definitions and returns structured entity data.

**Key Features**:
- 🔍 Search by business name (partial match supported)
- 📄 Paginated results (automatic pagination handling)
- 🔗 Drill-down to entity details  
- 🛡️ Per-state concurrency control (1 concurrent IA request)
- 📊 Comprehensive entity data (13+ fields)
- ⏱️ 30-second SLA target response time
- 🧪 Full diagnostic trace logging with screenshots

---

## Base Endpoint

```
POST /api/v2/execute-script
Content-Type: application/json
```

**Host**: Depends on deployment (local: `http://localhost:5000`, Azure AKS: `https://api.service.azure.com`)

---

## Example 1: Search for Iowa Entities

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{
  "definition": "us-ia-business-search",
  "parameters": {
    "searchTerm": "Acme Inc"
  }
}
```

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | string | Yes | Must be exactly `"us-ia-business-search"` |
| `parameters.searchTerm` | string | Yes | Business name to search (2-100 characters) |

### Response

```json
{
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "definition": "us-ia-business-search",
  "state": "IA",
  "executionTimeMs": 2847,
  "data": [
    {
      "identifier": "457975",
      "name": "Acme Inc",
      "status": "Active",
      "entityType": "Legal",
      "legalName": "Acme Incorporated",
      "formationDate": null,
      "uniqueKey": "eyJzdGF0ZSI6IklBIiwiYnVzaW5lc3NOdW1iZXIiOiI0NTc5NzUiLCJ1cmwiOiJodHRwczovL3Nv...",
      "state": "IA",
      "searchResultType": "legal-name"
    },
    {
      "identifier": "623841",
      "name": "Acme Ventures LLC",
      "status": "Inactive",
      "entityType": "Legal",
      "legalName": null,
      "formationDate": null,
      "uniqueKey": "eyJzdGF0ZSI6IklBIiwiYnVzaW5lc3NOdW1iZXIiOiI2MjM4NDEiLCJ1cmwiOiJodHRwczovL3Nv...",
      "state": "IA",
      "searchResultType": "legal-name"
    }
  ],
  "totalCount": 2,
  "siteTotalCount": 2,
  "truncated": false,
  "pagesScraped": 1,
  "searchTerm": "Acme Inc",
  "diagnosticsUrl": "https://storage.azure.com/diagnostics/550e8400-e29b-41d4-a716-446655440000/"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `correlationId` | string | Trace ID for this operation (for debugging) |
| `definition` | string | Script definition used: `"us-ia-business-search"` |
| `state` | string | State code: `"IA"` |
| `executionTimeMs` | integer | Total execution time in milliseconds |
| `data` | array | Array of business entity results |
| `totalCount` | integer | Number of results in this response |
| `siteTotalCount` | integer | Total entities on Iowa SOS matching query |
| `truncated` | boolean | `true` if results exceed page limit |
| `pagesScraped` | integer | Number of pages scraped (pagination indicator) |
| `searchTerm` | string | Original search term (normalized) |
| `diagnosticsUrl` | string | Azure Blob Storage URL to screenshots, logs, and HTML snapshots |

---

## Example 2: Drill Down to Entity Details

Use the `uniqueKey` from search results to retrieve comprehensive entity information.

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{
  "definition": "us-ia-entity-details",
  "parameters": {
    "uniqueKey": "eyJzdGF0ZSI6IklBIiwiYnVzaW5lc3NOdW1iZXIiOiI0NTc5NzUiLCJ1cmwiOiJodHRwczovL3Nv..."
  }
}
```

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | string | Yes | Must be exactly `"us-ia-entity-details"` |
| `parameters.uniqueKey` | string | Yes | Base64URL-encoded drill-down reference from search result |

### Response

```json
{
  "correlationId": "660e8400-e29b-41d4-a716-446655440001",
  "definition": "us-ia-entity-details",
  "state": "IA",
  "executionTimeMs": 1523,
  "data": {
    "identifier": "457975",
    "legalName": "Acme Incorporated",
    "status": "Active",
    "entityType": "Legal",
    "stateOfIncorporation": "IA",
    "modified": false,
    "expirationDate": null,
    "effectiveDate": "2015-03-15",
    "filingDate": "2015-03-15",
    "chapterCode": "CODE 489 DOMESTIC LIMITED LIABILITY COMPANY",
    "names": [
      {
        "type": "Legal",
        "status": "Active",
        "modified": false,
        "name": "Acme Incorporated"
      },
      {
        "type": "Fictitious",
        "status": "Active",
        "modified": false,
        "name": "Acme Ventures"
      }
    ],
    "registeredAgent": {
      "fullName": "John Smith",
      "address": "123 Main Street",
      "address2": "Suite 200",
      "cityStateZip": "Des Moines, IA, 50309"
    },
    "principalOffice": {
      "address": "456 Oak Avenue",
      "address2": null,
      "cityStateZip": "Cedar Rapids, IA, 52402"
    },
    "certificateNote": "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)",
    "state": "IA"
  },
  "diagnosticsUrl": "https://storage.azure.com/diagnostics/660e8400-e29b-41d4-a716-446655440001/"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `correlationId` | string | Trace ID for this operation |
| `definition` | string | Script definition used: `"us-ia-entity-details"` |
| `state` | string | State code: `"IA"` |
| `executionTimeMs` | integer | Total execution time in milliseconds |
| `data.identifier` | string | Iowa business registration number |
| `data.legalName` | string | Legal entity name |
| `data.status` | string | Registration status: "Active" or "Inactive" |
| `data.entityType` | string | Entity type: "Legal" or "Fictitious name" |
| `data.stateOfIncorporation` | string | State of incorporation (typically "IA") |
| `data.modified` | boolean | Whether entity has been amended since filing |
| `data.expirationDate` | string \| null | Certificate expiration date (ISO 8601) |
| `data.effectiveDate` | string | Date entity became effective (ISO 8601) |
| `data.filingDate` | string | Original filing date (ISO 8601) |
| `data.chapterCode` | string | Legal code/chapter designation |
| `data.names` | array | All associated business names (legal, fictitious, trade) |
| `data.registeredAgent` | object | Registered agent contact information |
| `data.principalOffice` | object | Principal office address |
| `data.certificateNote` | string | Note explaining certificate paywall limitation |
| `diagnosticsUrl` | string | Azure Blob Storage URL to diagnostic artifacts |

---

## Response Codes

### Success (2xx)

| Status | Condition | Notes |
|--------|-----------|-------|
| 200 | Search completed successfully | May contain 0 results if no entities match |
| 200 | Detail retrieval successful | Entity details fully populated |

### Client Errors (4xx)

| Status | Condition | Notes |
|--------|-----------|-------|
| 400 | Invalid parameters | Missing or malformed `definition`, `parameters`, etc. |
| 400 | Search term too short | Less than 2 characters |
| 404 | Entity not found | `uniqueKey` points to non-existent entity |

### Server Errors (5xx)

| Status | Condition | Notes |
|--------|-----------|-------|
| 503 | State concurrency limit exceeded | Too many concurrent IA requests; retry with backoff (30 seconds) |
| 503 | Iowa SOS website unavailable | Network error or site down; retry with exponential backoff |
| 504 | Timeout | Operation exceeded timeout threshold (30 seconds default) |

---

## Integration Patterns

### Pattern 1: Search-and-Results React Component

```typescript
// React component using Iowa business search
import { useEffect, useState } from 'react';

export function IowaEntitySearch() {
  const [searchTerm, setSearchTerm] = useState('');
  const [results, setResults] = useState<IowaBusinessSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (searchTerm.trim().length < 2) {
      setError('Search term must be at least 2 characters');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await fetch('/api/v2/execute-script', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          definition: 'us-ia-business-search',
          parameters: { searchTerm }
        })
      });

      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const data = await response.json();
      setResults(data.data); // Array of IowaBusinessSearchResult
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setLoading(false);
    }
  };

  const handleDrillDown = async (uniqueKey: string, name: string) => {
    try {
      const response = await fetch('/api/v2/execute-script', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          definition: 'us-ia-entity-details',
          parameters: { uniqueKey }
        })
      });

      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const data = await response.json();
      
      // Open detail modal or navigate to detail page
      console.log('Entity details:', data.data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Detail lookup failed');
    }
  };

  return (
    <div>
      <form onSubmit={handleSearch}>
        <input
          type="text"
          placeholder="Enter business name..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          disabled={loading}
        />
        <button type="submit" disabled={loading}>
          {loading ? 'Searching...' : 'Search'}
        </button>
      </form>

      {error && <div className="error">{error}</div>}

      {results.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Business No.</th>
              <th>Name</th>
              <th>Status</th>
              <th>Type</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {results.map((r) => (
              <tr key={r.identifier}>
                <td>{r.identifier}</td>
                <td>{r.name}</td>
                <td>{r.status}</td>
                <td>{r.entityType}</td>
                <td>
                  <button onClick={() => handleDrillDown(r.uniqueKey, r.name)}>
                    Details
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
```

### Pattern 2: Search-Results Handler (.NET)

```csharp
// ASP.NET Core handler for search
[HttpPost("api/v2/execute-script")]
public async Task<IActionResult> ExecuteScript([FromBody] ExecuteScriptRequest request)
{
    if (request.Definition == "us-ia-business-search")
    {
        var searchTerm = request.Parameters?["searchTerm"]?.ToString();
        
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
            return BadRequest(new { error = "Search term must be 2+ characters" });

        try
        {
            var response = await _executeScriptHandler.ExecuteAsync(
                definition: "us-ia-business-search",
                parameters: new Dictionary<string, object> { { "searchTerm", searchTerm } },
                cancellationToken: HttpContext.RequestAborted
            );

            return Ok(response); // Returns IowaSearchResponse
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { error = "Request timeout (30 seconds exceeded)" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("concurrency"))
        {
            Response.Headers.Add("Retry-After", "30");
            return StatusCode(503, new { error = "Rate limit exceeded. Retry after 30 seconds." });
        }
    }

    return BadRequest(new { error = "Unknown script name" });
}
```

### Pattern 3: Handle Certificate Limitation

```typescript
// Gracefully handle certificate paywall limitation
function displayEntityDetail(detail: IowaEntityDetails) {
  const hasCertificate = detail.certificateNote === 
    "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)";

  return (
    <div className="entity-detail">
      <h1>{detail.legalName}</h1>
      <p>Registration #: {detail.identifier}</p>
      <p>Status: {detail.status}</p>
      
      {hasCertificate && (
        <div className="certificate-note alert-info">
          <strong>Certificate:</strong> {detail.certificateNote}
          <a href="https://sos.iowa.gov/search/business/Search.aspx" target="_blank">
            Get certificate from Iowa SOS website
          </a>
        </div>
      )}

      {/* ... other detail fields ... */}
    </div>
  );
}
```

---

## Error Handling Examples

### Example: Retry with Exponential Backoff

```typescript
async function executeScriptWithRetry(
  definition: string,
  parameters: Record<string, unknown>,
  maxRetries = 3
): Promise<unknown> {
  for (let attempt = 0; attempt < maxRetries; attempt++) {
    try {
      const response = await fetch('/api/v2/execute-script', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ definition, parameters })
      });

      // Success
      if (response.ok) return await response.json();

      // Rate limit: extract Retry-After header
      if (response.status === 503) {
        const retryAfter = response.headers.get('Retry-After');
        const delayMs = (retryAfter ? parseInt(retryAfter) : Math.pow(2, attempt)) * 1000;
        await new Promise(resolve => setTimeout(resolve, delayMs));
        continue;
      }

      // Other errors
      throw new Error(`HTTP ${response.status}`);
    } catch (err) {
      if (attempt === maxRetries - 1) throw err;
      const delayMs = Math.pow(2, attempt) * 1000;
      await new Promise(resolve => setTimeout(resolve, delayMs));
    }
  }
}
```

---

## Diagnostics & Debugging

### Correlation ID Tracing

Every response includes a `correlationId`. Use this for end-to-end tracing:

```
GET /api/v1/diagnostics/{correlationId}

Response:
{
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "blobUri": "https://storage.azure.com/diagnostics/550e8400-e29b-41d4-a716-446655440000/",
  "artifacts": [
    "screenshot-001.png",      // Initial page load
    "screenshot-002.png",      // After search submission
    "screenshot-003.png",      // Results table
    "html-snapshot.html",      // Page DOM at end of script
    "execution-log.json"       // Detailed action execution log
  ]
}
```

### Azure Blob Storage Access

Diagnostic artifacts are stored in Azure Blob Storage:

```
https://storage.azure.com/diagnostics/{correlationId}/
  ├── screenshot-001.png           (page load)
  ├── screenshot-002.png           (after action)
  ├── html-snapshot.html           (final page state)
  └── execution-log.json           (detailed action trace)
```

---

## Common Issues & Solutions

### Issue: "Search term too short"
**Solution**: Ensure search term is at least 2 characters. Minimum validation happens client-side; server enforces this requirement.

### Issue: "Entity not found" (404)
**Solution**: The drill-down URL may have expired. Perform a fresh search and use the new `uniqueKey`.

### Issue: "Rate limit exceeded" (503)
**Solution**: Iowa has 1 concurrent request limit per state. Implement exponential backoff and retry after Retry-After header time (typically 30 seconds).

### Issue: "Timeout" (504)
**Solution**: Iowa SOS website may be slow. Default timeout is 30 seconds. Consider increasing timeout in options (max: 60 seconds).

---

## Performance SLA

| Operation | Target | Typical | Max |
|-----------|--------|---------|-----|
| Search (1-20 results) | 5 seconds | 2-3 seconds | 30 seconds |
| Search (20+ results, paginated) | 15 seconds | 8-12 seconds | 30 seconds |
| Entity detail drill-down | 5 seconds | 2-3 seconds | 30 seconds |

---

## Related Resources

- [Data Model Documentation](./data-model.md) — Iowa entity schemas and validation rules
- [Contract Definitions](./contracts/) — JSON script definitions for search and detail
- [Feature Specification](./spec.md) — Complete requirements and acceptance criteria
- [Iowa SOS Website](https://sos.iowa.gov/search/business/Search.aspx) — Source website
- [Diagnostics API](../../../BizScraper.Api/DIAGNOSTICS.md) — Full diagnostics endpoint documentation
