/speckit.specify
I would like to build an api to allow searching using names on the CO SOS Web Site.
State Url is https://www.sos.state.co.us/biz/BusinessEntityResults.do
Enter the search term in "Search by business name, trademark, trade name, ID or document number" edit box and click on search
On the results page, scrape data from the table into json. If there are multiple pages, scrape data from all the pages.
Elements in json format to scrape:
    DocumentNumber
    Name
    Event
    Status
    Form
    FormationDate
    DetailsUrl
you have access to the playwright mcp server to navigate the site for semantic intent.
The execution engine with use Azure Playwright workspaces.
Playwright details will be provided during the planning phase.

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts

## Platform & Language

- .NET 10 (LTS) with C# 14
- Target framework `net10.0`; language version `latest`
- Central Package Management via `Directory.Packages.props` at the repository root; no `Version` attributes in individual `.csproj` files
- Shared build settings via `Directory.Build.props` (target framework, language version, nullable enable, implicit usings, treat warnings as errors, code analysis treat warnings as errors, enforce code style in build, analysis level `latest-all`, deterministic builds, continuous integration build flag)
- `.editorconfig` at the repository root for code style rule severities; style violations set to `error` to enforce build failure
- `.slnx` solution file for project management
- Only open-source or permissively licensed packages (MIT, Apache-2.0, BSD); no packages with closed, dual-commercial, or re-licensing models (e.g., FluentAssertions, MediatR). Verify licence compatibility before adopting any new dependency.

## C# 14 / .NET 10 Conventions

- Use extension members (extension blocks) for cross-cutting utility methods instead of static helper classes
- Use the `field` keyword in property accessors to eliminate unnecessary backing fields
- Use null-conditional assignment (`?.=`) for null-guarded property setting
- Prefer `Span<T>` and `ReadOnlySpan<T>` with implicit span conversions for buffer and stream operations
- Use file-scoped namespaces, primary constructors, and collection expressions throughout
- Enable Native AOT readiness; avoid reflection-heavy patterns where possible

## Compile-Time Source Generation

- All logging via `[LoggerMessage]` source-generated partial methods; no runtime string formatting, no string interpolation in log calls. Log methods defined as `ILogger` extension methods in a static partial class per feature.
- All regular expressions via `[GeneratedRegex]` source-generated partial methods; no `new Regex()` or `Regex.Compiled` at runtime. Patterns are AOT-safe, trimmer-friendly, and validated at compile time.
- All JSON serialisation via `System.Text.Json` source-generated `JsonSerializerContext`; no reflection-based serialisation. All request/response models registered in the context for trim and AOT compatibility.

## Architecture

- Vertical slice architecture; each feature is self-contained under its own folder
- CQRS pattern using Litebus for command/query separation
- Streaming-first approach for all file I/O (HTML and PDF); no in-memory file loading
- Global exception handler (`IExceptionHandler`) for centralised error handling; no custom error-handling middleware. All unhandled exceptions mapped to RFC 9457 Problem Details responses.

## Core Libraries

- Playwright for HTML-to-PDF rendering
- Flurl.Http for outbound HTTP calls
- Polly for resilience patterns (retries, circuit breakers, timeouts, bulkheads)
- OpenTelemetry for logs, distributed tracing, and metrics

## Performance

- Pool and reuse Playwright `IBrowser` instances; create/dispose `BrowserContext` per request for isolation
- Automatic browser pool recycling: each pooled `IBrowser` instance is retired and replaced after a configurable number of conversions or elapsed time, whichever comes first, to prevent memory leaks
- Bound concurrent conversions to match browser pool capacity; excess requests receive `503` with `Retry-After`
- Enable Brotli/gzip response compression on PDF responses
- Enforce an overall request processing timeout at the middleware level
- Define CPU and memory requests/limits per pod in Helm values; never rely on unbounded defaults

## Data & Persistence

- Azure SQL Managed Instance for the audit trail
- Liquibase for all database schema deployments (versioned, reversible changelogs)
- Liquibase changelogs written in SQL format
- All Liquibase files (master files and changelogs) follow the hybrid SemVer+CalVer versioning standard (`MAJOR.MINOR.YYMM.PATCH`)
- Liquibase `validate` runs as a CI gate; changelogs that fail validation block the pipeline

## Testing

- xUnit + NSubstitute for unit, component, and integration tests
- Testcontainers for .NET with Azure SQL Edge for integration tests; real database interactions validated against a disposable container instead of mocking the persistence layer
- k6 for performance, load, and functional tests; p95/p99 latency thresholds and error rate ceilings defined per scenario and enforced as CD gates — breaches fail the pipeline
- Playwright (shared) for end-to-end browser-based validation

## Code Quality & Security Scanning

- SonarQube quality gates enforced in CI; SonarQube runs as a separate analysis step after the build to avoid overriding `TreatWarningsAsErrors`
- Black Duck SBOM scans scheduled weekly via a dedicated pipeline
- Treat warnings as errors (`TreatWarningsAsErrors`) enabled solution-wide via `Directory.Build.props`
- Code analysis warnings treated as errors (`CodeAnalysisTreatWarningsAsErrors`) enabled solution-wide via `Directory.Build.props`
- Code style enforced in build (`EnforceCodeStyleInBuild`) with style rule severities set to `error` in `.editorconfig`
- .NET code analysers enabled by default (`AnalysisLevel: latest-all`)

## Security Hardening

- Security headers middleware on every response: HSTS, X-Content-Type-Options (`nosniff`), X-Frame-Options (`DENY`), Referrer-Policy, Permissions-Policy, Content-Security-Policy
- ASP.NET Core rate limiting middleware to protect against abuse
- Request size enforced at both Kestrel (`MaxRequestBodySize`) and ingress controller level
- Non-root container user with read-only filesystem; writable volumes only where required (Playwright temp directory)
- Kubernetes `securityContext`: drop all capabilities, disable privilege escalation, `readOnlyRootFilesystem: true`
- Kubernetes NetworkPolicy restricting egress to public internet (external resource resolution) and Azure SQL MI only

## End-to-End Transport Encryption

- Self-signed TLS certificate generated during the Docker build stage and baked into the container image at a known path
- Every image build produces a fresh certificate; certificate rotates naturally with each image release
- Kestrel configured to load the certificate from the baked-in path and listen exclusively on port `8443` (HTTPS only); no plaintext HTTP listener is exposed
- Ingress controller is configured with the backend HTTPS protocol annotation to trust the self-signed certificate when proxying to the pod
- Kubernetes Service targets container port `8443`; liveness and readiness probes use HTTPS scheme on the same port

## Autoscaling

- Horizontal Pod Autoscaler (HPA) required for the deployment
- HPA scaling metrics, target thresholds, min/max replica counts, and stabilisation windows defined in Helm values
- Pod CPU and memory requests/limits, and HPA scale factor determined using the resource sizing playbook (`docs/runbooks/resource-sizing-playbook.md`)

## Observability

- OpenTelemetry as the single observability framework for logs, distributed tracing, and metrics
- OpenTelemetry exporter configured for Azure Monitor / Application Insights in deployed environments
- Open-source OpenTelemetry collector stack for the local development environment; setup documented in `docs/runbooks/local-otel-collector-setup.md`
- `Microsoft.Extensions.Logging` with the Console provider in plain text format for human-readable output via `kubectl logs`
- Correlation ID middleware: propagate or generate on every inbound request; include in all logs, traces, and outbound calls
- Custom service usage metrics emitted via OpenTelemetry: conversion request count, conversion success/failure rate, HTML input size distribution, PDF output size distribution, conversion duration histogram, concurrent active conversions gauge, browser pool utilisation, external resource fetch count and failure rate

## Configuration & Secrets

- All configurable values stored in `appsettings.json`
- Azure Key Vault referenced in `appsettings.json` configuration
- Key Vault accessed via `SecretProvider` class in deployed environments
- .NET User Secrets used for local development

## CI Pipeline (`ci.yaml`)

Triggers on feature branch push, PR to main, and merge to main. Stages: build → Liquibase validate → run tests with code coverage → SonarQube analysis (ingests test results and coverage, enforces quality gate) → Docker build and push (feature branch pushes to `devacr`, main pushes to `stagingacr`, PR builds the image but does not push).

## CD Pipeline (`cd.yaml`)

Runs k6 performance scenarios against the deployed environment after deployment, enforcing p95/p99 latency thresholds and error rate ceilings; breaches fail the pipeline and block promotion to the next environment.

## Deployment & Infrastructure

- Azure AKS as the target runtime
- Alpine-based container images preferred; alternative base images permitted when dependencies require it (e.g., Playwright requiring Debian/Ubuntu for browser binaries)
- Multi-stage Docker build: separate build, publish, and runtime stages to minimise image size and attack surface
- Helm charts for Kubernetes packaging and release
- Azure DevOps CI and CD pipelines

## Folder Structure

```
HtmlPdfConvertor.slnx
Directory.Build.props
Directory.Packages.props
.editorconfig

src/
├── htmlpdfconvertor-dockerfile
│
├── HtmlPdfConvertor.Api/
│   ├── HtmlPdfConvertor.Api.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   │
│   ├── Middleware/
│   │
│   ├── Features/
│   │   ├── Conversion/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   ├── Handlers/
│   │   │   └── Models/
│   │   │
│   │   └── AuditTrail/
│   │       ├── Commands/
│   │       ├── Queries/
│   │       ├── Handlers/
│   │       └── Models/
│   │
│   ├── Common/
│   │   ├── Interfaces/
│   │   └── Configuration/
│   │
│   ├── Infrastructure/
│   │   ├── Rendering/
│   │   ├── Http/
│   │   └── Persistence/
│   │
│   └── Domain/
│       └── Entities/

tests/
├── HtmlPdfConvertor.UnitTests/
│   ├── HtmlPdfConvertor.UnitTests.csproj
│   └── Features/
│       ├── Conversion/
│       └── AuditTrail/
│
├── HtmlPdfConvertor.IntegrationTests/
│   ├── HtmlPdfConvertor.IntegrationTests.csproj
│   └── Features/
│       ├── Conversion/
│       └── AuditTrail/
│
└── HtmlPdfConvertor.EndToEndTests/
    ├── HtmlPdfConvertor.EndToEndTests.csproj
    ├── Fixtures/
    ├── Scenarios/
    │   ├── Conversion/
    │   └── AuditTrail/
    └── Helpers/

deploy/
├── pipelines/
│   ├── ci.yaml
│   ├── cd.yaml
│   └── sbom.yaml
│
├── helm/
│   └── htmlpdfconvertor/
│       ├── Chart.yaml
│       ├── values.yaml
│       └── templates/
│
└── liquibase/
    ├── masterfiles/
    │   └── master-1.0.2603.0.yaml
    │
    ├── changelog/
    │   ├── dcl/
    │   │   └── 1.0.2603.0-grant-read-access.sql
    │   ├── ddl/
    │   │   └── 1.0.2603.0-create-audit-trail-table.sql
    │   └── dml/
    │       └── 1.0.2603.0-seed-initial-data.sql
    │
    └── liquibase_dockerfile

.github/
├── copilot_instructions.md
└── agents.md

docs/
├── architecture_decision_records/
│   └── *.md
│
├── runbooks/
│   ├── resource-sizing-playbook.md
│   ├── local-otel-collector-setup.md
│   └── *.md
│
└── playbooks/
    └── *.md

.editorconfig
.gitignore
README.md

Use Azure playwright workspaces for the actual execution of the web scraping scripts.
Dev Environment urls: 
  - API Base Url: https://eastus.api.playwright.microsoft.com/playwrightworkspaces/4657851e-eb0c-4f66-b834-b94117667ee9
  - Browser Endpoint: wss://eastus.api.playwright.microsoft.com/playwrightworkspaces/4657851e-eb0c-4f66-b834-b94117667ee9/browsers

The access token for the playwright workspace will come for keyvalut.
  - Azure Keyvault: zuscu-d003-kvt-d1-ils01
  - Secret Name: azure-playwright-workspace-token

Add swagger ui
Use simple console logger for logs and add utc timestamp


/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to capture traces and screenshots of the web scraping action

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts

/speckit.specify
I want to refine my screenshot saving strategy. I want to save the screenshots to a cloud storage account, all arranged in the foldername tied to the search request. I also want the screenshots saved locally to be organized in a foldername tied to the search request.

/speckit.clarify
The correlation id should be persisted to the audit trail. Also the cloud storage url where the screenshots are stored.

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use Azure Storage Account, Blob container
Follow current conventions in the current code

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
Build an new endpoint to pull the entity details from detailsUrl of an entity
When navigating to the details url, it pulls the entity details in a table. 
You have access to the playwright mcp server to navigate to the details and discover the structure. (When using the playwright mcp server, ignore tls errors. )

Sample1: https://www.sos.state.co.us/biz/BusinessEntityDetail.do?quitButtonDestination=BusinessEntityResults&nameTyp=ENT&masterFileId=20181871974&entityId2=20181871974&fileId=&srchTyp=ENTITY
Sample2: https://www.sos.state.co.us/biz/BusinessEntityDetail.do?quitButtonDestination=BusinessEntityResults&nameTyp=ENT&masterFileId=20181871974&entityId2=20181871974&fileId=20181871974&srchTyp=ENTITY
Sample3: https://www.sos.state.co.us/biz/BusinessEntityDetail.do?quitButtonDestination=BusinessEntityResults&nameTyp=ENT&masterFileId=20051063025&entityId2=20051063025&fileId=20051063025&srchTyp=ENTITY

If "Get a certificate of good standing" link is available, click on that link, and download the pdf. Store the pdf in the storage account with documentnumber-certificateofgoostanding.pdf filename.

/speckit.clarify
Store the url of the certificat of good standing to the audit table as an explicit item. Return the url back with the response json.

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code
Add rollback capabilities to liquibase changelogs

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.specify
I want to enhance the API. I want to add the ability to perform the initial search for Wyoming in addition to Colorado.\
Initial search page: https://wyobiz.wyo.gov/Business/FilingSearch.aspx
Enter the Filing Name and click on Search.
Once the page renders, Extract the name, filing number, status - tax, standing - ra, status and filed-on.
Reorganize the API to normalize the api to accept CO or WY as inputs in addition to the search term.
Normalize the response json.
You have access to playwright mcp server to perform this action to discover the search and results page. (Ignore tls cert errors due to running in the corp network).
Use this opportunity to describe the web scraping actions with semantic intent instead of the specific DOM model if that makes sense.

/speckit.clarify
Handle Captcha on Wyoming site. We have the capability of taking screenshot of the captcha and sending it to an AI agent, hosted in FAB (Foundations and beyond) to fill the captcha. If we can solve Captcha without AI, that works as well.

FAB Details
Yes. We will use HTTP POSt. FAB has a custom agent called as "Generic Document Processing Agent". This agent is configured on the fab dashboard. YOu can specify the system prompt, the user prompt, AI params (temp, top p), token size, structured json output and json schema etc. Please generate a markdown file on what the ai config is required, which needs to be done manually. 

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

FAB Interface:
Input:
{
  "input": {
    "format": "base64",
    "data": "base64 encoded png image",
  }
}

Output:
{
  "output": {
    "captchaText": "Tdd4eVa",
    "confidence": "high"
  }
}


/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to refactor the semantic intent for playwright actions, which is currently encoded in c# code to json files.
  - /api/v1/business-search  CO
  - /api/v1/business-search WY
  - /api/v1/entity-details CO
The backend execution engine should be generic and run through these json files for execution.

/speckit.clarify
Identify underspecified areas in the current feature spec by asking upto 5 highly targeted clarification questions and encoding the answer back into the spec.

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code
Consider renaming the API from CoSosWebScraping.Api to something more generic. If doing this, rename all references

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
Update the entity details endpoint to support WY entity details
It is ok to change the endpoint signature to accept state code
When navigating to the details url, it pulls the entity details in a table. 
You have access to the playwright mcp server to navigate to the details and discover the structure. (When using the playwright mcp server, ignore tls errors. )

Sample1: https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=148028109002063097078042099251229204182040235172
Sample2: https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=040210024178155224029087218229046059197210140039

/speckit.clarify StateCode is not optional. 

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Run a project analysis for consistency

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify

For the WY details (sample url: https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=094114166207167038170018020254100209134082027099), when the entity scrapper pulls the details it pulls this response json: 
{
  "details": {
    "name": "Wendy's Technology, LLC",
    "status": "Active",
    "formationDate": "06/13/2017",
    "idNumber": "2017-000757675",
    "form": "Limited Liability Company - Foreign",
    "periodicReportMonth": null,
    "jurisdiction": "Delaware",
    "principalOfficeStreetAddress": "One Dave Thomas BlvdDublin, OH 43017USA",
    "principalOfficeMailingAddress": "One Dave Thomas BlvdDublin, OH 43017USA",
    "subStatus": "Current",
    "standingTax": "Good",
    "standingRA": "Good",
    "standingOther": "Good",
    "inactiveDate": null,
    "termOfDuration": "Perpetual",
    "formedIn": "Delaware",
    "latestAnnualReportYear": "11347360 / 2025",
    "annualReportExempt": "No",
    "licenseTaxPaid": "$60.00"
  },
  "registeredAgent": {
    "name": "United Agent Group Inc.",
    "streetAddress": "5830 E 2nd StCasper, WY 82609 USA",
    "mailingAddress": null
  },
  "certificate": null,
  "parties": [],
  "historyDocuments": [
    {
      "eventTitle": "2025 Original Annual Report - 11347360",
      "date": "05/20/2025",
      "storageUrl": "https://wyobiz.wyo.gov/Business/GetImages.aspx?sid=048152219033110073003069219199051252082246142158&stid=121207255159209202156082212102091217249082056121",
      "fileName": "2025-original-annual-report-11347360.pdf",
      "error": null
    },
    {
      "eventTitle": "2024 Original Annual Report - 09899282",
      "date": "05/20/2024",
      "storageUrl": "https://wyobiz.wyo.gov/Business/GetImages.aspx?sid=162090157196019056073003134188053198157106005240&stid=121207255159209202156082212102091217249082056121",
      "fileName": "2024-original-annual-report-09899282.pdf",
      "error": null
    },
    {
      "eventTitle": "2023 Original Annual Report - 08502883",
      "date": "05/05/2023",
      "storageUrl": "https://wyobiz.wyo.gov/Business/GetImages.aspx?sid=036033140232245027092042017080142007154219207219&stid=121207255159209202156082212102091217249082056121",
      "fileName": "2023-original-annual-report-08502883.pdf",
      "error": null
    }
  ]
}

The historyDocuments should all be downloaded and uploaded to our storage account, and the url should be the storage url of our storage account.
Use the workspace spec folder, and not the repo specs folder. Localize all speckit work to this workspace folder. Add as memory item as well for further iterations.

/speckit.clarify
Identify underspecified areas in the current feature spec by asking upto 5 highly targeted clarification questions and encoding the responses back into the spec.

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Run a project analysis for consistency

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to also pull the WY Certificate of good standing from the web site when performing a detail search.
Navigate to url: https://wyobiz.wyo.gov/Business/ViewCertificate.aspx
Enter the Filing Id
You will go to the details page.
Click Print and save the pdf.
You have access to the playwright mcp server.
Here is a sample filing id for your reference: 2017-000757675

/speckit.clarify
Identify underspecified areas in the current feature spec by asking upto 5 highly targetted clarification questions and encoding answers back into the spec.

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want a test User Interface to test these capabilities
Initial search page, where the user enters the search term and the state
Search performs the search and displays the results in a grid
Selecting one item in the grid performs a detail search and displays the documents available
clicking on the document pulls the pdf from the storage account

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/specify.plan
Build the test ui as an angular app
You have access to jumpstart-mcp-server to create a Wolterskluwer compliant web site
Locate the test ui in src/test-ui folder
Create a docker file to containerize this angular app
Create a helm chart to deploy this to the aks cluster
AKS Cluster ingress will be matching the api

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Run a project analysis for consistency

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
The test ui has direct links to the storage account. The user using the storage account may not have direct access to this storage account url. Add a capability so that the API can stream the pdf from the storage account, which is the link the angular test app uses.


/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Run a project analysis for consistency

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md


/speckit.specify
When the colorado "record count exceeds", system should not retry. 
System should return this error in semantic form. 
Current response: {"type":"https://BizScraper.local/errors/unhandled-error","title":"Unhandled Error","status":500,"detail":"An unexpected error occurred.","instance":"/api/v1/business-search"}

Logs:
2026-03-27T13:08:33.255Z fail: BizScraper.Api.Middleware.GlobalExceptionHandler[0] Unhandled exception while processing request /api/v1/business-search. System.InvalidOperationException: Flow 'Colorado Business Search' failed: Check-text matched pattern 'Exceeded Record Count' with onMatch=throw.  ---> BizScraper.Api.Infrastructure.Scraping.Engine.Actions.CheckTextMatchException: Check-text matched pattern 'Exceeded Record Count' with onMatch=throw.    at BizScraper.Api.Infrastructure.Scraping.Engine.Actions.CheckTextActionHandler.ExecuteAsync(ActionContext context, FlowAction action, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\Actions\CheckTextActionHandler.cs:line 28    at BizScraper.Api.Infrastructure.Scraping.Engine.ScrapingFlowEngine.ExecuteActionsAsync(IReadOnlyList`1 actions, ActionContext context, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\ScrapingFlowEngine.cs:line 76    at BizScraper.Api.Infrastructure.Scraping.Engine.ScrapingFlowEngine.ExecuteAsync(FlowDefinition definition, ActionContext context, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\ScrapingFlowEngine.cs:line 22    --- End of inner exception stack trace ---    at BizScraper.Api.Infrastructure.Scraping.Engine.ScrapingFlowEngine.ExecuteAsync(FlowDefinition definition, ActionContext context, Cag\Engine\JsonDrivenSearchScraper.cs:line 300    at BizScraper.Api.Infrastructure.Scraping.Engine.JsonDrivenSearchScraper.ExecuteWithDiagnosticsAsync[T](IBrowserContext context, IPage page, String correlationId, Func`1 operation, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\JsonDrivenSearchScraper.cs:line 306    at BizScraper.Api.Infrastructure.Scraping.Engine.JsonDrivenSearchScraper.ExecuteWithDiagnosticsAsync[T](IBrowserContext context, IPage page, String correlationId, Func`1 operation, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\JsonDrivenSearchScraper.cs:line 314    at BizScraper.Api.Infrastructure.Scraping.Engine.JsonDrivenSearchScraper.SearchCoreAsync(String searchTerm, String correlationId, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\JsonDrivenSearchScraper.cs:line 86    at BizScraper.Api.Infrastructure.Scraping.Engine.JsonDrivenSearchScraper.SearchCoreAsync(String searchTerm, String correlationId, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\JsonDrivenSearchScraper.cs:line 86    at Polly.ResiliencePipeline.<>c__9`2.<<ExecuteAsync>b__9_0>d.MoveNext() --- End of stack trace from previous location ---    at Polly.Outcome`1.GetResultOrRethrow()    at Polly.ResiliencePipeline.ExecuteAsync[TResult,TState](Func`3 callback, TState state, CancellationToken cancellationToken)    at BizScraper.Api.Infrastructure.Scraping.Engine.JsonDrivenSearchScraper.SearchAsync(String searchTerm, String correlationId, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Infrastructure\Scraping\Engine\JsonDrivenSearchScraper.cs:line 55    at BizScraper.Api.Features.BusinessSearch.Handlers.SearchBusinessEntitiesHandler.HandleAsync(SearchBusinessEntitiesQuery query, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Features\BusinessSearch\Handlers\SearchBusinessEntitiesHandler.cs:line 50    at BizScraper.Api.Features.BusinessSearch.Handlers.SearchBusinessEntitiesHandler.HandleAsync(SearchBusinessEntitiesQuery query, CancellationToken cancellationToken) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Features\BusinessSearch\Handlers\SearchBusinessEntitiesHandler.cs:line 70    at LiteBus.Messaging.Abstractions.SingleAsyncHandlerMediationStrategy`2.Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext executionContext)    at LiteBus.Messaging.Abstractions.MessageContextExtensions.RunAsyncErrorHandlers(IMessageDependencies messageDependencies, Object message, Object messageResult, ExceptionDispatchInfo exceptionDispatchInfo)    at LiteBus.Messaging.Abstractions.SingleAsyncHandlerMediationStrategy`2.Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext executionContext)    at BizScraper.Api.Features.BusinessSearch.Endpoints.BusinessSearchEndpoints.<>c.<<MapBusinessSearchEndpoints>b__1_0>d.MoveNext() in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Features\BusinessSearch\Endpoints\BusinessSearchEndpoints.cs:line 35 --- End of stack trace from previous location ---    at Microsoft.AspNetCore.Http.RequestDelegateFactory.ExecuteTaskResult[T](Task`1 task, HttpContext httpContext)    at BizScraper.Api.Middleware.RequestTimeoutMiddleware.InvokeAsync(HttpContext context) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Middleware\RequestTimeoutMiddleware.cs:line 19    at BizScraper.Api.Middleware.SecurityHeadersMiddleware.InvokeAsync(HttpContext context) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Middleware\SecurityHeadersMiddleware.cs:line 20    at BizScraper.Api.Middleware.CorrelationIdMiddleware.InvokeAsync(HttpContext context) in C:\Repos\iLienMVP\MVPOC\Altaf\Speckit\BizScraper\src\BizScraper.Api\Middleware\CorrelationIdMiddleware.cs:line 19    at Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddlewareImpl.<Invoke>g__Awaited|10_0(ExceptionHandlerMiddlewareImpl middleware, HttpContext context, Task task)

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Run a project analysis for consistency

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to add a new capability to perform a business entity search in Germany. This should be exposed as a new API endpoint. 
Navigate to this web site: https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml
Enter the company name in the "Company or keywords" edit box.
Click Find
If results are found, return CompanyName, Registered Office, Status back.
If multiple pages found, scrape data from all the pages.
If The maximum number of 100 hits has been exceeded. Please limit your request further. message is detected, return that warning back in the response json.
Update the test-ui to add this capability.
You have access to playwright mcp server to exercise this workflow. 
Use "Rohde" as the search key to get multiple records with the max number of 100 hits condition.
Use "Rohde & Schwarz Group Services GmbH" for the one record found condition
Playwright mcp server will hit tls errors, as we are behind a corporate firewall. For the sake of discovery, you can ignore the tls cert errors.

/speckit.clarify
Identify underspecified areas in the current feature spec by asking upto 5 highly targeted clarification questions and encoding the answer back into the spec.

/speckit.analyze 
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md and tasks.md after task generation

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
Improve the json definitation to be recorder friendly
Here is a sample improved json schema to begin with.
{
  "$schema": "https://example.com/scraping-schema/v1.0.0",
  "schemaVersion": "1.0.0",

  "metadata": {
    "id": "6fdbe67e-5b9d-45e2-a58d-a583301979a8",
    "name": "Colorado Business Search",
    "description": "Search Colorado Secretary of State business database and extract results",
    "state": "CO",
    "endpoint": "business-search",
    "createdAt": "2026-03-27T14:07:43.691+00:00",
    "updatedAt": "2026-03-27T14:07:43.691+00:00",
    "author": "altaf-mehmood_wkl",
    "tags": ["secretary-of-state", "business-search", "colorado"]
  },

  "environment": {
    "viewport": { "width": 1157, "height": 631 },
    "userAgent": null,
    "locale": "en-US",
    "timeout": 30000,
    "retries": {
      "maxAttempts": 3,
      "delayMs": 1000,
      "backoffMultiplier": 2
    }
  },

  "variables": [
    {
      "name": "searchTerm",
      "source": "runtime",
      "required": true,
      "defaultValue": null,
      "sensitive": false,
      "description": "Business name to search for"
    },
    {
      "name": "correlationId",
      "source": "runtime",
      "required": true,
      "sensitive": false,
      "description": "Unique ID to correlate this execution with the caller"
    },
    {
      "name": "searchEntryUrl",
      "source": "config",
      "required": true,
      "defaultValue": "https://www.coloradosos.gov/biz/BusinessEntityCriteriaExt.do",
      "description": "URL of the search page"
    },
    {
      "name": "maxPages",
      "source": "config",
      "required": false,
      "defaultValue": 25,
      "description": "Maximum number of pages to paginate through"
    },
    {
      "name": "results",
      "source": "extracted",
      "description": "Accumulated business entity results across all pages"
    },
    {
      "name": "pageResults",
      "source": "extracted",
      "description": "Results extracted from the current page"
    },
    {
      "name": "paginationInfo",
      "source": "extracted",
      "description": "Pagination metadata (total count, current page, total pages)"
    }
  ],

  "actions": [
    {
      "id": "action-000",
      "order": 0,
      "type": "navigate",
      "description": "Load Colorado SOS search page",
      "url": "${searchEntryUrl}",
      "waitUntil": "networkidle",
      "screenshot": { "milestone": "01-page-load" },
      "metadata": {
        "timestamp": null,
        "pageTitle": "Colorado Secretary of State - Business Database Search"
      }
    },
    {
      "id": "action-001",
      "order": 1,
      "type": "click",
      "description": "Focus the search input field",
      "target": {
        "selectors": [
          { "strategy": "css",   "value": "#searchCriteria",              "confidence": 0.9 },
          { "strategy": "role",  "value": "textbox",                      "confidence": 0.6 },
          { "strategy": "xpath", "value": "//*[@id=\"searchCriteria\"]",  "confidence": 0.5 },
          { "strategy": "name",  "value": "input[name='searchName']",     "confidence": 0.8 }
        ],
        "description": "Search input field"
      },
      "metadata": {
        "timestamp": null,
        "pageUrl": "${searchEntryUrl}"
      }
    },
    {
      "id": "action-002",
      "order": 2,
      "type": "fill",
      "description": "Enter business name into search field",
      "target": {
        "selectors": [
          { "strategy": "css",   "value": "#searchCriteria",              "confidence": 0.9 },
          { "strategy": "role",  "value": "textbox",                      "confidence": 0.6 },
          { "strategy": "xpath", "value": "//*[@id=\"searchCriteria\"]",  "confidence": 0.5 },
          { "strategy": "name",  "value": "input[name='searchName']",     "confidence": 0.8 }
        ],
        "description": "Search input field"
      },
      "value": "${searchTerm}",
      "metadata": {
        "timestamp": null,
        "pageUrl": "${searchEntryUrl}"
      }
    },
    {
      "id": "action-003",
      "order": 3,
      "type": "click",
      "description": "Submit search form",
      "target": {
        "selectors": [
          { "strategy": "css",   "value": "input[name='cmd'][type='submit'][value='Search']", "confidence": 0.9 },
          { "strategy": "css",   "value": "tbody > tr > td:nth-of-type(1) > a > input.button", "confidence": 0.7 },
          { "strategy": "xpath", "value": "//form/table[2]/tbody/tr/td[1]/a/input",            "confidence": 0.5 }
        ],
        "description": "Search submit button"
      },
      "waitAfter": "networkidle",
      "screenshot": { "milestone": "02-search-results" },
      "metadata": {
        "timestamp": null,
        "pageUrl": "${searchEntryUrl}"
      }
    },
    {
      "id": "action-004",
      "order": 4,
      "type": "check-text",
      "description": "Detect exceeded record count",
      "pattern": "Exceeded Record Count",
      "onMatch": "throw",
      "errorType": "exceeded-record-count",
      "errorMessage": "Search term '${searchTerm}' returned too many results. Use a more specific query."
    },
    {
      "id": "action-005",
      "order": 5,
      "type": "extract",
      "description": "Extract search results from first page",
      "scriptRef": "extractBusinessResults",
      "storeAs": "results"
    },
    {
      "id": "action-006",
      "order": 6,
      "type": "extract",
      "description": "Extract pagination info",
      "scriptRef": "extractPaginationInfo",
      "storeAs": "paginationInfo"
    },
    {
      "id": "action-007",
      "order": 7,
      "type": "loop",
      "description": "Paginate through remaining result pages",
      "loopType": "pagination",
      "maxIterations": "${maxPages}",
      "aggregateInto": "results",
      "terminateWhen": {
        "type": "not",
        "condition": {
          "type": "element-exists",
          "target": {
            "selectors": [
              { "strategy": "css", "value": "a[href*='pi1=${nextPage}'][title*='Page']", "confidence": 0.9 }
            ],
            "description": "Next page link"
          }
        }
      },
      "actions": [
        {
          "id": "action-007-a",
          "order": 0,
          "type": "click",
          "description": "Click next page link",
          "target": {
            "selectors": [
              { "strategy": "css", "value": "a[href*='pi1=${nextPage}'][title*='Page']", "confidence": 0.9 }
            ],
            "description": "Next page link"
          },
          "waitAfter": "networkidle",
          "screenshot": { "milestone": "03-page-${currentPage}" }
        },
        {
          "id": "action-007-b",
          "order": 1,
          "type": "check-text",
          "description": "Detect error on pagination",
          "pattern": "Exceeded Record Count",
          "onMatch": "throw",
          "errorType": "pagination-error"
        },
        {
          "id": "action-007-c",
          "order": 2,
          "type": "extract",
          "description": "Extract page results",
          "scriptRef": "extractBusinessResults",
          "storeAs": "pageResults"
        }
      ]
    }
  ],

  "scripts": {
    "extractBusinessResults": {
      "language": "javascript",
      "description": "Extracts business entity rows from the results table",
      "body": "() => { const expectedHeaders = ['#', 'ID Number', 'Document Number', 'Name', 'Event', 'Status', 'Form', 'Formation Date']; const normalize = (value) => (value || '').replace(/\\s+/g, ' ').trim(); const tables = Array.from(document.querySelectorAll('table')); const target = tables.find(table => { const rows = Array.from(table.rows || []); if (rows.length < 2) return false; const headerRow = rows.find(row => row.querySelectorAll(':scope > th').length >= expectedHeaders.length); if (!headerRow) return false; const headers = Array.from(headerRow.querySelectorAll(':scope > th')).map(th => normalize(th.textContent)); return expectedHeaders.every((header, index) => headers[index] === header) && rows.some(row => row.querySelectorAll(':scope > td').length === expectedHeaders.length); }); if (!target) return JSON.stringify([]); const rows = Array.from(target.rows || []).filter(row => row.querySelectorAll(':scope > td').length === expectedHeaders.length).map(row => { const cells = Array.from(row.querySelectorAll(':scope > td')); const link = cells[1]?.querySelector('a'); const href = link?.getAttribute('href') || ''; const detailsUrl = href ? new URL(href, window.location.href).toString() : ''; return { documentNumber: normalize(cells[2]?.textContent), name: normalize(cells[3]?.textContent), event: normalize(cells[4]?.textContent), status: normalize(cells[5]?.textContent), form: normalize(cells[6]?.textContent), formationDate: normalize(cells[7]?.textContent), detailsUrl }; }); return JSON.stringify(rows); }"
    },
    "extractPaginationInfo": {
      "language": "javascript",
      "description": "Extracts pagination metadata from the page text",
      "body": "() => { const text = document.body.innerText || ''; const match = text.match(/Found\\s+(\\d+)\\s+matching record\\(s\\)\\.\\s+Viewing page\\s+(\\d+)\\s+of\\s+(\\d+)\\./i); return match ? JSON.stringify({ siteTotalCount: parseInt(match[1]), currentPage: parseInt(match[2]), totalPages: parseInt(match[3]) }) : JSON.stringify({ siteTotalCount: -1, currentPage: 1, totalPages: 1 }); }"
    }
  },

  "output": {
    "variableName": "results",
    "type": "BusinessEntityResult[]",
    "schema": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "documentNumber": { "type": "string" },
          "name":           { "type": "string" },
          "event":          { "type": "string" },
          "status":         { "type": "string" },
          "form":           { "type": "string" },
          "formationDate":  { "type": "string" },
          "detailsUrl":     { "type": "string", "format": "uri" }
        }
      }
    }
  }
}


/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code
Address defferred items:
 Support compound actions
 Migrate all the current json files from v1 to v2
 Implement output schema validation 
Ask for clarification if required with options

/speckit.tasks
Generate an actionable, dependency-oriented tasks.md for the feature based on available design artifacts.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
In the German business results, each row has a link "AD" which is "Current Hardcopy Printout". I want to enhance the german business search api 
to pull down the hard copy printout. This printout in in german, so I want to call a "Generic Document Processor" FAB Agent to convert this 
into English. I want to generate a pdf with this converted english document. I want to enhance the test-ui to offer testing this capability.
The api endpoint should be /api/v1/germany-search/current-hardcopy-printout.

/speckit.clarify
Identify underspecified areas in the current feature spec by asking up to 5 highly targetted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

/speckit.tasks
Generate an actionable, dependency-oriented tasks.md for the feature based on available design artifacts.

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to make this api generic. 
/api/v1/execute-script
{
  "definition": "co-business-search",
  "name": "Wendys"
} 
or
{
  "definition": "co-entity-details",
  "detailsUrl": "https://co.details.url"
}
or 
{
  "definition": "de-business-search": 
  "name": "Rhode"
}
This is to ensure that we can add new definitions and we can execute them through the scraping engine

/speckit.clarify
Identify underspecified areas in the current feature spec by asking up to 5 highly targetted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code
Mark all the existing api's as deprecated
Update the test UI to to use the new API

/speckit.tasks
Generate an actionable, dependency-oriented tasks.md for the feature based on available design artifacts.

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.constitution  I would like to amend the constitution. All Code, Scripts etc that are public should have documentation. API Documentation, Code Documentation, Script Documentation, Database Documentation etc.

/speckit.constitution  
I would like to maintain a global specification which contains a current snapshot of the specifications at all times as new specifications are added. Keep the global specifications in specs/current-specs folder.

/speckit.specify
Implement the feature specification based on the updated constitution. Perform an audit of our current codebase and bring it all up to the constitution.

/speckit.clarify
Identify underspecified areas in the current feature spec by asking up to 5 highly targetted clarification questions and encoding answers back into the spec
Use this opportunity to check the code drift from the spec and update the current specs which contains the consolidation of the specs and fix the drift of the code vs the global specs


/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

/speckit.tasks
Generate an actionable, dependency-oriented tasks.md for the feature based on available design artifacts.

/specckit.analyze

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md


/speckit.specify
I want to standardize the shapes of business entity summary and details betweek co and wy.
CO shape:
{
  "definition": "co-business-search",
  "correlationId": "f8eaff94843c473fba64337f38b409d2",
  "truncated": false,
  "data": [
    {
      "identifier": "20201849877",
      "name": "Wendy's of Limon",
      "event": "Trade Name",
      "status": "Effective",
      "entityType": "FPC",
      "formationDate": "09/30/2020",
      "state": "CO",
      "detailsUrl": "https://www.sos.state.co.us/biz/TradeNameSummary.do?quitButtonDestination=BusinessEntityResults&nameTyp=TRDNM&masterFileId=20201849877&entityId2=20201849877&fileId=20201849877&srchTyp=TRDNM"
    }
 ]
}

WY Shape:
{
  "definition": "wy-business-search",
  "correlationId": "5828fc6fd06c45c2a1859a7e42c8dcf2",
  "truncated": false,
  "data": [
    {
      "name": "Wendy's of Cody, Inc.",
      "identifier": "1987-000241679",
      "status": "Inactive - Withdraw/Dissolve/Cancel",
      "entityType": "CORP",
      "formationDate": "05/04/1987",
      "detailsUrl": "https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=095211136004012222042099209147048067169248238077",
      "state": "WY",
      "event": null,
      "standingTax": "Good",
      "standingRA": "Good",
      "registrationId": null
    }
  ]
}

DE shape:
{
  "definition": "de-business-search",
  "correlationId": "f33b2bc256794e0f8dd7b1fa2f557eaf",
  "truncated": false,
  "data": {
    "results": [
      {
        "companyName": "\"Heinrich Rhode Gesellschaft mit beschränkter Haftung\"",
        "registeredOffice": "Kirchheim",
        "status": "aktuell",
        "registrationId": "Amtsgericht München HRB 55558"
      }
    ]
  }
}

For instance the identifier, documentnumber and registrationId are the same, and can be probably standardized.
Maybe the detailsUrl can be a uniqueKey instead with all items required to perform a detail on that specific entity?

Remove all the deprecated API's.
For the Germany AD download, can that be also standardized into the execute-script? For instance, maybe the translated pdf, the original pdf can be combined, and then uploaded to storage account, and http get reference to pdf proxy return back? The document details can mirror the entity details, where we just have the document downloads?

Update test ui with all these changes

/speckit.clarify
Can we model DE document download as an entity details shape with just documents?
Can we recalibrate on the naming.
country-state-action
e.g.
us-co-business-search
us-co-entity-details
us-wy-business-search
us-wy-entity-details
de-de-business-search
de-de-entity-details

Also ask me upto 5 highly targetted questions about underspecified specs and encode back into the spec

/speckit.clarify 
Please model de document download an an entity-details scenario. Remove any germany specific api's

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Follow current conventions in the current code

/speckit.tasks
Generate an actionable, dependency-oriented tasks.md for the feature based on available design artifacts.

/specckit.analyze

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
Refine the CI Pipelines
- CI Pipelines
  - Build, test, helm lint, and liquibase validate (against a null sql server)
  - If the previous steps pass and for only branches (no PR's)
    - For main use "Azure Container Registry (Staging)-iLienMVP" as the container registry
    - For all other branches use "Azure Container Registry (Assembly)-iLienMVP" as the container registry
    - Build API container image and push to container registry
    - Build Angular container image and push to container registry
      - Angular UI will be hosted on a relative path /mvpoc/bizscraper-ui as the base path
      - API will be hosted on a relative path /mvpoc/bizscraper-api as the base path
    - Ingress host for non main will be devcaas-az.ilienonline.com
    - TLS Secret for non main will be ingress-tls-cert-secret
    - Ingress sample is as follows:
    '''
# Ingress exposes the service externally via HTTPS and rewrites the path
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: oracletools-mcp-http
  namespace: mvpoc
  annotations:
    nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"
    nginx.ingress.kubernetes.io/rewrite-target: /$2
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "120"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "120"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - devcaas-az.ilienonline.com
      secretName: ingress-tls-cert-secret
  rules:
    - host: devcaas-az.ilienonline.com
      http:
        paths:
          - path: /mvpoc/oracletools-mcp-http(/|$)(.*)
            pathType: ImplementationSpecific
            backend:
              service:
                name: oracletools-mcp-http
                port:
                  number: 8443
    '''

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
WY Business Entity Search Details is not pulling the Certificate of good standing.
When pulling the details, on the left pane select "Good Standing Certificates Online"
Enter the filing id of the entity and click search
On the details click print the download the certificate of good standing.
Example filing id with a certificate of good standing: 2017-000757675
Example filing id without a ceritificate of good standing: 2015-000679087 (Missing print button)
You have access to the playwright mcp server. However locally ignore tls cert errors.
Review and enhance us-wy-entity-details.json

/speckit.clarify
To clarify all the /api/v1/entity-details?state=WY is deprecated and should be removed. Use /api/v2/execute-script endpoint

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current code conventions
Remove all dead code
Update current specs

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want the ability to use either Playwright workspace or a local playwright instance.


/speckit.clarify
To clarify all the /api/v1/entity-details?state=WY is deprecated and should be removed. Use /api/v2/execute-script endpoint

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current code conventions
Remove all dead code
Update current specs

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to integrate Iowa business entity search. Integrate it with the unified search as in CO and WY currently.
Search Url: https://sos.iowa.gov/search/business/Search.aspx
Enter business name and click on search
Support drill down using entity details.
You have access to playwright mcp server to discover. Ignore TLS errors locally.

/speckit.clarify I want to clarify that the v1 api is deprecated, and this capability for summary and detail needs to be supported via the generic v2 execute script api. Also, the certificate is behind a paywall, so just notate that for now. You can also discover all the fieds in the drill down via playwright mcp server, and ignore tls errors locally.

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current code conventions
Remove all dead code
Update current specs

/speckit.tasks
Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to add Microsoft Entra ID authentication to the test-ui
Backend API should support authentication via tokens from entra id or also support client id / client secret calling from other systems (user identity and system identity)

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Generate a markdown document with instructions on how to create the app registration for dev and assembly environment
Redirect urls: 
dev => http://localhost:4200
assembly => https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to enhance the web scraping engine. Every action performed in the json file should be logged to the logger.

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.specify Show No Records Found in both UI and API

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to enhance the web scraping engine. Every action performed in the json file should be logged to the loggAer.

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.specify 
When the API downloads documents, the pdf proxy url is used as a link in the test-ui to allow the user to view the document in a new browser window. This is generating and HTTP 401 now after the Entra id authentication was implemented.

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs

/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
WY Filing History documents are not being downloaded

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs
Update the test-ui. All the document buttons dont stand out. Use the jumpstart mcp server to review and polish the link


/speckit.analyze
Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want the ability to either use a remote playwright workspace or local paywright instance with chrome. Some web sites have anti-bot measures, which detect playwright running in headless chromium mode.
IA should run with local playwright.
Rest should run via Azure Playwright workspaces.
Configuration should be at a json file level for full flexibility

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs

/speckit.specify  Add MO Business Entity Search. Url https://bsd.sos.mo.gov/BusinessEntity/BESearch.aspx?SearchType=0. Use Playwright MCP server running as chrome browser to discover. Certificate of Good Standing is behind a paywall, so just entity search and detail is required. 

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
Add WA Business Entity Searching and Details capability.
Url: https://ccfs.sos.wa.gov/#/
Use playwright mcp server to discover.

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want the ability to use the BizScraper.Api deployed on Windows VM for chrome / headed scenario.
The primary API runs on AKS, and when it encounters chrome / headed scrapping slug, it forwards the api
to WIndows VM. The API token that AKS recieves should be forwarded. 

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs
In the CI/cd Pipelines, currently the api is deployed to (https://devcaas-az.ilienonline.com/mvpoc/bizscraper-api/). The routing url for the windows vm will be
https://aegis.ilienonline.com/mvpoc/bizscraper-api.
Build the package for window as part of the CI, and consume and deploy to ZUSCUDILNAEGIS VM.
The SQLite database will be local to the windows vm. During deploy copy the file from AKS volume claim and copy to the windows vm so that we dont need to worry about applying liquibase migrations.


/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I want to enhance the CO SOS Entity Document download. When we are on the entity details page, if "Get certified copies of documents" link is available, web scraper for co detail should click on that link. On the document list, take a screenshot and click on "All documents" and download the pdf. Follow all conventions for doc downloads. You have access to mcp server to navigate the co sos web site and discover the details.
"SCHOLER WENDYS COLORADO LLC" , ID#:	20201222644 is an good example to explore this flow.

/speckit.clarify
Identify underspecified areas in the current feature set by asking up to 5 highly targeted clarification questions and encoding answers back into the spec

/speckit.plan
Execute the implementation planning workflow using the plan template to generate design artifacts
Use current conventions in code
Update current specs

/speckit.implement
Execute the implementation plan by processing and executing all tasks defined in tasks.md

/speckit.specify
I do not want to cache remote playwright instances. For each remote playwright run, perform the web scrape action and then close. Caching keeps the remote instance online incurring recurring charges.

/speckit.specify
Audit codebase for violations of SOLID and other constitution failures.
Remove all code encoded in the generic scraping engine which is specific to a specific web site / scenario.
Web Scraping engine should be very generic, and all code for a specific web site / scenario should be ideally encoded into the json script. If not possible it should be isolated to its own codebase.
