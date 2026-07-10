# Research: Microsoft Entra ID Authentication

**Feature**: 025-entra-id-auth | **Date**: 2026-04-02

---

## R1: Microsoft.Identity.Web for ASP.NET Core (.NET 10)

**Decision**: Use `Microsoft.Identity.Web` NuGet package for backend token validation.

**Rationale**:
- Official Microsoft library for integrating Entra ID with ASP.NET Core
- Handles JWT bearer token validation, issuer/audience checks, signing key rotation automatically
- Supports both delegated (user) and application (client credentials) tokens in a single configuration
- License: MIT — compatible with project requirements
- Works with minimal API endpoints via `.RequireAuthorization()`
- No reflection-heavy patterns — compatible with AOT readiness goals

**Alternatives Considered**:
- Raw `Microsoft.AspNetCore.Authentication.JwtBearer` — lower-level, requires manual OIDC discovery and claim mapping. Rejected because Microsoft.Identity.Web wraps it with Entra-specific defaults.
- Custom JWT validation middleware — violates YAGNI and introduces security risk from DIY token validation.

**Integration Pattern**:
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();

// In pipeline:
app.UseAuthentication();
app.UseAuthorization();

// Protect endpoints:
app.MapExecuteScriptEndpoints().RequireAuthorization();
app.MapDocumentEndpoints().RequireAuthorization();
app.MapHealthCheckEndpoints().AllowAnonymous();
```

---

## R2: MSAL for Angular 17

**Decision**: Use `@azure/msal-browser` + `@azure/msal-angular` for the test-ui.

**Rationale**:
- Official Microsoft MSAL library for SPAs
- `@azure/msal-angular` v3.x supports Angular 17 (peer dependency `@angular/core >=14`)
- Provides `MsalGuard` (route protection), `MsalInterceptor` (automatic token attachment), and `MsalRedirectComponent`
- Redirect interaction type avoids popup-blocker issues in corporate environments
- License: MIT

**Alternatives Considered**:
- `angular-auth-oidc-client` — generic OIDC library, but adds unnecessary abstraction over the Entra-specific MSAL SDK. Rejected.
- Custom OAuth implementation — security risk, violates KISS. Rejected.

**Integration Pattern**:
```typescript
// app.config.ts
import { MsalModule, MsalGuard, MsalInterceptor, MsalRedirectComponent } from '@azure/msal-angular';
import { PublicClientApplication, InteractionType } from '@azure/msal-browser';

provideHttpClient(withInterceptorsFromDi()),
importProvidersFrom(
  MsalModule.forRoot(new PublicClientApplication({ auth: { clientId, authority, redirectUri } }),
    { interactionType: InteractionType.Redirect, authRequest: { scopes: ['api://<api-client-id>/access_as_user'] } },
    { interactionType: InteractionType.Redirect, protectedResourceMap: new Map([['<api-base-url>/*', ['api://<api-client-id>/access_as_user']]]) }
  )
)
```

---

## R3: Single-Tenant Token Validation Configuration

**Decision**: Configure `AzureAd:TenantId` as a specific tenant GUID, not `common` or `organizations`.

**Rationale**:
- Single-tenant restricts accepted tokens to one Entra ID tenant
- Microsoft.Identity.Web validates the `iss` claim against `https://login.microsoftonline.com/{tenantId}/v2.0`
- Tokens from other tenants are automatically rejected (401)
- Simplest security model — no tenant allowlist logic needed

**Configuration Shape** (`appsettings.json`):
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-guid>",
    "ClientId": "<api-app-registration-client-id>",
    "Audience": "api://<api-app-registration-client-id>"
  }
}
```

---

## R4: Protecting Minimal API Endpoints

**Decision**: Use `.RequireAuthorization()` on endpoint groups and `.AllowAnonymous()` on health/swagger.

**Rationale**:
- ASP.NET Core minimal APIs support authorization via endpoint metadata
- `MapGroup()` or individual `Map*()` chains accept `.RequireAuthorization()`
- Health endpoints need `.AllowAnonymous()` for K8s probes
- Swagger UI needs `.AllowAnonymous()` for developer access
- The root redirect (`/` → `/healthz`) also needs `.AllowAnonymous()`

**Middleware Ordering** (critical):
```
UseExceptionHandler()
UseCors()
UseAuthentication()      // <-- NEW: before UseAuthorization
UseAuthorization()       // <-- NEW: after UseAuthentication
UseMiddleware<CorrelationId>
UseMiddleware<SecurityHeaders>
UseMiddleware<RequestTimeout>
```

---

## R5: Caller Identity Extraction from JWT Claims

**Decision**: Extract `preferred_username` (UPN) for delegated tokens and `azp` (app ID) for application tokens.

**Rationale**:
- Delegated tokens contain `preferred_username` claim = user's UPN (e.g., `user@contoso.com`)
- Application tokens have no user context; use `azp` (authorized party / client ID) to identify the calling system
- Distinguish token type via presence/absence of `scp` (scope) claim:
  - `scp` present → delegated token → use `preferred_username`
  - `roles` present without `scp` → application token → use `azp`
- Store as `CallerIdentity` TEXT column in both audit tables

**Implementation**:
```csharp
static string ExtractCallerIdentity(ClaimsPrincipal principal)
{
    var upn = principal.FindFirstValue("preferred_username");
    if (!string.IsNullOrEmpty(upn)) return upn;

    var appId = principal.FindFirstValue("azp");
    return appId ?? "unknown";
}
```

---

## R6: CORS Policy Update for Authorization Header

**Decision**: Add `Authorization` to the CORS `WithHeaders()` configuration.

**Rationale**:
- The current CORS policy allows `Content-Type` and `Accept` headers only
- MSAL attaches `Authorization: Bearer <token>` to API requests
- The browser preflight (OPTIONS) request must receive `Access-Control-Allow-Headers: Authorization` or the actual request will be blocked
- Simple change: `.WithHeaders("Content-Type", "Accept", "Authorization")`

---

## R7: Swagger OAuth2 Configuration

**Decision**: Configure Swashbuckle with OAuth2 authorization code flow + PKCE for Swagger UI.

**Rationale**:
- Developers need to test authenticated endpoints directly from Swagger
- Swashbuckle supports `AddSecurityDefinition` + `AddSecurityRequirement` for OAuth2
- Configure authorization endpoint from the Entra ID v2.0 discovery document
- Swagger UI will show an "Authorize" button that triggers the OAuth2 flow

**Configuration**:
```csharp
options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
{
    Type = SecuritySchemeType.OAuth2,
    Flows = new OpenApiOAuthFlows
    {
        AuthorizationCode = new OpenApiOAuthFlow
        {
            AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"),
            TokenUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token"),
            Scopes = new Dictionary<string, string>
            {
                { $"api://{clientId}/access_as_user", "Access BizScraper API" }
            }
        }
    }
});
```

---

## R8: Angular Environment Files Strategy

**Decision**: Create `environment.ts` and `environment.development.ts` with MSAL configuration.

**Rationale**:
- The test-ui does not currently have environment files — API URL is hardcoded via `ApiConfigService` + localStorage
- MSAL requires `clientId`, `authority`, `redirectUri`, and `scopes` at bootstrap — these vary by environment
- Angular CLI supports `fileReplacements` in `angular.json` for build-time environment swapping
- The Docker build already accepts `API_BASE_URL` as a build arg — extend this pattern for MSAL config
- Redirect URIs:
  - Dev: `http://localhost:4200`
  - Assembly: `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui`

**Environment Shape**:
```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:8443',
  msalConfig: {
    clientId: '<ui-app-client-id>',
    authority: 'https://login.microsoftonline.com/<tenant-id>',
    redirectUri: 'http://localhost:4200',
    postLogoutRedirectUri: 'http://localhost:4200',
    scopes: ['api://<api-client-id>/access_as_user']
  }
};
```

---

## R9: Package License Verification

**Decision**: Both Microsoft.Identity.Web and @azure/msal-* packages are MIT licensed.

**Details**:
- `Microsoft.Identity.Web` — MIT License (https://github.com/AzureAD/microsoft-identity-web/blob/master/LICENSE)
- `@azure/msal-browser` — MIT License (https://github.com/AzureAD/microsoft-authentication-library-for-js/blob/dev/LICENSE)
- `@azure/msal-angular` — MIT License (same repo)
- All compatible with project's open-source-only package policy
