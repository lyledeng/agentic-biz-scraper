# Research: Windows VM Proxy for Chrome Headed Mode

**Feature**: 033-windows-headed-proxy  
**Date**: 2026-04-06

## R1: Proxy Pattern — Flurl.Http vs HttpClient for Service-to-Service Forwarding

**Decision**: Use Flurl.Http (same pattern as `FabCaptchaSolver` and `FabDocumentTranslator`).

**Rationale**: The project already uses Flurl.Http for all outbound HTTP calls. Using a different HTTP client would introduce inconsistency and break the established convention. Flurl provides fluent API, built-in JSON serialization, and clean timeout handling.

**Alternatives considered**:
- `HttpClientFactory` + typed client: More verbose, requires DI registration ceremony. The project has no existing typed HttpClient pattern.
- YARP (reverse proxy): Overkill for proxying a single endpoint. YARP is designed for multi-route proxying at scale.

## R2: Token Forwarding — Bearer Token Propagation Pattern

**Decision**: Extract the `Authorization` header from the current `HttpContext` and forward it to the Windows VM in the proxy request.

**Rationale**: The caller's Entra ID token is already validated by the AKS API's middleware before reaching the handler. The Windows VM validates against the same Entra ID tenant/app registration, so the same token is valid. This avoids service-to-service token exchange complexity (no client credentials grant needed between AKS and VM).

**Alternatives considered**:
- API key auth (like FAB pattern): Would require a separate shared secret and bypass Entra ID on the Windows VM. The user explicitly requested forwarding the existing token.
- On-behalf-of flow (OBO): Unnecessary complexity — both APIs share the same app registration.

**Implementation**: `IHttpContextAccessor` is already injected into `GenericScriptScraper`. Extract `HttpContext.Request.Headers.Authorization` and pass it through.

## R3: Routing Signal — `mode: "windows"` in BrowserConfig

**Decision**: Add `"windows"` as a valid mode value for the existing `BrowserConfig.Mode` string property. No schema change needed — the property is already a string.

**Rationale**: Clarification Q5 decided on explicit `mode: "windows"` to decouple routing from the `headless` flag. This means:
- `"mode": "local"` → local browser pool (existing)
- `"mode": "remote"` → Azure Playwright workspace (existing)
- `"mode": "windows"` → proxy to Windows VM (new)

The `headless` and `channel` properties remain independent — the Windows VM definition can set `headless: false, channel: "chrome"` to run headed Chrome.

**Alternatives considered**:
- `headless: false` as routing signal: Rejected in clarification — overloads display mode with routing semantics.
- Separate `proxy` property: Over-engineering for a single proxy target.

## R4: Resilience — No Retry on Timeout

**Decision**: Use Polly with no retry policy (`ResiliencePipeline.Empty`). Return errors directly.

**Rationale**: Clarification Q2 decided headed scraping is non-idempotent (browser state, captcha solves, document uploads). Retrying risks duplicate side effects. A single 180s timeout is sufficient; operators should increase the timeout config if needed. 

Connection failures (refused, DNS) return 503 immediately. Timeout returns 504 immediately.

**Alternatives considered**:
- Retry once on timeout: Rejected in clarification due to non-idempotent operations.
- Retry on 5xx: Same concern — scraping may have partially completed with side effects.

## R5: Health Check — Probing the Windows VM

**Decision**: Extend `CanConnectAsync` to probe the Windows VM's `/healthz` endpoint via a lightweight HTTP GET.

**Rationale**: The existing health check probes browser pools by attempting browser connections. The Windows VM probe is simpler — just verify the HTTPS endpoint is reachable and returns 200. This keeps the health check fast while providing useful status information.

**Implementation**: Add a `windowsProxy` section to `ReadinessHealthResponse` alongside the existing `browserModes`. List definitions with `mode: "windows"` under `definitionsRoutedToWindows`.

## R6: CI/CD — Windows Publish and Deploy

**Decision**: Add a new CI stage that publishes a self-contained .NET deployment for `win-x64`, packages it as a zip artifact, and a deploy stage that copies the SQLite DB from the AKS PVC and deploys to the ZUSCUDILNAEGIS VM.

**Rationale**: The user specified:
- Windows VM URL: `https://aegis.ilienonline.com/mvpoc/bizscraper-api`
- Target VM: ZUSCUDILNAEGIS
- SQLite copied from AKS PVC to Windows VM (avoids Liquibase migration on Windows)

**Implementation**:
1. New CI job: `dotnet publish --runtime win-x64 --self-contained` → zip artifact
2. New deploy job: 
   a. `kubectl cp` to extract SQLite DB from the AKS pod's `/data/bizscraper.db`
   b. Copy zip + DB to ZUSCUDILNAEGIS via PowerShell remoting or Azure DevOps deployment group
   c. Stop IIS/Windows Service, extract, place DB, start service

**Alternatives considered**:
- Docker on Windows: The user specified a Windows Server VM, not Windows containers. IIS/Windows Service is simpler for a single VM.
- Shared database: Clarification decided audit is local; DB copy from AKS is a deployment-time sync only.

## R7: Post-Processing Skip for Proxied Requests

**Decision**: When the proxy returns a response, the AKS `ExecuteScriptHandler` skips all post-processing (mapper, DE entity processing, document downloads, audit write).

**Rationale**: The Windows VM runs the full `ExecuteScriptHandler` flow including post-processing. The proxy returns a fully-formed `ExecuteScriptResponse`. The AKS handler should return it directly to avoid duplicate processing.

**Implementation**: The proxy call happens inside `GenericScriptScraper.ExecuteAsync` — but actually, it's cleaner to intercept at the handler level. When the scraper returns a proxied result (flagged), skip the post-processing pipeline. Alternatively, the proxy can return the final `ExecuteScriptResponse` directly and the handler can detect this. 

Simplest approach: Check `mode: "windows"` in the handler before calling the scraper. If windows mode + configured endpoint, call `WindowsApiProxy` directly and return the response. This bypasses the scraper entirely for proxied requests.
