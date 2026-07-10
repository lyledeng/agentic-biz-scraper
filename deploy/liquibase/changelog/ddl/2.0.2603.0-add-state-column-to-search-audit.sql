-- liquibase formatted sql

--changeset bizscraper:2.0.2603.0-add-state-column-to-search-audit
--comment: Add State column to SearchAuditEntries for multi-state search support
ALTER TABLE SearchAuditEntries
    ADD COLUMN State TEXT NULL;

-- rollback ALTER TABLE SearchAuditEntries DROP COLUMN State;
