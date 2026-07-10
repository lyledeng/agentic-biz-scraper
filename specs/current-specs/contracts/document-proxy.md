# Contract: Document Proxy

**Endpoint**: `GET /api/v1/documents/{**blobPath}`
**Tag**: Documents
**Name**: StreamDocument

## Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| blobPath | path | string | ✓ | Blob storage path |
| download | query | bool | | Force download (Content-Disposition: attachment) |
| If-None-Match | header | string | | ETag for conditional request |

## Response

**200 OK** — PDF file stream

| Header | Value |
|--------|-------|
| Content-Type | application/pdf |
| Cache-Control | public, max-age=86400, immutable |
| ETag | Blob ETag value |
| Content-Disposition | inline (or attachment if `?download=true`) |

**304 Not Modified** — ETag matches `If-None-Match` header.

## Error Responses

| Status | Error Type | Cause |
|--------|-----------|-------|
| 400 | validation-error | Invalid blob path |
| 404 | not-found | Blob not found in storage |
| 502 | upstream-error | Blob storage error |
| 504 | execution-timeout | Storage timeout |
