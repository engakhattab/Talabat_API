# Research: Unified User Identity and Persistence Cutover

**Feature**: Phase 2 unified-user cutover  
**Date**: 2026-07-18  
**Sources**: repository inspection, Phase 1 artifacts, project constitution, governing refactor plan,
and official Microsoft documentation linked below

## R1 — Implementation prerequisite

**Decision**: Planning may complete, but implementation must stop until Phase 1 tasks T029/T030 are
accepted, Phase 1 is committed, the tree is clean, build/tests pass, and the transitive package
audit is clean.

**Rationale**: The current tree contains uncommitted Phase 1 production/test files and the Delivery
API still resolves the vulnerable `Microsoft.OpenApi` 2.0.0 dependency. Phase 2 deletes types that
are the Phase 1 rollback path. Starting now would remove the ordered checkpoint required by the
constitution and governing plan.

**Rejected alternatives**:

- Treat Phase 2 as authorization to fix and absorb the Phase 1 vulnerability blocker: rejected;
  planning does not broaden the prior implementation scope.
- Begin the compile-break window and commit everything later: rejected; it destroys the required
  recoverable boundary between phases.

## R2 — Identity model and integer key

**Decision**: `TalabatDbContext` derives from
`IdentityDbContext<User, IdentityRole<int>, int>`. Use the inherited `Users` set and Identity's
generated integer key mapping. `UserConfiguration` must not call `ConfigureIdentityKey`.

**Rationale**: `User` already inherits `IdentityUser<int>`, and ASP.NET Core Identity infers the key
type from the context. Microsoft documents `IdentityDbContext<TUser, TRole, TKey>` as the role-aware
custom-key context and recommends choosing the key type in the initial migration because changing
an existing primary key generally requires table recreation. This project explicitly authorizes a
disposable rebuild. See [Identity model customization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0).

**Rejected alternatives**:

- Keep `ApplicationUser` and link it to `User`: rejected; it recreates the forbidden dual model and
  linkage key.
- Keep string Identity keys: rejected; authenticated `sub`, business FKs, and unified IDs must be
  the same generated integer.
- Configure the Identity key through the project's aggregate helper: rejected; Identity already
  maps the key and duplicate key configuration is unnecessary/risky.

## R3 — Migration source before full SQL tests; database drop afterward

**Decision**: Split migration-source regeneration from the destructive development rebuild:

1. finish the runtime/EF model and make the solution compile;
2. remove legacy migration source files and scaffold `InitialUnifiedUser` without connecting to or
   updating the configured development database;
3. run all SQL-backed tests against their disposable Testcontainers/LocalDB databases;
4. commit and require a clean tree;
5. validate both development connections exactly; then drop/update only the authorized Talabat DB.

**Rationale**: Both `Talabat.Infrastructure.Tests` and `Talabat.Identity.Tests` create isolated
databases with `Database.MigrateAsync()`. The current migrations build string-key Identity plus
`Customers`/`DeliveryAgents`; they cannot produce a schema usable by the new int-key unified model.
Therefore the literal sequence “all tests green, then replace migrations” is circular. EF's
official migration guidance recognizes deleting the migrations folder and database, then creating
a new initial migration, as the clean reset procedure. Source regeneration itself does not touch a
database. See [Managing migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)
and [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

**Rejected alternatives**:

- Change SQL fixtures to `EnsureCreated`: rejected; it bypasses migration history and would make
  migration acceptance untested.
- Apply the old chain and add a conversion migration: rejected; data preservation and complex key
  conversion are explicitly out of scope.
- Drop the named development DB before tests: rejected; it weakens the clean/green safety boundary.

## R4 — Capability workflow transaction ownership

**Decision**: Every public `UserCapabilityService` method runs through one helper:

- if no current transaction exists, begin and own one;
- if one exists, create a uniquely named savepoint and join it;
- perform account/domain persistence, role delta, and required security-stamp update on the same
  scoped `TalabatDbContext` used by `UserManager<User>`;
- on success, commit only an owned transaction or release the savepoint;
- on a handled failure, roll back the owned transaction or savepoint and clear the change tracker;
- never catch or translate `OperationCanceledException`.

**Rationale**: `UserManager.CreateAsync`, role operations, and security-stamp updates save through
the EF Identity store. A shared explicit transaction makes their separate saves atomic. When an
outer transaction already exists, a savepoint is required so returning a failure cannot leave
earlier successful saves available for an outer commit. Clearing the tracker prevents rolled-back
in-memory mutations from being saved later in the scoped context. EF documents transaction
control and automatic/manual savepoints in [Using transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

**Rejected alternatives**:

- Inject both `IUserRepository` and `UserManager`: rejected; the governing architecture reserves
  that mix and direct context coordination to this explicit workflow and does not need a second
  aggregate abstraction inside it.
- Use a transaction per `UserManager` call: rejected; flags/account/role/stamp could drift.
- Join an ambient transaction without savepoint rollback: rejected; a failure result could be
  followed by an outer commit of partial state.

## R5 — Exact workflow sequences and failures

**Decision**:

### Register customer

1. `User.Register(email, email, fullName)`.
2. `InitializeCustomerProfile(fullName, age, phoneNumber)`.
3. `UserManager.CreateAsync(user, password)`.
4. Add server-owned Customer role.
5. Update security stamp because a role changed.
6. Commit and return generated `user.Id`.

### Register delivery-agent applicant

1. `User.Register(email, email, fullName)`.
2. Normalize optional phone (`null`/whitespace → `null`, otherwise trim) and assign
   `user.PhoneNumber` before persistence.
3. `SubmitDeliveryAgentApplication(vehicleType)`.
4. `UserManager.CreateAsync(user, password)`.
5. Commit with `UserType.None`, no DeliveryAgent role, `PendingApproval`, and null operational
   status.

No security-stamp refresh is necessary because there is no role delta and no existing session.

### Grant customer to an existing account

1. Load the tracked user in the workflow transaction.
2. If absent, return `UserNotFound`/NotFound.
3. If the Customer flag already exists, return the existing `ProfileAlreadyExists`/Conflict before
   mutation.
4. Initialize customer profile, save, add Customer role, update stamp, and commit.

### Approve applicant

Load or return `UserNotFound`; call Domain approval (which enforces PendingApproval), save flag and
Offline status, add DeliveryAgent role, update stamp, and commit.

### Reject applicant

Load or return `UserNotFound`; call Domain rejection, save, and commit. There is no role delta.

### Deactivate user

Load or return `UserNotFound`; call the idempotent Domain `Deactivate`, save, update security stamp,
and commit. Repeated deactivation is a successful no-op in business state but refreshes the stamp.

**Failure mapping**:

| Failure | Application result |
|---|---|
| `CreateAsync` Identity failure, including duplicate normalized email/name | `IdentityOperationFailed`, Validation |
| Role/stamp `IdentityResult` failure | `IdentityOperationFailed`, Conflict |
| Identity role-store `InvalidOperationException` from the role step (for example a missing role definition) | `IdentityOperationFailed`, Conflict |
| Missing existing target | `UserNotFound`, NotFound |
| Existing Customer capability | `ProfileAlreadyExists`, Conflict |
| Non-pending approval/rejection | mapped `AgentApplicationNotPendingException`, Conflict |
| EF concurrency | `ConcurrencyConflict`, Conflict |
| Invalid Domain/argument input | existing `DomainExceptionMapper` result |
| Unexpected exception | rollback, then rethrow for existing host error handling |

Identity error messages are deterministic: retain the `IdentityResult.Errors` order, trim
descriptions, and join them with `"; "`. Wrap only the role/stamp calls so the EF Identity store's
documented/source-defined missing-role `InvalidOperationException` becomes the same conflict result
after rollback; unrelated `InvalidOperationException` instances remain unexpected and propagate.
The EF store behavior is visible in the official
[ASP.NET Core UserStore source](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/EntityFrameworkCore/src/UserStore.cs).
Never expose password hashes, stamps, role input, or exception stack traces.

**Rejected alternatives**:

- Reuse `CustomerNotFound` for every missing user: rejected; approval/deactivation targets are not
  necessarily customers.
- Treat role failure as validation: rejected; input was accepted but the server could not complete
  its consistency projection.
- Add capability revocation: rejected; deactivation/soft deletion are the Phase 2 disablement paths.

## R6 — Server-owned role names and startup seed

**Decision**: Define one internal role-name source with exactly:

```text
Customer
DeliveryAgent
Admin
RestaurantOwner
```

`UserCapabilityService` selects only these constants. `IdentityDataSeeder` checks
`RoleExistsAsync` and creates only missing `IdentityRole<int>` rows. Any failed create throws an
`InvalidOperationException` containing deterministic Identity descriptions so the Identity host
does not begin serving in a partially initialized state. Repeated startup and partially existing
role sets converge to the exact four definitions; no users are seeded.

**Rationale**: Role definitions are infrastructure metadata; user-role membership is the
authorization projection owned only by the capability workflow. `HasData` is avoided because role
concurrency stamps cause migration churn.

**Rejected alternatives**:

- Caller role string: rejected; it permits privilege selection/escalation.
- EF `HasData`: rejected; it mixes changing Identity metadata with schema migrations.
- Ignore seed failures: rejected; later registrations would fail unpredictably.

## R7 — Sign-in and session invalidation

**Decision**: `TalabatSignInManager : SignInManager<User>` overrides `CanSignInAsync`:

1. return false for `!user.IsActive || user.IsDeleted`;
2. otherwise return `await base.CanSignInAsync(user)`.

Register it only in the Identity host's full Identity builder. Configure
`SecurityStampValidatorOptions.ValidationInterval = TimeSpan.FromMinutes(5)`. Update the stamp
after every role delta and after deactivation.

**Rationale**: The explicit rule covers inactive accounts; the global query filter is only
defense-in-depth for deleted accounts. ASP.NET Core exposes `CanSignInAsync` as the overridable
sign-in eligibility gate. Microsoft documents security-stamp refresh and validation intervals as
the mechanism for invalidating cached cookie principals. See
[CanSignInAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.signinmanager-1.cansigninasync?view=aspnetcore-10.0)
and [Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-8.0#isecuritystampvalidator-and-sign-out-everywhere).

**Rejected alternatives**:

- Depend only on `HasQueryFilter`: rejected; it does not reject inactive users and would make the
  business rule accidental.
- Set Identity lockout for deactivation: rejected; lockout and business activation have different
  meanings.
- Trust an old cookie until natural expiry: rejected; violates the five-minute requirement.

## R8 — Current user resolution and malformed subjects

**Decision**: `CurrentUser` is request-scoped and resolves once:

1. require an authenticated principal;
2. select `ClaimTypes.NameIdentifier` when present, otherwise `sub`;
3. require `int.TryParse` and `id > 0`; do not fall through to a second claim after a malformed
   preferred claim;
4. query once with `AsNoTracking`, filter by ID, and project only nullable `UserType`;
5. `HasCustomerCapability` is true only when the persisted flags contain Customer;
6. `CustomerId` equals `UserId` only when capable.

For `/api/me/*`, `ProfileEnforcementFilter` returns an empty-body 401 when ASP.NET authenticated a
principal but `CurrentUser` rejects its subject. This check occurs before the POST-profile
exemption. A valid non-customer subject retains the existing POST exemption and 404/409
`ProfileNotCreated` behavior.

**Rationale**: `[Authorize]` checks authentication, not whether the application can parse a valid
business ID. Merely returning `ICurrentUser.IsAuthenticated == false` can allow an authenticated
principal to reach actions that dereference `CustomerId!.Value`. Persisted flags—not cached role
claims—are the business source of truth.

**Rejected alternatives**:

- Trust role claims: rejected; tokens/cookies can be stale after capability changes.
- Query the full user and roles: rejected; one scalar flag query is sufficient.
- Let malformed IDs reach handlers: rejected; it can cause null dereferences or resolve the wrong
  owner boundary.

## R9 — Available-agent query

**Decision**: Implement the governing query exactly:

```csharp
_dbContext.Users
    .AsNoTracking()
    .Where(user => user.DeliveryAgentStatus == DeliveryAgentStatus.Available)
    .OrderBy(user => user.FullName)
```

**Rationale**: The Domain state machine can assign an operational status only after approval grants
the DeliveryAgent capability; applicants have null status, and rejected applicants never become
Available. The global filter excludes deleted users. This keeps the specified query and existing
semantic source of truth without duplicating approval predicates in every read.

`IsActive` is not an operational agent status and the governing plan specifies deactivation as a
login/session rule, not an automatic agent suspension. Phase 3 may harden assignment authorization;
Phase 2 must not invent it.

**Rejected alternatives**:

- Join Identity roles: rejected; roles are a projection and role-table checks/joins are prohibited
  for business state.
- Add `IsActive` filtering or auto-suspend on deactivation: rejected as an unapproved behavior
  change.

## R10 — User mapping and rowversion

**Decision**: Map `User.RowVersion` with `.IsRowVersion()`. `UnitOfWork.SaveChangesAsync` translates
`DbUpdateConcurrencyException` to `ConcurrencyConflictException`; the Application mapper emits
`ConcurrencyConflict`/Conflict, which existing web result mapping turns into HTTP 409.

**Rationale**: SQL Server `rowversion` automatically changes whenever the principal row changes
and EF includes the loaded value in update/delete concurrency predicates. See
[SQL Server value generation](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/value-generation#rowversions)
and [Handling concurrency conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

Owned-address-only updates do not update the `AspNetUsers` row and therefore need not change its
rowversion. Concurrent writers selecting different default addresses are still constrained by the
unique filtered index; a uniqueness failure is not misreported as a rowversion conflict.

**Rejected alternatives**:

- Use Identity `ConcurrencyStamp` as the business token: rejected; it is application-managed
  Identity metadata and does not provide SQL row-wide automatic versioning.
- Increment a custom integer: rejected; SQL rowversion is the decided standard.

## R11 — Audit/query-filter extraction

**Decision**: Add generic `ConfigureAuditing<TEntity>() where TEntity : class, IAuditable,
ISoftDeletable` containing the exact current `AuditableEntity` property mapping and
`HasQueryFilter(!IsDeleted)`. Keep `ConfigureAuditableEntity` as a delegating compatibility wrapper.

**Rationale**: `User` must inherit Identity's user class and therefore cannot inherit
`AuditableEntity`, but it implements the same interfaces. A shared mapping prevents audit column or
soft-delete behavior drift across aggregate roots.

**Rejected alternatives**:

- Duplicate the mapping in `UserConfiguration`: rejected; later changes could diverge.
- Remove the query filter for Identity internals: rejected; deleted users are intentionally hidden
  from ordinary loads and UserManager.

## R12 — Address deletion semantics

**Decision**: Port the existing owned-address mapping exactly, including the shadow `IsDeleted`
column and filtered active-default index. Do not add a Domain address soft-delete workflow in Phase
2; `User.RemoveAddress` continues to remove the owned entity and EF physically deletes it.

**Rationale**: The governing plan explicitly requests the ported mapping but does not authorize a
new address lifecycle. `UserAddress` has no deletion interface/member. Expanding it would change
Phase 1 behavior and test scope.

**Rejected alternatives**:

- Invent interceptor logic that turns owned-row deletes into shadow-property updates: rejected;
  it is not specified and complicates aggregate behavior.
- Remove the shadow column/filter: rejected; the exact target schema requires them.

## R13 — Public HTTP contracts

**Decision**:

- replace `/account/register` with `/account/register/customer` and
  `/account/register/delivery-agent`;
- accept only the fields in `contracts/identity-api.md`, never a role;
- return HTTP 200 with numeric `id` and echoed normalized/accepted email on success;
- retain login/logout requests and `{ "message": ... }` responses;
- retain `/account/me` shape with only the ID type changing to JSON number; and
- preserve the Customer API's two `ProfileNotCreated` anonymous objects byte-for-byte.

`VehicleType` uses its existing integer JSON representation: Bike=1, Motorcycle=2, Car=3. Invalid
enum values reach the Domain validation result; no global string-enum converter is introduced.

**Rejected alternatives**:

- One route with a registration-kind or role string: rejected; it recreates caller-selected role
  behavior.
- Add approval/rejection controller endpoints: rejected; Phase 2 approval is service-level only.
- Refactor exact error objects into `ProblemDetails`: rejected; serialization bytes/property order
  are a compatibility requirement.

## R14 — Test architecture

**Decision**:

- Application tests use `FakeUserRepository` and `FakeUserCapabilityService`.
- Infrastructure workflow tests use real SQL plus actual `UserManager<User>`/role stores.
- Identity test DbContext replacement retains the audit interceptor and removes/replaces both
  context and options descriptors.
- Customer API test databases seed deterministic integer unified users and the four roles; tests
  use distinct customer/non-customer identities or reset state to prevent shared-fixture leakage.
- Compatibility tests compare exact raw `ProfileNotCreated` JSON and empty malformed-sub 401.
- SQL fixtures continue using `MigrateAsync` to prove the new migration.

**Rationale**: Role synchronization and rollback depend on actual Identity EF store behavior and
cannot be proven by an in-memory fake. Current Customer API assertions that accept multiple status
codes are too permissive for byte-identical compatibility.

**Rejected alternatives**:

- Mock `UserManager` for all workflow tests: rejected; it cannot prove shared-transaction rollback
  or store configuration.
- Reuse one mutable test user across all endpoint cases: rejected; test order can hide capability
  and profile regressions.

## R15 — Destructive connection validation and recovery

**Decision**: Before database drop, parse both Customer API and Identity development connection
strings with `SqlConnectionStringBuilder`. Require exact normalized:

```text
DataSource     = DESKTOP-5IHGJ9F\SQLEXPRESS
InitialCatalog = Talabat
IntegratedSecurity = true
```

Run six non-destructive negative checks through the same validator: wrong server, wrong catalog,
and disabled integrated security for each configuration source. Every case must throw before an EF
drop/update process can be started.

Also require full code/test/audit gates, an authorized Phase 2 checkpoint, and empty status. After
that checkpoint, keep tracked files unchanged through connection validation and the rebuild; defer
task checkbox/evidence updates until the final authorized evidence commit. If the disposable DB has
been dropped and migration/seeding fails, there is no data recovery: fix source, repeat all
gates/checkpoint, and rerun the clean rebuild.

**Rationale**: A substring check for `Database=Talabat` can match a different unsafe database name.
The repository explicitly authorizes destruction only for this local disposable catalog.

**Rejected alternatives**:

- Text substring alone: rejected as insufficiently exact.
- Data-preserving/squashed history manipulation: rejected; the phase explicitly chooses a clean
  disposable rebuild.

## R16 — Host-specific Identity registration without duplicate schemes

**Decision**: `AddInfrastructure` registers the shared DbContext, repositories,
`IUserCapabilityService`, and seeder, but does not call either Identity bootstrap method. A public
Infrastructure extension named `AddUnifiedUserIdentityCore` returns this exact builder for the
Customer and Delivery hosts:

```csharp
services.AddIdentityCore<User>()
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<TalabatDbContext>();
```

The Identity host does not call that core extension. It owns the one full registration:

```csharp
services.AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<TalabatDbContext>()
    .AddSignInManager<TalabatSignInManager>()
    .AddDefaultTokenProviders();
```

During the foundational compile window, the Identity host may first use the same full chain without
the custom sign-in-manager call; US3 adds `TalabatSignInManager` and the five-minute validator before
full Phase 2 acceptance. Duende binds to that same user type through `.AddAspNetIdentity<User>()`.

**Rationale**: The [.NET 10 `AddIdentityCore` contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.identityservicecollectionextensions.addidentitycore?view=aspnetcore-10.0)
supplies the specified user system and requires `AddRoles` for role services, while the
[.NET 10 `AddIdentity` contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.identityservicecollectionextensions.addidentity?view=aspnetcore-10.0)
supplies the default full Identity configuration. Host-specific registration keeps
`UserManager<User>` available where the capability service is used without registering the full
Identity stack or duplicate authentication schemes in every host.

**Rejected alternatives**:

- Call `AddIdentityCore` inside `AddInfrastructure` and then call full `AddIdentity` in the Identity
  host: rejected because the same host would receive two overlapping bootstrap paths.
- Register full `AddIdentity` in every host: rejected because Customer and Delivery hosts do not own
  Identity cookies or sign-in behavior.

## Resolved checklist risks

| Checklist concern | Planning resolution |
|---|---|
| Missing target user | `UserNotFound`/NotFound for all existing-account workflow targets |
| Repeated deactivation | Idempotent success plus security-stamp refresh; missing/deleted ordinary loads return NotFound |
| Role seed partial/failure | Create only missing definitions; fail startup on any failed creation; retry converges |
| Role projection preservation | Add only the workflow's one role; never remove unrelated flags/roles |
| Normalized duplicate | Identity's retained normalization/validators decide; create failure rolls back |
| Session invalidation measurement | Phase 2 proves atomic stamp refresh and the exact five-minute validator configuration; the live-cookie elapsed-time journey remains Phase 3 |
| Soft-deleted lookup | Global filter is intended; explicit sign-in rule still exists for a materialized user |
| Address concurrency | Rowversion covers User row; unique filtered index arbitrates competing defaults |
| Role failure proof | At least one real-SQL missing-role/failure rollback test in Phase 2; full matrix remains Phase 3 |
| Destructive mismatch matrix | Both configuration sources reject wrong server, wrong catalog, and disabled integrated security before any EF process starts |
| Rebuild partial failure | Disposable DB remains rebuildable; fix, regate, checkpoint, rerun; no data recovery |
| Observability | Existing baselines retained; fail-fast exception/result/test evidence added, no new logging platform |
| Delivery scaffold/vulnerability | Dependency-only remediation is a Phase 1 acceptance prerequisite; no Delivery business endpoint work |
