# Data Model: Microsoft Entra ID Authentication

**Feature**: 025-entra-id-auth | **Date**: 2026-04-02

---

## Schema Changes

### SearchAuditEntries — New Column

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| CallerIdentity | TEXT | YES | NULL | UPN (user@contoso.com) for delegated tokens, app client ID for client-credentials tokens |

**Migration**: `2.0.2604.0-add-caller-identity-to-search-audit.sql`

### ScriptExecutionAuditEntries — New Column

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| CallerIdentity | TEXT | YES | NULL | UPN (user@contoso.com) for delegated tokens, app client ID for client-credentials tokens |

**Migration**: `2.0.2604.1-add-caller-identity-to-script-execution-audit.sql`

---

## Domain Entity Changes

### SearchAuditEntry (modified)

| Property | Type | Change | Description |
|----------|------|--------|-------------|
| CallerIdentity | string? | **Added** | Authenticated caller identity extracted from JWT claims |

### ScriptExecutionAuditEntry (modified)

| Property | Type | Change | Description |
|----------|------|--------|-------------|
| CallerIdentity | string? | **Added** | Authenticated caller identity extracted from JWT claims |

---

## Configuration Entities (new)

### AzureAd Configuration Section (`appsettings.json`)

| Key | Type | Required | Description |
|-----|------|----------|-------------|
| Instance | string | ✓ | `https://login.microsoftonline.com/` |
| TenantId | string | ✓ | Entra ID tenant GUID |
| ClientId | string | ✓ | API app registration client ID |
| Audience | string | ✓ | `api://<api-client-id>` |

> Managed by Microsoft.Identity.Web's built-in `MicrosoftIdentityOptions`. No custom options class needed.

### MSAL Configuration (Angular `environment.ts`)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| clientId | string | ✓ | UI app registration client ID |
| authority | string | ✓ | `https://login.microsoftonline.com/<tenant-id>` |
| redirectUri | string | ✓ | Redirect URI after login |
| postLogoutRedirectUri | string | ✓ | Redirect URI after sign-out |
| scopes | string[] | ✓ | `['api://<api-client-id>/access_as_user']` |

---

## Entra ID Entities (configured in Azure Portal — not in code)

### App Registration: BizScraper API

| Setting | Value |
|---------|-------|
| Name | BizScraper API |
| Supported account types | Single tenant |
| Application ID URI | `api://<client-id>` |
| Exposed scope | `access_as_user` (delegated, admin consent not required) |
| App role | `BizScraper.Execute` (application type, for client credentials) |

### App Registration: BizScraper UI

| Setting | Value |
|---------|-------|
| Name | BizScraper UI |
| Supported account types | Single tenant |
| Platform | Single-page application (SPA) |
| Redirect URIs (Dev) | `http://localhost:4200` |
| Redirect URIs (Assembly) | `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui` |
| API permissions | `api://<api-client-id>/access_as_user` (delegated) |

### App Registration: BizScraper System Client (for M2M)

| Setting | Value |
|---------|-------|
| Name | BizScraper System Client |
| Supported account types | Single tenant |
| Client credential | Client secret (stored in Key Vault) |
| API permissions | `BizScraper.Execute` (application, requires admin consent) |

---

## Relationships

```
BizScraper UI App Registration
    ──[requests delegated scope]──▶ BizScraper API App Registration
                                        │
BizScraper System Client                │
    ──[assigned app role]───────────────┘
                                        │
                                        ▼
                            Token validated by API
                                        │
                                        ▼
                            CallerIdentity stored in
                            SearchAuditEntries /
                            ScriptExecutionAuditEntries
```
