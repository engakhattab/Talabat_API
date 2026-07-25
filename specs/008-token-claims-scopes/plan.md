# Implementation Plan: Token, Claims, and Scopes Refinement (Phase 9)

**Branch**: `feature/user-aggregate-refactor` | **Date**: 2026-07-25 | **Spec**: [spec.md](file:///d:/link-dev/talabat/specs/008-token-claims-scopes/spec.md)  
**Input**: Feature specification from `specs/008-token-claims-scopes/spec.md` and `phase-9-token-claims-scopes-plan.md`

---

## Summary

Phase 9 establishes centralized OpenID Connect authentication using Duende IdentityServer on `Talabat.Identity`, configuring OIDC public SPA client registrations with Authorization Code + PKCE, distinct API Resources/Audiences (`talabat.customer.api`, `talabat.delivery.api`), API Scopes (`customer.api`, `delivery.api`), custom profile link claims (`customer_id`, `delivery_agent_id`), and JWT Bearer validation on both business API hosts (`Talabat.Customer.API` and `Talabat.Delivery.API`).

---

## Technical Context

**Language/Version**: C# / .NET 10.0  
**Primary Dependencies**: 
- `Duende.IdentityServer` (8.0.2) in `Talabat.Identity`
- `Duende.IdentityServer.AspNetIdentity` (8.0.2) in `Talabat.Identity`
- `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.9) in `Talabat.Customer.API` & `Talabat.Delivery.API`  
**Storage**: `TalabatDbContext` (`IdentityDbContext<User, IdentityRole<int>, int>`) — single DbContext rule retained  
**Testing**: xUnit test projects (`Talabat.Identity.Tests`, `Talabat.Customer.API.Tests`, `Talabat.Infrastructure.Tests`, `Talabat.Application.Tests`)  
**Target Platform**: Windows / Kestrel Web API hosts  
**Project Type**: Microservice / Multi-Host Web API Architecture  
**Performance Goals**: Sub-10ms JWT signature validation latency on business APIs via public JWKS caching  
**Constraints**: 
- Single DbContext rule (no secondary Duende EF configuration/operational store DbContexts).
- Domain and Application remain 100% free of Duende, IdentityServer, JWT, or ASP.NET Core Identity dependencies.
- Audience isolation: Customer API token MUST NOT be accepted by Delivery API (and vice-versa).  
**Scale/Scope**: Phase 9 Token, Claims & Scopes Refinement increment.

---

## Constitution Check

| Principle / Rule | Compliance Status | Rationale |
|---|---|---|
| **Principle 1: Domain Independence** | ✅ Compliant | `Talabat.Domain` and `Talabat.Application` contain 0 auth/web packages. All IdentityServer logic is strictly in `Talabat.Identity` and Host projects. |
| **Principle 5: Thin Host Roots** | ✅ Compliant | Business APIs only add standard JwtBearer middleware configuration in composition roots. Business logic remains in Application handlers. |
| **Principle 6: Unified User & Single DbContext** | ✅ Compliant | Duende uses in-memory client/resource configurations (`AddInMemoryClients`, `AddInMemoryApiScopes`). No extra DbContexts introduced. |
| **Principle 7: Database-Generated Integer IDs** | ✅ Compliant | Subject claim `sub` and profile link claims (`customer_id` / `delivery_agent_id`) map directly to integer `User.Id`. |

---

## Project Structure

### Documentation & Design Artifacts

```text
specs/008-token-claims-scopes/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── openid-connect-contracts.md
└── checklists/
    └── requirements.md
```

### Source Code Touched

```text
src/Talabat/
├── Talabat.Identity/
│   ├── IdentityServer/
│   │   ├── IdentityServerConfig.cs          <-- API resources, scopes, clients config
│   │   └── TalabatProfileService.cs         <-- Custom IProfileService for sub, role, customer_id, delivery_agent_id
│   └── Program.cs                           <-- AddIdentityServer, AddProfileService, CORS
├── Talabat.API/ (Talabat.Customer.API)
│   └── Program.cs                           <-- AddJwtBearer (authority, audience = talabat.customer.api)
└── Talabat.Delivery.API/
    └── Program.cs                           <-- AddJwtBearer (authority, audience = talabat.delivery.api)
```

---

## Phase 0: Research

Completed in `specs/008-token-claims-scopes/research.md`:
1. In-memory client and API resource configuration selected to enforce single DbContext rule.
2. Custom `TalabatProfileService` chosen to emit `sub`, `role`, and profile link claims (`customer_id` / `delivery_agent_id`).
3. Audience isolation verified using distinct `ApiResource` definitions (`talabat.customer.api` and `talabat.delivery.api`).
4. Public SPA PKCE flow (`RequirePkce = true`, no client secrets) configured for client registrations.
5. All required NuGet packages verified present, restored, and compiled cleanly (0 errors).

---

## Phase 1: Design And Contracts

Design artifacts generated:
- `data-model.md`: JWT payload structure, OIDC client configurations, and claim definitions.
- `contracts/openid-connect-contracts.md`: Authorization code flow, token exchange, discovery endpoints, and API Bearer validation contracts.
- `quickstart.md`: Developer guide for launching services, authenticating via PKCE, and verifying audience isolation.
- `AGENTS.md`: Updated to point to `specs/008-token-claims-scopes/plan.md`.

---

## Phase 2: Planning Handoff

Implementation sequence for task execution:
1. **Host Configuration (`Talabat.Identity`)**:
   - Create `IdentityServerConfig.cs` defining `IdentityResources`, `ApiScopes`, `ApiResources`, and `Clients`.
   - Implement `TalabatProfileService` implementing `IProfileService` to emit `sub`, `role`, and `customer_id` / `delivery_agent_id` claims.
   - Wire Duende IdentityServer in `Talabat.Identity/Program.cs` (`AddInMemory*`, `AddProfileService`, `AddAspNetIdentity`).
2. **Customer API Host (`Talabat.Customer.API`)**:
   - Configure `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()` pointing to Authority with `Audience = "talabat.customer.api"`.
   - Add CORS policy matching `AllowedCorsOrigins`.
3. **Delivery API Host (`Talabat.Delivery.API`)**:
   - Configure `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()` pointing to Authority with `Audience = "talabat.delivery.api"`.
   - Add CORS policy matching `AllowedCorsOrigins`.
4. **Verification & Tests**:
   - Run integration tests to verify token issuance, claim generation, and audience rejection.

---

## Post-Design Constitution Check

All architecture principles, single DbContext rules, and package isolation rules remain fully satisfied and verified.
