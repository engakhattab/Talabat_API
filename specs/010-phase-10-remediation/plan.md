# Implementation Plan: Phase 10 Remediation

**Branch**: `feature/user-aggregate-refactor` | **Date**: 2026-07-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/010-phase-10-remediation/spec.md`

## Summary

Remediates 8 critical blockers and consistency issues found during the architectural and security audit of Phase 10. The remediation is a separate spec-kit feature — Phase 10's existing artifacts are not modified except for a single D1 cross-reference edit.

The key changes:
1. **FR-R001**: Disable inbound claim mapping (`MapInboundClaims = false`) in both API hosts so `RequireRole` works with real JWTs.
2. **FR-R002**: Fix `GetPendingDeliveriesHandler` to check `HasDeliveryAgentCapability` and reduce PII in `PendingDeliveryDto`.
3. **FR-R003**: Implement delivery creation after checkout via a new `CreateDeliveryForOrder` use case (BR-DEL-016 fire-and-forget).
4. **FR-R004**: Add agent approval endpoints gated behind `IsDevelopment()`.
5. **FR-R005**: Add `RowVersion` to `Delivery` for optimistic concurrency on assignment.
6. **FR-R006**: Harden Identity host (signing credential gate, token lifetimes, profile service, redirect URIs).
7. **FR-R007**: Fix CI quality gates (SQL Server container, vulnerability check, `Database.Migrate()`).
8. **FR-R008**: Ownership convergence (controller-injection pattern, `[RequireCustomerProfile]` attribute, `ICurrentUserCapabilityResolver`, Delivery API middleware, CORS config, OpenAPI OAuth2, architecture test strengthening, `User.SetPhoneNumber`, `TimeProvider`).

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: ASP.NET Core 10, Duende IdentityServer 8, ASP.NET Core Identity, EF Core 10, xUnit 2.9.3
**Storage**: SQL Server via EF Core (single `TalabatDbContext` = `IdentityDbContext<User, IdentityRole<int>, int>`)
**Testing**: xUnit 2.9.3; existing test projects: `Talabat.Application.Tests`, `Talabat.Customer.API.Tests`, `Talabat.Delivery.API.Tests`, `Talabat.Identity.Tests`, `Talabat.Infrastructure.Tests`, `Talabat.ArchitectureTests`
**Target Platform**: ASP.NET Core Web API (two hosts: `Talabat.Customer.API` on port 5000, `Talabat.Delivery.API` on port 5002)
**Project Type**: Backend API hosts + Application layer + Domain
**Constraints**: Domain/Application must stay free of EF Core, ASP.NET Core, Duende (except documented `Identity.Stores` exception); ownership enforced by scoping the load; 404 for ownership failures

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| 1. Domain independence | ✅ PASS | No new packages in Domain or Application. `RowVersion` is plain `byte[]` — no dependency added. `CreateDeliveryForOrder` lives in Application. |
| 2. Application orchestrates | ✅ PASS | New `CreateDeliveryForOrderHandler` is a pure use case. Ownership convergence moves agent-id injection to controllers. |
| 3. Aggregate roots protect invariants | ✅ PASS | `Delivery.AssignAgent` already enforces status check; `RowVersion` adds DB-level concurrency. |
| 4. Repository contracts in Domain | ✅ PASS | `IDeliveryRepository.GetByOrderIdAsync` already exists. No new repository contracts needed. |
| 5. Thin composition roots | ✅ PASS | Agent approval endpoints in Identity host. CORS config in `Program.cs`. OpenAPI security in `Program.cs`. |
| 6. Unified user model | ✅ PASS | No changes to `User` aggregate shape. `SetPhoneNumber` encapsulation fix only. |
| 7. Database-generated IDs | ✅ PASS | No changes to entity IDs. |
| 8. EF mapping encapsulation | ✅ PASS | `RowVersion` with private setter mirrors `User.RowVersion` — identical shape, no new mutation surface. |
| 9. Database constraints | ✅ PASS | No new constraints. `UX_Deliveries_AssignedAgentId_Active` and `UX_Deliveries_OrderId` already exist. |

**Verdict**: No violations. All changes stay within constitution bounds.

## Project Structure

### Documentation

```text
specs/010-phase-10-remediation/
  spec.md              # Feature specification
  plan.md              # This file
  research.md          # Phase 0 research decisions
  data-model.md        # Phase 1 data model changes
  quickstart.md        # Local dev and testing guide
```

### Source Code (changes by area)

```text
src/Talabat/
  Talabat.API/                          # Customer API host
    Program.cs                          # MODIFY — add MapInboundClaims = false
    Controllers/
      CheckoutController.cs             # MODIFY — invoke CreateDeliveryForOrder after success
    Middleware/
      RequireCustomerProfileAttribute.cs  # NEW — replaces ProfileEnforcementFilter
      ProfileEnforcementFilter.cs       # DELETE

  Talabat.Delivery.API/                 # Delivery API host
    Program.cs                          # MODIFY — add MapInboundClaims = false, DomainExceptionHandler, /health, CORS config, OpenAPI OAuth2
    Controllers/
      StatusController.cs               # MODIFY — inject ICurrentUser, add AgentId to commands
      LocationController.cs             # MODIFY — inject ICurrentUser, use separate request contract
      DeliveriesController.cs           # MODIFY — extract TryGetAgentId to shared helper
    Auth/
      TryGetAgentIdHelper.cs            # NEW — shared helper extracted from DeliveriesController
      RequireCustomerProfileAttribute.cs  # (N/A — Delivery API has no profile enforcement)

  Talabat.Identity/                     # Identity host
    Program.cs                          # MODIFY — gate developer signing credential, add Postman redirect URIs
    IdentityServerConfig.cs             # MODIFY — token lifetime, UpdateAccessTokenClaimsOnRefresh, remove duplicate role claims
    TalabatProfileService.cs            # MODIFY — use AddRequestedClaims instead of IssuedClaims.AddRange
    IdentityConfig.cs                   # DELETE — dead pass-through duplicate
    Controllers/
      AccountController.cs              # MODIFY — add approve/reject endpoints

  Talabat.Application/
    DeliveryAgents/
      GetPendingDeliveries/
        GetPendingDeliveriesHandler.cs  # MODIFY — check HasDeliveryAgentCapability, remove PII from projection
        PendingDeliveryDto.cs           # MODIFY — remove Street, BuildingNumber, Floor, CustomerId
    Deliveries/
      CreateForOrder/
        CreateDeliveryForOrderCommand.cs  # NEW
        CreateDeliveryForOrderHandler.cs  # NEW
    Ordering/
      Checkout/
        CheckoutOutcome.cs              # MODIFY — add RestaurantId, DeliveryAddress properties (if not already present)
    Abstractions/
      ICurrentUserCapabilityResolver.cs  # NEW
    DependencyInjection.cs             # MODIFY — register new handler

  Talabat.Domain/
    Aggregates/
      DeliveryManagement/
        Delivery.cs                     # MODIFY — add RowVersion byte[]
      Users/
        User.cs                         # MODIFY — add SetPhoneNumber method
    Interfaces/
      IDeliveryRepository.cs            # (unchanged — GetByOrderIdAsync already exists)

  Talabat.Infrastructure/
    Persistence/
      Configurations/
        DeliveryConfiguration.cs        # MODIFY — add IsRowVersion() for RowVersion
      UnitOfWork.cs                     # VERIFY — DbUpdateConcurrencyException handling
    Identity/
      CurrentUserCapabilityResolver.cs  # NEW — implements ICurrentUserCapabilityResolver
    Migrations/
      AddDeliveryRowVersion.cs          # NEW — EF migration

  Talabat.Customer.API/               # (project reference, not modified directly)
  Talabat.Delivery.API/               # (project reference, not modified directly)
```

### Test Projects

```text
tests/
  Talabat.Customer.API.Tests/
    RealTokenPipelineTests.cs           # NEW — real JWT pipeline tests (FR-R001)
    CheckoutEndpointTests.cs            # MODIFY — add delivery-after-checkout tests (FR-R003)

  Talabat.Delivery.API.Tests/
    RealTokenPipelineTests.cs           # NEW — real JWT pipeline tests (FR-R001)
    DeliveriesAuthorizationTests.cs     # NEW — capability check tests (FR-R002)
    AssignmentConcurrencyTests.cs       # NEW — concurrent assignment tests (FR-R005)

  Talabat.Application.Tests/
    Domain/
      DeliveryAgents/
        GetPendingDeliveriesHandlerTests.cs  # NEW — capability guard + PII tests (FR-R002)
    Deliveries/
      CreateForOrder/
        CreateDeliveryForOrderHandlerTests.cs  # NEW — delivery creation tests (FR-R003)

  Talabat.Identity.Tests/
    AgentApprovalEndpointTests.cs       # NEW — approve/reject tests (FR-R004)

  Talabat.Infrastructure.Tests/
    Persistence/
      DeliveryPersistenceTests.cs       # MODIFY — add concurrency token test (FR-R005)

  Talabat.ArchitectureTests/
    ApiArchitectureTests.cs             # NEW — API projects must not reference TalabatDbContext
    DomainArchitectureTests.cs          # MODIFY — add csproj PackageReference assertions
    ApplicationArchitectureTests.cs     # MODIFY — add csproj PackageReference assertions
```

### CI

```text
.github/workflows/ci.yml               # NEW — GitHub Actions CI pipeline
```

## Phase 0: Research

### R1: MapInboundClaims Fix

**Decision**: Add `options.MapInboundClaims = false;` as the first line inside each `.AddJwtBearer` block.

**Rationale**: The default `MapInboundClaims = true` rewrites `"role"` to `ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`). When `RoleClaimType = "role"` is also set, `RequireRole("Customer")` looks for `ClaimTypes.Role` but finds `"role"` (unmapped). Setting `MapInboundClaims = false` preserves the raw claim type from the token.

**Alternatives considered**:
- Change `RoleClaimType` to `ClaimTypes.Role`: Would work but contradicts the token design where claims are emitted as `"role"`.
- Custom claim mapping: Over-engineered for this fix.

### R2: Delivery Creation After Checkout

**Decision**: Create a separate `CreateDeliveryForOrder` use case invoked by `CheckoutController` after `CheckoutHandler` succeeds. Not inside `CheckoutHandler`.

**Rationale**: Ordering must not depend on Delivery (constitution layering). The controller invokes the delivery creation as a fire-and-forget step — if it fails, the checkout still returns 200 (BR-DEL-016). The `CheckoutOutcome` already exposes `OrderId` but not `RestaurantId` or `DeliveryAddress`; these need to be added.

**Alternatives considered**:
- Domain event: Requires transactional outbox (deferred). Not feasible today.
- Inside `CheckoutHandler`: Couples ordering to delivery. Violates constitution.
- Background job: Over-engineered for MVP; adds infrastructure dependency.

### R3: Agent Approval Endpoints

**Decision**: Add `POST /account/delivery-agents/{userId:int}/approve` and `/reject` to `AccountController`, gated behind `IHostEnvironment.IsDevelopment()`.

**Rationale**: `IUserCapabilityService.ApproveDeliveryAgentAsync` is implemented but unreachable. No `Admin` policy exists yet. Gating behind `IsDevelopment()` prevents accidental production exposure. TODO comment references Phase 9 `AdminAccess` policy.

**Alternatives considered**:
- Create an `Admin` policy: Explicitly out of scope (no approved use cases).
- Separate controller: `AccountController` already handles registration; approval belongs here.

### R4: Concurrency Token for Delivery

**Decision**: Add `byte[] RowVersion` with `IsRowVersion()` mapping, mirroring `User.RowVersion`.

**Rationale**: Under READ COMMITTED, two agents can both read `PendingAssignment`, both pass `AssignAgent`'s status check, and both write. `RowVersion` ensures the second `SaveChanges` throws `DbUpdateConcurrencyException`, which `UnitOfWork` translates to `ConcurrencyConflictException` → 409.

**Alternatives considered**:
- Pessimistic locking: Requires `SELECT FOR UPDATE` — not idiomatic with EF Core.
- Application-level lock: Would drag locking infrastructure into Application layer.

### R5: ProfileEnforcementFilter Replacement

**Decision**: Replace path-matching filter with `[RequireCustomerProfile]` attribute applied per action.

**Rationale**: The filter's `StartsWith("/api/me/")` matching is fragile — a trailing slash turns 404 into 409. An attribute is explicit, discoverable, and cannot accidentally affect new routes.

**Alternatives considered**:
- Keep the filter: Continues to accumulate accidental behaviors.
- Global authorization filter: Cannot differentiate per-endpoint status codes (404 vs 409).

### R6: CurrentUserCapabilityResolver

**Decision**: Add `ICurrentUserCapabilityResolver` to `Talabat.Application/Abstractions/`, implement in `Talabat.Infrastructure/Identity/`, reducing each host's `CurrentUser` to claims + one resolver call, cached per request.

**Rationale**: `CurrentUser` is duplicated in both hosts and injects `TalabatDbContext` directly, issuing a synchronous DB query inside a property getter. Centralizing the capability resolution eliminates duplication and enables async resolution.

**Alternatives considered**:
- Keep current pattern: Duplicated code, sync DB query in property getter.
- Read from JWT claims: Loses real-time capability check (stale token problem).

### R7: Architecture Test Strengthening

**Decision**: Add assertions on `.csproj` `PackageReference` items (not just `Assembly.GetReferencedAssemblies()`). Add: no type in either API assembly may depend on `TalabatDbContext`.

**Rationale**: `Assembly.GetReferencedAssemblies()` prunes unused references — an unused forbidden package passes silently. `.csproj` checks catch the reference at the source.

**Alternatives considered**:
- Keep reflection-only: Insufficient — compiler prunes unused refs.
- NetArchTest: Already referenced in the project; use it for the new assertions.

### R8: CI Pipeline

**Decision**: GitHub Actions with SQL Server 2022 container, connection string via environment variable, `dotnet test` with code coverage, vulnerability check that fails on findings.

**Rationale**: The current CI has no SQL Server, so four test suites cannot run. The vulnerability check always exits 0. The container approach is standard for GitHub Actions.

**Alternatives considered**:
- SQLite: Different SQL dialect; `EnsureCreated` vs `Migrate` semantics differ.
- Azure SQL: Requires credentials; not suitable for open-source CI.

## Phase 1: Design And Contracts

### Data Model

**Delivery aggregate** — new property:
- `byte[] RowVersion { get; private set; }` — initialized to `[]` in private constructor. Mirrors `User.RowVersion`.

**EF mapping** — `DeliveryConfiguration`:
- `builder.Property(d => d.RowVersion).IsRowVersion();`

**No new entities introduced.** The `CreateDeliveryForOrderCommand` is a use case record, not an entity.

See [data-model.md](data-model.md) for the full entity/policy inventory.

### Contracts

The remediation's external contracts are:
- **Customer API**: `POST /api/me/checkout` response unchanged; delivery creation is internal.
- **Delivery API**: `GET /api/agent/deliveries/pending` response shape changes (PII removed).
- **Identity API**: `POST /account/delivery-agents/{userId}/approve` and `/reject` added (Development only).

See [data-model.md](data-model.md) for the full contract inventory.

### Quickstart

See [quickstart.md](quickstart.md) for local development, testing, and CI instructions.

## Phase 2: Planning Handoff

### Implementation Sequence

The remediation follows 8 phases (one per FR), each producing one commit. Phases are ordered by dependency and risk:

1. **Phase 0 — Spec-kit alignment** (commit: `docs:`):
   - Create remediation spec, plan, tasks.
   - Amend constitution quality gates.
   - Add BR-DEL-016 to delivery business rules.
   - Cross-reference FR-014 in Phase 10's tasks.md (T045, T048).

2. **Phase 0.5 — Baseline** (no commit):
   - `dotnet restore`, `dotnet build`, `dotnet test`.
   - Record pass/fail/error status.

3. **Phase 1 — JWT inbound claim mapping** (commit: `fix(auth):`):
   - Add `MapInboundClaims = false` to both API hosts.
   - Create `RealTokenPipelineTests` in both API test projects.
   - Verify tests fail before fix, pass after.

4. **Phase 2 — Pending-delivery capability check + PII** (commit: `fix(delivery):`):
   - Fix `GetPendingDeliveriesHandler` guard.
   - Reduce `PendingDeliveryDto` fields.
   - Update handler projection.
   - Create handler tests + API authorization tests.

5. **Phase 3 — Delivery creation after checkout** (commit: `feat(delivery):`):
   - Create `CreateDeliveryForOrder` use case.
   - Extend `CheckoutOutcome` with `RestaurantId` and `DeliveryAddress`.
   - Invoke from `CheckoutController` after success.
   - Implement BR-DEL-016 (fire-and-forget).
   - Create handler tests + checkout endpoint tests.

6. **Phase 4 — Agent approval endpoints** (commit: `feat(identity):`):
   - Add approve/reject endpoints to `AccountController`.
   - Gate behind `IsDevelopment()`.
   - Create `AgentApprovalEndpointTests`.

7. **Phase 5 — Delivery concurrency token** (commit: `fix(delivery):`):
   - Add `RowVersion` to `Delivery`.
   - Add `IsRowVersion()` to `DeliveryConfiguration`.
   - Create EF migration.
   - Create persistence + concurrency tests.

8. **Phase 6 — Identity host hardening** (commit: `fix(identity):`):
   - Gate developer signing credential.
   - Add Postman redirect URIs.
   - Fix token lifetimes.
   - Fix profile service claims.
   - Remove duplicate role claims.
   - Delete `IdentityConfig.cs`.
   - Create token claim tests.

9. **Phase 7 — CI quality gates** (commit: `ci:`):
   - Create `.github/workflows/ci.yml`.
   - Replace hardcoded connection strings.
   - Fix vulnerability check.
   - Switch fixtures to `Database.Migrate()`.

10. **Phase 8 — Ownership convergence** (commit: `refactor:`):
    - Convert 6 self-resolving handlers to controller-injection.
    - Extract `TryGetAgentId` shared helper.
    - Add `UpdateLocationRequest` separate contract.
    - Replace `ProfileEnforcementFilter` with `[RequireCustomerProfile]`.
    - Add `ICurrentUserCapabilityResolver`.
    - Add `DomainExceptionHandler` + `/health` to Delivery API.
    - Move CORS to configuration.
    - Add OpenAPI OAuth2 security scheme.
    - Strengthen architecture tests.
    - Add `User.SetPhoneNumber`.
    - Replace `ISystemClock` with `TimeProvider`.

### Risk Areas

- **Phase 1 test design**: `RealTokenPipelineTests` must use real `AddJwtBearer` (not `TestAuthHandler`). The factory variant must `PostConfigure<JwtBearerOptions>` to override only the signing key. If this doesn't work, the diagnosis was wrong.
- **Phase 3 ordering dependency**: `CheckoutOutcome` needs `RestaurantId` and `DeliveryAddress` — read it first to confirm what's already exposed.
- **Phase 5 migration under CI**: The `AddDeliveryRowVersion` migration must be created carefully to avoid breaking existing tests that use `EnsureCreated`.
- **Phase 7 fixture migration**: Switching from `EnsureCreated` to `Migrate` may expose real migration bugs — report, don't revert.
- **Phase 8 BOLA trap**: `UpdateLocationCommand` is currently bound directly from `[FromBody]`. Adding `AgentId` to the record would let callers post a fake agent id. Must introduce a separate `UpdateLocationRequest` contract.

## Complexity Tracking

No constitution violations requiring justification. The `RowVersion` addition to `Delivery` is justified by Principle 8's identical shape to `User.RowVersion`.

## Post-Design Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| 1. Domain independence | ✅ PASS | No new packages. `RowVersion` is plain `byte[]`. |
| 2. Application orchestrates | ✅ PASS | `CreateDeliveryForOrderHandler` is a pure use case. |
| 3. Aggregate roots protect invariants | ✅ PASS | `Delivery.AssignAgent` enforces status; `RowVersion` adds DB-level concurrency. |
| 4. Repository contracts in Domain | ✅ PASS | No new repository contracts. |
| 5. Thin composition roots | ✅ PASS | All middleware, policies, and CORS in host composition roots. |
| 6. Unified user model | ✅ PASS | `SetPhoneNumber` is an encapsulation fix, not a model change. |
| 7. Database-generated IDs | ✅ PASS | No changes. |
| 8. EF mapping encapsulation | ✅ PASS | `RowVersion` with private setter mirrors `User`. |
| 9. Database constraints | ✅ PASS | No new constraints. |

**Verdict**: All gates pass. Design is constitution-compliant. Ready for implementation.
