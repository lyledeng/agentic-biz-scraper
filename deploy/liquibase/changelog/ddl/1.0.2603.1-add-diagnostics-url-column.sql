-- liquibase formatted sql

--changeset bizscraper:1.0.2603.1-add-diagnostics-url-column
--comment: Add DiagnosticsUrl column to SearchAuditEntries for scraping trace links
ALTER TABLE SearchAuditEntries
    ADD COLUMN DiagnosticsUrl TEXT NULL;