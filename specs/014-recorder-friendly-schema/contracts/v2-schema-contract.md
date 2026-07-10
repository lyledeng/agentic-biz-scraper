# V2 Flow Definition JSON Schema Contract

**Feature**: 014-recorder-friendly-schema | **Date**: 2026-03-27

## Overview

This document defines the JSON shape contract for v2 flow definition files. This is the structure that definition authors and recording tools must produce.

## V2 JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "BizScraper Flow Definition V2",
  "description": "Recorder-friendly scraping flow definition",
  "type": "object",
  "required": ["schemaVersion", "metadata", "variables", "actions", "output"],
  "properties": {
    "$schema": {
      "type": "string",
      "description": "Optional JSON Schema URI for editor validation"
    },
    "schemaVersion": {
      "type": "string",
      "const": "1.0.0",
      "description": "Schema version (semver string)"
    },
    "metadata": {
      "type": "object",
      "required": ["id", "name", "state", "endpoint"],
      "properties": {
        "id": { "type": "string", "minLength": 1 },
        "name": { "type": "string", "minLength": 1 },
        "description": { "type": "string" },
        "state": { "type": "string", "minLength": 1 },
        "endpoint": { "type": "string", "minLength": 1 },
        "createdAt": { "type": "string", "format": "date-time" },
        "updatedAt": { "type": "string", "format": "date-time" },
        "author": { "type": "string" },
        "tags": { "type": "array", "items": { "type": "string" } }
      }
    },
    "environment": {
      "type": "object",
      "properties": {
        "viewport": {
          "type": "object",
          "required": ["width", "height"],
          "properties": {
            "width": { "type": "integer", "minimum": 1 },
            "height": { "type": "integer", "minimum": 1 }
          }
        },
        "userAgent": { "type": ["string", "null"] },
        "locale": { "type": "string" },
        "timeout": { "type": "integer", "minimum": 1 },
        "selectorTimeoutMs": { "type": "integer", "minimum": 1 },
        "retries": {
          "type": "object",
          "required": ["maxAttempts", "delayMs", "backoffMultiplier"],
          "properties": {
            "maxAttempts": { "type": "integer", "minimum": 0 },
            "delayMs": { "type": "integer", "minimum": 0 },
            "backoffMultiplier": { "type": "number", "minimum": 1 }
          }
        }
      }
    },
    "variables": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["name", "source"],
        "properties": {
          "name": { "type": "string", "minLength": 1 },
          "source": { "type": "string", "enum": ["runtime", "config", "extracted"] },
          "required": { "type": "boolean" },
          "defaultValue": {},
          "sensitive": { "type": "boolean" },
          "description": { "type": "string" }
        }
      }
    },
    "actions": {
      "type": "array",
      "minItems": 1,
      "items": { "$ref": "#/definitions/action" }
    },
    "scripts": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "required": ["language", "body"],
        "properties": {
          "language": { "type": "string", "const": "javascript" },
          "body": { "type": "string", "minLength": 1 },
          "description": { "type": "string" }
        }
      }
    },
    "output": {
      "type": "object",
      "required": ["variableName", "type"],
      "properties": {
        "variableName": { "type": "string", "minLength": 1 },
        "type": { "type": "string" },
        "schema": {
          "type": "object",
          "description": "Embedded JSON Schema for advisory output validation"
        }
      }
    }
  },
  "definitions": {
    "target": {
      "type": "object",
      "required": ["selectors"],
      "properties": {
        "selectors": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "object",
            "required": ["strategy", "value", "confidence"],
            "properties": {
              "strategy": { "type": "string", "enum": ["css", "xpath", "role", "name"] },
              "value": { "type": "string", "minLength": 1 },
              "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
            }
          }
        },
        "description": { "type": "string" }
      }
    },
    "condition": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "type": {
          "type": "string",
          "enum": ["element-exists", "element-visible", "text-contains",
                   "variable-truthy", "variable-falsy", "not", "and", "or"]
        },
        "selector": { "type": "string" },
        "target": { "$ref": "#/definitions/target" },
        "text": { "type": "string" },
        "variableName": { "type": "string" },
        "condition": { "$ref": "#/definitions/condition" },
        "conditions": {
          "type": "array",
          "items": { "$ref": "#/definitions/condition" }
        }
      }
    },
    "action": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "id": { "type": "string" },
        "order": { "type": "integer" },
        "type": {
          "type": "string",
          "enum": ["navigate", "fill", "click", "wait-for-load",
                   "wait-for-condition", "extract", "check-text",
                   "screenshot", "download", "loop", "call-service"]
        },
        "description": { "type": "string" },
        "group": { "type": "string" },
        "screenshot": {
          "type": "object",
          "required": ["milestone"],
          "properties": {
            "milestone": { "type": "string" },
            "force": { "type": "boolean" }
          }
        },
        "condition": { "$ref": "#/definitions/condition" },
        "metadata": {
          "type": "object",
          "properties": {
            "timestamp": { "type": ["string", "null"] },
            "pageUrl": { "type": "string" },
            "pageTitle": { "type": "string" }
          }
        },
        "target": { "$ref": "#/definitions/target" },
        "selector": { "type": "string" },
        "url": { "type": "string" },
        "waitUntil": { "type": "string", "enum": ["networkidle", "domcontentloaded", "load"] },
        "value": { "type": "string" },
        "waitAfter": { "type": "string" },
        "state": { "type": "string" },
        "javascript": { "type": "string" },
        "timeoutMs": { "type": "integer" },
        "storeAs": { "type": "string" },
        "postProcessor": { "type": "string" },
        "scriptRef": { "type": "string" },
        "pattern": { "type": "string" },
        "isRegex": { "type": "boolean" },
        "onMatch": { "type": "string", "enum": ["throw", "store", "skip"] },
        "errorType": { "type": "string" },
        "errorMessage": { "type": "string" },
        "milestone": { "type": "string" },
        "force": { "type": "boolean" },
        "linkSelector": { "type": "string" },
        "validateExtension": { "type": "string" },
        "filenamePattern": { "type": "string" },
        "loopType": { "type": "string" },
        "maxIterations": { "type": ["integer", "string"] },
        "terminateWhen": { "$ref": "#/definitions/condition" },
        "actions": { "type": "array", "items": { "$ref": "#/definitions/action" } },
        "aggregateInto": { "type": "string" },
        "serviceName": { "type": "string" },
        "inputVariable": { "type": "string" },
        "outputVariable": { "type": "string" }
      }
    }
  }
}
```

## Validation Rules (Load-Time)

These rules are enforced by `FlowDefinitionLoader` at startup. Any violation prevents the definition from loading.

| Rule | Error Message |
|------|---------------|
| `metadata.id` is empty | "Missing required field 'metadata.id' in '{file}'" |
| `metadata.name` is empty | "Missing required field 'metadata.name' in '{file}'" |
| `metadata.state` is empty | "Missing required field 'metadata.state' in '{file}'" |
| `metadata.endpoint` is empty | "Missing required field 'metadata.endpoint' in '{file}'" |
| `actions` is empty | "Flow definition '{file}' must contain at least one action" |
| `output.variableName` is empty | "Missing required 'output.variableName' in '{file}'" |
| Unknown action type | "Unknown action type '{type}' in '{file}'" |
| Duplicate action `id` | "Duplicate action id '{id}' in '{file}'" |
| `scriptRef` references non-existent script | "Action '{id}' references unknown script '{ref}' in '{file}'" |
| Both `scriptRef` and `javascript` set | "Action '{id}' has both scriptRef and javascript in '{file}'" |
| `target.selectors` is empty | "Action '{id}' has target with empty selectors in '{file}'" |
| Unknown selector strategy | "Unknown selector strategy '{strategy}' in action '{id}' in '{file}'" |

## Sample V2 Definition (Colorado Business Search)

See the sample in [spec.md](spec.md) under the user's original input — that JSON is the canonical reference for the Colorado business search v2 format, demonstrating:
- `metadata` section with all fields
- `environment` with viewport, timeout, retries
- `variables` with defaultValue, sensitive, description
- `actions` with `id`, `order`, `target` (multi-selector)
- `scripts` section with named functions
- `output` with embedded JSON Schema
