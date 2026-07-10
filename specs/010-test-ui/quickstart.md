# Quickstart: Test UI for BizScraper API

**Feature**: 010-test-ui
**Date**: 2026-03-26

## Prerequisites

- Node.js 22 LTS (`node --version` → `v22.x`)
- Angular CLI 17 (`npm install -g @angular/cli@17`)
- BizScraper API running locally at `https://localhost:8443` (CORS policy configured for `http://localhost:4200`)
- Docker Desktop (for container build verification)
- Helm 3 (for chart validation)

## Angular App — Local Development

```powershell
# from repo root
cd src/test-ui

# Install dependencies
npm install

# Start dev server (hot-reload)
ng serve --open
# → http://localhost:4200
```

Set the API base URL in the on-screen input field to `https://localhost:8443` for local API use.

## Angular App — Production Build

```powershell
cd src/test-ui
ng build --configuration production
# Output: src/test-ui/dist/test-ui/browser/
```

## Docker Build & Run

```powershell
# from repo root
docker build -f src/bizscraper-ui-dockerfile -t bizscraper-ui:latest .

docker run -p 8080:80 bizscraper-ui:latest
# → http://localhost:8080
```

## Helm Chart — Lint & Template Validation

```powershell
helm lint deploy/helm/bizscraper-ui/

helm template bizscraper-ui deploy/helm/bizscraper-ui/ \
  --set image.tag=latest \
  --set ingress.host=bizscraper.dev.example.com \
  | kubectl apply --dry-run=client -f -
```

## BizScraper API — CORS Configuration (for local dev)

Ensure `appsettings.Development.json` includes:

```json
{
  "Cors": {
    "TestUiOrigin": "http://localhost:4200"
  }
}
```

And that `Program.cs` registers and applies the `"TestUiPolicy"` CORS policy (see contracts [cors-policy.md](contracts/cors-policy.md)).

## API Usage Examples (via test UI)

### Business Search

1. Open `http://localhost:4200`
2. Set API URL to `https://localhost:8443`
3. Enter search term: `Wendy`
4. Select state: `WY`
5. Click **Search**
6. Grid displays matching entities

### Entity Details

1. Click any row in the search results grid
2. Detail view loads entity name, status, formation date, registered agent, and history documents
3. Click a document file name → PDF opens in new tab

### Direct API calls for verification

```bash
# Business search
curl -k "https://localhost:8443/api/v1/business-search?name=Wendy&state=WY"

# Entity details
curl -k "https://localhost:8443/api/v1/entity-details?state=WY&detailsUrl=<url>"
```

## Key Files

| File | Purpose |
|------|---------|
| `src/test-ui/src/app/app.config.ts` | Angular providers: router, HTTP client, WK modules |
| `src/test-ui/src/app/app.routes.ts` | Route definitions: `/search`, `/entity-details` |
| `src/test-ui/src/app/core/services/api-config.service.ts` | API base URL (localStorage) |
| `src/test-ui/src/app/core/services/search.service.ts` | `GET /api/v1/business-search` |
| `src/test-ui/src/app/core/services/entity-details.service.ts` | `GET /api/v1/entity-details` |
| `src/test-ui/nginx.conf` | nginx config: SPA fallback + security headers |
| `src/bizscraper-ui-dockerfile` | Multi-stage docker build |
| `deploy/helm/bizscraper-ui/values.yaml` | Helm chart default values |
| `src/BizScraper.Api/Program.cs` | CORS policy registration (modify) |

## Environment Variables — Container

| Variable | Default | Description |
|----------|---------|-------------|
| (none) | — | API URL is user-configurable at runtime via localStorage; no build-time env vars needed |

## Helm Deployment

```powershell
# Deploy to AKS
helm upgrade --install bizscraper-ui deploy/helm/bizscraper-ui/ \
  --namespace bizscraper \
  --set image.repository=devacr.azurecr.io/bizscraper-ui \
  --set image.tag=<ci-build-tag> \
  --set ingress.host=bizscraper.aks.example.com
```

## Running Unit Tests

```powershell
cd src/test-ui
ng test --watch=false --browsers=ChromeHeadless
```
