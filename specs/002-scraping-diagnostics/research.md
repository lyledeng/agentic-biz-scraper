# Research: Scraping Diagnostics — Traces & Screenshots

**Feature**: 002-scraping-diagnostics  
**Date**: 2026-03-24

## R1: Playwright .NET Tracing API

### Decision
Use `IBrowserContext.Tracing.StartAsync()` and `StopAsync()` with the built-in Playwright tracing API. Tracing is started at the `BrowserContext` level (per-request), not at the `Browser` level.

### Rationale
- Playwright tracing is designed for exactly this use case — capturing browser session diagnostics.
- The API is stable and bundled with `Microsoft.Playwright` (no additional packages needed).
- Traces are saved as `.zip` files that contain DOM snapshots, network HAR, console logs, and optionally periodic screenshots — all replayable in the Playwright Trace Viewer.
- The trace captures every action on every page within the context, making it ideal for multi-page scraping sessions.

### API Usage
```csharp
// Start tracing after creating the browser context
await context.Tracing.StartAsync(new TracingStartOptions
{
    Screenshots = true,   // Embed periodic screenshots in the trace
    Snapshots = true,     // Capture DOM snapshots at each action
    Sources = false       // No source files needed (we're scraping, not testing our own app)
});

// ... perform all scraping actions ...

// Stop tracing and save the archive
await context.Tracing.StopAsync(new TracingStopOptions
{
    Path = "/path/to/trace.zip"
});
```

### Key Observations
- `StartAsync` must be called before any navigation on the context.
- `StopAsync` writes the `.zip` atomically — no partial files on success.
- If the context is disposed before `StopAsync`, the trace data is lost.
- The `Screenshots = true` option captures a screenshot after each action step (click, fill, navigate), not just at explicit `ScreenshotAsync` calls.
- Trace files are typically 500KB–5MB depending on session length and screenshot density.

### Alternatives Considered
- **Manual HAR capture** (`context.Tracing` vs `page.RouteAsync` + HAR recording): More complex, doesn't capture DOM snapshots. Rejected.
- **Custom trace format**: Would require building a viewer. No benefit over Playwright's format. Rejected.

---

## R2: Playwright .NET Screenshot API

### Decision
Use `IPage.ScreenshotAsync()` for milestone screenshots. Screenshots are captured as full-page PNGs at defined workflow points.

### Rationale
- `ScreenshotAsync` is a single async call that produces a PNG byte array or writes directly to a file path.
- Full-page screenshots capture the entire rendered content, including below-the-fold results.
- PNGs are human-readable without any special tooling — operators can view them directly in any image viewer or browser.

### API Usage
```csharp
await page.ScreenshotAsync(new PageScreenshotOptions
{
    Path = "/path/to/screenshot.png",
    FullPage = true
});
```

### Key Observations
- `FullPage = true` captures the entire scrollable content, not just the viewport. Important for results tables that may extend beyond the visible area.
- Screenshot files are typically 100KB–500KB per capture.
- The call is synchronous relative to the page — it waits for the current frame to be painted.
- Screenshots at milestones are independent of trace-embedded screenshots (which are per-action). Both serve different purposes: milestone PNGs for quick visual triage; trace screenshots for action-by-action replay.

### Alternatives Considered
- **Viewport-only screenshots**: Would miss below-the-fold results. Rejected.
- **JPEG format**: Smaller files but lossy — text can become unreadable. Rejected for diagnostic use.

---

## R3: Azure Playwright Workspaces Compatibility

### Decision
Playwright tracing and screenshots work with Azure Playwright Workspaces (remote browser via WSS) without modification.

### Rationale
- Azure Playwright Workspaces exposes a standard Chromium browser over WebSocket. The Playwright client runs locally and sends commands to the remote browser.
- Tracing and screenshot APIs operate at the client level — the client requests trace data and screenshots from the remote browser, which transfers them back over the WebSocket connection.
- The trace `.zip` and screenshot `.png` files are written to the **local filesystem** (where the .NET app runs), not on the remote browser host.
- No Azure-specific configuration is needed for tracing or screenshots.

### Key Observations
- Trace file size may be slightly larger with remote browsers due to network serialization overhead, but this is negligible.
- The `StopAsync` call may take slightly longer over WSS as trace data is transferred from the remote browser to the client before being written to disk.
- If the WSS connection drops during `StopAsync`, the trace data may be lost. This is handled by FR-008 (diagnostic failures don't propagate).

### Alternatives Considered
- **Server-side trace storage on Azure Playwright Workspaces**: Not supported by the service. Traces must be collected client-side. N/A.

---

## R4: File Naming Convention

### Decision
Use the pattern `{correlationId}_{utcTimestamp}_{artifactType}.{extension}` for all diagnostic artifacts.

### Rationale
- The correlation ID is already generated per-request by the `CorrelationIdMiddleware` and available via `HttpContext.TraceIdentifier`.
- Including the UTC timestamp ensures uniqueness even if correlation IDs were somehow reused, and allows chronological sorting in file explorers.
- The artifact type suffix (`trace`, `01-page-load`, `02-search-results`, `03-page-N`, `error`) makes each file's purpose immediately clear without opening it.

### Format
```
{correlationId}_{yyyyMMddTHHmmssfffZ}_{milestone}.{ext}

Examples:
  abc123_20260324T143025123Z_trace.zip
  abc123_20260324T143025123Z_01-page-load.png
  abc123_20260324T143026456Z_02-search-results.png
  abc123_20260324T143027789Z_03-page-2.png
  abc123_20260324T143028012Z_error.png
```

### Key Observations
- All diagnostic artifacts for a given request share the same correlation ID prefix, making `grep` or `ls | grep` trivial.
- The timestamp uses ISO 8601 compact format (no colons or hyphens in the time portion) for filesystem compatibility across Windows and Linux.
- Milestone numbering (`01-`, `02-`, `03-`) ensures natural sort order reflects workflow order.

### Alternatives Considered
- **GUID-based filenames**: Would make correlation with requests harder. Rejected.
- **Subdirectory per request**: Adds directory management complexity for little benefit. Rejected.
- **Correlation ID only (no timestamp)**: Risk of collision if correlation IDs are short. Rejected.

---

## R5: Conditional Tracing Strategy (on-failure mode)

### Decision
When tracing mode is "on-failure", always start tracing but only save the trace file if the scraping session fails. Discard the trace data on success.

### Rationale
- Playwright requires `StartAsync` before the first action to capture the full session. There's no way to "retroactively" start tracing after a failure occurs.
- The performance overhead of tracing without saving is minimal — the browser captures data in memory, and discarding it (by not calling `StopAsync` with a path, or calling `StopAsync` without saving) is cheap.
- This "always capture, conditionally save" approach is the standard pattern recommended by Playwright documentation for CI and production diagnostics.

### Implementation Pattern
```csharp
var tracingEnabled = mode is TracingMode.Always or TracingMode.OnFailure;
if (tracingEnabled)
{
    await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
}

bool failed = false;
try
{
    // ... scraping logic ...
}
catch
{
    failed = true;
    throw;
}
finally
{
    if (tracingEnabled && (mode == TracingMode.Always || (mode == TracingMode.OnFailure && failed)))
    {
        await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
    }
    else if (tracingEnabled)
    {
        // Discard trace data without saving
        await context.Tracing.StopAsync(new TracingStopOptions());
    }
}
```

### Alternatives Considered
- **Start tracing only on failure (replay impossible)**: Can't capture what already happened. Rejected.
- **Always save, delete on success**: Extra I/O for the common case. Rejected.
