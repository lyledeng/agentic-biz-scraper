# FAB Generic Document Processing Agent — CAPTCHA Solver Configuration

**Purpose**: Manual configuration guide for the FAB (Foundations and Beyond) dashboard to set up the CAPTCHA-solving agent used by the Wyoming business entity scraper.  
**Feature**: `005-multi-state-search`  
**Action Required**: This agent must be configured on the FAB dashboard **before** deploying this feature.

---

## Agent Identity

| Field | Value |
|-------|-------|
| **Agent Name** | Wyoming CAPTCHA Solver |
| **Agent Type** | Generic Document Processing Agent |
| **Description** | Extracts text from CAPTCHA challenge images presented by the Wyoming Secretary of State website (wyobiz.wyo.gov). |

---

## System Message

```text
You are a professional and secure AI assistant specialized in document processing. Your primary goal is to provide helpful, accurate, and safe analysis while adhering strictly to your operational guidelines.

Your task is to read and extract the exact text shown in a CAPTCHA image. The image contains distorted alphanumeric characters with visual noise (lines, dots, warping). You must:

1. Analyze the image carefully, distinguishing characters from background noise.
2. Return ONLY the exact characters visible in the image.
3. Preserve the original casing (uppercase/lowercase) exactly as shown.
4. Do not add spaces, punctuation, or any extra characters.
5. If you are uncertain about a character, make your best determination based on the visual evidence.

Common CAPTCHA characteristics:
- Characters are typically 5-7 alphanumeric characters (a-z, A-Z, 0-9).
- Characters may be rotated, overlapping, or distorted.
- Background may contain lines, dots, or gradient noise.
```

---

## User Prompt Template

The user prompt sent with each request:

```text
Extract the text from this CAPTCHA image. Return only the characters shown, preserving exact casing. No spaces or extra characters.
```

---

## Structured Output Configuration

| Setting | Value |
|---------|-------|
| **Enable Structured Output** | Yes |

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "captchaText": {
      "type": "string",
      "description": "The exact alphanumeric text extracted from the CAPTCHA image, preserving original casing."
    },
    "confidence": {
      "type": "string",
      "enum": ["high", "medium", "low"],
      "description": "The agent's confidence level in the extracted text."
    }
  },
  "required": ["captchaText", "confidence"],
  "additionalProperties": false
}
```

### Example Response

```json
{
  "captchaText": "vr9HFg",
  "confidence": "high"
}
```

---

## Advanced Settings

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| **Request Retries Count** | 2 | Allow up to 2 retries if the FAB agent call fails (network transient errors). |
| **Request Timeout** | 120 seconds | CAPTCHA images are small; 120s is generous for model inference. |
| **Max Tokens** | 3000 | Response is very short (< 50 tokens), but buffer allows for edge cases. Can be reduced to 100 if FAB supports it. |
| **Temperature** | 0.7 | Moderate creativity to handle ambiguous characters while staying grounded in visual evidence. Consider lowering to 0.3 if accuracy is poor. |

---

## Application Configuration

The following settings must be added to `appsettings.json` (or Key Vault for secrets) so the scraper can call this agent at runtime:

### appsettings.json

```json
{
  "FabAgent": {
    "CaptchaSolver": {
      "EndpointUrl": "https://<fab-host>/api/agents/<agent-id>/process",
      "TimeoutSeconds": 30
    }
  }
}
```

### Key Vault Secrets

| Secret Name | Description |
|-------------|-------------|
| `fab-captcha-agent-token` | Bearer token or API key for authenticating with the FAB agent endpoint. |

---

## Integration Contract

The scraper will call the FAB agent using the following HTTP contract:

### Request

```
POST {EndpointUrl}
Authorization: Bearer {fab-captcha-agent-token}
Content-Type: application/json

{
  "input": {
    "format": "base64",
    "data": "<base64-encoded-png-screenshot>"
  }
}
```

### Expected Response

```
HTTP/1.1 200 OK
Content-Type: application/json

{
  "output": {
    "captchaText": "Tdd4eVa",
    "confidence": "high"
  }
}
```

### Error Handling

| HTTP Status | Meaning | Scraper Behavior |
|-------------|---------|------------------|
| 200 | Success | Extract `captchaText`, submit on the CAPTCHA form. |
| 4xx | Client error (bad request, auth failure) | Log error, fail the search with an upstream error. |
| 5xx | Server error (FAB agent down) | Log error, fail the search with an upstream error. |
| Timeout | No response within configured timeout | Log error, fail the search with an upstream error. |

---

## Verification Checklist

- [ ] Agent created on FAB dashboard with the name "Wyoming CAPTCHA Solver"
- [ ] System message configured exactly as specified above
- [ ] Structured output enabled with the JSON schema above
- [ ] Advanced settings (retries, timeout, max tokens, temperature) configured
- [ ] Endpoint URL noted and added to `appsettings.json`
- [ ] Auth token generated and stored in Key Vault as `fab-captcha-agent-token`
- [ ] Test call with a sample CAPTCHA image returns valid JSON with `captchaText` field
