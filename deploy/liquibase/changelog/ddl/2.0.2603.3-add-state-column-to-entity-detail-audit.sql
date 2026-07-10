--liquibase formatted sql

--changeset bizscraper:2.0.2603.3-add-state-column-to-entity-detail-audit
--comment: Add StateCode column to EntityDetailAuditEntries table for multi-state support
ALTER TABLE EntityDetailAuditEntries
ADD COLUMN StateCode TEXT NULL;

--rollback ALTER TABLE EntityDetailAuditEntries DROP COLUMN StateCode;
