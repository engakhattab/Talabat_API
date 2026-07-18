# Implementation Plan: Unified User Domain Model

**Branch**: `feature/user-aggregate-refactor` | **Date**: 2026-07-18 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/004-unified-user-domain-model/spec.md`  
**Governing increment plan**: [user-aggregate-refactor-plan.md](../../user-aggregate-refactor-plan.md), Phase 1

## Summary

Phase 1 adds the unified `User : IdentityUser<int>` business aggregate alongside the still-running
Customer and DeliveryAgent models. It introduces combinable user capabilities, customer profile and
address behavior, the delivery-agent application/approval lifecycle, the existing agent operational
state machine, activation, audit/soft-delete parity, future concurrency state, and stable repository
and capability-workflow contracts. The design is deliberately additive: it does not wire User to
Identity, EF, hosts, roles, endpoints, handlers, migrations, or a database. Four focused Domain
behavior test files prove the new model while every pre-existing test remains unchanged.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`; local SDK 10.0.301)  
**Primary Dependencies**: Existing Domain/Application dependencies plus exactly
`Microsoft.Extensions.Identity.Stores` 10.0.9 in `Talabat.Domain`; xUnit 2.9.3 for tests  
**Storage**: Not applicable in Phase 1; `RowVersion = []` is only a future mapping seam  
**Testing**: xUnit in `tests/Talabat.Application.Tests/Domain/Users/`; full four-project regression suite  
**Target Platform**: Framework-neutral Domain and Application class libraries; solution builds on .NET 10  
**Project Type**: Layered backend, additive Domain-model refactor  
**Performance Goals**: No runtime performance target because no runtime path changes; all new tests
and the full existing suite must complete successfully at the phase gate  
**Constraints**: Follow the governing Phase 1 order; keep old aggregates operational; no persistence,
host, role, or API wiring; no `UserManager`, HTTP, EF, or web types in Domain/Application; UserType
mutations remain aggregate-internal and are invoked only through the future capability workflow;
`MarkBusy`/`MarkAvailable` remain internal; only the approved Identity Stores package may enter Domain  
**Scale/Scope**: 12 new production files, 3 modified production/project files, 4 new test files,
0 deleted files, 0 migrations, 0 endpoint or runtime behavior changes

## Constitution Check

### Pre-Design Evaluation

| Gate | Status | Plan evidence |
|------|--------|---------------|
| P1: Domain independence | PASS | The sole new Domain package is the constitution-approved `Microsoft.Extensions.Identity.Stores` 10.0.9; no EF/web/host types; Application receives no new package |
| P2: Application orchestration | PASS | Application adds only the framework-neutral `IUserCapabilityService`; all invariants remain in User |
| P3: Aggregate ownership | PASS | User owns and mutates `UserAddress`; no child repository is introduced |
| P4: Repository boundaries | PASS | `IUserRepository` lives in Domain, exposes aggregates only, and has no EF/query types or add operation |
| P5: Thin hosts | PASS / N/A | No host, controller, endpoint, or composition-root change exists in Phase 1 |
| P6: Unified user and capability workflow | PASS | One `User : IdentityUser<int>` is introduced; roles are absent; capability service shape is frozen for later implementation |
| P7: Database-generated integer IDs | PASS | User/UserAddress IDs start at 0; no generator or persistence mapping is introduced |
| P8: Encapsulation | PASS | Private materialization constructors, private address backing list, read-only collection, private setters, and internal coordination methods are preserved |
| P9: Database constraints | PASS / N/A | Phase 1 has no database model; constraint work is explicitly deferred to Phase 2 |
| Audit/soft-delete standard | PASS | Interface extraction preserves existing method bodies and lets User implement the same contract despite Identity inheritance |
| Concurrency standard | PASS | RowVersion surface and Domain failure are prepared; mapping/enforcement remains Phase 2 |
| Testing gate | PASS | Four required Domain behavior files plus unchanged full-suite execution are planned |
| Phase 1 scope | PASS | Additive only; no legacy deletion, Identity/EF cutover, role work, host wiring, or database work |

No constitution gate fails. The direct Identity inheritance is the constitution's explicit approved
exception, not a complexity waiver.

## Project Structure

### Documentation

```text
specs/004-unified-user-domain-model/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── domain-interfaces.md
└── checklists/
    ├── requirements.md
    └── domain.md
```

### Source Code

```text
src/Talabat/
├── Talabat.Domain/
│   ├── Talabat.Domain.csproj                         # MODIFY: package + friend test assembly
│   ├── Common/Abstractions/
│   │   ├── AuditableEntity.cs                       # MODIFY: implement both contracts
│   │   ├── IAuditable.cs                            # NEW
│   │   └── ISoftDeletable.cs                        # NEW
│   ├── Aggregates/Users/
│   │   ├── User.cs                                  # NEW aggregate
│   │   ├── UserAddress.cs                           # NEW child entity
│   │   ├── UserType.cs                              # NEW flags enum
│   │   └── AgentApprovalStatus.cs                   # NEW approval enum
│   ├── Exceptions/
│   │   ├── CustomerProfileNotInitializedException.cs # NEW
│   │   ├── DeliveryAgentNotInitializedException.cs   # NEW
│   │   ├── AgentApplicationNotPendingException.cs    # NEW
│   │   └── ConcurrencyConflictException.cs            # NEW future failure
│   └── Interfaces/
│       └── IUserRepository.cs                       # NEW contract; no implementation
├── Talabat.Application/
│   └── Abstractions/
│       └── IUserCapabilityService.cs                # NEW contract; no implementation
└── Talabat.Infrastructure/
    └── Persistence/Auditing/
        └── AuditableEntitySaveChangesInterceptor.cs # MODIFY: Entries<IAuditable>()

tests/Talabat.Application.Tests/
└── Domain/Users/
    ├── UserCustomerCapabilityTests.cs               # NEW
    ├── UserAddressInvariantTests.cs                 # NEW
    ├── UserAgentLifecycleTests.cs                   # NEW
    └── UserAccountStateTests.cs                     # NEW
```

Legacy Customer, CustomerAddress, DeliveryAgent, their contracts/implementations/configurations,
`ApplicationUser`, all hosts, and all existing tests remain present and unchanged.

## Phase 0: Research

All design decisions and repository-specific unknowns are resolved in
[research.md](research.md). No design question remains unresolved.

| ID | Decision |
|----|----------|
| R1 | Use `IdentityUser<int>` with only Identity Stores 10.0.9 in Domain |
| R2 | Use fixed `[Flags] UserType` values and preserve every existing bit |
| R3 | Guard full name/profile values in Domain; defer Identity username/email/credential policy |
| R4 | Port address ownership/equality/default/snapshot behavior exactly |
| R5 | Separate nullable approval lifecycle from nullable pre-approval operational status |
| R6 | Extract audit/soft-delete interfaces and generalize interceptor discovery only |
| R7 | Freeze repository and capability-workflow contract shapes without implementations |
| R8 | Add empty RowVersion and Domain concurrency failure, with no Phase 1 persistence behavior |
| R9 | Test in Application.Tests and use a friend-test assembly for internal transitions |
| R10 | Execute the exact additive sequence and require full regression acceptance |

Research also reconciles two wording risks with the governing plan: non-positive address IDs remain
argument-range failures while unknown positive IDs are not-found; User.Register assigns
username/email but Identity policy validation remains a later workflow concern.

## Phase 1: Design And Contracts

Design artifacts completed:

- **[data-model.md](data-model.md)** defines the User/UserAddress fields, initial state, aggregate
  boundary, flags, approval lifecycle, exact agent transition matrix, audit/soft-delete behavior,
  failures, and explicit no-persistence boundary.
- **[contracts/domain-interfaces.md](contracts/domain-interfaces.md)** freezes the exact
  `IUserRepository` and `IUserCapabilityService` signatures and their cross-layer responsibilities.
- **[quickstart.md](quickstart.md)** provides the checkpoint prerequisite, ordered implementation,
  focused/full validation commands, symbol sweeps, and rollback boundary.
- **[research.md](research.md)** records ten decisions and rejected alternatives; no unknown remains.

There is no external HTTP/message contract because Phase 1 is internal and non-runtime. The
`contracts/` artifact documents the two project-boundary interfaces that Phase 2 will implement.

## Phase 2: Planning Handoff

This section is the Spec Kit planning handoff, not Phase 2 of the governing three-phase refactor.
Implementation tasks generated next must preserve this dependency order.

### Ordered Implementation Sequence

1. **Checkpoint and baseline gate**: require the correct branch, commit all current work, require a
   clean status, then build and test the entire solution. Do not proceed on red.
2. **Domain project seam**: add Identity Stores 10.0.9 plus friend access for
   `Talabat.Application.Tests` in the existing Domain project file.
3. **Audit contracts**: add `IAuditable`/`ISoftDeletable`; make `AuditableEntity` implement them with
   no body change.
4. **Audit discovery**: retarget only the interceptor entry query to `IAuditable`, then build.
5. **Capability types**: add `UserType` and `AgentApprovalStatus` with fixed numeric values.
6. **Failures**: add the four Domain exception types in existing style.
7. **Address child**: port CustomerAddress exactly to UserAddress; keep the legacy type.
8. **Aggregate**: add User with registration, customer, address, application, operational,
   activation, audit/soft-delete, and RowVersion behavior from the data model.
9. **Domain repository contract**: add the exact `IUserRepository` contract with no implementation.
10. **Application workflow contract**: add the exact `IUserCapabilityService` contract with no
    implementation or registration.
11. **Behavior tests**: add the four prescribed files, cover the full transition/default/guard
    matrices, and keep every existing test unchanged.
12. **Acceptance**: run focused tests, whole-solution build/test, package inspection, forbidden-symbol
    sweeps, and legacy-file checks from quickstart.

### Dependency Order

```text
clean checkpoint + green baseline
              │
              ▼
Domain package ──► audit interfaces ──► interceptor generalization ──► build gate
                                      │
                                      ▼
                         enums + failures + UserAddress
                                      │
                                      ▼
                                    User
                           ┌──────────┴──────────┐
                           ▼                     ▼
                    IUserRepository     IUserCapabilityService
                           └──────────┬──────────┘
                                      ▼
                            four behavior suites
                                      │
                                      ▼
                          full Phase 1 acceptance gate
```

### Test Decomposition

| Test file | Required behavior groups |
|-----------|--------------------------|
| `UserCustomerCapabilityTests` | initialization/update, valid normalization, direct repeat behavior, customer guard, flag preservation, atomic failed validation |
| `UserAddressInvariantTests` | duplicates, default uniqueness, no implicit default, removal, unknown/non-positive IDs, snapshot, guard, unchanged state after failure |
| `UserAgentLifecycleTests` | submit/pending refresh/reject/resubmit/approve, out-of-order decisions, uninitialized guard, complete status matrix, location validation, internal reserve/release |
| `UserAccountStateTests` | registration defaults, ID/RowVersion defaults, activate/deactivate preservation, audit/soft-delete parity |

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Domain gains an unintended framework dependency | Inspect direct packages and forbidden symbols; allow only Identity Stores 10.0.9 |
| Audit behavior diverges between User and AuditableEntity | Copy the seven properties/four method bodies verbatim, retaining each member's existing visibility (business properties use `private set`; audit members keep their original modifiers, e.g. `CreatedAt`'s `protected set`), and cover parity in account-state tests |
| Internal busy/release transitions become untestable or public | Keep methods internal and add SDK friend access only for Application.Tests |
| A literal port throws the wrong pre-approval agent error | Run `RequireAgent` before every agent operation, including internal transitions and location update |
| Address failure semantics drift | Preserve Guard.Positive for non-positive IDs and AddressNotFound only for unknown positive IDs |
| Username/email validation is duplicated or omitted later | Phase 1 assigns them; Identity policy validation is explicitly Phase 2 workflow work |
| `UserType UserType` name collision causes incorrect expressions | Qualify enum values where needed and always OR flags instead of assignment |
| Vehicle/status namespace move happens early | Keep both enums in DeliveryManagement for Phase 1; move only during Phase 2 cutover |
| Legacy runtime accidentally changes | No legacy file deletion/retargeting; unchanged tests plus scope/file sweeps are mandatory |
| RowVersion is treated as active concurrency now | Initialize `[]`; do not map, compare, or translate conflicts until Phase 2 |

### Explicit Exclusions

- No User repository/service implementation or DI registration.
- No User EF configuration, query filter, owned-address mapping, constraint, migration, or database.
- No Identity role, registration endpoint, approval endpoint, sign-in manager, seeder, or security
  stamp behavior.
- No application-handler, current-user, host, controller, DTO, route, or public error-contract change.
- No deletion, movement, or retargeting of legacy Customer/DeliveryAgent production types.
- No capability revocation, Delivery API feature, frontend, discount, or data-preserving migration.

## Complexity Tracking

No constitution gate failures require justification. The Identity inheritance/package is an
explicit constitution exception. The friend-test assembly declaration preserves internal production
visibility and exists solely to satisfy the required transition-matrix tests without adding another
source file.

## Post-Design Constitution Check

| Gate | Status | Design verification |
|------|--------|---------------------|
| P1: Domain independence | PASS | Contracts and data model introduce no EF, HTTP, host, request, or manager types; package scope is exact |
| P2: Application orchestration | PASS | Capability operations are abstracted; all Phase 1 business rules remain on User |
| P3: Aggregate ownership | PASS | UserAddress has internal mutation and no repository |
| P4: Repository boundaries | PASS | Contract is in Domain and persistence-neutral; creation intentionally absent |
| P5: Thin hosts | PASS / N/A | Design contains no host work or external API contract |
| P6: Unified user/workflow | PASS | One multi-capability User; no linkage key, roles, or competing aggregate introduced |
| P7: Integer IDs | PASS | Data model fixes `int`, initial 0, later database generation |
| P8: Encapsulation | PASS | Private/backing-field/read-only/internal boundaries are explicit |
| P9: Constraints | PASS / N/A | No schema work; Phase 2 boundary is explicit throughout all artifacts |
| Audit/soft delete | PASS | Shared contracts and verbatim behavior parity are designed |
| Concurrency | PASS | Future seam is present without premature persistence behavior |
| Phase scope | PASS | Structure, sequence, contracts, tests, and exclusions remain additive |
| Acceptance gates | PASS | Quickstart includes focused/full tests, build, dependency sweeps, and legacy checks |

All pre- and post-design gates pass, and all clarifications are resolved. The feature is
ready for `/speckit-tasks`.
