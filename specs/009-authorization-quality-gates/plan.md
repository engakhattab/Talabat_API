# Implementation Plan: Authorization Strategy And Quality Gates

**Branch**: `feature/user-aggregate-refactor` | **Date**: 2026-07-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/009-authorization-quality-gates/spec.md`

## Summary

Phase 10 enforces layered authorization (authentication → audience → scope+role policy → ownership) on every protected endpoint in both Customer API and Delivery API, hardens ownership so no endpoint derives caller identity from request fields, and establishes quality gates (domain tests, delivery API tests, architecture tests, authorization integration tests, CI pipeline) to prevent regression.

The key changes are:
1. **Scope enforcement**: `ScopeRequirement` + `ScopeHandler` in both API hosts, rejecting missing-scope tokens with 403.
2. **Named policies**: `CustomerAccess`, `CustomerScopeOnly`, `DeliveryAgentAccess` registered in each host and applied to controllers via `[Authorize(Policy = ...)]`.
3. **Delivery ownership hardening**: Lifecycle commands gain `AgentId` from the controller (not from `ICurrentUser` in handlers); `AssignDelivery` becomes self-assignment (no body-supplied agent id).
4. **Quality gates**: Three new test projects (`Talabat.Domain.Tests`, `Talabat.Delivery.API.Tests`, `Talabat.ArchitectureTests`), authorization integration tests in both API test projects, and a GitHub Actions CI workflow.
5. **Documentation**: `docs/authorization-strategy.md` and `docs/authorization-endpoint-matrix.md` superseding the old matrix.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: ASP.NET Core 10, Duende IdentityServer (in `Talabat.Identity`), ASP.NET Core Identity, Entity Framework Core 10, xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing` 10.0.9
**Storage**: SQL Server via EF Core (single `TalabatDbContext` = `IdentityDbContext<User, IdentityRole<int>, int>`)
**Testing**: xUnit 2.9.3; existing test projects: `Talabat.Application.Tests`, `Talabat.Customer.API.Tests`, `Talabat.Identity.Tests`, `Talabat.Infrastructure.Tests`
**Target Platform**: ASP.NET Core Web API (two hosts: `Talabat.Customer.API` on port 5000, `Talabat.Delivery.API` on port 5002)
**Project Type**: Backend API hosts + Application layer + Domain
**Performance Goals**: No explicit latency targets; standard web-app expectations
**Constraints**: Domain must stay free of EF Core, ASP.NET Core, Duende (except documented `Microsoft.Extensions.Identity.Stores` exception); Application must stay free of EF Core, HttpContext, ClaimsPrincipal, Duende; ownership enforced by scoping the load (not post-load comparison); 404 for ownership failures (never 403)
**Scale/Scope**: Phase 10 — authorization policies + ownership hardening + quality gates for existing endpoints

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| 1. Domain independence | ✅ PASS | `ScopeRequirement`/`ScopeHandler` live in API hosts, not Domain. No new packages enter Domain or Application. |
| 2. Application orchestrates | ✅ PASS | Handlers remain transport-neutral. Phase 10 moves agent-id injection from handlers to controllers (matching customer-side pattern). |
| 3. Aggregate roots protect invariants | ✅ PASS | Ownership is enforced by scoping the load (repository query filters by agent/customer id). Domain state machines unchanged. |
| 4. Repository contracts in Domain | ✅ PASS | New repository method `GetByIdForAgentAsync` goes in `IDeliveryRepository` (Domain/Interfaces). |
| 5. Thin composition roots | ✅ PASS | Policies registered in `Program.cs` of each host. No business logic in controllers. |
| 6. Unified user model | ✅ PASS | No changes to `User` aggregate. |
| 7. Database-generated IDs | ✅ PASS | No changes to entity IDs. |
| 8. EF mapping encapsulation | ✅ PASS | No changes to EF mappings. |
| 9. Database constraints | ✅ PASS | No changes to constraints. |

**Verdict**: No violations. All Phase 10 changes stay within the allowed composition root (policies, scope handlers) and Application layer (command record changes, handler refactors).

## Project Structure

### Documentation

```text
specs/009-authorization-quality-gates/
  spec.md              # Feature specification
  plan.md              # This file
  research.md          # Phase 0 research decisions
  data-model.md        # Phase 1 data model (no new entities)
  contracts/
    authorization-matrix.md  # Endpoint × policy matrix
  quickstart.md        # Local dev and testing guide
```

### Source Code (changes by area)

```text
src/Talabat/
  Talabat.API/                          # Customer API host
    Auth/
      ScopeRequirement.cs               # NEW — IAuthorizationRequirement
      ScopeHandler.cs                   # NEW — AuthorizationHandler<ScopeRequirement>
      AuthorizationPolicies.cs          # NEW — policy name constants
    Controllers/
      CustomerController.cs             # MODIFY — [Authorize(Policy = "CustomerAccess")] except POST profile
      AddressController.cs              # MODIFY — [Authorize(Policy = "CustomerAccess")]
      CartController.cs                 # MODIFY — [Authorize(Policy = "CustomerAccess")]
      CheckoutController.cs             # MODIFY — [Authorize(Policy = "CustomerAccess")]
      OrderController.cs               # MODIFY — [Authorize(Policy = "CustomerAccess")]
      CatalogController.cs              # UNCHANGED — anonymous
    Program.cs                          # MODIFY — register ScopeHandler, addAuthorization with policies

  Talabat.Delivery.API/                 # Delivery API host
    Auth/
      ScopeRequirement.cs               # NEW — same pattern as Customer API
      ScopeHandler.cs                   # NEW
      AuthorizationPolicies.cs          # NEW — DeliveryAgentAccess constant
    Controllers/
      DeliveriesController.cs           # MODIFY — [Authorize(Policy = "DeliveryAgentAccess")], inject ICurrentUser, add AgentId to lifecycle commands, remove AssignDeliveryBody.AgentId
      StatusController.cs               # MODIFY — [Authorize(Policy = "DeliveryAgentAccess")]
      LocationController.cs             # MODIFY — [Authorize(Policy = "DeliveryAgentAccess")]
    Program.cs                          # MODIFY — register ScopeHandler, addAuthorization with policies

  Talabat.Application/
    DeliveryAgents/
      ProgressOutForDelivery/OutForDeliveryCommand.cs   # MODIFY — add AgentId
      ProgressArrive/ArrivedAtRestaurantCommand.cs      # MODIFY — add AgentId
      ProgressPickup/PickUpOrderCommand.cs              # MODIFY — add AgentId
      ProgressDeliver/DeliverOrderCommand.cs            # MODIFY — add AgentId
      ProgressCancel/CancelDeliveryCommand.cs           # MODIFY — add AgentId
      ProgressFail/FailDeliveryCommand.cs               # MODIFY — add AgentId
      AssignDelivery/AssignDeliveryCommand.cs           # MODIFY — remove AgentId (self-assignment)
      ProgressOutForDelivery/OutForDeliveryHandler.cs   # MODIFY — remove ICurrentUser, use command.AgentId
      ProgressArrive/ArrivedAtRestaurantHandler.cs     # MODIFY — same
      ProgressPickup/PickUpOrderHandler.cs             # MODIFY — same
      ProgressDeliver/DeliverOrderHandler.cs           # MODIFY — same
      ProgressCancel/CancelDeliveryHandler.cs          # MODIFY — same
      ProgressFail/FailDeliveryHandler.cs              # MODIFY — same
      AssignDelivery/AssignDeliveryHandler.cs          # MODIFY — use command from token, not body

  Talabat.Domain/
    Interfaces/
      IDeliveryRepository.cs           # MODIFY — add GetByIdForAgentAsync(int deliveryId, int agentId)

  Talabat.Infrastructure/
    Repositories/
      DeliveryRepository.cs            # MODIFY — implement GetByIdForAgentAsync

docs/
  authorization-strategy.md             # NEW — four-gate model, naming reconciliation, role inventory
  authorization-endpoint-matrix.md      # NEW — endpoint × policy table for both APIs
  authorization-matrix.md               # MODIFY — mark superseded
```

### Test Projects (new)

```text
tests/
  Talabat.Domain.Tests/                 # NEW — pure unit tests
  Talabat.Delivery.API.Tests/           # NEW — mirrors Customer.API.Tests pattern
  Talabat.ArchitectureTests/            # NEW — NetArchTest assertions
```

## Phase 0: Research

### R1: Scope Enforcement Pattern

**Decision**: Create `ScopeRequirement` + `ScopeHandler` in each API host's `Auth/` directory (not shared).

**Rationale**: The scope claim may arrive as one space-delimited string or as multiple claim entries depending on the token. The handler must call `FindAll("scope").SelectMany(c => c.Value.Split(' '))` to handle both formats. Duplicating this small class in each host avoids a shared library dependency and keeps the hosts independent.

**Alternatives considered**:
- Shared library: Over-engineered for two hosts; adds a project dependency.
- Built-in scope check: ASP.NET Core has no built-in "has scope" handler; `ClaimTypes.Authorization` is not standardized across token issuers.

### R2: Policy Registration Pattern

**Decision**: Use `AddAuthorizationBuilder()` with named policies in each host's `Program.cs`. Policy names defined as constants in `Auth/AuthorizationPolicies.cs`.

**Rationale**: Named constants prevent magic-string errors. `AddAuthorizationBuilder()` is the modern .NET 10 API (replaces `AddAuthorization` lambda pattern). Policies combine `RequireAuthenticatedUser()`, `ScopeRequirement`, and `RequireRole()`.

**Alternatives considered**:
- Attribute-based inline policies: Violates DRY; magic strings in controllers.
- Global authorization filter: Cannot differentiate `CustomerAccess` vs `CustomerScopeOnly` per endpoint.

### R3: Delivery Ownership Hardening

**Decision**: Move agent-id injection from handlers to controllers for lifecycle commands. Add `AgentId` to command records. Add `GetByIdForAgentAsync` to `IDeliveryRepository`.

**Rationale**: This matches the customer-side pattern (controllers inject `CustomerId` from `ICurrentUser`). Handlers become identity-unaware. The repository scoping ensures 404 for unauthorized access (not post-load 403).

**Alternatives considered**:
- Keep `ICurrentUser` in handlers: Inconsistent with customer-side pattern; leaks identity concerns into Application layer.
- Post-load comparison in controller: Returns 403 (confirms resource exists) — violates the 404-only ownership rule.

### R4: Self-Assignment for AssignDelivery

**Decision**: Agent claims pending delivery for themselves. `AgentId` from `ICurrentUser.AgentId`, not from request body.

**Rationale**: The `DeliveryOperations` role (operations-assigned model) has no approved use case. Self-assignment is the MVP. Body-supplied agent id is a security hole (any agent can assign to any other agent).

**Alternatives considered**:
- Operations-assigned: Requires `DeliveryOperations` role — out of scope.
- Keep body-supplied agent id: Security vulnerability; contradicts roadmap.

### R5: Architecture Testing Approach

**Decision**: Use reflection-based assertions (no external library). Assert prohibited package references and prohibited type usages via `Assembly.GetReferencedAssemblies()` and type scanning.

**Rationale**: The existing codebase has no NetArchTest dependency. Reflection-based tests are self-contained and do not add a new package. The assertions are straightforward: check `Assembly.GetReferencedAssemblies().Name` for prohibited packages, and scan types for prohibited base types/interfaces.

**Alternatives considered**:
- NetArchTest: Popular library, but adds a dependency. The existing test projects have minimal dependencies.
- Manual code review: Not automated; cannot prevent regression.

### R6: Test Auth Pattern for Delivery API Tests

**Decision**: Mirror `Talabat.Customer.API.Tests` pattern: `CustomWebApplicationFactory` with `TestAuthHandler` using `X-Test-Subject` and `X-Test-Roles` headers. Add scope claim support via `X-Test-Scopes` header.

**Rationale**: The existing customer API tests use header-based auth (not JWT minting). Adding scope support to the header-based handler is simpler than introducing JWT minting. The `ScopeHandler` reads `FindAll("scope")`, so the test handler must emit scope claims.

**Alternatives considered**:
- JWT minting in tests: More realistic but adds complexity (key management, token signing). The existing pattern avoids this.
- Mock `IAuthorizationService`: Bypasses the real authorization pipeline; integration tests lose value.

### R7: CI Pipeline

**Decision**: GitHub Actions workflow with: checkout → setup .NET 10 → restore → build → test + coverage → vulnerability scan.

**Rationale**: Matches the plan's §6.5 specification. `XPlat Code Coverage` for coverage. `dotnet list package --vulnerable --include-transitive` for vulnerability scanning (fail on any finding).

**Alternatives considered**:
- Azure DevOps: Not specified in the plan; GitHub Actions is the default for open-source.
- Skip vulnerability scan: Security risk; the plan explicitly requires it.

## Phase 1: Design And Contracts

### Data Model

No new entities are introduced in Phase 10. The changes are to:
- **Command records**: `OutForDeliveryCommand`, `ArrivedAtRestaurantCommand`, `PickUpOrderCommand`, `DeliverOrderCommand`, `CancelDeliveryCommand`, `FailDeliveryCommand` gain an `int AgentId` property. `AssignDeliveryCommand` loses its `AgentId` property (replaced by controller injection).
- **Repository interface**: `IDeliveryRepository` gains `GetByIdForAgentAsync(int deliveryId, int agentId, CancellationToken)`.
- **Authorization policies**: New constants in `Auth/AuthorizationPolicies.cs` in each host.

See [data-model.md](data-model.md) for the full entity/policy inventory.

### Contracts

See [contracts/authorization-matrix.md](contracts/authorization-matrix.md) for the complete endpoint × policy × ownership matrix for both APIs.

### Quickstart

See [quickstart.md](quickstart.md) for local development, testing, and CI instructions.

## Phase 2: Planning Handoff

### Implementation Sequence

1. **Documentation first** (drives code decisions):
   - Write `docs/authorization-strategy.md` (four-gate model, status-code policy, naming reconciliation, role inventory, claim-freshness caveat, self-assignment decision, domain-cleanliness statement).
   - Write `docs/authorization-endpoint-matrix.md` (endpoint × policy table for both APIs).
   - Mark `docs/authorization-matrix.md` as superseded.

2. **Scope enforcement infrastructure**:
   - Create `Auth/ScopeRequirement.cs` + `Auth/ScopeHandler.cs` in both API hosts.
   - Create `Auth/AuthorizationPolicies.cs` in both API hosts with policy constants.
   - Register `ScopeHandler` and policies in `Program.cs` of both hosts.

3. **Apply policies to controllers**:
   - Replace `[Authorize]` → `[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]` on Customer API controllers.
   - Add `[Authorize(Policy = AuthorizationPolicies.CustomerScopeOnly)]` to `POST /api/me/profile` action.
   - Replace `[Authorize(Roles = "DeliveryAgent")]` → `[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)]` on Delivery API controllers.

4. **Delivery ownership hardening**:
   - Add `AgentId` to lifecycle command records.
   - Add `GetByIdForAgentAsync` to `IDeliveryRepository` and implement in `DeliveryRepository`.
   - Refactor lifecycle handlers to use `command.AgentId` instead of `ICurrentUser`.
   - Refactor `DeliveriesController` to inject `ICurrentUser` and populate `AgentId` in commands.
   - Refactor `AssignDelivery` to self-assignment: remove `AssignDeliveryBody.AgentId`, use `ICurrentUser.AgentId`.

5. **Audit customer handlers**: Verify all customer-side controllers use `ICurrentUser.CustomerId` (already confirmed — no changes needed).

6. **Test projects**:
   - Create `Talabat.Domain.Tests` with pure unit tests.
   - Create `Talabat.Delivery.API.Tests` mirroring the customer API test pattern.
   - Create `Talabat.ArchitectureTests` with reflection-based assertions.
   - Add authorization integration tests to both API test projects.

7. **CI pipeline**: Create `.github/workflows/ci.yml`.

8. **Final verification**: Build + all tests + vulnerability scan green.

### Risk Areas

- **Claim-freshness gap**: After `POST /api/me/profile`, the user gains the `Customer` capability but holds a token without the `Customer` role until refresh. Document this; do not weaken the policy.
- **ProfileEnforcementFilter coexistence**: The filter returns 409 for missing profiles; the new policies return 403 for missing scope/role. Ensure these don't conflict (the filter runs after authorization).
- **Handler refactoring scope**: 6 lifecycle handlers + `AssignDeliveryHandler` need `ICurrentUser` removal. Test each handler's existing test still passes after refactoring.

## Complexity Tracking

No constitution violations requiring justification.

## Post-Design Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| 1. Domain independence | ✅ PASS | No new packages in Domain or Application. `ScopeRequirement`/`ScopeHandler` in API hosts only. |
| 2. Application orchestrates | ✅ PASS | Handlers become identity-unaware (agent id from command, not `ICurrentUser`). |
| 3. Aggregate roots protect invariants | ✅ PASS | Ownership by load-scoping; domain state machines unchanged. |
| 4. Repository contracts in Domain | ✅ PASS | `GetByIdForAgentAsync` in `IDeliveryRepository`. |
| 5. Thin composition roots | ✅ PASS | Policies in `Program.cs`; no business logic in controllers. |
| 6. Unified user model | ✅ PASS | No changes to `User`. |
| 7. Database-generated IDs | ✅ PASS | No changes. |
| 8. EF mapping encapsulation | ✅ PASS | No changes. |
| 9. Database constraints | ✅ PASS | No changes. |

**Verdict**: All gates pass. Design is constitution-compliant. Ready for implementation.
