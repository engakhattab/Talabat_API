# Tasks: Unified User Domain Model

**Input**: Design documents from `specs/004-unified-user-domain-model/`  
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/domain-interfaces.md](contracts/domain-interfaces.md),
[quickstart.md](quickstart.md)  
**Tests**: Required by FR-026/FR-027 and the governing Phase 1 acceptance criteria  
**Scope**: Additive Phase 1 only; legacy Customer/DeliveryAgent runtime remains unchanged

## Phase 1: Setup

**Purpose**: Establish the mandatory recoverable checkpoint and green baseline before production
code changes.

- [ ] T001 Verify `feature/user-aggregate-refactor`, commit the complete planning/current working tree as the required checkpoint, require clean `git status --short`, then run baseline build and all tests for `src/Talabat/Talabat.slnx`; stop on any failure
- [ ] T002 Add `Microsoft.Extensions.Identity.Stores` 10.0.9 as the only direct package plus SDK `InternalsVisibleTo` for `Talabat.Application.Tests` in `src/Talabat/Talabat.Domain/Talabat.Domain.csproj`

---

## Phase 2: Foundational

**Purpose**: Introduce audit/soft-delete contracts, generalized audit discovery, shared capability
types, and Domain failure types that block the unified aggregate.

**Critical**: Complete this phase and its build gate before starting User implementation.

- [ ] T003 [P] Add the exact creation/modification audit contract to `src/Talabat/Talabat.Domain/Common/Abstractions/IAuditable.cs`
- [ ] T004 [P] Add the exact retained soft-deletion contract to `src/Talabat/Talabat.Domain/Common/Abstractions/ISoftDeletable.cs`
- [ ] T005 Make `AuditableEntity` implement both new interfaces without changing any existing property visibility or method body in `src/Talabat/Talabat.Domain/Common/Abstractions/AuditableEntity.cs`
- [ ] T006 Retarget only change-tracker discovery from `Entries<AuditableEntity>()` to `Entries<IAuditable>()` while preserving sync/async stamping behavior in `src/Talabat/Talabat.Infrastructure/Persistence/Auditing/AuditableEntitySaveChangesInterceptor.cs`
- [ ] T007 Run the intermediate build gate for `src/Talabat/Talabat.slnx` and resolve only compile errors caused by T002-T006 before continuing
- [ ] T008 [P] Add `[Flags] UserType` with `None=0`, `Customer=1`, `DeliveryAgent=2`, `Admin=4`, and `RestaurantOwner=8` in `src/Talabat/Talabat.Domain/Aggregates/Users/UserType.cs`
- [ ] T009 [P] Add `AgentApprovalStatus` with `PendingApproval=1`, `Approved=2`, and `Rejected=3` in `src/Talabat/Talabat.Domain/Aggregates/Users/AgentApprovalStatus.cs`
- [ ] T010 [P] Add `CustomerProfileNotInitializedException : DomainException` in existing exception style in `src/Talabat/Talabat.Domain/Exceptions/CustomerProfileNotInitializedException.cs`
- [ ] T011 [P] Add `DeliveryAgentNotInitializedException : DomainException` in existing exception style in `src/Talabat/Talabat.Domain/Exceptions/DeliveryAgentNotInitializedException.cs`
- [ ] T012 [P] Add `AgentApplicationNotPendingException : DomainException` for non-pending decisions and approved resubmission in `src/Talabat/Talabat.Domain/Exceptions/AgentApplicationNotPendingException.cs`
- [ ] T013 [P] Add the future-facing `ConcurrencyConflictException : DomainException` without persistence mapping or use in `src/Talabat/Talabat.Domain/Exceptions/ConcurrencyConflictException.cs`

**Checkpoint**: The solution builds with the generalized audit contracts; all capability enums and
failures exist; no User, persistence, role, or host wiring has started.

---

## Phase 3: User Story 1 - One Person, One Multi-Capability User (Priority: P1) 🎯 MVP

**Goal**: Deliver one behavior-rich User aggregate that can be registered, initialized as a
customer, apply for and operate as an approved delivery agent, hold both capabilities, own
addresses, preserve audit/soft-delete behavior, and remain completely disconnected from runtime
hosts and persistence.

**Independent Test**: Run
`dotnet test tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj --filter "FullyQualifiedName~Domain.Users"`.
The four new suites must prove registration/account state, customer/profile guards, every address
invariant, the application lifecycle, and every legal/illegal agent transition without a web host,
database, authorization service, or replacement of legacy aggregates.

### Tests for User Story 1

> Write these tests first. They may not compile/pass until T018-T023 complete.

- [ ] T014 [P] [US1] Add registration-default tests (inherited `UserName` and `Email` are assigned from the registration inputs; a null/blank full name is rejected by the guard; `IsActive` true; `UserType.None`), activation/deactivation preservation, ID/RowVersion defaults, and audit/soft-delete parity tests in `tests/Talabat.Application.Tests/Domain/Users/UserAccountStateTests.cs`
- [ ] T015 [P] [US1] Add customer initialization/update, direct repeat behavior, validation atomicity, capability guard, and flag-preservation tests in `tests/Talabat.Application.Tests/Domain/Users/UserCustomerCapabilityTests.cs`
- [ ] T016 [P] [US1] Add duplicate equality, default uniqueness/no-default behavior, removal/selection, positive-unknown versus non-positive IDs, snapshot, guard, and unchanged-on-failure tests in `tests/Talabat.Application.Tests/Domain/Users/UserAddressInvariantTests.cs`
- [ ] T017 [P] [US1] Add vehicle validation, pending refresh/reject/resubmit/approve flows, non-pending decisions, uninitialized guards, location validation, the complete public/internal status transition matrix, and the explicit SC-001 same-user dual-capability test (one instance initialized as Customer then submitted and approved as DeliveryAgent; assert both flags present and customer profile, addresses, and Offline agent state all preserved) in `tests/Talabat.Application.Tests/Domain/Users/UserAgentLifecycleTests.cs`

### Implementation for User Story 1

- [ ] T018 [US1] Port `CustomerAddress` behavior exactly into the internal-construction/read-only child entity `src/Talabat/Talabat.Domain/Aggregates/Users/UserAddress.cs` without deleting or modifying the legacy type
- [ ] T019 [US1] Create `User : IdentityUser<int>, IAuditable, ISoftDeletable` with private-set business properties, private address backing list, registration defaults (`Register` assigns the inherited `UserName`/`Email` and guards `FullName` via `Guard.RequiredText`), activation methods, empty RowVersion, and audit/soft-delete members copied verbatim from `AuditableEntity` retaining their existing member visibility (including the `protected set` on `CreatedAt`) in `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs`
- [ ] T020 [US1] Implement customer initialization/update, Customer flag preservation, `RequireCustomer`, address add/remove/default/snapshot behavior, helper ordering, and exact existing address failures in `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs`
- [ ] T021 [US1] Implement vehicle application submit/refresh/reject/approve rules, DeliveryAgent flag grant, nullable-to-Offline approval, `RequireAgent`, availability/location behavior, and the exact Offline/Available/Busy/Suspended transition matrix with internal reserve/release methods in `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs`
- [ ] T022 [P] [US1] Add the exact tracked/read-only/address-inclusive/available-agent/update-only contract with no add method or persistence types in `src/Talabat/Talabat.Domain/Interfaces/IUserRepository.cs`
- [ ] T023 [P] [US1] Add the six exact transport-neutral capability workflow operations with no caller-supplied role, HTTP, Identity-manager, EF, or transaction types in `src/Talabat/Talabat.Application/Abstractions/IUserCapabilityService.cs`
- [ ] T024 [US1] Run and make green all four Domain.Users suites in `tests/Talabat.Application.Tests/Domain/Users/` without changing pre-existing tests or widening production member visibility

**Checkpoint**: User Story 1 is independently testable and complete. The User aggregate supports
Customer and DeliveryAgent simultaneously; the two future workflow contracts compile; no runtime
consumer uses them.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Prove the additive boundary, dependency rules, regression safety, and clean handoff to
the governing refactor's Phase 2.

- [ ] T025 Run a clean whole-solution build for `src/Talabat/Talabat.slnx` and resolve only Phase 1 compile issues
- [ ] T026 Run all four test projects through `dotnet test src/Talabat/Talabat.slnx` and keep every pre-existing test and assertion unchanged
- [ ] T027 [P] Confirm `Microsoft.Extensions.Identity.Stores` 10.0.9 is the only direct Domain package and that forbidden package/symbol searches have zero hits in `src/Talabat/Talabat.Domain/Talabat.Domain.csproj` and `src/Talabat/Talabat.Domain/`
- [ ] T028 [P] Confirm legacy `Customer.cs`, `CustomerAddress.cs`, and `DeliveryAgent.cs` still exist and inspect the Phase 1 diff for zero DbContext, mapping, migration, host, endpoint, handler, DI, or role changes and zero changes to externally observable runtime, HTTP, or public error contracts (the new `IUserRepository` and `IUserCapabilityService` interfaces from T022-T023 are expected additive contracts, not violations), and confirm `src/Talabat/Talabat.Infrastructure/Persistence/Auditing/AuditableEntitySaveChangesInterceptor.cs` contains `Entries<IAuditable>()` with no remaining occurrence of `Entries<AuditableEntity>()`, using the file boundaries in `specs/004-unified-user-domain-model/plan.md`
- [ ] T029 Run `dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive` and require zero known vulnerabilities before acceptance (constitution package-vulnerability quality gate)
- [ ] T030 Record the successful Phase 1 acceptance gates, require clean final status after committing the Phase 1 implementation, and leave `specs/004-unified-user-domain-model/tasks.md` ready as the completed checkpoint required before the governing refactor's Phase 2

---

## Dependencies

### Phase Dependencies

- **Setup (Phase 1)**: Starts only on the required branch; T001 must establish the clean green
  checkpoint before T002.
- **Foundational (Phase 2)**: Depends on T002. T003 and T004 may run in parallel; T005 depends on
  both; T006 depends on T005; T007 gates all remaining work. T008-T013 may run in parallel after
  T007.
- **User Story 1 (Phase 3)**: Depends on all foundational tasks. T014-T017 may be authored in
  parallel. T018-T021 are ordered because they build the same aggregate surface. T022 and T023 may
  run in parallel after User is complete; T024 depends on T014-T023.
- **Final Phase**: Depends on T024. T025 precedes T026. T027 and T028 may run in parallel after the
  full suite passes. T029 runs after T026. T030 depends on every acceptance check including the
  T029 vulnerability audit.

### User Story Dependency Graph

```text
Setup
  └── Foundational audit/capability types
        └── US1: Unified User Domain Model (P1 / MVP)
              └── Final additive/regression acceptance
```

There is one primary user story in the specification; no artificial P2/P3 stories are introduced.
Its account, customer, address, and agent subflows are separately testable inside the four required
suites.

## Parallel Execution Examples

### Foundational Contracts

After T002, two agents can create the audit interfaces independently:

```text
T003: src/Talabat/Talabat.Domain/Common/Abstractions/IAuditable.cs
T004: src/Talabat/Talabat.Domain/Common/Abstractions/ISoftDeletable.cs
```

After T007, the two enums and four failures can be created concurrently:

```text
T008 + T009: src/Talabat/Talabat.Domain/Aggregates/Users/*.cs
T010 + T011 + T012 + T013: src/Talabat/Talabat.Domain/Exceptions/*.cs
```

### User Story 1 Tests

After T013, the four behavior suites can be authored concurrently because they use distinct files:

```text
T014: UserAccountStateTests.cs
T015: UserCustomerCapabilityTests.cs
T016: UserAddressInvariantTests.cs
T017: UserAgentLifecycleTests.cs
```

After T021, the two boundary contracts can be created concurrently:

```text
T022: src/Talabat/Talabat.Domain/Interfaces/IUserRepository.cs
T023: src/Talabat/Talabat.Application/Abstractions/IUserCapabilityService.cs
```

### Acceptance

After T026, package/dependency validation and additive-scope inspection can run concurrently:

```text
T027: Domain package and forbidden-symbol sweeps
T028: legacy-file and changed-path scope inspection
```

## Implementation Strategy

### MVP First

The specification has one P1 story, so the MVP is the complete additive User Story 1:

1. Finish Setup and Foundational gates.
2. Author the four story test suites.
3. Port UserAddress and implement User incrementally: account state, customer behavior, then agent
   behavior.
4. Freeze both cross-layer contracts.
5. Pass focused tests before running full regression acceptance.

### Incremental Delivery

1. **Checkpoint**: commit planning artifacts and prove the existing solution green.
2. **Foundation increment**: package plus audit contracts/generalization; build green.
3. **Account increment**: User construction, activation, audit, soft delete, RowVersion.
4. **Customer increment**: profile and address invariants.
5. **Agent increment**: approval lifecycle and complete operational transition matrix.
6. **Boundary increment**: repository/capability interfaces only.
7. **Acceptance increment**: focused tests, full build/test, dependency/scope sweeps, final commit.

### Scope Discipline

At every task, reject changes to `ApplicationUser`, legacy Customer/DeliveryAgent production files,
DbContext/configurations/migrations, handlers/`ICurrentUser`, hosts/endpoints/DTOs, role operations,
or databases. Those changes belong to later governing refactor phases.
