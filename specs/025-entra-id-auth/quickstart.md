# Quickstart: Microsoft Entra ID App Registration Setup

**Feature**: 025-entra-id-auth | **Date**: 2026-04-02

This guide walks through creating the required Entra ID app registrations
for the BizScraper API and test-ui in both the **Dev** and **Assembly**
environments.

---

## Prerequisites

- Azure CLI installed (`az --version`)
- Signed in to the correct Entra ID tenant (`az login --tenant <tenant-id>`)
- Permission to create app registrations in the tenant (Application Developer role or higher)

---

## Step 1: Register the API Application

This app registration represents the BizScraper backend API.

### 1.1 Create the App Registration

```bash
az ad app create \
  --display-name "BizScraper API" \
  --sign-in-audience "AzureADMyOrg" \
  --identifier-uris "api://<note-the-client-id-after-creation>"
```

> After creation, note the **Application (client) ID**. You will use it as `<api-client-id>` throughout.

### 1.2 Update the Identifier URI

```bash
az ad app update \
  --id <api-client-id> \
  --identifier-uris "api://<api-client-id>"
```

### 1.3 Expose the Delegated Scope (`access_as_user`)

In the Azure Portal:
1. Navigate to **App registrations** → **BizScraper API** → **Expose an API**
2. Click **+ Add a scope**
3. Fill in:
   - **Scope name**: `access_as_user`
   - **Who can consent**: Admins and users
   - **Admin consent display name**: Access BizScraper API
   - **Admin consent description**: Allow the application to access BizScraper API on behalf of the signed-in user
   - **User consent display name**: Access BizScraper API
   - **User consent description**: Allow the application to access BizScraper on your behalf
   - **State**: Enabled
4. Click **Add scope**

Or via CLI:
```bash
az ad app update --id <api-client-id> --set api='{"oauth2PermissionScopes":[{"adminConsentDescription":"Access BizScraper API on behalf of the signed-in user","adminConsentDisplayName":"Access BizScraper API","id":"<generate-a-guid>","isEnabled":true,"type":"User","userConsentDescription":"Access BizScraper on your behalf","userConsentDisplayName":"Access BizScraper API","value":"access_as_user"}]}'
```

### 1.4 Define the Application Role (`BizScraper.Execute`)

In the Azure Portal:
1. Navigate to **App registrations** → **BizScraper API** → **App roles**
2. Click **+ Create app role**
3. Fill in:
   - **Display name**: BizScraper Execute
   - **Allowed member types**: Applications
   - **Value**: `BizScraper.Execute`
   - **Description**: Allows system-to-system execution of BizScraper scripts
   - **State**: Enabled
4. Click **Apply**

---

## Step 2: Register the UI Application (SPA)

This app registration represents the Angular test-ui.

### 2.1 Create the App Registration

```bash
az ad app create \
  --display-name "BizScraper UI" \
  --sign-in-audience "AzureADMyOrg"
```

> After creation, note the **Application (client) ID** — this is `<ui-client-id>`.

### 2.2 Configure SPA Platform with Redirect URIs

In the Azure Portal:
1. Navigate to **App registrations** → **BizScraper UI** → **Authentication**
2. Click **+ Add a platform** → **Single-page application**
3. Add the following **Redirect URIs**:

| Environment | Redirect URI |
|-------------|-------------|
| Dev (local) | `http://localhost:4200` |
| Assembly (AKS) | `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui` |

4. Under **Implicit grant and hybrid flows**, ensure both checkboxes are **unchecked** (MSAL uses auth code + PKCE, not implicit)
5. Click **Configure**

Or via CLI:
```bash
az ad app update --id <ui-client-id> --set spa='{"redirectUris":["http://localhost:4200","https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui"]}'
```

### 2.3 Grant API Permission (Delegated Scope)

In the Azure Portal:
1. Navigate to **App registrations** → **BizScraper UI** → **API permissions**
2. Click **+ Add a permission** → **My APIs** → **BizScraper API**
3. Select **Delegated permissions** → check `access_as_user`
4. Click **Add permissions**

Or via CLI:
```bash
az ad app permission add \
  --id <ui-client-id> \
  --api <api-client-id> \
  --api-permissions "<access_as_user-scope-guid>=Scope"
```

### 2.4 (Optional) Admin Consent

If the tenant requires admin consent for delegated permissions:
```bash
az ad app permission admin-consent --id <ui-client-id>
```

---

## Step 3: Register the System Client (for M2M / Client Credentials)

This app registration represents external systems that call the API without a user.

### 3.1 Create the App Registration

```bash
az ad app create \
  --display-name "BizScraper System Client" \
  --sign-in-audience "AzureADMyOrg"
```

### 3.2 Create a Client Secret

In the Azure Portal:
1. Navigate to **App registrations** → **BizScraper System Client** → **Certificates & secrets**
2. Click **+ New client secret**
3. Enter a description (e.g., "Dev environment") and expiry
4. Copy the secret **Value** immediately — it will not be shown again
5. Store this in Azure Key Vault (secret name: `bizscraper-system-client-secret`)

### 3.3 Assign the Application Role

> **Note**: The Azure Portal's "Users and groups" blade does not support
> assigning app roles to service principals — only to users and groups.
> Use the Azure CLI or Microsoft Graph API instead.

Via CLI:
```bash
# Get the service principal object IDs
API_SP=$(az ad sp show --id <api-client-id> --query id -o tsv)
CLIENT_SP=$(az ad sp show --id <system-client-id> --query id -o tsv)
ROLE_ID=$(az ad app show --id <api-client-id> --query "appRoles[?value=='BizScraper.Execute'].id" -o tsv)

az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${CLIENT_SP}/appRoleAssignments" \
  --body "{\"principalId\":\"${CLIENT_SP}\",\"resourceId\":\"${API_SP}\",\"appRoleId\":\"${ROLE_ID}\"}"
```

---

## Step 4: Configure the Application

### 4.1 Backend API (`appsettings.json`)

Add the `AzureAd` section:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-guid>",
    "ClientId": "<api-client-id>",
    "Audience": "api://<api-client-id>"
  }
}
```

For local development, store these values in .NET User Secrets:
```bash
cd src/BizScraper.Api
dotnet user-secrets set "AzureAd:TenantId" "<tenant-guid>"
dotnet user-secrets set "AzureAd:ClientId" "<api-client-id>"
dotnet user-secrets set "AzureAd:Audience" "api://<api-client-id>"
```

### 4.2 Angular Test-UI (`environment.development.ts`)

```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:8443',
  msalConfig: {
    clientId: '<ui-client-id>',
    authority: 'https://login.microsoftonline.com/<tenant-guid>',
    redirectUri: 'http://localhost:4200',
    postLogoutRedirectUri: 'http://localhost:4200',
    scopes: ['api://<api-client-id>/access_as_user']
  }
};
```

### 4.3 Angular Test-UI — Assembly (`environment.ts`)

```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://devcaas-az.ilienonline.com/mvpoc/bizscraper-api',
  msalConfig: {
    clientId: '<ui-client-id>',
    authority: 'https://login.microsoftonline.com/<tenant-guid>',
    redirectUri: 'https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui',
    postLogoutRedirectUri: 'https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui',
    scopes: ['api://<api-client-id>/access_as_user']
  }
};
```

### 4.4 Helm Values (Assembly)

```yaml
# bizscraper/values.yaml
azureAd:
  instance: "https://login.microsoftonline.com/"
  tenantId: "<tenant-guid>"
  clientId: "<api-client-id>"
  audience: "api://<api-client-id>"
```

---

## Step 5: Verify

### Test Interactive Login (Dev)

1. Start the API: `cd src/BizScraper.Api && dotnet run`
2. Start the UI: `cd src/test-ui && npm start`
3. Open `http://localhost:4200`
4. You should be redirected to `login.microsoftonline.com`
5. Sign in with a tenant account
6. After redirect, the UI should show your display name and search should work

### Test Client Credentials (Dev)

```bash
# Obtain token
TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/<tenant-guid>/oauth2/v2.0/token" \
  -d "client_id=<system-client-id>" \
  -d "client_secret=<secret>" \
  -d "scope=api://<api-client-id>/.default" \
  -d "grant_type=client_credentials" | jq -r .access_token)

# Call API
curl -k https://localhost:8443/api/v2/definitions \
  -H "Authorization: Bearer $TOKEN"
```

### Test Unauthenticated Access Denied

```bash
# Should return 401
curl -k -o /dev/null -w "%{http_code}" https://localhost:8443/api/v2/definitions

# Should return 200 (anonymous)
curl -k -o /dev/null -w "%{http_code}" https://localhost:8443/healthz
```

---

## Reference: Environment Summary

| Value | Dev | Assembly |
|-------|-----|----------|
| API Redirect URI | N/A (API does not redirect) | N/A |
| UI Redirect URI | `http://localhost:4200` | `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui` |
| API Base URL | `https://localhost:8443` | `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-api` |
| Tenant ID | Same across environments | Same across environments |
| API Client ID | Same across environments | Same across environments |
| UI Client ID | Same across environments | Same across environments |
