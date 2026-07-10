# Feature Specification: Microsoft Entra ID Authentication

**Feature Branch**: `025-entra-id-auth`  
**Created**: 2026-04-02  
**Status**: Draft  
**Input**: User description: "I want to add Microsoft Entra ID authentication to the test-ui. Backend API should support authentication via tokens from Entra ID or also support client id / client secret calling from other systems (user identity and system identity)"

## Clarifications

### Session 2026-04-02

- Q: Should the API accept tokens from a single Entra ID tenant or multiple tenants? → A: Single-tenant — only users and apps from one specific Entra ID tenant can authenticate.
- Q: What should the delegated scope name exposed by the API registration be? → A: `access_as_user` — standard Microsoft convention for delegated user access.
- Q: How should developers authenticate when running the API locally? → A: Real tokens locally — developers authenticate against the dev Entra ID tenant; config stored in .NET User Secrets.
- Q: Should the authenticated user's identity be captured in the audit trail? → A: Yes — record caller identity (UPN for users, app ID for system clients) in the audit trail alongside correlation ID.
- Q: How should the MSAL login be triggered — redirect or popup? → A: Redirect — full-page navigation to Entra ID login, most compatible across browsers and corporate environments.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Interactive User Login via Entra ID (Priority: P1)

A user opens the test-ui in a browser and is redirected to Microsoft Entra ID to sign in. After successful authentication, the user is returned to the test-ui with a valid session. All subsequent API calls from the test-ui include a bearer token obtained from Entra ID. The backend API validates this token and allows access to protected endpoints.

**Why this priority**: Without interactive user authentication, the test-ui is completely open. This is the foundational security capability that all other stories depend on.

**Independent Test**: Can be fully tested by opening the test-ui, being redirected to Entra ID login, signing in with a valid account, and verifying that API calls succeed with the token attached.

**Acceptance Scenarios**:

1. **Given** an unauthenticated user navigates to the test-ui, **When** the page loads, **Then** the user is redirected to the Microsoft Entra ID login page.
2. **Given** a user successfully authenticates with Entra ID, **When** they are redirected back to the test-ui, **Then** the user sees the application content and their display name in the UI.
3. **Given** an authenticated user performs a search, **When** the API request is sent, **Then** the request includes a bearer token in the Authorization header and the API returns results.
4. **Given** an authenticated user's token has expired, **When** they perform an action, **Then** the MSAL library silently refreshes the token or redirects the user to re-authenticate.

---

### User Story 2 - System-to-System Authentication via Client Credentials (Priority: P1)

An external system calls the BizScraper API using a client ID and client secret (OAuth 2.0 client credentials flow). The system obtains a bearer token from Entra ID and includes it in API requests. The backend validates the token and grants access without requiring an interactive user login.

**Why this priority**: System-to-system authentication is equally critical as interactive login because external systems need to integrate with the API programmatically. Both authentication modes are foundational.

**Independent Test**: Can be tested by obtaining a token via the OAuth 2.0 client credentials endpoint using a registered app's client ID and secret, then calling the API with that token and verifying a successful response.

**Acceptance Scenarios**:

1. **Given** an external system has a valid client ID and client secret registered in Entra ID, **When** it requests a token using the client credentials flow, **Then** it receives a valid bearer token.
2. **Given** an external system sends a request with a valid client credentials bearer token, **When** the API receives the request, **Then** the API validates the token and returns the requested data.
3. **Given** an external system sends a request with an expired or invalid token, **When** the API receives the request, **Then** the API returns a 401 Unauthorized response.

---

### User Story 3 - Backend API Token Validation (Priority: P1)

The backend API validates all incoming bearer tokens against Microsoft Entra ID. It accepts tokens from both interactive user logins (delegated permissions) and system-to-system client credentials (application permissions). Unauthenticated requests to protected endpoints receive a 401 response. Requests with insufficient permissions receive a 403 response.

**Why this priority**: Token validation is the core security gate — without it, authentication on the client side would be meaningless.

**Independent Test**: Can be tested by sending requests with valid tokens, invalid tokens, expired tokens, and no tokens to the API and verifying the correct HTTP status codes are returned.

**Acceptance Scenarios**:

1. **Given** a request with no Authorization header, **When** it reaches a protected endpoint, **Then** the API returns 401 Unauthorized.
2. **Given** a request with a malformed or tampered token, **When** it reaches a protected endpoint, **Then** the API returns 401 Unauthorized.
3. **Given** a request with a valid delegated user token, **When** it reaches a protected endpoint, **Then** the API processes the request and returns data.
4. **Given** a request with a valid application (client credentials) token, **When** it reaches a protected endpoint, **Then** the API processes the request and returns data.
5. **Given** a request with a valid token but insufficient scope or role, **When** it reaches a protected endpoint, **Then** the API returns 403 Forbidden.

---

### User Story 4 - User Sign-Out (Priority: P2)

An authenticated user can sign out from the test-ui. Signing out clears the local session and tokens, and optionally redirects the user to the Entra ID sign-out page to end the SSO session.

**Why this priority**: Sign-out is important for security hygiene but is secondary to the ability to sign in and make authenticated calls.

**Independent Test**: Can be tested by signing in, clicking the sign-out button, and verifying that the local session is cleared and subsequent API calls fail with 401.

**Acceptance Scenarios**:

1. **Given** an authenticated user clicks the sign-out button, **When** the sign-out completes, **Then** the user's tokens are cleared from the browser and the user is returned to the login page.
2. **Given** a signed-out user attempts to access a protected page, **When** the page loads, **Then** the user is redirected to the Entra ID login page.

---

### User Story 5 - Health and Swagger Endpoints Remain Public (Priority: P2)

The health check endpoint (`/healthz`) and the Swagger UI remain accessible without authentication to support infrastructure monitoring and API discovery.

**Why this priority**: Kubernetes liveness/readiness probes and developer tooling must function without authentication tokens.

**Independent Test**: Can be tested by sending unauthenticated requests to `/healthz` and `/swagger` and verifying successful responses.

**Acceptance Scenarios**:

1. **Given** no authentication token is provided, **When** a request is sent to `/healthz`, **Then** the API returns 200 OK.
2. **Given** no authentication token is provided, **When** a user navigates to `/swagger`, **Then** the Swagger UI loads successfully.

---

### Edge Cases

- What happens when the Entra ID service is temporarily unavailable? The API should return 503 Service Unavailable and the test-ui should display an appropriate error message.
- What happens when a token is valid but the signing keys have rotated? The API should automatically refresh its token validation metadata from the Entra ID discovery endpoint.
- What happens when a user's account is disabled in Entra ID after they obtained a token? The token remains valid until expiry; the next token refresh will fail and the user will be prompted to re-authenticate.
- What happens when the CORS policy encounters a preflight request with an Authorization header? The CORS middleware must allow the Authorization header in preflight responses.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The test-ui MUST authenticate users via Microsoft Entra ID using the OAuth 2.0 Authorization Code flow with PKCE, using a full-page redirect (not popup) for the login interaction.
- **FR-002**: The test-ui MUST use the Microsoft Authentication Library (MSAL) for Angular to handle authentication, token acquisition, and token renewal.
- **FR-003**: The test-ui MUST attach a bearer token to all API requests via an HTTP interceptor.
- **FR-004**: The test-ui MUST display the signed-in user's display name in the application header.
- **FR-005**: The test-ui MUST provide a sign-out button that clears the local session and redirects to the Entra ID sign-out endpoint.
- **FR-006**: The test-ui MUST guard all application routes so that unauthenticated users are redirected to sign in.
- **FR-007**: The backend API MUST validate bearer tokens issued by Microsoft Entra ID on all protected endpoints. Token issuer MUST be restricted to a single configured Entra ID tenant.
- **FR-008**: The backend API MUST accept tokens obtained via the delegated (user) flow (Authorization Code with PKCE).
- **FR-009**: The backend API MUST accept tokens obtained via the application (client credentials) flow for system-to-system calls.
- **FR-010**: The backend API MUST return 401 Unauthorized for requests with missing, expired, or invalid tokens.
- **FR-011**: The backend API MUST return 403 Forbidden for requests with valid tokens but insufficient permissions.
- **FR-012**: The health check endpoint (`/healthz`) and Swagger UI MUST remain accessible without authentication.
- **FR-013**: The backend API MUST update the CORS policy to allow the `Authorization` header from the test-ui origin.
- **FR-014**: The backend API MUST log authentication failures with the correlation ID for observability.
- **FR-015**: The Swagger UI MUST be configured with OAuth2 authorization support so developers can test authenticated endpoints directly from the Swagger page.
- **FR-016**: The backend API MUST record the authenticated caller identity (user principal name for delegated tokens, application/client ID for client credentials tokens) in audit trail records alongside the correlation ID.

### Key Entities

- **Entra ID Application Registration (UI)**: Represents the test-ui as a single-page application in Entra ID. Configured with redirect URIs, SPA platform, and API permissions (delegated scopes).
- **Entra ID Application Registration (API)**: Represents the backend API in Entra ID. Exposes a delegated scope named `access_as_user` and defines an application role named `BizScraper.Execute` for client credentials. Validates audience claims.
- **Bearer Token**: A JWT issued by Entra ID containing claims such as audience, issuer, roles, and scopes. Used by both interactive users and system clients.
- **App Role**: An application-level permission named `BizScraper.Execute` defined on the API registration that can be assigned to client applications for system-to-system access.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of protected API calls without a valid token return 401 Unauthorized.
- **SC-002**: Users can complete sign-in via Entra ID and perform a search within 30 seconds of first page load.
- **SC-003**: System clients can obtain a token and make a successful API call within 5 seconds.
- **SC-004**: Token refresh happens silently — users are not interrupted for re-authentication during a normal session (up to the configured token lifetime).
- **SC-005**: Health check and Swagger endpoints respond successfully without any authentication.
- **SC-006**: All authentication failures are logged with correlation IDs for traceability.

## Assumptions

- An Azure Entra ID tenant is available and the user has permissions to create application registrations.
- Single-tenant mode: only tokens issued by the organization's own Entra ID tenant are accepted. Multi-tenant access is out of scope.
- Two application registrations will be created in Entra ID: one for the test-ui (SPA) and one for the backend API.
- The Entra ID tenant ID, client IDs, and API scopes will be stored in configuration (`appsettings.json` for the API, `environment.ts` for the Angular app) and can be overridden via environment variables or Key Vault.
- MSAL for Angular (@azure/msal-angular) is compatible with Angular 17 in the test-ui.
- The client credentials flow uses a client secret managed via Azure Key Vault in deployed environments and .NET User Secrets locally.
- Local development uses real Entra ID tokens against the dev tenant. No authentication bypass or mock mode is provided. Entra ID tenant ID, client IDs, and scopes are configured via .NET User Secrets for the API and `environment.development.ts` for the test-ui.
- Existing CORS policy will be extended to include the `Authorization` header; no new origins are needed.
- Token validation will use the standard Microsoft.Identity.Web library for ASP.NET Core, which handles issuer validation, audience validation, and signing key rotation automatically.
