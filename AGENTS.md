# Agent Context

<!-- SPECKIT START -->
Current roadmap increment:

- Phase: **Token, Claims, and Scopes Refinement** (Phase 9)
- Branch: `feature/user-aggregate-refactor`
- Technical plan: `specs/008-token-claims-scopes/plan.md`
- Governing scope: `.specify/memory/constitution.md` v3.0.1 -> "Current Phase Scope: User Aggregate Refactor"

Scope guard for the current increment: execute the plan's phases in order for Phase 9.
Key deliverables:
1. Public OIDC SPA Client registrations (`talabat-customer-spa`, `talabat-delivery-spa`) in `IdentityServerConfig.cs` using Authorization Code + PKCE.
2. API Resources (`talabat.customer.api`, `talabat.delivery.api`) and API Scopes (`customer.api`, `delivery.api`) for strict audience isolation.
3. Custom `IProfileService` (`TalabatProfileService`) emitting `sub`, `role` claims, and domain profile link claims (`customer_id`, `delivery_agent_id`) mapped to `User.Id`.
4. Eager profile linkage and role assignment during registration flows.
5. `Microsoft.AspNetCore.Authentication.JwtBearer` configuration in `Talabat.Customer.API` and `Talabat.Delivery.API` for authority and audience validation.
6. Exact CORS origin matching for SPA clients.

Keep business names `CustomerId` and `Delivery.AssignedAgentId` (FKs reference `AspNetUsers.Id`). Domain and Application must remain 100% free of Duende, IdentityServer, JWT, or ASP.NET Core Identity dependencies.
<!-- SPECKIT END -->
