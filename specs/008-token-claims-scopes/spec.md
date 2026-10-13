# Feature Specification: Token, Claims, and Scopes Refinement

**Feature Branch**: `feature/user-aggregate-refactor`  
**Created**: 2026-07-25  
**Status**: Draft  
**Input**: User description: "Phase 9 Token, Claims & Scopes Refinement based on phase-9-token-claims-scopes-plan.md with Package Dependency Verification"

## Clarifications

### Session 2026-07-25
- Q: What should be the default lifetime configuration for issued Access Tokens and Refresh Tokens? → A: 1 Hour Access Token, with sliding Refresh Tokens enabled (15 days).
- Q: How are client origins and CORS rules configured for client SPAs in Talabat.Identity? → A: Duende dynamically resolves allowed CORS origins from registered client configuration (`AllowedCorsOrigins`), with exact origin matching.
- Q: How are UserType flags and domain profile linkages mapped to JWT claims? → A: Map UserType flags to standard `role` claims, and map domain profile IDs (`CustomerId` / `DeliveryAgentId`) to custom profile link claims (`customer_id` / `delivery_agent_id`) emitted via a custom `IProfileService`.
- Q: Are all required packages for Centralized Auth present in the solution? → A: Verified. `Duende.IdentityServer` (8.0.2) and `Duende.IdentityServer.AspNetIdentity` (8.0.2) are present in `Talabat.Identity`. `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.9) is present in both `Talabat.Customer.API` and `Talabat.Delivery.API`. All projects restore and build cleanly with 0 errors.

---

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As a registered Customer or Delivery Agent, I want to authenticate securely using a single centralized identity provider (`Talabat.Identity`) via standard Authorization Code Flow with PKCE, so that my credentials are kept isolated, and I receive a signed JWT access token containing my profile identifier and scope to interact with my designated business API host (`Talabat.Customer.API` or `Talabat.Delivery.API`).

### Acceptance Scenarios

1. **Centralized OIDC Authentication & Redirect Flow**
   - **Given** an unauthenticated user initiating login from the Customer Web SPA (`http://localhost:4200`),
   - **When** they request authorization at `Talabat.Identity` (`/connect/authorize`) with PKCE parameters (`code_challenge`),
   - **Then** after entering valid credentials, they are redirected back to the registered `RedirectUri` with an Authorization Code.

2. **Access Token Retrieval with Custom Claims & Scopes**
   - **Given** a client website has obtained an Authorization Code,
   - **When** it calls `/connect/token` at `Talabat.Identity` with the PKCE code verifier,
   - **Then** it receives a 1-hour signed JWT access token containing `sub` (user's integer ID), `client_id`, `scope` (`customer.api`), `role` claims, and a profile-link claim (`customer_id`), alongside a 15-day sliding refresh token.

3. **API Access Token Validation on Customer Resource Server**
   - **Given** a Customer SPA possesses a valid JWT access token issued by `Talabat.Identity`,
   - **When** it sends a request to `Talabat.Customer.API` with `Authorization: Bearer <token>`,
   - **Then** `Talabat.Customer.API` validates the JWT signature, issuer, and audience (`talabat.customer.api`) locally, and accepts the request.

4. **Audience & Scope Isolation (Cross-API Rejection)**
   - **Given** a customer client possesses a JWT token issued for audience `talabat.customer.api` and scope `customer.api`,
   - **When** the client attempts to present this token to `Talabat.Delivery.API` (which requires audience `talabat.delivery.api`),
   - **Then** `Talabat.Delivery.API` immediately rejects the request with HTTP `403 Forbidden` due to audience mismatch.

### Edge Cases

- **Expired or Tampered JWT Token**: When an API receives a JWT with an invalid signature or expired timestamp, the JWT Bearer middleware rejects the request with HTTP `401 Unauthorized` before any Application logic runs.
- **Unregistered Redirect URI**: When an authentication request specifies a `redirect_uri` not listed in the client's `RedirectUris` in `Talabat.Identity`, IdentityServer immediately rejects the request with a protocol error.
- **Profile Linkage Absence**: If an account lacks a domain profile claim (e.g. `customer_id`), the API handles token parsing gracefully without throwing null reference exceptions.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 (Centralized Authority)**: `Talabat.Identity` must act as the single OpenID Connect Authority using Duende IdentityServer, exposing standard endpoints (`/.well-known/openid-configuration`, `/connect/authorize`, `/connect/token`, `/connect/endsession`).
- **FR-002 (Public SPA PKCE Configuration)**: Client registrations for the Customer SPA and Delivery Agent SPA must use Authorization Code Flow with PKCE (`AllowedGrantTypes = GrantTypes.Code`, `RequirePkce = true`) with no client secrets.
- **FR-003 (Audience & Scope Isolation)**:
  - Define distinct API Resources: `talabat.customer.api` (audience for Customer API) and `talabat.delivery.api` (audience for Delivery API).
  - Define API Scopes: `customer.api` and `delivery.api`.
  - Customer SPA tokens must carry audience `talabat.customer.api` and scope `customer.api`.
  - Delivery SPA tokens must carry audience `talabat.delivery.api` and scope `delivery.api`.
- **FR-004 (JWT Token Emission & Lifetime)**: Issued Access Tokens must be standard signed JWTs with a 1-hour expiration. Sliding Refresh Tokens must be supported with a 15-day maximum lifetime (`offline_access` scope).
- **FR-005 (Claims & Profile Link Service)**: A custom `IProfileService` (`TalabatProfileService`) must enrich emitted access tokens with:
  - `sub`: User database integer identifier (`User.Id`).
  - `role`: User capability flag roles (`Customer`, `DeliveryAgent`, `Admin`, `RestaurantOwner`).
  - Domain profile link claims: `customer_id` for customer accounts and `delivery_agent_id` for delivery accounts.
- **FR-006 (Account-to-Profile Linkage)**: Registration flow must eagerly establish the relationship by creating the domain profile aggregate via Application handlers and assigning the corresponding profile link claim (`customer_id` / `delivery_agent_id`) and role to the `ApplicationUser`.
- **FR-007 (Resource Server JWT Bearer Validation)**:
  - `Talabat.Customer.API` must configure `Microsoft.AspNetCore.Authentication.JwtBearer` to validate authority, issuer, signing key, lifetime, and audience (`talabat.customer.api`).
  - `Talabat.Delivery.API` must configure JWT Bearer middleware to validate authority, issuer, signing key, lifetime, and audience (`talabat.delivery.api`).
- **FR-008 (Package Dependency Verification)**:
  - `Talabat.Identity` must reference `Duende.IdentityServer` (8.0.2) and `Duende.IdentityServer.AspNetIdentity` (8.0.2).
  - `Talabat.Customer.API` and `Talabat.Delivery.API` must reference `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.9).
  - All package references must resolve cleanly under .NET 10 without restore or compilation errors.
- **FR-009 (Domain Purity Guardrail)**: `Talabat.Domain` and `Talabat.Application` must remain 100% free of IdentityServer, JWT, `ClaimsPrincipal`, `HttpContext`, or ASP.NET Core Identity dependencies.

### Key Entities

- **User Aggregate**: The unified identity account entity in persistence acting as the subject (`sub`) of token issuance.
- **OIDC Client**: Registered SPA client configuration in Duende IdentityServer containing allowed grant types, redirect URIs, CORS origins, and allowed scopes.
- **API Resource / Scope**: Architectural boundaries defining token audiences (`talabat.customer.api`, `talabat.delivery.api`) and access permissions.
- **JSON Web Token (JWT)**: Cryptographically signed security token carrying subject, audience, scope, role, and profile link claims.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of user authentication requests are routed centrally through `Talabat.Identity`; zero credentials or password validations execute directly on business API hosts.
- **SC-002**: Local JWT validation overhead on business APIs (`Talabat.Customer.API` and `Talabat.Delivery.API`) adds less than 10ms of latency per authenticated request using cached public signing keys (JWKS).
- **SC-003**: 100% of cross-audience requests (e.g. presenting a Customer API token to the Delivery API) are rejected with HTTP `403 Forbidden` prior to handler execution.
- **SC-004**: 100% of project solution builds complete with 0 errors and 0 package restoration warnings across all 7 projects.

---

## Assumptions

- Development environments use developer signing credentials (`AddDeveloperSigningCredential()`), while production environments will supply a persisted RSA/ECDSA signing certificate.
- Local SPA origins run on standard ports (`http://localhost:4200` for Customer SPA, `http://localhost:4300` for Delivery SPA).

---

## Out of Scope

- Implementing the frontend Angular applications.
- Endpoint-level policy matrix enforcement (`[Authorize(Policy = ...)]`), which is assigned to **Phase 10**.
- External social logins (Google, Facebook, OAuth2 providers).
- Multi-DbContext configuration or operational stores in EF Core (retaining single DbContext rule).
