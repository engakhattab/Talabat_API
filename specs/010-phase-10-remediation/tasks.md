# Tasks: Phase 10 Remediation

**Input**: Design documents from `specs/010-phase-10-remediation`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md

## Phase 1: Setup

- [x] T001 Amend constitution Quality Gates in `.specify/memory/constitution.md` — change "all four test projects MUST pass" to "all test projects in the solution MUST pass"
- [x] T002 Add FR-014 scenario references to T045 (Delivery) and T048 (Customer) descriptions in `specs/009-authorization-quality-gates/tasks.md` (description text only — no status change)
- [x] T003 Verify `docs/delivery/delivery-business-rules.md` contains BR-DEL-016 (already added in Phase 0e)

## Phase 2: Foundational

- [x] T004 Run baseline: `dotnet restore src/Talabat/Talabat.slnx`, `dotnet build src/Talabat/Talabat.slnx --no-restore -c Release`, `dotnet test src/Talabat/Talabat.slnx --no-build -c Release` — record pass/fail/error per suite
- [x] T005 Add `ApplicationErrorCodes.DeliveryAlreadyExists` to `src/Talabat/Talabat.Application/Common/Results/ApplicationErrorCodes.cs` (needed by Phase 4 delivery creation)

## Phase 3: FR-R001 — JWT Inbound Claim Mapping

**Goal**: `RequireRole("Customer")` and `RequireRole("DeliveryAgent")` work with real JWT tokens
**Independent Test**: `RealTokenPipelineTests` pass with real `AddJwtBearer` pipeline; tests fail before the fix

- [x] T006 [P] [US1] Add `options.MapInboundClaims = false;` as first line in `.AddJwtBearer` block in `src/Talabat/Talabat.API/Program.cs:48`
- [x] T007 [P] [US1] Add `options.MapInboundClaims = false;` as first line in `.AddJwtBearer` block in `src/Talabat/Talabat.Delivery.API/Program.cs:36`
- [x] T008 [US1] Create `tests/Talabat.Customer.API.Tests/RealTokenPipelineTests.cs` — factory variant with `PostConfigure<JwtBearerOptions>` overriding signing key; mint tokens via `JsonWebTokenHandler` with `sub`, `role`, `scope`, `customer_id`, `aud`, `iss`; assert: valid token → 200, wrong aud → 401, missing scope → 403, wrong role → 403, expired → 401, no header → 401
- [x] T009 [US1] Create `tests/Talabat.Delivery.API.Tests/RealTokenPipelineTests.cs` — same pattern as T008 but with delivery audience/scope/role
- [x] T010 [US1] Verify T008 and T009 tests fail before T006/T007 fix, pass after — record before/after results

## Phase 4: FR-R002 — Pending-Delivery Capability Check + PII

**Goal**: Only authenticated delivery agents with active capability see pending deliveries; PII is minimized pre-assignment
**Independent Test**: `GetPendingDeliveriesHandlerTests` verify capability guard and DTO shape; API test: revoked capability → 403

- [x] T011 [US2] Replace guard in `src/Talabat/Talabat.Application/DeliveryAgents/GetPendingDeliveries/GetPendingDeliveriesHandler.cs:24` — check `!_currentUser.IsAuthenticated || !_currentUser.HasDeliveryAgentCapability || _currentUser.AgentId is null` and return `AgentRequired` failure
- [x] T012 [US2] Update handler projection in `src/Talabat/Talabat.Application/DeliveryAgents/GetPendingDeliveries/GetPendingDeliveriesHandler.cs:34` — remove `CustomerId`, `Street`, `BuildingNumber`, `Floor` from DTO construction
- [x] T013 [US2] Modify `src/Talabat/Talabat.Application/DeliveryAgents/GetPendingDeliveries/PendingDeliveryDto.cs` — remove `CustomerId`, `Street`, `BuildingNumber`, `Floor` parameters; keep `Id`, `OrderId`, `RestaurantId`, `Status`, `City`, `CreatedAt`
- [x] T014 [US2] Create `tests/Talabat.Application.Tests/Domain/DeliveryAgents/GetPendingDeliveriesHandlerTests.cs` — test: authenticated no agent capability → `AgentRequired`; authenticated agent → success; DTO has no PII fields
- [x] T015 [US2] Create `tests/Talabat.Delivery.API.Tests/DeliveriesAuthorizationTests.cs` — test: token with `DeliveryAgent` role but user's `UserType` lacks `DeliveryAgent` flag → `GET /api/agent/deliveries/pending` returns 403

## Phase 5: FR-R003 — Delivery Creation After Checkout

**Goal**: A `Delivery` in `PendingAssignment` is created after successful checkout; delivery failure does not invalidate the order (BR-DEL-016)
**Independent Test**: `CreateDeliveryForOrderHandlerTests` verify creation + duplicate; `CheckoutEndpointTests` verify delivery exists after checkout; checkout returns 200 when delivery creation throws

- [x] T016 [P] [US3] Read `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutOutcome.cs` — confirm `CheckoutSucceededOutcome` shape; identify what properties need adding (`RestaurantId`, `DeliveryAddress`)
- [x] T017 [US3] Extend `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutOutcome.cs` — add `RestaurantId` (int) and `DeliveryAddress` (DeliveryAddressSnapshot) to `CheckoutSucceededOutcome`
- [x] T018 [US3] Update `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutResultMapper.cs` — pass `RestaurantId` and `DeliveryAddress` into `CheckoutSucceededOutcome` constructor
- [x] T019 [P] [US3] Create `src/Talabat/Talabat.Application/Deliveries/CreateForOrder/CreateDeliveryForOrderCommand.cs` — record with `OrderId`, `CustomerId`, `RestaurantId`, `DeliveryAddress`
- [x] T020 [US3] Create `src/Talabat/Talabat.Application/Deliveries/CreateForOrder/CreateDeliveryForOrderHandler.cs` — check `GetByOrderIdAsync` for idempotency; create `Delivery` in `PendingAssignment`; add + save; map unique-index violation to `DeliveryAlreadyExists`
- [x] T021 [US3] Register `CreateDeliveryForOrderHandler` in `src/Talabat/Talabat.Application/DependencyInjection.cs`
- [x] T022 [US3] Modify `src/Talabat/Talabat.API/Controllers/CheckoutController.cs` — after successful checkout with `CheckoutSucceededOutcome`, invoke `CreateDeliveryForOrderHandler` in try/catch; log failure at Error with `OrderId`; return checkout success regardless (BR-DEL-016)
- [x] T023 [US3] Create `tests/Talabat.Application.Tests/Deliveries/CreateForOrder/CreateDeliveryForOrderHandlerTests.cs` — test: creates `PendingAssignment` delivery with order data; duplicate call → `Conflict`/`DeliveryAlreadyExists`
- [x] T024 [US3] Update `tests/Talabat.Customer.API.Tests/CheckoutEndpointTests.cs` — test: after successful checkout, delivery row exists with `PendingAssignment`; test: checkout returns 200 when delivery creation throws (inject throwing repository)

## Phase 6: FR-R004 — Agent Approval Endpoints

**Goal**: `IUserCapabilityService.ApproveDeliveryAgentAsync` is reachable via HTTP; gated behind Development
**Independent Test**: `AgentApprovalEndpointTests` verify register→approve flow, edge cases, and Development gate

- [x] T025 [US1] Add `POST /account/delivery-agents/{userId:int}/approve` endpoint to `src/Talabat/Talabat.Identity/Controllers/AccountController.cs` — gate behind `!_environment.IsDevelopment()` → return NotFound; call `_capabilityService.ApproveDeliveryAgentAsync`; add `// TODO(Phase 9): replace with AdminAccess policy`
- [x] T026 [US1] Add `POST /account/delivery-agents/{userId:int}/reject` endpoint to `src/Talabat/Talabat.Identity/Controllers/AccountController.cs` — same Development gate pattern; call `_capabilityService.RejectDeliveryAgentAsync`
- [x] T027 [US1] Inject `IHostEnvironment _environment` into `AccountController` constructor
- [x] T028 [US1] Create `tests/Talabat.Identity.Tests/AgentApprovalEndpointTests.cs` — test: register applicant → approve → `UserType.DeliveryAgent` set, role assigned, `DeliveryAgentStatus == Offline`, security stamp changed; approve already-approved → 409; approve non-existent → 404; outside Development → 404

## Phase 7: FR-R005 — Delivery Concurrency Token

**Goal**: Concurrent assignment of the same delivery yields exactly one success and one 409
**Independent Test**: `DeliveryPersistenceTests` verify concurrency exception; `AssignmentConcurrencyTests` verify HTTP 409

- [x] T029 [P] [US2] Add `public byte[] RowVersion { get; private set; }` to `src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/Delivery.cs` — initialize to `[]` in private constructor
- [x] T030 [US2] Add `builder.Property(d => d.RowVersion).IsRowVersion();` to `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryConfiguration.cs`
- [x] T031 [US2] Create EF migration: `dotnet ef migrations add AddDeliveryRowVersion -p src/Talabat/Talabat.Infrastructure -s src/Talabat/Talabat.API`
- [x] T032 [US2] Verify `src/Talabat/Talabat.Infrastructure/Persistence/UnitOfWork.cs` handles `DbUpdateConcurrencyException` → `ConcurrencyConflictException` (add if missing — in UnitOfWork, not Application)
- [x] T033 [US2] Update `tests/Talabat.Infrastructure.Tests/Persistence/DeliveryPersistenceTests.cs` — test: two contexts load same delivery, both assign, second `SaveChanges` throws concurrency exception
- [x] T034 [US2] Create `tests/Talabat.Delivery.API.Tests/AssignmentConcurrencyTests.cs` — test: concurrent `POST {id}/assign` from two agents yields exactly one 200 and one 409

## Phase 8: FR-R006 — Identity Host Hardening

**Goal**: IdentityServer configuration is secure and correct for development; profile service emits scope-appropriate claims
**Independent Test**: Token claim tests verify scope-appropriate claims; redirect URIs present; `UpdateAccessTokenClaimsOnRefresh == true`

- [x] T035 [P] [US1] Modify `src/Talabat/Talabat.Identity/Program.cs` — gate `AddDeveloperSigningCredential()` behind `builder.Environment.IsDevelopment()`; throw `InvalidOperationException` in non-Development
- [x] T036 [P] [US1] Modify `src/Talabat/Talabat.Identity/IdentityServerConfig.cs` — add `https://oauth.pstmn.io/v1/browser-callback` to both clients' `RedirectUris`; set `AccessTokenLifetime = 900`; set `UpdateAccessTokenClaimsOnRefresh = true`; set `RefreshTokenExpiration = TokenExpiration.Sliding`; keep `SlidingRefreshTokenLifetime = 1296000`
- [x] T037 [US1] Remove duplicate `JwtClaimTypes.Role` / `"role"` entries in `src/Talabat/Talabat.Identity/IdentityServerConfig.cs` ApiResource.UserClaims — keep only `"role"`
- [x] T038 [US1] Modify `src/Talabat/Talabat.Identity/TalabatProfileService.cs:51` — replace `context.IssuedClaims.AddRange(claims)` with `context.AddRequestedClaims(claims)`
- [x] T039 [US1] Delete `src/Talabat/Talabat.Identity/IdentityConfig.cs` (dead pass-through duplicate)
- [x] T040 [US1] Verify `src/Talabat/Talabat.Identity/Program.cs` references `IdentityServerConfig` directly (not via deleted `IdentityConfig`)
- [x] T041 [US1] Create token claim tests (add to existing test project or new file in `tests/Talabat.Identity.Tests/`) — test: dual-capability user gets scope-appropriate claims; Postman URIs present; `UpdateAccessTokenClaimsOnRefresh == true`

## Phase 9: FR-R007 — CI Quality Gates

**Goal**: CI pipeline runs all seven test projects against a real SQL Server and fails on vulnerable packages
**Independent Test**: CI green on clean run with all seven test projects reported

- [x] T042 [US1] Create `.github/workflows/ci.yml` — trigger on push/PR; setup .NET 10; SQL Server 2022 service container with health check; `ConnectionStrings__TalabatDb` env var; restore → build → test + coverage → vulnerability scan
- [x] T043 [US1] Make vulnerability check a real gate in `.github/workflows/ci.yml` — pipe `dotnet list package --vulnerable` output, fail if "has the following vulnerable packages" found
- [x] T044 [US1] Replace hardcoded `DESKTOP-5IHGJ9F\SQLEXPRESS` connection strings with placeholders in `src/Talabat/Talabat.API/appsettings.Development.json`, `src/Talabat/Talabat.Delivery.API/appsettings.Development.json`, `src/Talabat/Talabat.Identity/appsettings.Development.json`, `src/Talabat/Talabat.Infrastructure/Persistence/TalabatDbContextFactory.cs`
- [x] T045 [US1] Switch test fixtures from `Database.EnsureCreated()` to `Database.Migrate()` in all `SqlServerDatabaseFixture` / `CustomWebApplicationFactory` classes — report if any fixture breaks (genuine migration bug)

## Phase 10: FR-R008 — Ownership Convergence

**Goal**: All delivery-agent handlers use controller-injection pattern; Delivery API has middleware and health; architecture tests are strengthened
**Independent Test**: All existing tests pass; architecture tests pass with no new exemptions; `UpdateLocationRequest` BOLA regression test passes

- [ ] T046 [P] [US2] Create `src/Talabat/Talabat.Delivery.API/Auth/TryGetAgentIdHelper.cs` — extract `TryGetAgentId` from `DeliveriesController` into a shared static helper or injectable service
- [ ] T047 [US2] Add `int AgentId` to `src/Talabat/Talabat.Application/DeliveryAgents/GoOnline/GoOnlineCommand.cs`; remove `ICurrentUser` dependency and guard from `src/Talabat/Talabat.Application/DeliveryAgents/GoOnline/GoOnlineHandler.cs`; inject `ICurrentUser` into `src/Talabat/Talabat.Delivery.API/Controllers/StatusController.cs` and call `TryGetAgentId` in `GoOnline` action
- [ ] T048 [US2] Add `int AgentId` to `src/Talabat/Talabat.Application/DeliveryAgents/GoOffline/GoOfflineCommand.cs`; remove `ICurrentUser` from `GoOfflineHandler.cs`; update `StatusController.GoOffline` to inject agent id
- [ ] T049 [US2] Create `src/Talabat/Talabat.Application/DeliveryAgents/UpdateLocation/UpdateLocationRequest.cs` — separate record `(decimal Latitude, decimal Longitude)`; modify `src/Talabat/Talabat.Delivery.API/Controllers/LocationController.cs` to accept `UpdateLocationRequest`, call `TryGetAgentId`, construct `UpdateLocationCommand(agentId, request.Latitude, request.Longitude)` server-side
- [ ] T050 [US2] Add `int AgentId` to `src/Talabat/Talabat.Application/DeliveryAgents/UpdateLocation/UpdateLocationCommand.cs`; remove `ICurrentUser` from `UpdateLocationHandler.cs`
- [ ] T051 [US2] Add `int AgentId` to `src/Talabat/Talabat.Application/DeliveryAgents/GetActiveDelivery/GetActiveDeliveryQuery.cs`; remove `ICurrentUser` from `GetActiveDeliveryHandler.cs`; update `DeliveriesController.GetActiveDelivery` to inject agent id via `TryGetAgentId`
- [ ] T052 [US2] Add `int AgentId` to `src/Talabat/Talabat.Application/DeliveryAgents/GetDeliveryHistory/GetDeliveryHistoryQuery.cs`; remove `ICurrentUser` from `GetDeliveryHistoryHandler.cs`; update `DeliveriesController.GetDeliveryHistory`
- [ ] T053 [US2] Add `int AgentId` to `src/Talabat/Talabat.Application/DeliveryAgents/GetPendingDeliveries/GetPendingDeliveriesQuery.cs`; remove `ICurrentUser` from `GetPendingDeliveriesHandler.cs` (supersedes Phase 2 in-handler guard); update `DeliveriesController.GetPendingDeliveries`
- [ ] T054 [US2] Update `DeliveriesController` constructor — use `TryGetAgentIdHelper` instead of private method
- [ ] T055 [US1] Create `src/Talabat/Talabat.API/Middleware/RequireCustomerProfileAttribute.cs` — attribute that checks `_currentUser.HasCustomerCapability`; returns 404 for `GET /api/me/profile`, 409 for other `/api/me/*`; allows `POST /api/me/profile` through
- [x] T056 [US1] Apply `[RequireCustomerProfile]` to relevant actions in `src/Talabat/Talabat.API/Controllers/` (CustomerController, AddressController, CartController, OrderController — not CatalogController, not POST profile)
- [x] T057 [US1] Delete `src/Talabat/Talabat.API/Middleware/ProfileEnforcementFilter.cs`; remove `options.Filters.Add<ProfileEnforcementFilter>()` from `src/Talabat/Talabat.API/Program.cs`
- [x] T058 [US1] Create `src/Talabat/Talabat.Application/Abstractions/ICurrentUserCapabilityResolver.cs` — interface with `Task ResolveAsync(CancellationToken ct)` method
- [x] T059 [US1] Create `src/Talabat/Talabat.Infrastructure/Identity/CurrentUserCapabilityResolver.cs` — implement `ICurrentUserCapabilityResolver`; resolve capabilities from `TalabatDbContext`; cache per request
- [x] T060 [US1] Modify both hosts' `CurrentUser` class — inject `ICurrentUserCapabilityResolver`; reduce to claims + one resolver call; remove direct `TalabatDbContext` injection
- [ ] T061 [US1] Add `AddExceptionHandler<DomainExceptionHandler>()` + `UseExceptionHandler()` and `MapHealthChecks("/health")` to `src/Talabat/Talabat.Delivery.API/Program.cs`
- [ ] T062 [US1] Move `app.UseCors("SpaCorsPolicy")` out of `IsDevelopment()` block in both API hosts; drive origins from `Cors:AllowedOrigins` configuration
- [ ] T063 [US1] Add OAuth2 security scheme to OpenAPI documents in all three hosts' `Program.cs`
- [ ] T064 [US1] Add `User.SetPhoneNumber(string?)` with `Guard.OptionalText` normalization to `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs`; update `UserCapabilityService` to call `user.SetPhoneNumber(phoneNumber.Trim())` instead of direct assignment
- [ ] T065 [US1] Replace `#pragma warning disable CS0618` + `ISystemClock` constructor in both `tests/Talabat.Customer.API.Tests/Infrastructure/TestAuthHandler.cs` and `tests/Talabat.Delivery.API.Tests/Infrastructure/TestAuthHandler.cs` with `TimeProvider` constructor overload
- [ ] T066 [US1] Strengthen `tests/Talabat.ArchitectureTests/DomainArchitectureTests.cs` — add `.csproj` PackageReference assertions using `NetArchTest.Rules`
- [ ] T067 [US1] Strengthen `tests/Talabat.ArchitectureTests/ApplicationArchitectureTests.cs` — add `.csproj` PackageReference assertions
- [ ] T068 [US1] Create `tests/Talabat.ArchitectureTests/ApiArchitectureTests.cs` — assert: no type in either API assembly depends on `TalabatDbContext`
- [ ] T069 [US1] Add BOLA regression test: posting `agentId` in `UpdateLocationRequest` body must not change which agent's location is updated

## Final Phase: Verification

- [ ] T070 Run per-phase gate: `dotnet build src/Talabat/Talabat.slnx -c Release --no-restore` — zero new warnings
- [ ] T071 Run per-phase gate: `dotnet test src/Talabat/Talabat.slnx -c Release --no-build` — all seven test projects pass
- [ ] T072 Run per-phase gate: `dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive` — zero findings
- [ ] T073 Verify `Talabat.ArchitectureTests` passes with no new exemptions
- [ ] T074 Verify no new `PackageReference` in `Talabat.Domain` or `Talabat.Application` csproj files
- [ ] T075 Verify no new EF Core / ASP.NET Core / Duende type referenced from Application layer

## Dependencies

```text
Phase 1 (Setup): T001, T002, T003 — no dependencies
Phase 2 (Foundational): T004, T005 — T005 blocks T019-T024
Phase 3 (FR-R001): T006-T010 — independent
Phase 4 (FR-R002): T011-T015 — independent
Phase 5 (FR-R003): T016-T024 — T005 blocks T020; T016 blocks T017-T018
Phase 6 (FR-R004): T025-T028 — independent
Phase 7 (FR-R005): T029-T034 — independent
Phase 8 (FR-R006): T035-T041 — independent
Phase 9 (FR-R007): T042-T045 — independent
Phase 10 (FR-R008): T046-T069 — T046 blocks T054; T049-T053 depend on T046; T055-T057 independent; T058-T060 independent; T066-T068 depend on T058-T060
Final Phase: T070-T075 — depends on all previous phases
```

## Parallel Execution Examples

```text
Batch 1 (no dependencies):
  T001, T002, T003, T005

Batch 2 (after T001-T003):
  T006 + T007 (parallel — different files)
  T011 + T013 (parallel — different files)
  T016 + T019 (parallel — different files)
  T025 + T026 + T027 (sequential — same file)
  T029 + T030 (parallel — different files)
  T035 + T036 + T037 (sequential — same file)
  T042 + T044 (parallel — different files)
  T046 + T055 + T058 + T062 + T063 (parallel — different files)

Batch 3 (tests after implementation):
  T008 + T009 (parallel — different test projects)
  T014 + T015 (parallel — different test projects)
  T023 + T024 (parallel — different test projects)
  T028 + T034 (parallel — different test projects)
  T041 + T043 (parallel — different concerns)
  T066 + T067 + T068 (parallel — different test files)

Batch 4 (convergence — sequential within file):
  T047 → T048 → T049 → T050 → T051 → T052 → T053 → T054

Batch 5 (final verification):
  T070 → T071 → T072 → T073 → T074 → T075
```

## Implementation Strategy

**MVP Scope**: Phase 3 (FR-R001) + Phase 4 (FR-R002) + Phase 5 (FR-R003) — these are the three security/functionality blockers. After these, the system is usable.

**Incremental Delivery**:
1. **Security fixes first** (FR-R001, FR-R002): Close the two live security holes
2. **Delivery lifecycle completion** (FR-R003, FR-R004, FR-R005): Make the agent workflow end-to-end operational
3. **Infrastructure hardening** (FR-R006, FR-R007): Secure the identity host and CI
4. **Convergence** (FR-R008): Clean up ownership patterns and strengthen tests

**Commit cadence**: One commit per phase, Conventional Commits format. No commit with a failing gate.
