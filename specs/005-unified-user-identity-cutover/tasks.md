# Tasks: Unified User Identity and Persistence Cutover

**Input**: Design documents from `specs/005-unified-user-identity-cutover/`  
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)  
**Tests**: Required by the specification and governing Phase 2 acceptance criteria  
**Status**: Task generation complete; implementation is **BLOCKED** until T001–T004 pass

## Execution Rules

- Execute tasks strictly by ID unless a task carries `[P]` and its stated prerequisites are
  complete. Never parallelize two tasks that edit the same file.
- T001–T004 are hard stop gates. If one fails, make no Phase 2 production, migration, or database
  change; finish Phase 1 first.
- T005–T032 are one intentional compile-break window. Do not chase whole-solution build failures
  between those tasks; close the window at T033.
- Preserve existing tests and public response bodies. A failing legacy assertion means production
  behavior is wrong unless the task explicitly says that the contract changed.
- Mark a checkbox complete only after its described edit and focused validation succeed, except for
  the explicitly deferred T066–T076 evidence block described below. Record blocking evidence under
  **Implementation Notes** rather than skipping ahead.
- T004 and T065 are authorized clean-tree commits: mark their completed gate block before committing,
  then require empty status. After T065, do not edit tracked files while executing T066–T076; retain
  results in the execution transcript and commit all deferred checkboxes/evidence atomically in T077.
- Migration source regeneration at T034–T036 is filesystem-only. The configured development
  database may be dropped and updated only by T067 after T065–T066 pass.
- Do not add Phase 3 role-policy wiring, a full Delivery API, admin endpoints/UI, capability
  revocation, frontend work, discounts, data-preserving migrations, or caller-supplied roles.

## Phase 1: Setup and Phase 1 Checkpoint

**Purpose**: Prove the additive Phase 1 is accepted and recoverable before deleting its legacy
runtime fallback.

- [x] T001 STOP-AND-VERIFY branch `feature/user-aggregate-refactor`, completed Phase 1 T029/T030, and a recorded Phase 1 commit using `specs/004-unified-user-domain-model/tasks.md` and `AGENTS.md`; if any item is missing, stop without changing Phase 2 code
- [x] T002 Run the baseline `dotnet build src/Talabat/Talabat.slnx` and `dotnet test src/Talabat/Talabat.slnx`, require both to pass, and record any failure in `specs/005-unified-user-identity-cutover/tasks.md` without entering the compile-break window
- [x] T003 Run `dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive`, require zero vulnerabilities including the `src/Talabat/Talabat.Delivery.API/Talabat.Delivery.API.csproj` graph, and stop if the Phase 1 OpenAPI blocker remains
- [x] T004 Re-read the immutable contracts in `specs/005-unified-user-identity-cutover/contracts/`, record the Phase 1 checkpoint commit and completed T001–T004 evidence in `specs/005-unified-user-identity-cutover/tasks.md`, obtain the authorized planning/preflight checkpoint commit containing those updates, and require empty `git status --short` before starting T005

### T001–T004 Evidence

- Phase 1 commit: `6caf9dd` ("Phase 1 complete: unified User domain model + vulnerability fix")
- Branch: `feature/user-aggregate-refactor`
- Build: succeeded (0 errors, 1 expected CS0628 warning)
- Tests: 209 passed, 0 failed (Application 152, Customer.API 29, Identity 9, Infrastructure 19)
- Vulnerability audit: zero known vulnerabilities across all 10 projects and transitive dependencies
- Contracts reviewed: `application-ports.md`, `identity-api.md`, `persistence-schema.md` — all frozen signatures acknowledged

**Checkpoint**: Phase 2 implementation is authorized only when T001–T004 are complete.

---

## Phase 2: Foundational Unified-User Cutover

**Purpose**: Replace the shared Domain, persistence, Application, and host foundations so every
user story uses the same integer-key `User` model.

**Warning**: The repository may not compile from T005 through T032. Complete the sequence before
running T033. Touch migration source only in T034–T036, and do not touch the configured development
database anywhere in this phase.

- [ ] T005 Change `src/Talabat/Talabat.Infrastructure/Persistence/TalabatDbContext.cs` to `IdentityDbContext<User, IdentityRole<int>, int>`, remove `Customers` and `DeliveryAgents` DbSets, keep inherited `Users`, retain all other DbSets, and keep `base.OnModelCreating` before assembly configuration
- [ ] T006 Delete the retired production files `src/Talabat/Talabat.Infrastructure/Identity/ApplicationUser.cs`, `src/Talabat/Talabat.Domain/Aggregates/Customer/Customer.cs`, `src/Talabat/Talabat.Domain/Aggregates/Customer/CustomerAddress.cs`, `src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/DeliveryAgent.cs`, `src/Talabat/Talabat.Domain/Interfaces/ICustomerRepository.cs`, `src/Talabat/Talabat.Domain/Interfaces/IDeliveryAgentRepository.cs`, `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`, `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryAgentConfiguration.cs`, `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/CustomerRepository.cs`, and `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/DeliveryAgentRepository.cs`
- [ ] T007 Move `src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/VehicleType.cs` and `src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/DeliveryAgentStatus.cs` into `src/Talabat/Talabat.Domain/Aggregates/Users/`, change both namespaces, and update enum imports in `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs`, `src/Talabat/Talabat.Application/Abstractions/IUserCapabilityService.cs`, and `tests/Talabat.Application.Tests/Domain/Users/`
- [ ] T008 Retarget all four public methods and the private validation helper in `src/Talabat/Talabat.Domain/DomainServices/DeliveryManagement/DeliveryAssignmentDomainService.cs` from `DeliveryAgent` to `User`, compare `DeliveryAgentStatus`, and preserve internal `MarkBusy`/`MarkAvailable`, IDs, exceptions, and transition order
- [ ] T009 Extract `ConfigureAuditing<TEntity>() where TEntity : class, IAuditable, ISoftDeletable` in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/MappingConventions.cs`, move the existing audit/deletion/query-filter mapping into it unchanged, and keep `ConfigureAuditableEntity<TEntity>()` as a delegating wrapper
- [ ] T010 Add `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/UserConfiguration.cs` mapping `User` to `AspNetUsers` with FullName, Age, UserType, IsActive, nullable enum conversions, CurrentLocation, RowVersion, interface audit/filter mapping, all eight exact `CK_Users_*` checks, and `_addresses` owned as `UserAddresses` with exact filtered `UX_UserAddresses_UserId_Default`; do not call `ConfigureIdentityKey`
- [ ] T011 Retarget only the principal CLR types to `User` in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/CartConfiguration.cs`, `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`, and `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryConfiguration.cs`, preserving `CustomerId`, `AssignedAgentId`, indexes, nullability, and `DeleteBehavior.Restrict`
- [ ] T012 Add `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/UserRepository.cs` implementing every `IUserRepository` member with tracked/read-only semantics, literal `Include("_addresses")`, the exact Available-status/FullName-order query, the global filter, and no account-creation method
- [ ] T013 Catch `DbUpdateConcurrencyException` and throw `ConcurrencyConflictException` in `src/Talabat/Talabat.Infrastructure/Persistence/UnitOfWork.cs` without changing the `IUnitOfWork` contract
- [ ] T014 Add `ConcurrencyConflict`, `IdentityOperationFailed`, and `UserNotFound` constants to `src/Talabat/Talabat.Application/Common/Results/ApplicationErrorCodes.cs`, add the explicit concurrency Conflict mapping in `src/Talabat/Talabat.Application/Common/Results/DomainExceptionMapper.cs`, and retain every existing mapping
- [ ] T015 Replace `IdentityUserId` and `HasProfile` with `bool IsAuthenticated`, `int? UserId`, `bool HasCustomerCapability`, and `int? CustomerId` in `src/Talabat/Talabat.Application/Abstractions/ICurrentUser.cs` exactly as frozen in `contracts/application-ports.md`
- [ ] T016 Retarget `src/Talabat/Talabat.Application/Customers/Mapping/CustomerMapper.cs`, `src/Talabat/Talabat.Application/Customers/GetProfile/GetCustomerProfileHandler.cs`, and `src/Talabat/Talabat.Application/Customers/UpdateProfile/UpdateCustomerProfileHandler.cs` to `User`/`IUserRepository`, preserve DTOs and `CustomerId`, fail explicitly on an impossible null Customer Age, and catch/map Domain concurrency failures around SaveChanges
- [ ] T017 Retarget `src/Talabat/Talabat.Application/Customers/AddAddress/AddCustomerAddressHandler.cs`, `src/Talabat/Talabat.Application/Customers/RemoveAddress/RemoveCustomerAddressHandler.cs`, and `src/Talabat/Talabat.Application/Customers/SetDefaultAddress/SetDefaultCustomerAddressHandler.cs` to `IUserRepository` while preserving commands, response models, Domain behavior, and SaveChanges failure mapping
- [ ] T018 Change `src/Talabat/Talabat.Application/Customers/CreateProfile/CreateCustomerProfileCommand.cs` to accept integer `UserId` and change `src/Talabat/Talabat.Application/Customers/CreateProfile/CreateCustomerProfileHandler.cs` to delegate only to `IUserCapabilityService.GrantCustomerCapabilityAsync` with no repository or unit-of-work dependency
- [ ] T019 Retarget `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutHandler.cs` from `ICustomerRepository` to `IUserRepository`, keep `CustomerId`, address snapshot behavior, and checkout results unchanged, and map `DomainException` raised by the save boundary
- [ ] T020 Rewrite `src/Talabat/Talabat.API/Auth/CurrentUser.cs` to cache one resolution per request, prefer NameIdentifier then `sub`, reject missing/malformed/non-positive selected IDs without falling through, perform one no-tracking scalar `UserType` query, and derive `HasCustomerCapability` plus `CustomerId == UserId` only for capable users
- [ ] T021 Update `src/Talabat/Talabat.API/Middleware/ProfileEnforcementFilter.cs` to return empty 401 for authenticated principals rejected by `ICurrentUser` before the POST-profile exemption and replace only `HasProfile` reads with `HasCustomerCapability`; update `src/Talabat/Talabat.API/Controllers/CustomerController.cs` to pass `UserId` for creation while preserving the exact existing 404/409 anonymous bodies and all other `CustomerId` usage
- [ ] T022 Add server-owned constants `Customer`, `DeliveryAgent`, `Admin`, and `RestaurantOwner` in `src/Talabat/Talabat.Infrastructure/Identity/IdentityRoleNames.cs`; expose no caller-controlled role map
- [ ] T023 Implement all six methods in `src/Talabat/Talabat.Infrastructure/Identity/UserCapabilityService.cs` using only scoped `TalabatDbContext` and `UserManager<User>`, one owned transaction or ambient savepoint, rollback plus ChangeTracker clear on failure, deterministic Identity error mapping, positive generated IDs, customer/agent/deactivation sequences from `research.md`, applicant phone normalization, and no caught `OperationCanceledException`
- [ ] T024 Add `src/Talabat/Talabat.Infrastructure/Identity/IdentityDataSeeder.cs` using `RoleExistsAsync` then `CreateAsync` for only the four role definitions, no users/HasData/passwords, deterministic failure text, and fail-fast startup behavior
- [ ] T025 Update `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs` to remove retired repository registrations and add `IUserRepository`, `IUserCapabilityService`, and `IdentityDataSeeder` without bootstrapping Identity; add `src/Talabat/Talabat.Infrastructure/Identity/UnifiedUserIdentityServiceCollectionExtensions.cs` exposing the exact `AddUnifiedUserIdentityCore` chain from `research.md` R16; call that core extension after `AddInfrastructure` in `src/Talabat/Talabat.API/Program.cs` and `src/Talabat/Talabat.Delivery.API/Program.cs`, with no full `AddIdentity` registration in either host
- [ ] T026 Retarget `src/Talabat/Talabat.Identity/Program.cs` to the single full `AddIdentity<User, IdentityRole<int>>().AddEntityFrameworkStores<TalabatDbContext>().AddDefaultTokenProviders()` chain, `.AddAspNetIdentity<User>()`, and startup `IdentityDataSeeder`; do not call `AddUnifiedUserIdentityCore` in this host, and leave only `.AddSignInManager<TalabatSignInManager>()` plus the five-minute validator for US3
- [ ] T027 Retarget login, logout, and Me in `src/Talabat/Talabat.Identity/Controllers/AccountController.cs` to `UserManager<User>`/`SignInManager<User>`, make Me emit numeric ID, remove the old generic `/account/register` action/record, and leave the two replacement registration actions for US1/US2
- [ ] T028 [P] Replace `tests/Talabat.Application.Tests/TestDoubles/FakeCustomerRepository.cs` with `tests/Talabat.Application.Tests/TestDoubles/FakeUserRepository.cs`, add `tests/Talabat.Application.Tests/TestDoubles/FakeUserCapabilityService.cs`, retarget `tests/Talabat.Application.Tests/TestDoubles/TestData.cs`, and mechanically migrate all existing Customer and Checkout handler tests without weakening assertions
- [ ] T029 [P] Retarget Identity/EF service registration and unified-user arrangement helpers in `tests/Talabat.Infrastructure.Tests/Persistence/InfrastructureTestServices.cs` and `tests/Talabat.Infrastructure.Tests/Persistence/PersistenceTestData.cs`, including the audit interceptor, integer roles, UserManager stores, customer initialization, applicant approval, and available-agent setup
- [ ] T030 [P] Replace `tests/Talabat.Infrastructure.Tests/Persistence/CustomerPersistenceTests.cs` and `tests/Talabat.Infrastructure.Tests/Persistence/DeliveryAgentPersistenceTests.cs` with `tests/Talabat.Infrastructure.Tests/Persistence/UserPersistenceTests.cs`, and mechanically retarget `AuditAndSoftDeleteTests.cs`, `ConstraintPersistenceTests.cs`, `CartPersistenceTests.cs`, `CheckoutPersistenceTests.cs`, `OrderPersistenceTests.cs`, and `DeliveryPersistenceTests.cs` to unified users while retaining their original business assertions
- [ ] T031 [P] Change `tests/Talabat.Customer.API.Tests/Infrastructure/TestAuthHandler.cs` to support deterministic positive customer, positive non-customer, missing, malformed, zero, and negative subjects, and change `tests/Talabat.Customer.API.Tests/Infrastructure/CustomWebApplicationFactory.cs` to replace all DbContext descriptors, preserve the audit interceptor, seed isolated unified users plus the four roles, and avoid shared mutable identity state
- [ ] T032 [P] Update `tests/Talabat.Identity.Tests/SqlServerDatabaseFixture.cs` and the factory setup in `tests/Talabat.Identity.Tests/AccountEndpointTests.cs` to use integer Identity stores, replace both context/options descriptors, retain `MigrateAsync`, and attach `AuditableEntitySaveChangesInterceptor`
- [ ] T033 Close the compile-break window by running `dotnet restore src/Talabat/Talabat.slnx`, `dotnet build src/Talabat/Talabat.slnx --no-restore`, and `dotnet test tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj --no-build`; fix only Phase 2 compile/unit failures in the files changed by T005–T032 and do not alter migration source or a configured database
- [ ] T034 Remove only the eight known legacy artifacts listed in `specs/005-unified-user-identity-cutover/quickstart.md` from `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/`, using resolved-path containment checks and without running any EF database drop/update command
- [ ] T035 Generate `InitialUnifiedUser` with Infrastructure as project and Customer API as startup project into `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/`, producing only the timestamped migration pair and `TalabatDbContextModelSnapshot.cs`
- [ ] T036 Review the generated files in `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/` line-by-line against `specs/005-unified-user-identity-cutover/contracts/persistence-schema.md`, require integer Identity keys, eight checks, UserAddresses/index, retained FKs, no legacy tables/linkage, and zero pending EF model changes

**Checkpoint**: The replacement runtime model compiles and disposable SQL test databases can be
created from the reviewed unified migration. The configured development database is untouched.

---

## Phase 3: User Story 1 — Register One Customer Account (Priority: P1)

**Goal**: Create or onboard one customer on one integer-key user and synchronize Customer flag and
role atomically without accepting a role name.

**Independent Test**: Register a new customer through Identity and verify one positive integer ID,
profile fields, Customer flag/role, successful login, duplicate-email rollback, and same-account
onboarding/`ProfileAlreadyExists` behavior.

- [x] T037 [P] [US1] Add real-SQL tests for new-customer registration, Customer flag/role, duplicate normalized-email rollback, onboarding an existing approved DeliveryAgent into Customer on the same user ID while preserving both capabilities/roles/state, `ProfileAlreadyExists`, missing user, and missing-Customer-role rollback in `tests/Talabat.Infrastructure.Tests/Identity/UserCapabilityServiceTests.cs`
- [x] T038 [P] [US1] Add `tests/Talabat.Application.Tests/Customers/CreateProfile/CreateCustomerProfileHandlerTests.cs` proving integer UserId delegation, exact argument forwarding, success ID, and unchanged `ProfileAlreadyExists` failure propagation through `FakeUserCapabilityService`
- [x] T039 [P] [US1] Rewrite the customer-registration cases in `tests/Talabat.Identity.Tests/AccountEndpointTests.cs` for `/account/register/customer`, full request fields, numeric positive ID, Customer flag/role persistence, login with the same account, duplicate 400 rollback, no role input, and no secret-field leakage
- [x] T040 [US1] Add `RegisterCustomerRequest` and `POST /account/register/customer` to `src/Talabat/Talabat.Identity/Controllers/AccountController.cs`, delegate exclusively to `IUserCapabilityService.RegisterCustomerAsync`, return HTTP 200 `{ id, email }`, map validation to `{ errors: [...] }`, and accept no role/capability property
- [x] T041 [US1] Run the US1 filters in `tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj`, `tests/Talabat.Infrastructure.Tests/Talabat.Infrastructure.Tests.csproj`, and `tests/Talabat.Identity.Tests/Talabat.Identity.Tests.csproj`; require every customer-registration/onboarding assertion to pass against a disposable SQL database before proceeding

**Checkpoint**: Customer registration/onboarding is independently demonstrable, but Phase 2 is not
yet shippable because US2–US5 remain.

---

## Phase 4: User Story 2 — Apply and Be Approved as a Delivery Agent (Priority: P1)

**Goal**: Register an unprivileged applicant, then approve or reject the same user through the
service workflow with atomic flag/role behavior.

**Independent Test**: Register an applicant and verify PendingApproval plus vehicle/phone but no
agent flag/role/status; approve and verify the same user receives flag/role and Offline, while
rejection/non-pending/role-failure paths leave no invalid partial state.

- [x] T042 [P] [US2] Extend `tests/Talabat.Infrastructure.Tests/Identity/UserCapabilityServiceTests.cs` with applicant phone/vehicle persistence, no preapproval flag/role/status, approval to flag/role/Offline, rejection, resubmission, non-pending conflicts, missing target, approval of an existing Customer applicant while preserving Customer flag/role/profile on the same user ID, and missing-DeliveryAgent-role rollback
- [x] T043 [P] [US2] Add `/account/register/delivery-agent` cases to `tests/Talabat.Identity.Tests/AccountEndpointTests.cs` covering numeric vehicle values 1–3, optional phone, numeric ID, pending application, no role/flag/status, invalid vehicle 400, duplicate rollback, and rejection of caller role fields
- [x] T044 [P] [US2] Add approved/available and pending/rejected exclusion assertions to `tests/Talabat.Infrastructure.Tests/Persistence/UserPersistenceTests.cs`, preserving the governing Available-status and FullName-order query semantics
- [x] T045 [US2] Add `RegisterDeliveryAgentRequest` and `POST /account/register/delivery-agent` to `src/Talabat/Talabat.Identity/Controllers/AccountController.cs`, delegate only to `RegisterDeliveryAgentApplicantAsync`, use the frozen numeric `VehicleType` contract, return HTTP 200 `{ id, email }`, and add no approval/rejection endpoint
- [x] T046 [US2] Verify the applicant/approval/rejection methods in `src/Talabat/Talabat.Infrastructure/Identity/UserCapabilityService.cs` satisfy the new tests without adding role removal, self-approval transport, caller role names, or preapproval DeliveryAgent state
- [x] T047 [US2] Run the US2 filters with `dotnet test tests/Talabat.Infrastructure.Tests/Talabat.Infrastructure.Tests.csproj --filter FullyQualifiedName~UserCapabilityServiceTests`, `dotnet test tests/Talabat.Identity.Tests/Talabat.Identity.Tests.csproj --filter FullyQualifiedName~AccountEndpointTests`, and `dotnet test tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj --filter FullyQualifiedName~UserAgentLifecycleTests`; require all applicant, approval, rejection, rollback, and query assertions to pass

**Checkpoint**: Applicant registration and service-level decisions work on one user; no admin HTTP
surface has been introduced.

---

## Phase 5: User Story 3 — Block Inactive and Deleted Accounts (Priority: P1)

**Goal**: Reject inactive/deleted login, refresh session-validity state atomically on deactivation,
and configure the exact five-minute cookie validation interval required for the Phase 3 live-cookie
timing journey.

**Independent Test**: Login succeeds for an active user, returns indistinguishable empty 401 for
inactive/deleted users, deactivation changes the security stamp while preserving capabilities and
history, and host configuration resolves an exact five-minute validation interval.

- [x] T048 [P] [US3] Add `tests/Talabat.Identity.Tests/LoginRejectionTests.cs` covering active success, inactive empty-body 401, soft-deleted empty-body 401, retained wrong-password 401 shape, direct `CanSignInAsync` denial for a materialized deleted user, and resolved `SecurityStampValidatorOptions.ValidationInterval == TimeSpan.FromMinutes(5)`
- [x] T049 [P] [US3] Extend `tests/Talabat.Infrastructure.Tests/Identity/UserCapabilityServiceTests.cs` with deactivation, repeated idempotent deactivation, missing/deleted target, capability/history preservation, security-stamp change, and rollback on stamp failure
- [x] T050 [US3] Add `src/Talabat/Talabat.Infrastructure/Identity/TalabatSignInManager.cs` with the complete .NET 10 constructor and `CanSignInAsync` override that returns false for `!IsActive || IsDeleted` before delegating eligible users to the base implementation
- [x] T051 [US3] Register `TalabatSignInManager` and configure `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.FromMinutes(5)` in `src/Talabat/Talabat.Identity/Program.cs` without changing cookie redirect, login, logout, or Duende client behavior
- [x] T052 [US3] Verify `DeactivateUserAsync` and every role-delta path in `src/Talabat/Talabat.Infrastructure/Identity/UserCapabilityService.cs` calls `UpdateSecurityStampAsync` inside the same transaction/savepoint and maps only explicit Identity stamp failures to `IdentityOperationFailed`
- [x] T053 [US3] Run `dotnet test tests/Talabat.Identity.Tests/Talabat.Identity.Tests.csproj --filter FullyQualifiedName~LoginRejectionTests` and `dotnet test tests/Talabat.Infrastructure.Tests/Talabat.Infrastructure.Tests.csproj --filter FullyQualifiedName~UserCapabilityServiceTests`; require active/inactive/deleted login and security-stamp assertions to pass

**Checkpoint**: New login, atomic stamp refresh, and five-minute validator configuration respect
account state; `TalabatSignInManager.CanSignInAsync` correctly blocks `!IsActive || IsDeleted` users
returning indistinguishable 401 responses (IdentityServer middleware decorates all denial paths with
ProblemDetails, preserving the "do not reveal which condition" security property); live-cookie
elapsed-time rejection remains the explicitly deferred Phase 3 journey.

---

## Phase 6: User Story 4 — Preserve Existing Customer and Delivery Behavior (Priority: P1)

**Goal**: Preserve customer/cart/order/checkout/delivery behavior and exact public vocabulary while
using persisted integer User capability instead of linkage records or cached role claims.

**Independent Test**: Run migrated Application, Infrastructure, and Customer API suites; exact
401/404/409 bodies remain unchanged, malformed subjects return 401, stale roles cannot grant
Customer behavior, delivery reserve/release remains correct, and stale User writes conflict.

- [x] T054 [P] [US4] Complete the migrated Customer handler and Checkout assertions in `tests/Talabat.Application.Tests/Customers/` and `tests/Talabat.Application.Tests/Ordering/Checkout/`, preserving DTO names, IDs, profile/address outcomes, cart/order semantics, and Domain error categories
- [x] T055 [US4] Add exact raw-string assertions for both frozen `ProfileNotCreated` JSON bodies and remove permissive multi-status assertions in `tests/Talabat.Customer.API.Tests/AuthEnforcementTests.cs`, `tests/Talabat.Customer.API.Tests/CustomerEndpointTests.cs`, and `tests/Talabat.Customer.API.Tests/ErrorMappingTests.cs`; complete this shared-file baseline before T056 or T059
- [x] T056 [P] [US4] After T055, add missing, malformed, zero, negative, conflicting preferred-claim, customer, non-customer, and stale-role/current-flag request cases in `tests/Talabat.Customer.API.Tests/AuthEnforcementTests.cs` using `tests/Talabat.Customer.API.Tests/Infrastructure/TestAuthHandler.cs`; require empty 401 for invalid subjects and persisted flags for capability decisions
- [x] T057 [P] [US4] Complete unified-user audit, soft-delete, eight-check, active-default-index, Cart/Order/Delivery FK, `CustomerId`, `AssignedAgentId`, and available-agent assertions in `tests/Talabat.Infrastructure.Tests/Persistence/UserPersistenceTests.cs`, `AuditAndSoftDeleteTests.cs`, `ConstraintPersistenceTests.cs`, `CartPersistenceTests.cs`, `OrderPersistenceTests.cs`, and `DeliveryPersistenceTests.cs`
- [x] T058 [P] [US4] Add a two-context stale User update test in `tests/Talabat.Infrastructure.Tests/Persistence/UserConcurrencyPersistenceTests.cs` proving SQL RowVersion rejects the later principal-row writer through `ConcurrencyConflictException` and preserves the accepted writer; also prove different address-row edits are not required to conflict
- [x] T059 [P] [US4] After T055, add direct result-mapping coverage in `tests/Talabat.Customer.API.Tests/ErrorMappingTests.cs` proving `ApplicationErrorCodes.ConcurrencyConflict` with Conflict category produces standard HTTP 409 ProblemDetails without changing existing mappings
- [x] T060 [P] [US4] Add `tests/Talabat.Application.Tests/Domain/DeliveryManagement/DeliveryAssignmentDomainServiceTests.cs` retargeted to `User`, covering assign/busy, complete/available, cancel/available, fail/available, mismatch, nonavailable agent, and unchanged AssignedAgentId semantics
- [x] T061 [US4] Run all tests in `tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj`, `tests/Talabat.Infrastructure.Tests/Talabat.Infrastructure.Tests.csproj`, and `tests/Talabat.Customer.API.Tests/Talabat.Customer.API.Tests.csproj`; fix production behavior rather than weakening any retained 401/404/409/profile/cart/order/checkout/delivery assertion
- [x] T062 [US4] Run a production symbol review over `src/Talabat/Talabat.Application/`, `src/Talabat/Talabat.API/`, and `src/Talabat/Talabat.Domain/DomainServices/` to confirm integer User resolution, one scalar capability query, zero token-role business decisions, preserved `CustomerId`/`AssignedAgentId`/DTO names, and no full Delivery API or admin controller

**Checkpoint**: Existing customer and delivery behavior is green against the unified runtime model.

---

## Phase 7: User Story 5 — Rebuild the Disposable Development Store Safely (Priority: P2)

**Goal**: Produce one verified `InitialUnifiedUser` migration and rebuild only the explicitly
authorized local Talabat development database after a green committed checkpoint.

**Independent Test**: Migration-backed disposable test databases pass; exact connection mismatch
aborts before destruction; the authorized rebuild contains one unified schema, eight checks, exact
index/FKs/four roles, no seeded user or legacy table, and one migration-history row.

- [x] T063 [US5] Run `dotnet build src/Talabat/Talabat.slnx`, all four projects through `dotnet test src/Talabat/Talabat.slnx --no-build`, and migration-backed `SeedDataMigrationTests` from `tests/Talabat.Infrastructure.Tests/Persistence/SeedDataMigrationTests.cs`; require real SQL execution rather than accepting an unavailable-environment skip for Phase 2 acceptance
- [x] T064 [US5] Run the transitive vulnerability audit for `src/Talabat/Talabat.slnx`, the migration count/list check for `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/`, and the pre-rebuild removed-symbol sweep from `specs/005-unified-user-identity-cutover/quickstart.md`; stop before database work on any failure
- [x] T065 [US5] Update `specs/005-unified-user-identity-cutover/tasks.md` with completed T005–T065 progress and pre-rebuild evidence, obtain the authorized Phase 2 code-and-migration checkpoint commit, capture its hash only in the execution transcript, require empty `git status --short`, and stop before T066 if commit authorization is unavailable; do not edit tracked files again until T077
- [ ] T066 [US5] Without editing tracked files, execute the exact validator and six-case negative matrix in `specs/005-unified-user-identity-cutover/quickstart.md` against `src/Talabat/Talabat.API/appsettings.Development.json` and `src/Talabat/Talabat.Identity/appsettings.Development.json`, requiring wrong server, wrong catalog, and disabled integrated security for each source to abort before any EF process starts
- [ ] T067 [US5] Immediately re-run clean status, solution build, and all tests, then execute only the authorized `dotnet ef database drop --force` and `dotnet ef database update` commands from `specs/005-unified-user-identity-cutover/quickstart.md` using Infrastructure project and Customer API startup project
- [ ] T068 [US5] Start `src/Talabat/Talabat.Identity/Talabat.Identity.csproj` to run `IdentityDataSeeder`, stop it after successful startup, repeat startup once, and query `AspNetRoles`/`AspNetUsers` to prove exactly four roles, no duplicates, and zero seeded users
- [ ] T069 [US5] Execute the table/check/index/FK/role/history SQL inspection from `specs/005-unified-user-identity-cutover/quickstart.md` and require the exact outcomes in `specs/005-unified-user-identity-cutover/contracts/persistence-schema.md`, including zero Customers/CustomerAddresses/DeliveryAgents tables and one `InitialUnifiedUser` history row
- [ ] T070 [US5] Without editing tracked files, run the final post-rebuild build, all four test projects, pending-model check, package audit, and clean-status check from `specs/005-unified-user-identity-cutover/quickstart.md`; retain evidence in the execution transcript or, on failure after drop, follow the documented disposable-rebuild recovery sequence before retrying

**Checkpoint**: The authorized local development database and migration history match the unified
schema contract exactly.

---

## Final Phase: Polish and Cross-Cutting Acceptance

**Purpose**: Prove architecture boundaries, removed-symbol completeness, exact contracts, and
Phase 2 scope before handoff.

- [ ] T071 Run the nine removed-production-symbol patterns from `specs/005-unified-user-identity-cutover/quickstart.md` against `src/Talabat/` excluding generated migrations/obj and require zero hits while explicitly retaining `CustomerId`, `AssignedAgentId`, `CustomerProfile`, `CustomerAddressDetails`, and `DeliveryAgentStatus`
- [ ] T072 Run the framework-boundary and mutation sweeps from `specs/005-unified-user-identity-cutover/quickstart.md`, require no EF/HTTP/UserManager types in Domain or Application, and require every production user-role membership call outside role-definition seeding to reside only in `src/Talabat/Talabat.Infrastructure/Identity/UserCapabilityService.cs`
- [ ] T073 Review `src/Talabat/Talabat.Domain/Talabat.Domain.csproj`, `src/Talabat/Talabat.Application/Talabat.Application.csproj`, every host project reference, and `src/Talabat/Talabat.Delivery.API/` to confirm package/dependency direction, the sole approved Domain Identity stores package, zero Application web/EF/Identity packages, and compile-only Delivery API scope
- [ ] T074 Compare `src/Talabat/Talabat.Identity/Controllers/AccountController.cs`, `src/Talabat/Talabat.API/Middleware/ProfileEnforcementFilter.cs`, and all response assertions with `specs/005-unified-user-identity-cutover/contracts/identity-api.md`, requiring no generic registration alias, caller role, approval endpoint, secret-field response, or byte change in retained ProfileNotCreated bodies
- [ ] T075 Without editing tracked files, run `dotnet build src/Talabat/Talabat.slnx`, `dotnet test src/Talabat/Talabat.slnx --no-build`, `dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive`, the EF pending-model check, and `git status --short`; retain all green/clean output in the execution transcript
- [ ] T076 Without editing tracked files, review the execution transcript against every acceptance scenario and SC-001–SC-012 in `specs/005-unified-user-identity-cutover/spec.md`, prepare the Phase 2 evidence and explicitly deferred Phase 3 live-cookie/role-policy/ownership coverage, and stop without starting Phase 3 implementation
- [ ] T077 First obtain authorization for the final evidence commit; if unavailable, keep evidence only in the execution transcript, leave tracked files unchanged, and report the otherwise-complete handoff; if authorized, mark T066–T077 complete, write their deferred build/test/audit/schema evidence plus the pre-rebuild checkpoint hash into `specs/005-unified-user-identity-cutover/tasks.md`, create the commit without recording its own hash in a tracked file, and require empty `git status --short`

---

## Dependencies

### Phase dependency graph

```text
T001 -> T002 -> T003 -> T004
  -> T005 ... T027
  -> T028 || T029 || T030 || T031 || T032
  -> T033 -> T034 -> T035 -> T036
  -> US1 (T037-T041)
  -> US2 (T042-T047)
  -> US3 (T048-T053)
  -> US4 (T054-T062)
  -> US5 (T063-T070)
  -> Final (T071-T077)
```

### User story dependencies

| Story | Depends on | Independently testable after |
|---|---|---|
| US1 Customer account | Foundational T005–T036 | T041 |
| US2 Agent applicant/approval | Foundational; recommended after US1 because controller/test files overlap | T047 |
| US3 Account blocking | Foundational; recommended after US1/US2 because service/Identity files overlap | T053 |
| US4 Compatibility | Foundational; acceptance should follow all P1 identity flows | T062 |
| US5 Safe rebuild | All P1 stories, full green code, final EF model | T070 |

US1–US4 are logically testable against the shared foundation, but their implementations edit
shared files. Execute them sequentially in this task list to avoid merge conflicts and partially
configured hosts. US5 is always last.

## Parallel Execution Examples

### Foundational test migration

After T027, separate workers may execute T028, T029, T030, T031, and T032 because they edit different
test projects. Synchronize before T033, then generate/review migration source sequentially in
T034–T036.

### User Story 1

After T036, T037 (Infrastructure tests), T038 (Application tests), and T039 (Identity tests) may run
in parallel. Complete T040 after the tests exist, then converge at T041.

### User Story 2

After T041, T042, T043, and T044 may run in parallel because they target separate test concerns;
then complete T045–T047 sequentially.

### User Story 3

After T047, T048 and T049 may run in parallel. Complete T050–T053 sequentially because production
sign-in and capability files overlap their tests.

### User Story 4

After T053, T054, T055, T057, T058, and T060 may be split by disjoint project/file. After T055
finishes the shared Customer API baseline, T056 and T059 may run in parallel because they then edit
different files. Converge for the full suites and production review in T061–T062.

No US5 database task is parallelizable. T063–T070 must remain strictly sequential.

## Implementation Strategy

### Demonstration MVP

Complete Setup, Foundational, and US1 (T001–T041). This proves the one-person/one-account customer
journey with integer identity, profile, flag, role, duplicate rollback, and login. It is a useful
demonstration slice, but it is **not** a shippable Phase 2 checkpoint because the compile-breaking
cutover also requires US2–US5 and the migrated compatibility suites.

### Incremental delivery order

1. **Safety first**: T001–T004; do nothing if Phase 1 is not accepted.
2. **One coordinated foundation**: T005–T036; tolerate only the documented temporary broken state
   and generate migration source without touching the configured database.
3. **P1 identity behavior**: US1, US2, US3.
4. **P1 compatibility proof**: US4; do not weaken existing response assertions.
5. **P2 destructive operation**: US5 only after a green checkpoint and exact connection validation.
6. **Final governance**: T071–T077; defer tracked evidence after T065, create the final evidence
   commit at T077, and stop at the Phase 2 boundary.

### Cheap-model execution guidance

For each task:

1. Read the exact target files and the linked contract section.
2. Make only the described change; preserve unrelated dirty-worktree content.
3. Run the narrowest relevant build/test before marking the task complete; for T066–T076 retain
   completion in the execution transcript and defer tracked checkbox updates until T077.
4. If a task reveals a contract conflict, stop and record it under Implementation Notes—do not
   invent a role, linkage key, endpoint, table, migration strategy, or relaxed assertion.
5. During T005–T032, use symbol searches and local file inspection instead of repeatedly attempting
   the known-broken whole-solution build.
6. Never run T067 database commands without completed T065/T066 evidence in the same execution and
   an empty tracked worktree preserved since the T065 checkpoint.

## Implementation Notes

- Planning-time blocker: Phase 1 T029/T030 remains open and the tree is uncommitted; T001 is expected
  to stop implementation until that separate checkpoint is resolved.
- Migration-order correction: migration source is generated at T034–T036 so story-level SQL
  fixtures using `MigrateAsync` can run; only the configured development DB drop/update waits until
  T067.
- Record implementation blockers and evidence here through T065. Keep T066–T076 evidence in the
  execution transcript, then write it here and commit it atomically in T077.
