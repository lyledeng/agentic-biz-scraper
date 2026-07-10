# Data Model: Refine CI Pipelines

**Feature**: 019-ci-pipeline-refinement  
**Date**: 2026-03-30

## Overview

This feature introduces no new application-level entities or database schema changes. The "data model" describes the CI/CD and infrastructure configuration entities that are created or modified.

## Pipeline Entities

### CI Pipeline Stages

| Stage | Trigger Condition | Jobs | Gate Behavior |
|-------|-------------------|------|---------------|
| **Validation** | Every commit (all branches + PRs) | DotNetBuild, AngularBuild, HelmLint, LiquibaseValidate, DocumentationValidation | All jobs must pass |
| **ImageBuildAndPush** | Branch push only (not PR), Validation passed | BuildAndPushAPI, BuildAndPushUI | Depends on Validation stage |

### Pipeline Variables

| Variable | Source | Value (main) | Value (non-main) |
|----------|--------|-------------|-------------------|
| `containerRegistryServiceConnection` | Conditional | `Azure Container Registry (Staging)-iLienMVP` | `Azure Container Registry (Assembly)-iLienMVP` |
| `imageTag` | Built-in | `$(Build.BuildNumber)` | `$(Build.BuildNumber)` |
| `apiImageRepository` | Static | `bizscraper` | `bizscraper` |
| `uiImageRepository` | Static | `bizscraper-ui` | `bizscraper-ui` |

### Container Image Artifacts

| Image | Dockerfile | Build Args | Tag |
|-------|-----------|------------|-----|
| `bizscraper` | `src/bizscraper-dockerfile` | (none) | `$(Build.BuildNumber)` |
| `bizscraper-ui` | `src/bizscraper-ui-dockerfile` | `BASE_HREF=/mvpoc/bizscraper-ui/`, `API_BASE_URL=/mvpoc/bizscraper-api` | `$(Build.BuildNumber)` |

## Helm Values Model

### bizscraper (API) — New Ingress Values

```yaml
ingress:
  enabled: true
  className: nginx
  annotations:
    nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"
    nginx.ingress.kubernetes.io/rewrite-target: /$2
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "120"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "120"
  hosts:
    - host: devcaas-az.ilienonline.com
      paths:
        - path: /mvpoc/bizscraper-api(/|$)(.*)
          pathType: ImplementationSpecific
  tls:
    - hosts:
        - devcaas-az.ilienonline.com
      secretName: ingress-tls-cert-secret
```

### bizscraper-ui — Updated Ingress Values

```yaml
ingress:
  enabled: true
  className: nginx
  annotations:
    nginx.ingress.kubernetes.io/backend-protocol: "HTTP"
    nginx.ingress.kubernetes.io/rewrite-target: /$2
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "120"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "120"
  hosts:
    - host: devcaas-az.ilienonline.com
      paths:
        - path: /mvpoc/bizscraper-ui(/|$)(.*)
          pathType: ImplementationSpecific
  tls:
    - hosts:
        - devcaas-az.ilienonline.com
      secretName: ingress-tls-cert-secret
```

Note: API uses `backend-protocol: "HTTPS"` (Kestrel on 8443), UI uses `backend-protocol: "HTTP"` (Nginx on 80).

## Liquibase CI Entity

| Component | Image | Command Sequence |
|-----------|-------|-----------------|
| SQL Server Edge | `mcr.microsoft.com/azure-sql-edge:latest` | Starts as sidecar, health-checked |
| Liquibase | `cososwebscraping-liquibase:latest` (built in CI) | `validate` → `update` |

## State Transitions

```
Commit Push / PR Open
        │
        ▼
┌─────────────────┐
│   Validation     │ ◄── Always runs
│   Stage          │
└────────┬────────┘
         │ All jobs pass?
         │
    ┌────┴────┐
    │ Yes     │ No → Pipeline fails, stop
    │         │
    ▼         │
  Is PR?      │
    │         │
  ┌─┴──┐     │
  │Yes │     │
  │    │     │
  ▼    │     │
 Stop  │     │
 (no   │     │
 push) │     │
       │     │
   ┌───┘     │
   │ No (branch push)
   ▼         │
┌──────────────────┐
│  Image Build &    │
│  Push Stage       │
└────────┬─────────┘
         │
    ┌────┴─────────┐
    │ main branch? │
    ├──Yes─────────┤
    │  → Staging   │
    │    ACR       │
    ├──No──────────┤
    │  → Assembly  │
    │    ACR       │
    └──────────────┘
```
