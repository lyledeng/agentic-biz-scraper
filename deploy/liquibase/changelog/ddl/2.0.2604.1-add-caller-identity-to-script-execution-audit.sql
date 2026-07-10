-- liquibase formatted sql

--changeset 025-entra-id-auth:2.0.2604.1-add-caller-identity-to-script-execution-audit
--comment: Add CallerIdentity column to ScriptExecutionAuditEntries for recording authenticated caller identity (UPN or app client ID)
ALTER TABLE ScriptExecutionAuditEntries ADD COLUMN CallerIdentity TEXT NULL;

-- rollback ALTER TABLE ScriptExecutionAuditEntries DROP COLUMN CallerIdentity;
