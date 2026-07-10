-- liquibase formatted sql

--changeset 025-entra-id-auth:2.0.2604.0-add-caller-identity-to-search-audit
--comment: Add CallerIdentity column to SearchAuditEntries for recording authenticated caller identity (UPN or app client ID)
ALTER TABLE SearchAuditEntries ADD COLUMN CallerIdentity TEXT NULL;

-- rollback ALTER TABLE SearchAuditEntries DROP COLUMN CallerIdentity;
