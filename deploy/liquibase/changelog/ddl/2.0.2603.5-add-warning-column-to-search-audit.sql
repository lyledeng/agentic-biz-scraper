--liquibase formatted sql

--changeset bizscraper:2.0.2603.5-add-warning-column-to-search-audit
--comment Add WarningMessage column to SearchAuditEntries for exceeded-hits warning passthrough
ALTER TABLE SearchAuditEntries ADD COLUMN WarningMessage TEXT NULL;

--rollback ALTER TABLE SearchAuditEntries DROP COLUMN WarningMessage;
