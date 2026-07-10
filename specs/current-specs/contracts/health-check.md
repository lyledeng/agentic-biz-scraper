# Contract: Health Check

## Liveness Probe

**Endpoint**: `GET /healthz`

### Response

**200 OK**

```json
{ "status": "Healthy" }
```

## Readiness Probe

**Endpoint**: `GET /ready`

Validates Playwright browser connectivity.

### Response

**200 OK** — Browser is available

```json
{ "status": "Ready" }
```

**503 Service Unavailable** — Browser is not available

```json
{ "status": "Not Ready", "error": "<details>" }
```
