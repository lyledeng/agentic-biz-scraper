# Quickstart: Refine CI Pipelines

**Feature**: 019-ci-pipeline-refinement  
**Date**: 2026-03-30

## Prerequisites

- Azure DevOps project with the following service connections configured:
  - `Azure Container Registry (Staging)-iLienMVP`
  - `Azure Container Registry (Assembly)-iLienMVP`
- Docker available on the CI agent (Azure DevOps `ubuntu-latest` pool)
- Helm 3 installed (via `HelmInstaller@1` task)
- Kubernetes cluster with NGINX Ingress Controller and `ingress-tls-cert-secret` in namespace `mvpoc`

## Local Validation

### Helm Lint

```bash
helm lint deploy/helm/bizscraper
helm lint deploy/helm/bizscraper-ui
```

### Liquibase Validate (local Docker)

```bash
# Build Liquibase image
docker build -f deploy/liquibase/liquibase_dockerfile -t cososwebscraping-liquibase:latest .

# Start SQL Server Edge
docker run -d --name sqledge -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='YourStrong!Pass' \
  -p 1433:1433 mcr.microsoft.com/azure-sql-edge:latest

# Wait for SQL Edge to be ready
sleep 15

# Run Liquibase validate
docker run --rm --network host cososwebscraping-liquibase:latest \
  --url='jdbc:sqlserver://localhost:1433;databaseName=master;encrypt=false' \
  --username=sa --password='YourStrong!Pass' \
  validate

# Run Liquibase update
docker run --rm --network host cososwebscraping-liquibase:latest \
  --url='jdbc:sqlserver://localhost:1433;databaseName=master;encrypt=false' \
  --username=sa --password='YourStrong!Pass' \
  update

# Cleanup
docker stop sqledge && docker rm sqledge
```

### Docker Build (API)

```bash
docker build -f src/bizscraper-dockerfile -t bizscraper:local .
```

### Docker Build (UI with base href)

```bash
docker build -f src/bizscraper-ui-dockerfile \
  --build-arg BASE_HREF=/mvpoc/bizscraper-ui/ \
  --build-arg API_BASE_URL=/mvpoc/bizscraper-api \
  -t bizscraper-ui:local .
```

## Pipeline Flow

1. **Push to any branch or PR** → Validation stage runs (build, test, Helm lint, Liquibase validate+update, Angular build+test, documentation validation)
2. **Branch push (not PR)** + validation passes → Image build & push stage runs
   - `main` → pushes to Staging ACR
   - Other branches → pushes to Assembly ACR
3. **PR** + validation passes → Pipeline succeeds; no images pushed

## Deploy Commands (Namespace)

Use the shared `mvpoc` namespace for all deployments:

```bash
helm upgrade --install bizscraper deploy/helm/bizscraper --namespace mvpoc --create-namespace
helm upgrade --install bizscraper-ui deploy/helm/bizscraper-ui --namespace mvpoc --create-namespace
```

## Verifying a Deployment

After the CI pipeline pushes images and a subsequent CD deploys to AKS:

```bash
# Check API ingress
curl -k https://devcaas-az.ilienonline.com/mvpoc/bizscraper-api/healthz

# Check UI ingress
curl -k https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui/

# Verify image tag in cluster
kubectl -n mvpoc get deployment bizscraper -o jsonpath='{.spec.template.spec.containers[0].image}'
kubectl -n mvpoc get deployment bizscraper-ui -o jsonpath='{.spec.template.spec.containers[0].image}'
```
