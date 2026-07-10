# Contract: CORS Policy for BizScraper API

**Feature**: 010-test-ui
**Affected Component**: `src/BizScraper.Api/Program.cs`
**Type**: API change required by this feature

## Overview

The BizScraper API must enable Cross-Origin Resource Sharing (CORS) for the Test UI origin. Without this change, browser security blocks all API calls from the Angular app.

## Policy Specification

| Property | Value |
|----------|-------|
| Policy Name | `TestUiPolicy` |
| Allowed Origins | Configurable via `Cors:TestUiOrigin` in `appsettings.json` |
| Allowed Methods | `GET` |
| Allowed Headers | `Content-Type`, `Accept` |
| Allow Credentials | `false` |

## Configuration

**`appsettings.json`** (default — production override via environment):
```json
{
  "Cors": {
    "TestUiOrigin": "https://bizscraper-ui.aks.example.com"
  }
}
```

**`appsettings.Development.json`** (local dev override):
```json
{
  "Cors": {
    "TestUiOrigin": "http://localhost:4200"
  }
}
```

## Code Change — `Program.cs`

```csharp
// Register CORS policy (before builder.Build())
var testUiOrigin = builder.Configuration["Cors:TestUiOrigin"]
    ?? "http://localhost:4200";

builder.Services.AddCors(options =>
{
    options.AddPolicy("TestUiPolicy", policy =>
    {
        policy.WithOrigins(testUiOrigin)
              .WithMethods("GET")
              .WithHeaders("Content-Type", "Accept");
    });
});

// Apply CORS middleware (after builder.Build(), before UseRouting)
app.UseCors("TestUiPolicy");
```

## Middleware Placement

```csharp
// Correct order in the middleware pipeline:
app.UseCors("TestUiPolicy");      // ← BEFORE routing
app.UseRouting();
app.UseAuthentication();          // (if applicable)
app.UseAuthorization();
app.MapEndpoints();               // or app.MapGet(...)
```

CORS middleware must precede routing to ensure pre-flight `OPTIONS` requests are handled before they reach the endpoint pipeline.

## Security Notes

- The allowed origin is a single, explicit value — not a wildcard (`*`).
- `AllowCredentials()` is NOT called — no cookies or auth headers are transmitted.
- This policy MUST NOT be applied globally if other endpoints do not require CORS.
- In production, `Cors:TestUiOrigin` is set to the AKS ingress hostname of the UI via Helm values or a Kubernetes ConfigMap.
