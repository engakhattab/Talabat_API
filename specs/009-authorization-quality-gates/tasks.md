# Tasks: Authorization Strategy And Quality Gates

**Input**: Design documents from `specs/009-authorization-quality-gates`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Phase 1: Setup

- [x] T001 Create `Talabat.Domain.Tests` project in `tests/Talabat.Domain.Tests/` with xUnit 2.9.3 and project references to `Talabat.Domain` only (no EF, no Infrastructure, no API)
- [x] T002 Create `Talabat.Delivery.API.Tests` project in `tests/Talabat.Delivery.API.Tests/` with xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing` 10.0.9, `System.IdentityModel.Tokens.Jwt` 8.0.2, and project references to `Talabat.Delivery.API`, `Talabat.Application`, `Talabat.Domain`, `Talabat.Infrastructure`
- [x] T003 Create `Talabat.ArchitectureTests` project in `tests/Talabat.ArchitectureTests/` with xUnit 2.9.3 and project references to `Talabat.Domain`, `Talabat.Application`, `Talabat.Customer.API`, `Talabat.Delivery.API`, `Talabat.Identity`, `Talabat.Infrastructure`
- [x] T004 Add all three new test projects to `src/Talabat/Talabat.slnx`

## Phase 2: Foundational

**Goal**: Scope enforcement and named policies — blocks all user stories
**Independent Test**: Build succeeds; `GET /api/catalog/*` returns 200 (anonymous); `GET /api/me/profile` with no token returns 401

- [x] T005 Create `Auth/ScopeRequirement.cs` and `Auth/ScopeHandler.cs` in `src/Talabat/Talabat.API/Auth/` implementing `IAuthorizationRequirement` and `AuthorizationHandler<ScopeRequirement>` with space-delimited and multi-entry scope claim support
- [x] T006 Create `Auth/AuthorizationPolicies.cs` in `src/Talabat/Talabat.API/Auth/` with constants `CustomerAccess`, `CustomerScopeOnly`
- [x] T007 Register `ScopeHandler` and policies (`CustomerAccess`, `CustomerScopeOnly`) in `src/Talabat/Talabat.API/Program.cs` using `AddAuthorizationBuilder()`
- [x] T008 Create `Auth/ScopeRequirement.cs` and `Auth/ScopeHandler.cs` in `src/Talabat/Talabat.Delivery.API/Auth/` (same pattern as Customer API)
- [x] T009 Create `Auth/AuthorizationPolicies.cs` in `src/Talabat/Talabat.Delivery.API/Auth/` with constant `DeliveryAgentAccess`
- [x] T010 Register `ScopeHandler` and `DeliveryAgentAccess` policy in `src/Talabat/Talabat.Delivery.API/Program.cs` using `AddAuthorizationBuilder()`
- [x] T011 Apply `[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]` to `AddressController`, `CartController`, `CheckoutController`, `OrderController` in `src/Talabat/Talabat.API/Controllers/` (CustomerController uses per-action `CustomerScopeOnly` — see T012)
- [x] T012 Apply `[Authorize(Policy = AuthorizationPolicies.CustomerScopeOnly)]` to all actions in `CustomerController` (no class-level `[Authorize]` — avoids ASP.NET Core policy merging where class-level and action-level policies are ANDed)
- [x] T013 Replace `[Authorize(Roles = "DeliveryAgent")]` with `[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)]` on `DeliveriesController`, `StatusController`, `LocationController` in `src/Talabat/Talabat.Delivery.API/Controllers/`
- [x] T014 Verify `CatalogController` and `/health` remain anonymous (no `[Authorize]` attribute)
- [x] T015 Build solution and verify no compilation errors

## Phase 3: User Story 1 — Delivery Agent Authorization

**Goal**: Delivery agent can only progress deliveries assigned to them; no agent can hijack another's work
**Independent Test**: Agent A calling lifecycle endpoint on Agent B's delivery returns 404; self-assignment works

- [x] T016 [US1] Add `int AgentId` property to `OutForDeliveryCommand` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressOutForDelivery/OutForDeliveryCommand.cs`
- [x] T017 [US1] Add `int AgentId` property to `ArrivedAtRestaurantCommand` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressArrive/ArrivedAtRestaurantCommand.cs`
- [x] T018 [US1] Add `int AgentId` property to `PickUpOrderCommand` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressPickup/PickUpOrderCommand.cs`
- [x] T019 [US1] Add `int AgentId` property to `DeliverOrderCommand` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressDeliver/DeliverOrderCommand.cs`
- [x] T020 [US1] Add `int AgentId` property to `CancelDeliveryCommand` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressCancel/CancelDeliveryCommand.cs`
- [x] T021 [US1] Add `int AgentId` property to `FailDeliveryCommand` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressFail/FailDeliveryCommand.cs`
- [x] T022 [US1] Remove `int AgentId` from `AssignDeliveryCommand` in `src/Talabat/Talabat.Application/DeliveryAgents/AssignDelivery/AssignDeliveryCommand.cs` (keep only `int DeliveryId`)
- [x] T023 [US1] Add `Task<Delivery?> GetByIdForAgentAsync(int deliveryId, int agentId, CancellationToken)` to `IDeliveryRepository` in `src/Talabat/Talabat.Domain/Interfaces/IDeliveryRepository.cs`
- [x] T024 [US1] Implement `GetByIdForAgentAsync` in `src/Talabat/Talabat.Infrastructure/Repositories/DeliveryRepository.cs` — query delivery where `Id == deliveryId && AssignedAgentId == agentId`
- [x] T025 [US1] Refactor `OutForDeliveryHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressOutForDelivery/OutForDeliveryHandler.cs` — remove `ICurrentUser` dependency, use `command.AgentId`, load delivery via `GetByIdForAgentAsync`, return not-found when null
- [x] T026 [US1] Refactor `ArrivedAtRestaurantHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressArrive/ArrivedAtRestaurantHandler.cs` — same pattern
- [x] T027 [US1] Refactor `PickUpOrderHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressPickup/PickUpOrderHandler.cs` — same pattern
- [x] T028 [US1] Refactor `DeliverOrderHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressDeliver/DeliverOrderHandler.cs` — remove `ICurrentUser`, use `command.AgentId`, load delivery via `GetByIdForAgentAsync`, keep `IUserRepository` + `DeliveryAssignmentDomainService` for domain service calls
- [x] T029 [US1] Refactor `CancelDeliveryHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressCancel/CancelDeliveryHandler.cs` — same pattern as DeliverOrderHandler
- [x] T030 [US1] Refactor `FailDeliveryHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/ProgressFail/FailDeliveryHandler.cs` — same pattern
- [x] T031 [US1] Refactor `AssignDeliveryHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/AssignDelivery/AssignDeliveryHandler.cs` — remove `AgentId` from command, load delivery from pending pool, assign to caller's agent id (from controller)
- [x] T032 [US1] Refactor `DeliveriesController` in `src/Talabat/Talabat.Delivery.API/Controllers/DeliveriesController.cs` — inject `ICurrentUser`, populate `AgentId` in all lifecycle commands from `_currentUser.AgentId`, remove `AssignDeliveryBody.AgentId` from assign endpoint
- [x] T033 [US1] Update lifecycle handler tests in `tests/Talabat.Application.Tests/Domain/DeliveryAgents/` — update command construction to include `AgentId`, verify handler returns not-found when delivery not assigned to agent
- [x] T034 [US1] Update `AssignDeliveryHandlerTests` in `tests/Talabat.Application.Tests/Domain/DeliveryAgents/` — remove `AgentId` from command, verify self-assignment behavior
- [x] T035 [US1] Build and run all existing tests to verify no regressions

## Phase 4: User Story 2 — Customer Authorization

**Goal**: Customer data is protected by scope+role policies; cross-customer access returns 404
**Independent Test**: Customer A requesting Customer B's order returns 404; anonymous catalog access returns 200

- [x] T036 [US2] Audit all Customer API controllers (`CustomerController`, `AddressController`, `CartController`, `CheckoutController`, `OrderController`) to confirm every handler receives `CustomerId` from `ICurrentUser.CustomerId` (no caller-supplied IDs)
- [x] T037 [US2] Verify `ProfileEnforcementFilter` in `src/Talabat/Talabat.API/Middleware/ProfileEnforcementFilter.cs` still functions correctly with new policies (runs after authorization, returns 409 for missing profiles)
- [x] T038 [US2] Build and run all existing Customer API tests to verify no regressions from policy changes

## Phase 5: User Story 3 — Quality Gates

**Goal**: Automated tests prevent authorization and architecture regression; CI runs on every push/PR
**Independent Test**: All new test projects pass; CI workflow file exists and is valid

- [x] T039 [US3] Create `Talabat.Domain.Tests/` test files: `UserCapabilityTests.cs` (InitializeCustomerProfile, SubmitDeliveryAgentApplication, Approve/Reject, GoOnline/GoOffline/Suspend/MarkBusy/MarkAvailable transitions, invalid transitions throw correct exceptions)
- [x] T040 [US3] Create `Talabat.Domain.Tests/` test files: `AddressTests.cs` (duplicate rejection, default-address switching, RequireCustomer guard)
- [x] T041 [US3] Create `Talabat.Domain.Tests/` test files: `ValueObjectsTests.cs` (Money, GeoLocation, TimeRange, DeliveryAddressSnapshot, CheckoutItemSnapshot, CatalogProductSnapshot)
- [x] T042 [US3] Create `Talabat.Domain.Tests/` test files: `ValueObjectTests.cs` (Address, GeoLocation, Money range validation) — consolidated into T041
- [x] T043 [US3] Create `Talabat.Delivery.API.Tests/Infrastructure/CustomWebApplicationFactory.cs` mirroring `Talabat.Customer.API.Tests` pattern — unique per-test DB, role seeding, `TestAuthHandler` with `X-Test-Subject`, `X-Test-Roles`, `X-Test-Scopes` headers
- [x] T044 [US3] Create `Talabat.Delivery.API.Tests/Infrastructure/TestAuthHandler.cs` — header-based auth with scope claim support via `X-Test-Scopes` comma-separated header, emitting `"scope"` claims
- [x] T045 [US3] Create `Talabat.Delivery.API.Tests/DeliveriesAuthorizationTests.cs` — test matrix: no token → 401, wrong scope → 403, wrong role → 403, valid token → passes authorization, anonymous → 401
- [x] T046 [US3] Delivery ownership is enforced at handler level via `GetByIdForAgentAsync` — tested in Phase 3 handler tests (T025-T031)
- [x] T047 [US3] Update `Talabat.Customer.API.Tests/Infrastructure/TestAuthHandler.cs` — add `X-Test-Scopes` header support and `"scope"` claim emission
- [x] T048 [US3] Create `Talabat.Customer.API.Tests/AuthorizationTests.cs` — test matrix: no token → 401, wrong scope → 403, correct scope (no role) → passes, correct scope + role → passes, anonymous catalog → 200, profile creation with no role → not 403
- [x] T049 [US3] Create `Talabat.ArchitectureTests/DomainArchitectureTests.cs` — assert `Talabat.Domain` references no `Microsoft.EntityFrameworkCore`, no `Microsoft.AspNetCore.*` (except explicit `Microsoft.Extensions.Identity.Stores` allow), no Duende packages (16 tests passing)
- [x] T050 [US3] Create `Talabat.ArchitectureTests/ApplicationArchitectureTests.cs` — assert `Talabat.Application` references no EF Core, no `HttpContext`, no `ClaimsPrincipal`, no Duende
- [x] T051 [US3] Create `Talabat.ArchitectureTests/CrossCuttingTests.cs` — assert no aggregate type references `ICurrentUser` or claim/role types; no API project references `Talabat.Identity`
- [x] T052 [US3] Create `.github/workflows/ci.yml` — checkout, setup .NET 10, restore, build, test with `XPlat Code Coverage`, `dotnet list package --vulnerable --include-transitive` (fail on finding), run on push and PR
- [x] T053 [US3] Add new test projects to solution file `src/Talabat/Talabat.slnx` (done in Phase 1 T004)
- [x] T054 [US3] Build and run all test projects — verify green across the board

## Final Phase: Polish & Cross-Cutting Concerns

- [x] T055 Write `docs/authorization-strategy.md` — four-gate model, status-code policy (401/403/404/409), naming reconciliation table (audiences, scopes, policies), role inventory, claim-freshness caveat, self-assignment decision, domain-cleanliness statement
- [x] T056 Write `docs/authorization-endpoint-matrix.md` — one table per host (Customer API, Delivery API) with columns: Endpoint, Method, Anonymous, Policy, Role, Scope, Ownership Rule, Failure Codes
- [x] T057 Add superseded header to `docs/authorization-matrix.md` pointing to `docs/authorization-endpoint-matrix.md`
- [x] T058 Run full build + all tests + vulnerability scan to verify final state is green

## Dependencies

```text
Phase 1 (Setup)
  └─> Phase 2 (Foundational — scope enforcement + policies)
        ├─> Phase 3 (US1 — Delivery Agent Authorization)
        └─> Phase 4 (US2 — Customer Authorization)
              └─> Phase 5 (US3 — Quality Gates)
                    └─> Final Phase (Polish & Documentation)
```

- Phase 3 and Phase 4 are **independent** of each other (both depend on Phase 2)
- Phase 5 depends on Phases 3+4 (integration tests exercise the policies and ownership rules)
- Final Phase depends on Phase 5 (documentation references the test matrix)

## Parallel Execution Examples

**Within Phase 3 (US1)**: Tasks T016-T022 (command record changes) are independent and can run in parallel. Tasks T025-T030 (handler refactors) are independent and can run in parallel after T016-T022 + T023-T024 complete.

**Between Phase 3 and Phase 4**: Phase 3 (delivery ownership) and Phase 4 (customer audit) can run in parallel since they touch different API hosts and different command records.

**Within Phase 5 (US3)**: Tasks T039-T042 (domain tests) are independent. Tasks T043-T046 (Delivery API tests) are independent. Tasks T049-T051 (architecture tests) are independent. These three groups can run in parallel.

## Implementation Strategy

**MVP Scope**: Phase 2 (Foundational) + Phase 3 (US1 — Delivery Agent Authorization). This delivers the core security fix (delivery ownership hardening) and scope enforcement.

**Incremental Delivery**:
1. Ship Phase 2 first — scope enforcement is a prerequisite for all authorization changes
2. Ship Phase 3 next — delivery ownership is the highest security priority
3. Ship Phase 4 — customer authorization policy application
4. Ship Phase 5 — quality gates prevent regression on the above
5. Ship Final Phase — documentation for maintainability

**Risk Mitigation**: Each phase ends with a build + test gate. If any phase breaks existing tests, stop and fix before proceeding. The handler refactors (T025-T030) are the highest-risk tasks — test each handler's existing test suite after refactoring.
