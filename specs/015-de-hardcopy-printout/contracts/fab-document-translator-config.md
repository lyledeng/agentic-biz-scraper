# FAB Document Translator Agent Configuration

**Feature**: 015-de-hardcopy-printout
**Date**: 2026-03-27

## Overview

This document describes the manual configuration required on the FAB (Foundational AI Bot) dashboard to set up the Generic Document Processor agent for German→English document translation. This configuration is separate from the CAPTCHA solver agent (Feature 005).

## Agent Configuration

| Setting | Value |
|---------|-------|
| **Agent Name** | German Document Translator |
| **Agent Type** | Generic Document Processor |
| **Input Format** | Base64-encoded PDF |
| **Output Format** | Structured JSON |

## System Prompt

```text
You are a professional translator specializing in German legal and business registration documents from the Handelsregister (German Commercial Register).

Translate the provided document from German into English. You must populate the following output fields:

- `translatedMarkdown`: the full English translation formatted as Markdown (see formatting rules below)
- `sourceLanguage`: the detected source language code — set this to "de"

Formatting rules for `translatedMarkdown`:
- Use # for the document title and ## for major sections
- Use pipe tables (| Column | Value |) for structured data fields
- Use - bullet lists for lists of items
- Separate sections and paragraphs with a blank line
- Preserve dates in their original format (DD.MM.YYYY)
- Keep proper nouns untranslated (company names, city names, court names, register abbreviations such as HRB, HRA, GsR, PR)
- Translate legal terms accurately: "Geschäftsführer" → "Managing Director", "Handelsregister" → "Commercial Register", "Gesellschaft mit beschränkter Haftung" → "Limited Liability Company (GmbH)", "Prokurist" → "Authorised Signatory", "Prokura" → "Commercial Power of Attorney", "Amtsgericht" → "Local Court"
```

## AI Parameters

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Temperature | 0.1 | Low creativity for accurate, deterministic translation |
| Top-P | 1.0 | Default value — claude-sonnet-4-6 requires either temperature or top_p to be at its default; set top_p to 1.0 to allow custom temperature |
| Max Tokens | 8000 | Sufficient for multi-page Handelsregister documents |

## Output Schema

The agent must return a JSON response with the following structure:

```json
{
  "output": {
    "translatedMarkdown": "string",
    "sourceLanguage": "string"
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `translatedMarkdown` | string | Full English translation of the document formatted as Markdown |
| `sourceLanguage` | string | Detected source language code (expected: `"de"`) |

## Example Output

```json
{
  "output": {
    "translatedMarkdown": "# Commercial Register Extract\n\n## District Court Leipzig, HRB 30226\n\n| Field | Value |\n|-------|-------|\n| Company Name | Rohde & Schwarz Group Services GmbH |\n| Registered Office | Leipzig |\n| Legal Form | Limited Liability Company (GmbH) |\n| Status | Active |\n\n## Officers\n\n- Managing Director: Max Mustermann\n- Managing Director: Erika Musterfrau\n\n## Object of the Company\n\nThe provision of services in the areas of information technology, finance, human resources, and general administration for affiliated companies.",
    "sourceLanguage": "de"
  }
}
```

> **Note**: The `\n` sequences above are JSON escape sequences — each represents a real newline character in the parsed string, producing multi-line Markdown. The rendered output must have proper headings, tables, and paragraph breaks on separate lines.

## Application Configuration

The BizScraper API connects to this agent via the following `appsettings.json` section:

```json
{
  "FabDocumentTranslator": {
    "EndpointUrl": "https://<fab-host>/api/agents/<agent-id>/invoke",
    "AuthToken": "<from-key-vault>",
    "TimeoutSeconds": 60
  }
}
```

The `AuthToken` should be stored in Azure Key Vault and referenced via the `FabDocumentTranslator:AuthToken` configuration key.
