# Quickstart: Comprehensive Action Logging

## What Changed

The scraping engine now logs every action from JSON flow definitions at `Information` level with action-specific details, correlation ID, and elapsed execution time.

## Verify Logging

Run any scraping flow and observe the console output:

```bash
dotnet run --project src/BizScraper.Api
```

Execute a search (e.g., Colorado business search) and look for log entries like:

```
info: BizScraper.Api.Infrastructure.Scraping.Engine.ScrapingFlowEngine
  Action started: [0] navigate (id=go-to-search) - Navigate to search page [url=https://..., waitUntil=NetworkIdle] [CorrelationId=abc-123]
  Action completed: [0] navigate (id=go-to-search) in 1234ms [CorrelationId=abc-123]
  Action started: [1] fill (id=enter-search-term) - Enter business name [target=Business Name, value=Acme Corp] [CorrelationId=abc-123]
  Action completed: [1] fill (id=enter-search-term) in 45ms [CorrelationId=abc-123]
```

For sensitive variables:

```
  Action started: [3] fill (id=enter-captcha) - Enter captcha solution [target=Captcha Input, value=[6 chars]] [CorrelationId=abc-123]
```

## Log Filtering by Correlation ID

Filter logs for a single request:

```bash
# In structured logging output (JSON), filter by CorrelationId property
grep "CorrelationId=abc-123" logs.txt
```

## Key Conventions

- All action logs are at `Information` level (promoted from `Debug`)
- All log methods use `[LoggerMessage]` source generation — zero runtime string formatting
- Sensitive values are redacted based on the `sensitive` flag in the flow definition's variable declarations
- Elapsed time in milliseconds appears in every `ActionCompleted` entry
- Retry attempts are logged at `Warning` level with attempt number and delay
