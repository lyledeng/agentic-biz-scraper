-- liquibase formatted sql

--changeset bizscraper:1.0.2603.0-create-search-audit-table
--comment: Create SearchAuditEntries table for business search audit trail
CREATE TABLE IF NOT EXISTS SearchAuditEntries (
    Id TEXT NOT NULL PRIMARY KEY,
    SearchTerm TEXT NOT NULL,
    ResultCount INTEGER NOT NULL,
    Truncated INTEGER NOT NULL,
    PagesScraped INTEGER NOT NULL,
    DurationMs INTEGER NOT NULL,
    Status TEXT NOT NULL,
    ErrorDetail TEXT NULL,
    CreatedAt TEXT NOT NULL,
    CorrelationId TEXT NOT NULL
);
