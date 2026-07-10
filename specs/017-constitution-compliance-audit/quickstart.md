# Quickstart: Constitution Compliance Audit

## Overview

This feature produces documentation artifacts and CI validation scripts.
There are no new runtime dependencies or services to configure.

## Implementation Order

### Phase 1: CI Validation Scripts (Test-First)

Create the validation scripts first so they fail, confirming the gaps:

1. Create `tools/ci/check-xml-doc-coverage.ps1`
2. Create `tools/ci/check-liquibase-comments.ps1`
3. Create `tools/ci/check-global-snapshot.ps1`
4. Run all 3 — they should all fail (Red)

### Phase 2: Liquibase Comments (Quick Win)

Add `--comment:` lines to the 4 non-compliant changelog files:

```
deploy/liquibase/changelog/ddl/1.0.2603.0-create-search-audit-table.sql
deploy/liquibase/changelog/ddl/1.0.2603.1-add-diagnostics-url-column.sql
deploy/liquibase/changelog/ddl/1.0.2603.2-create-entity-detail-audit-table.sql
deploy/liquibase/changelog/ddl/2.0.2603.0-add-state-column-to-search-audit.sql
```

Insert `--comment: <description>` immediately after the `--changeset` line.
Run `check-liquibase-comments.ps1` — should now pass (Green).

### Phase 3: XML Doc-Comments

1. Enable `GenerateDocumentationFile` in `Directory.Build.props` with
   CS1591 suppressed via `NoWarn`
2. Add `/// <summary>` comments to all 102 undocumented public types
3. Remove CS1591 from `NoWarn`
4. Build — verify zero CS1591 warnings
5. Run `check-xml-doc-coverage.ps1` — should now pass (Green)

### Phase 4: Global Specification Snapshot

1. Create `specs/current-specs/` folder structure
2. Write `spec.md` organized by capability (see data-model.md for sections)
3. Write `data-model.md` with unified entity catalog
4. Create `contracts/` with individual contract files
5. Add "Constitution Compliance" section with principle/gate assessment
6. Run `check-global-snapshot.ps1` — should now pass (Green)

### Phase 5: CI Pipeline Integration

Add `DocumentationValidation` job to `deploy/pipelines/ci.yaml`:

```yaml
- job: DocumentationValidation
  displayName: 'Documentation Compliance Checks'
  pool:
    vmImage: 'ubuntu-latest'
  steps:
    - task: PowerShell@2
      displayName: 'Check XML doc coverage'
      inputs:
        filePath: 'tools/ci/check-xml-doc-coverage.ps1'
    - task: PowerShell@2
      displayName: 'Check Liquibase comments'
      inputs:
        filePath: 'tools/ci/check-liquibase-comments.ps1'
    - task: PowerShell@2
      displayName: 'Check global snapshot'
      inputs:
        filePath: 'tools/ci/check-global-snapshot.ps1'
```

## Verification

After implementation, verify all success criteria:

```powershell
# SC-001: Global snapshot exists
Test-Path specs/current-specs/spec.md
Test-Path specs/current-specs/data-model.md
Test-Path specs/current-specs/contracts/

# SC-002: All public types documented
.\tools\ci\check-xml-doc-coverage.ps1

# SC-003: All changelogs have comments
.\tools\ci\check-liquibase-comments.ps1

# SC-004: Zero CS1591 warnings
dotnet build -c Release 2>&1 | Select-String "CS1591"

# SC-005 & SC-006: Compliance section exists
Select-String "Constitution Compliance" specs/current-specs/spec.md
```
