# Contracts: WY Certificate of Good Standing

**Feature**: 009-wy-certificate-good-standing
**Date**: 2026-03-26

## No New or Modified Contracts

This feature does not change any external-facing API contracts. The existing `GET /api/v1/entity-details` endpoint already includes the `certificate` field in its response schema (established in feature 004).

The `CertificateResult` schema in the OpenAPI spec (`specs/004-entity-details-endpoint/contracts/openapi.yaml`) already documents:

```yaml
CertificateResult:
  type: object
  required:
    - available
  properties:
    available:
      type: boolean
    storageUrl:
      type: string
      format: uri
      nullable: true
    fileName:
      type: string
      nullable: true
    error:
      type: string
      nullable: true
```

The only change is **behavioral**: the `certificate` field in WY responses will now be populated (previously always `null`). The response structure is unchanged.
