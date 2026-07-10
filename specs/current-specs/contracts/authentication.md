# Contract: Authentication

**Feature**: 025-entra-id-auth | **Date**: 2026-04-02

---

## Authentication Scheme

| Setting | Value |
|---------|-------|
| Scheme | Bearer (JWT) |
| Token Issuer | `https://login.microsoftonline.com/{tenantId}/v2.0` |
| Audience | `api://<api-client-id>` |
| Tenant Mode | Single-tenant |

## Protected Endpoints

All endpoints require a valid bearer token **except** those listed under Anonymous Endpoints.

| Endpoint | Method | Auth Required |
|----------|--------|---------------|
| `/api/v2/execute-script` | POST | ✅ Yes |
| `/api/v2/definitions` | GET | ✅ Yes |
| `/api/v1/documents/{**blobPath}` | GET | ✅ Yes |
| `/healthz` | GET | ❌ No |
| `/ready` | GET | ❌ No |
| `/swagger/**` | GET | ❌ No |
| `/` | GET | ❌ No (redirects to /healthz) |

## Request Format

```http
Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGciOi...
```

## Error Responses

### 401 Unauthorized — Missing or Invalid Token

Returned when:
- No `Authorization` header is present
- Token is expired
- Token signature is invalid
- Token issuer does not match configured tenant
- Token audience does not match API client ID

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401
}
```

**Response Headers**:
```
WWW-Authenticate: Bearer error="invalid_token", error_description="..."
```

### 403 Forbidden — Insufficient Permissions

Returned when:
- Token is valid but lacks the required scope (`access_as_user`) or role (`BizScraper.Execute`)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403
}
```

## Token Types Accepted

### Delegated (User) Token

Obtained via Authorization Code + PKCE flow. Contains:
- `scp` claim with `access_as_user`
- `preferred_username` claim with user's UPN
- `aud` claim matching `api://<api-client-id>`

### Application Token (Client Credentials)

Obtained via client credentials flow. Contains:
- `roles` claim containing `BizScraper.Execute`
- `azp` claim with calling application's client ID
- `aud` claim matching `api://<api-client-id>`
- No `scp` or `preferred_username` claims

## Audit Trail Impact

All authenticated requests record the caller identity:
- **Delegated tokens**: `preferred_username` (e.g., `user@contoso.com`)
- **Application tokens**: `azp` client ID (e.g., `a1b2c3d4-...`)
- Stored in `SearchAuditEntries.CallerIdentity` and `ScriptExecutionAuditEntries.CallerIdentity`

## CORS Update

| Header | Previous Value | Updated Value |
|--------|---------------|---------------|
| `Access-Control-Allow-Headers` | `Content-Type, Accept` | `Content-Type, Accept, Authorization` |

## Swagger OAuth2

The Swagger UI provides an "Authorize" button supporting OAuth2 Authorization Code + PKCE against the Entra ID tenant. Scopes: `api://<api-client-id>/access_as_user`.
