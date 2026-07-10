# BizScraper — Unified Data Model

**Version**: 1.3.2604.0 | **Last Updated**: 2026-04-05

All C# records and entities forming the public API surface, organized by
domain area.

---

## Execute Script

### ExecuteScriptRequest

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Definition | string | ✓ | Definition slug to execute |
| Parameters | Dictionary\<string, object?\> | | Runtime parameters |

### ExecuteScriptResponse

| Field | Type | Description |
|-------|------|-------------|
| Definition | string | Definition slug executed |
| CorrelationId | string | Request correlation ID |
| Truncated | bool | Whether results were truncated |
| ResultCount | int? | Number of results for business-search definitions; omitted (null) for non-search definitions |
| Data | object? | Execution result (shape varies by definition) |

### DefinitionInfo

| Field | Type | Description |
|-------|------|-------------|
| DefinitionSlug | string | Slug identifier |
| Name | string | Human-readable name |
| Description | string? | What the definition does |
| State | string | State code (CO, IA, MO, WY, DE) |
| RequiredParameters | ParameterInfo[] | Required runtime parameters |

### ParameterInfo

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Parameter name |
| Description | string? | Parameter description |

---

## Business Search

### NormalizedSearchResponse

| Field | Type | Description |
|-------|------|-------------|
| Results | NormalizedSearchResult[] | Search result items |
| TotalCount | int | Total matching records on site |
| SiteTotalCount | int | Site's count of matching records |
| Truncated | bool | Whether results were truncated |
| PagesScraped | int | Number of pages scraped |
| SearchTerm | string | Original search term |
| State | string | State code |

### NormalizedSearchResult

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Entity name |
| Identifier | string | Entity ID |
| Status | string | Entity status |
| EntityType | string | Business type |
| FormationDate | string | Formation date |
| UniqueKey | string | Encoded detail lookup reference |
| State | string | State code (CO, IA, MO, WY, DE) |
| Event | string? | Last event |
| StandingTax | string? | Tax standing (WY) |
| StandingRA | string? | Registered agent standing (WY) |
| RegistrationId | string? | Registration ID |

---

## Entity Details

### EntityDetailResponse

| Field | Type | Description |
|-------|------|-------------|
| Details | EntityDetailResult | Core entity information |
| RegisteredAgent | RegisteredAgentResult? | Registered agent info |
| Certificate | CertificateResult? | Certificate of good standing |
| CertifiedDocuments | CertificateResult? | Certified copies of all filed documents (CO) |
| Parties | PartyResult[]? | Officers, directors, agents |
| HistoryDocuments | HistoryDocumentResult[]? | Filing history documents |

### EntityDetailResult

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Entity name |
| Status | string | Entity status |
| FormationDate | string | Formation date |
| IdNumber | string | Entity ID number |
| Form | string | Entity form (LLC, Corp, etc.) |
| PeriodicReportMonth | string? | Report due month |
| Jurisdiction | string | Jurisdiction |
| PrincipalOfficeStreetAddress | string? | Street address |
| PrincipalOfficeMailingAddress | string? | Mailing address |
| SubStatus | string? | Sub-status |
| StandingTax | string? | Tax standing |
| StandingRA | string? | Registered agent standing |
| StandingOther | string? | Other standing |
| InactiveDate | string? | Date entity became inactive |
| TermOfDuration | string? | Duration term |
| FormedIn | string? | Formation jurisdiction |
| LatestAnnualReportYear | string? | Latest annual report year |
| AnnualReportExempt | string? | Exempt from annual reports |
| LicenseTaxPaid | string? | License tax paid status |
| ChapterCode | string? | Iowa chapter/code classification |
| CertificateNote | string? | Iowa certificate availability note |
| IowaNames | IowaNameEntry[]? | Iowa SOS names list |

### IowaNameEntry

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Name value |
| Type | string | Name type (Legal, Fictitious, etc.) |
| Status | string | Name status (Active, etc.) |
| Modified | bool | Whether the name was recently modified |

### RegisteredAgentResult

| Field | Type | Description |
|-------|------|-------------|
| Name | string? | Agent name |
| StreetAddress | string? | Street address |
| MailingAddress | string? | Mailing address |

### CertificateResult

| Field | Type | Description |
|-------|------|-------------|
| Available | bool | Whether certificate is available |
| StorageUrl | string? | Azure blob storage URL |
| FileName | string? | Document file name |
| Error | string? | Error if retrieval failed |

### PartyResult

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Party name |
| Role | string | Role (Officer, Director, etc.) |
| Organization | string? | Associated organization |
| Address | string? | Address |

### HistoryDocumentResult

| Field | Type | Description |
|-------|------|-------------|
| EventTitle | string | Filing event title |
| Date | string | Filing date |
| StorageUrl | string? | Azure blob storage URL |
| FileName | string? | Document file name |
| Error | string? | Error if retrieval failed |

---

## Germany Search

### GermanySearchResponse

| Field | Type | Description |
|-------|------|-------------|
| Results | GermanyBusinessResult[] | Search result items |
| TotalCount | int | Total matching records |
| PagesScraped | int | Number of pages scraped |
| SearchTerm | string | Original search term |
| Warning | string? | Warning message |

### GermanyBusinessResult

| Field | Type | Description |
|-------|------|-------------|
| CompanyName | string | Company name |
| RegisteredOffice | string | Registered office location |
| Status | string | Entity status |
| RegistrationId | string | Registration ID |

### HardcopyPrintoutRequest

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| SearchTerm | string | ✓ | Company search term |
| RegistrationId | string | ✓ | Registration ID |

---

## Documents

### BlobDownloadResult

| Field | Type | Description |
|-------|------|-------------|
| Content | Stream | File content stream |
| ContentType | string | MIME type |
| ContentLength | long? | Content length in bytes |
| ETag | string | Entity tag for caching |
| FileName | string | File name |

---

## Audit Trail

### SearchAuditEntry

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| SearchTerm | string | Search term |
| ResultCount | int | Number of results |
| Truncated | bool | Whether results were truncated |
| PagesScraped | int | Pages scraped |
| DurationMs | long | Execution time |
| Status | string | Outcome (Success, Failed) |
| ErrorDetail | string? | Error message |
| CreatedAt | DateTimeOffset | Timestamp |
| CorrelationId | string | Request correlation ID |
| DiagnosticsUrl | string? | Blob storage diagnostics URL |
| State | string? | State code |
| WarningMessage | string? | Warning message |
| CallerIdentity | string? | Authenticated caller (UPN or app client ID) |

### EntityDetailAuditEntry

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| DetailsUrl | string | Entity details URL |
| EntityIdNumber | string? | Entity ID |
| EntityName | string? | Entity name |
| EntityStatus | string? | Entity status |
| CertificateUrl | string? | Certificate URL |
| DiagnosticsUrl | string? | Diagnostics URL |
| DocumentStorageUrl | string? | Document storage URL |
| DurationMs | long | Execution time |
| Status | string | Outcome |
| ErrorDetail | string? | Error message |
| CorrelationId | string | Correlation ID |
| StateCode | string? | State code |
| CreatedAt | DateTimeOffset | Timestamp |

### ScriptExecutionAuditEntry

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| DefinitionSlug | string | Definition slug |
| CorrelationId | string | Correlation ID |
| InputParameters | string? | JSON serialized parameters |
| DurationMs | long | Execution time |
| Status | string | Outcome |
| ErrorDetail | string? | Error message |
| DiagnosticsUrl | string? | Diagnostics URL |
| CreatedAt | DateTimeOffset | Timestamp |
| CallerIdentity | string? | Authenticated caller (UPN or app client ID) |

---

## CI/CD Pipeline Configuration

### PipelineStage

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Stage name (`Validation`, `ImageBuildAndPush`) |
| RunsOnPr | bool | Whether stage runs on PR triggers |
| DependsOn | string? | Upstream stage dependency |
| Condition | string? | Azure DevOps condition expression |

---

## Missouri Search

### MissouriBusinessSearchResult

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Business entity name |
| Identifier | string | Charter number |
| EntityType | string | Entity type description |
| Status | string | Entity status |
| FormationDate | string? | Formation/creation date |
| RegisteredAgentName | string? | Registered agent name |
| PreviousNames | string[] | Previous names (collapsed from search) |
| UniqueKey | string | Base64URL key: { state: "MO", id: "{entityId}" } |
| State | string | Always "MO" |

### MissouriEntityDetails

| Field | Type | Description |
|-------|------|-------------|
| Details | MissouriGeneralInfo | General info from detail page |
| Filings | MissouriFiledDocument[] | Filing records with PDFs |
| Addresses | MissouriPrincipalAddress[] | Principal office addresses |

### MissouriGeneralInfo

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Current entity name |
| EntityType | string | Entity type |
| CharterNumber | string | Charter number |
| Domesticity | string | Domestic or Foreign |
| HomeState | string? | Home state (foreign entities) |
| RegisteredAgentName | string? | Registered agent name |
| RegisteredAgentAddress | string? | Registered agent full address |
| Status | string | Entity status |
| DateFormed | string? | Formation date |
| Duration | string? | Duration (e.g., "Perpetual") |

### MissouriFiledDocument

| Field | Type | Description |
|-------|------|-------------|
| Action | string | Filing action type |
| DocumentType | string | Document type description |
| DateFiled | string? | Filing date |
| EffectiveDate | string? | Effective date |
| StorageUrl | string? | Proxy URL to downloaded PDF |
| FileName | string? | Generated PDF filename |
| Error | string? | Error if download failed |

### MissouriPrincipalAddress

| Field | Type | Description |
|-------|------|-------------|
| AddressType | string | Address type (e.g., "Reg. Office") |
| Address | string | Full address |
| Since | string? | Effective date |

## Washington Search

### WashingtonBusinessSearchResult

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Business name |
| Identifier | string | UBI number |
| EntityType | string | Business type (e.g., "WA PROFIT CORPORATION") |
| Status | string | Entity status (e.g., "ACTIVE", "TERMINATED") |
| PrincipalOfficeAddress | string? | Principal office address |
| RegisteredAgentName | string? | Registered agent name |
| UniqueKey | string | Base64url key with businessId + businessType |
| State | string | "WA" |
| FormationDate | string? | Always null in search results |

### WashingtonEntityDetails

| Field | Type | Description |
|-------|------|-------------|
| Details | WashingtonGeneralInfo | General info from detail page |
| RegisteredAgent | WashingtonRegisteredAgent? | Agent info |
| Governors | WashingtonGovernor[] | Officers/governors |
| Filings | WashingtonFilingRecord[] | Filing history with documents |

### WashingtonGeneralInfo

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Business name |
| UbiNumber | string | UBI number |
| BusinessType | string | Business type |
| Status | string | Business status |
| PrincipalOfficeStreetAddress | string? | Street address |
| PrincipalOfficeMailingAddress | string? | Mailing address |
| ExpirationDate | string? | Expiration date |
| Jurisdiction | string? | State of incorporation |
| FormationDate | string? | Formation/registration date |
| PeriodOfDuration | string? | Duration |
| InactiveDate | string? | Inactive date |
| NatureOfBusiness | string? | Nature of business |
| CharitableCorporation | bool? | Nonprofit: charitable corporation |
| NonprofitEin | string? | Nonprofit: EIN |
| GrossRevenueUnder500K | bool? | Nonprofit: gross revenue < $500K |
| HasMembers | bool? | Nonprofit: has members |
| PublicBenefitDesignation | bool? | Nonprofit: public benefit |
| HostHome | bool? | Nonprofit: host home |

### WashingtonRegisteredAgent

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Agent name |
| StreetAddress | string? | Street address |
| MailingAddress | string? | Mailing address |

### WashingtonGovernor

| Field | Type | Description |
|-------|------|-------------|
| Title | string | Title (e.g., "GOVERNOR") |
| GovernorType | string | Type (INDIVIDUAL or ENTITY) |
| EntityName | string? | Entity name (if type is ENTITY) |
| FirstName | string? | First name |
| LastName | string? | Last name |

### WashingtonFilingRecord

| Field | Type | Description |
|-------|------|-------------|
| FilingNumber | string | Filing number |
| FilingDateTime | string? | Filing date/time |
| EffectiveDate | string? | Effective date |
| FilingType | string | Filing type (e.g., "ANNUAL REPORT") |
| Documents | WashingtonFilingDocument[] | Downloaded documents |

### WashingtonFilingDocument

| Field | Type | Description |
|-------|------|-------------|
| DocumentType | string | Document type |
| CreatedDate | string? | Creation date |
| StorageUrl | string? | Proxy URL (null on failure) |
| FileName | string? | Generated filename |
| Error | string? | Error message (null on success) |

### PipelineVariable

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Variable name |
| ValueMain | string | Value when branch is `main` |
| ValueNonMain | string | Value for non-main branches |

### ContainerImageArtifact

| Field | Type | Description |
|-------|------|-------------|
| Repository | string | Image repository name |
| Dockerfile | string | Dockerfile path |
| Tag | string | Image tag pattern |
| BuildArgs | Dictionary<string,string> | Build arguments |

### HelmIngressConfig

| Field | Type | Description |
|-------|------|-------------|
| Chart | string | Helm chart name |
| Host | string | Ingress host |
| Path | string | Regex path pattern |
| PathType | string | Kubernetes path type |
| BackendProtocol | string | NGINX backend protocol (`HTTPS` or `HTTP`) |
| TlsSecret | string | TLS secret name |

---

## Per-Definition Browser Mode

### BrowserConfig

Optional per-definition browser mode override. Present as a top-level
`browser` property on `FlowDefinitionV2`.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| Mode | string | ✓ | — | "local" or "remote" |
| Channel | string? | | null | Browser channel for local mode (e.g., "chrome", "msedge"). Ignored for remote. |
| Headless | bool | | true | Headless toggle for local mode. Ignored for remote. |

**Validation**: `Mode` must be "local" or "remote". When "remote" requested
but global `BrowserEndpoint` is empty, falls back to local with warning.

### BrowserPool (internal)

Encapsulates browser lifecycle for a single mode.

| Field | Type | Description |
|-------|------|-------------|
| Mode | string | "local" or "remote" |
| Playwright | IPlaywright? | Playwright instance for this pool |
| Browser | IBrowser? | Current browser instance |
| BrowserLock | SemaphoreSlim | Serializes browser creation/recycling |
| UsageCount | int | Requests served by current browser |

Both pools share the global `ReuseBrowserForRequests` threshold (default 25).
Each pool has an independent concurrency limit of 3 (6 total system-wide).

### ReadinessHealthResponse

| Field | Type | Description |
|-------|------|-------------|
| Status | string | "Ready" \| "Degraded" \| "NotReady" |
| BrowserModes | BrowserModeStatusMap | Per-mode health breakdown |
| DefinitionsRequiringLocal | string[] | Slugs with browser.mode = "local" |
| DefinitionsRequiringRemote | string[] | Slugs with browser.mode = "remote" |

### BrowserModeStatus

| Field | Type | Description |
|-------|------|-------------|
| Status | string | "healthy" \| "unhealthy" \| "not-configured" |
| Channel | string? | Browser channel (local mode only) |
| Endpoint | string? | Remote endpoint URL (remote mode only) |
| Error | string? | Error message if unhealthy |
