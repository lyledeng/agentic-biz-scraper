--liquibase formatted sql

--changeset bizscraper:2.0.2603.6-create-script-execution-audit-table
--comment Create generic script execution audit table
CREATE TABLE IF NOT EXISTS ScriptExecutionAuditEntries (
    Id TEXT NOT NULL PRIMARY KEY,
    DefinitionSlug TEXT NOT NULL,
    CorrelationId TEXT NOT NULL,
    InputParameters TEXT NULL,
    DurationMs INTEGER NOT NULL,
    Status TEXT NOT NULL,
    ErrorDetail TEXT NULL,
    DiagnosticsUrl TEXT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

--rollback DROP TABLE ScriptExecutionAuditEntries;
