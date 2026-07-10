# Data Model: Semantic Error Handling for Colorado Exceeded Record Count

**Feature**: 012-co-record-count-error  
**Date**: 2026-03-27

## Overview

This feature introduces no new data entities, database tables, or schema changes. All required types already exist in the codebase.

## Existing Entities (unchanged)

### CheckTextMatchException

- **Location**: `Infrastructure/Scraping/Engine/Actions/CheckTextActionHandler.cs`
- **Properties**: `ErrorType` (string), `Message` (string, inherited)
- **Role**: Thrown by the scraping engine when a flow's `check-text` action with `onMatch=throw` matches page content

### ExceededRecordCountException

- **Location**: `Features/BusinessSearch/Exceptions/ExceededRecordCountException.cs`
- **Properties**: `Message` (string, inherited)
- **Role**: Domain-specific exception for the "Exceeded Record Count" business-rule violation, mapped to HTTP 422

### Audit Trail (existing table)

- **Status column**: Already supports `"ExceededRecordCount"` value via `SearchBusinessEntitiesHandler.MapStatus()`
- **No schema changes required**

## State Transitions

```
CheckTextMatchException (engine layer)
  → ExceededRecordCountException (scraper layer, via existing catch in SearchCoreAsync)
    → HTTP 422 Problem Details (handler layer, via GlobalExceptionHandler)
    → Audit entry with status "ExceededRecordCount" (handler layer, via MapStatus)
```
