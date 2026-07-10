# Database Contract: Entity Detail Audit Table Migration

**Feature**: 007-wy-entity-details  
**Date**: 2026-03-25

## Changelog: 2.0.2603.3-add-state-column-to-entity-detail-audit.sql

### Forward Migration

```sql
ALTER TABLE dbo.EntityDetailAuditEntries
ADD StateCode NVARCHAR(10) NULL;
```

### Rollback

```sql
ALTER TABLE dbo.EntityDetailAuditEntries
DROP COLUMN StateCode;
```

### Schema After Migration

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | UNIQUEIDENTIFIER | NOT NULL | PK |
| DetailsUrl | NVARCHAR(2000) | NOT NULL | |
| EntityIdNumber | NVARCHAR(50) | NULL | |
| EntityName | NVARCHAR(500) | NULL | |
| EntityStatus | NVARCHAR(50) | NULL | |
| CertificateUrl | NVARCHAR(2000) | NULL | |
| DiagnosticsUrl | NVARCHAR(2000) | NULL | |
| DurationMs | BIGINT | NOT NULL | |
| Status | NVARCHAR(50) | NOT NULL | |
| ErrorDetail | NVARCHAR(2000) | NULL | |
| CorrelationId | NVARCHAR(100) | NOT NULL | |
| CreatedAt | DATETIMEOFFSET | NOT NULL | |
| **StateCode** | **NVARCHAR(10)** | **NULL** | **NEW — "CO" or "WY"** |

### Rationale

- `NULL` for backward compatibility with existing CO records
- New records always populate StateCode
- NVARCHAR(10) accommodates future 2-letter state codes
