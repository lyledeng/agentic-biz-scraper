--liquibase formatted sql

--changeset bizscraper:2.0.2603.4-add-document-storage-url-to-entity-detail-audit
--comment Add DocumentStorageUrl column to EntityDetailAuditEntries for history document cloud storage tracking
ALTER TABLE EntityDetailAuditEntries ADD COLUMN DocumentStorageUrl TEXT NULL;

--rollback ALTER TABLE EntityDetailAuditEntries DROP COLUMN DocumentStorageUrl;
