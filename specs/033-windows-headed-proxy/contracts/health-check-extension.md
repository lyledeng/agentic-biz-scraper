# Health Check Contract Extension: Windows Proxy

**Feature**: 033-windows-headed-proxy

## GET /ready — Extended Response

No new endpoints are added. The existing `/ready` endpoint response is extended with Windows proxy status.

### New Fields in ReadinessHealthResponse

```json
{
  "status": "Ready",
  "browserModes": {
    "local": { "status": "healthy", "channel": "chrome" },
    "remote": { "status": "healthy", "endpoint": "wss://..." }
  },
  "windowsProxy": {
    "status": "healthy",
    "endpoint": "https://aegis.ilienonline.com/mvpoc/bizscrapper-api",
    "error": null
  },
  "definitionsRequiringLocal": ["..."],
  "definitionsRequiringRemote": ["..."],
  "definitionsRoutedToWindows": ["us-ia-business-search", "us-ia-entity-details"]
}
```

### WindowsProxy Status Values

| Status | Meaning |
|--------|---------|
| `"healthy"` | Windows VM endpoint is configured and responding |
| `"unhealthy"` | Windows VM endpoint is configured but not responding |
| `"not-configured"` | No `WindowsProxy:EndpointUrl` configured |

### Backward Compatibility

- The `windowsProxy` and `definitionsRoutedToWindows` fields are additive.
- Existing consumers of `/ready` that don't recognize these fields will ignore them.
- The overall `status` field logic is unchanged: `"Ready"` if any mode is healthy, `"NotReady"` only if all modes fail. Windows proxy unhealthiness does not cause NotReady (other modes still work).
