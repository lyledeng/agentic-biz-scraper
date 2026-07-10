-- liquibase formatted sql

--changeset bizscraper:1.0.2603.2-create-entity-detail-audit-table
--comment: Create EntityDetailAuditEntries table for entity detail scraping audit trail
CREATE TABLE IF NOT EXISTS EntityDetailAuditEntries (
    Id TEXT NOT NULL PRIMARY KEY,
    DetailsUrl TEXT NOT NULL,
    EntityIdNumber TEXT NULL,
    EntityName TEXT NULL,
    EntityStatus TEXT NULL,
    CertificateUrl TEXT NULL,
    DiagnosticsUrl TEXT NULL,
    DurationMs INTEGER NOT NULL,
    Status TEXT NOT NULL,
    ErrorDetail TEXT NULL,
    CorrelationId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

-- rollback DROP TABLE EntityDetailAuditEntries;
