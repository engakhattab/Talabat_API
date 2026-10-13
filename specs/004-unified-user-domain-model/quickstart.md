# Quickstart: Unified User Domain Model (Phase 1)

This guide is the implementation and validation handoff for the additive first phase. It does not
authorize Phase 2 cutover, database, Identity-host, or endpoint work.

## Preconditions

1. Work on `feature/user-aggregate-refactor`.
2. Review [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
   [data-model.md](data-model.md), and [contracts/domain-interfaces.md](contracts/domain-interfaces.md).
3. Before production-code work begins, checkpoint the complete current working tree as required by
   the governing plan, then require a clean status.
4. Establish a green whole-solution baseline. Stop on any failure.

```powershell
git branch --show-current
git status --short
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx
```

Expected branch: `feature/user-aggregate-refactor`. The implementation starts only after the
planning/checklist artifacts are committed and `git status --short` is empty.

## Ordered Implementation

Follow this order; it mirrors the governing plan and preserves a buildable additive path.

1. Update `Talabat.Domain.csproj`:
   - add `Microsoft.Extensions.Identity.Stores` 10.0.9 as its only package reference;
   - add an SDK `InternalsVisibleTo` item for `Talabat.Application.Tests` so internal agent
     reservation/release transitions stay internal but are directly testable.
2. Add `IAuditable.cs` and `ISoftDeletable.cs` under
   `Talabat.Domain/Common/Abstractions/` with the exact members in [data-model.md](data-model.md).
3. Make `AuditableEntity` implement both interfaces without changing any existing property or
   method body.
4. Change only audit entity discovery in `AuditableEntitySaveChangesInterceptor` from
   `Entries<AuditableEntity>()` to `Entries<IAuditable>()`. Build once here to catch contract drift.
5. Add `UserType.cs` and `AgentApprovalStatus.cs` under `Aggregates/Users/` with the fixed numeric
   values.
6. Add four Domain failures: `CustomerProfileNotInitializedException`,
   `DeliveryAgentNotInitializedException`, `AgentApplicationNotPendingException`, and
   `ConcurrencyConflictException`.
7. Add `UserAddress.cs` as a namespace-adjusted behavioral port of `CustomerAddress.cs`. Do not
   delete or modify the legacy child.
8. Add `User.cs` with the aggregate surface, guards, capability changes, address behaviors,
   application lifecycle, operational transition matrix, activation, audit/soft-delete parity, and
   empty RowVersion specified in [data-model.md](data-model.md).
9. Add `IUserRepository.cs` with the exact signature in
   [contracts/domain-interfaces.md](contracts/domain-interfaces.md). Do not implement or register it.
10. Add `IUserCapabilityService.cs` with the exact signature in the contract document. Do not
    implement or register it.
11. Add the four prescribed xUnit files under
    `tests/Talabat.Application.Tests/Domain/Users/`:
    - `UserCustomerCapabilityTests.cs`
    - `UserAddressInvariantTests.cs`
    - `UserAgentLifecycleTests.cs`
    - `UserAccountStateTests.cs`

Do not start a later step by deleting legacy code. No Phase 1 task changes `ApplicationUser`,
`Customer`, `CustomerAddress`, `DeliveryAgent`, their repositories/configurations, `ICurrentUser`, a
DbContext, a host, an endpoint, a migration, or a database.

## Focused Test Coverage

The four new files cover:

- registration defaults, `Id == 0`, empty RowVersion, activation/deactivation, and audit/soft-delete
  method parity;
- customer initialization/update, flag preservation, guards, and failed-validation atomicity;
- duplicate address equality, one/default/no-default cases, unknown and non-positive IDs, removal,
  default selection, and independent snapshots;
- application submit/refresh/reject/resubmit/approve flows, non-pending decisions, capability
  guards, all legal/illegal operational transitions, location range/null behavior, and
  reservation/release through the friend test assembly.

Use existing xUnit conventions and the existing `TestIds.SetId` helper for child IDs.

```powershell
dotnet test tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj --filter "FullyQualifiedName~Domain.Users"
```

## Phase 1 Acceptance Gate

Run the focused tests, then the whole solution:

```powershell
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx
```

Run dependency/scope sweeps. Both forbidden-symbol searches must return no matches:

```powershell
rg -n "Microsoft\.AspNetCore\.Identity" src/Talabat/Talabat.Domain/Talabat.Domain.csproj
rg -n "\b(UserManager|RoleManager|SignInManager|ClaimsPrincipal|HttpContext)\b" src/Talabat/Talabat.Domain -g "*.cs"
```

Confirm the approved package is the only direct Domain package and legacy types still exist:

```powershell
dotnet list src/Talabat/Talabat.Domain/Talabat.Domain.csproj package
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
Test-Path src/Talabat/Talabat.Domain/Aggregates/Customer/Customer.cs
Test-Path src/Talabat/Talabat.Domain/Aggregates/Customer/CustomerAddress.cs
Test-Path src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/DeliveryAgent.cs
```

Expected results:

- the build and every pre-existing/new test pass;
- the Domain direct-package list contains only `Microsoft.Extensions.Identity.Stores` 10.0.9;
- the solution vulnerability audit reports no known vulnerable packages;
- both forbidden-symbol searches have zero hits;
- all three legacy files return `True`;
- no database, migration, host, endpoint, runtime registration, or externally observable runtime,
  HTTP, or public error contract changed.

## Rollback Boundary

Phase 1 is additive. If acceptance fails, revert only the Phase 1 implementation range; the old
Customer and DeliveryAgent runtime remains intact. Do not run a database rollback because Phase 1
creates no database change.
