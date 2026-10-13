# Implementation Plan: Unified User Identity and Persistence Cutover

**Branch**: `feature/user-aggregate-refactor` | **Date**: 2026-07-18 | **Spec**: [spec.md](spec.md)  
**Input**: Phase 2 of [user-aggregate-refactor-plan.md](../../user-aggregate-refactor-plan.md)  
**Implementation status**: **BLOCKED** until the Phase 1 T029/T030 acceptance checkpoint is complete,
committed, and the working tree is clean

## Summary

Phase 2 makes the Phase 1 `User : IdentityUser<int>` aggregate the single runtime and persistence
model for accounts, customers, and delivery agents. The cutover replaces string-key
`ApplicationUser`, the separate `Customer` and `DeliveryAgent` aggregates, their repositories, and
their tables with one integer-key Identity user backed by `AspNetUsers` and owned
`UserAddresses`.

The implementation uses one coordinated schema-affecting compile-break window. It retargets the
Domain delivery service, EF model and foreign keys, repositories, capability/role transaction
workflow, Application handlers, host Identity stores, Customer API current-user resolution, and
the structural portions of all four test projects before generating a clean `InitialUnifiedUser`
migration. Schema-neutral registration and sign-in stories then complete against disposable SQL
databases created from that migration. The configured development database is dropped only after
all code, tests, symbol sweeps, package audit, an authorized checkpoint, exact connection checks,
and clean status pass.

The design is intentionally explicit enough for a low-context implementation model: exact
contracts are frozen under `contracts/`, persistence and workflow decisions are resolved in
`research.md`, and the ordered work packages below identify files, invariants, and gates. A later
`speckit-tasks` pass should translate these work packages into `tasks.md` without changing their
order or scope.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`)  
**Primary Dependencies**: ASP.NET Core Identity 10.0.9, EF Core SQL Server 10.0.9, Duende
IdentityServer 8.0.2, `Microsoft.Extensions.Identity.Stores` 10.0.9 in Domain only  
**Storage**: SQL Server; one `TalabatDbContext`; integer Identity keys; SQL `rowversion`; EF owned
`UserAddress` rows; one clean `InitialUnifiedUser` migration  
**Testing**: xUnit; Application unit tests; Infrastructure SQL Server integration tests using
Testcontainers/LocalDB fallback; Identity and Customer API `WebApplicationFactory` integration
tests  
**Target Platform**: ASP.NET Core backend hosts on Windows/development SQL Server; runtime remains
cross-platform where the existing solution supports it  
**Project Type**: Layered backend solution with Identity, Customer API, and Delivery API hosts  
**Performance Goals**: Current baselines remain unchanged; current-user capability resolution is
one cached scalar query per request; available-agent reads are no-tracking and ordered by full name  
**Constraints**: One physical user row; no legacy linkage key; flags are business truth and roles
are the authorization projection; only `IUserCapabilityService` may change either; no Identity/EF
packages in Application; no new Domain package beyond the approved Identity stores abstraction;
preserve `CustomerId`, `AssignedAgentId`, DTO names, and exact `ProfileNotCreated` bodies; use
host-specific Identity registration so the Identity host has one full `AddIdentity` chain and the
Customer/Delivery hosts have only the shared `AddIdentityCore`/role/store chain  
**Scale/Scope**: Phase 2 only—runtime/persistence cutover, registrations, service-level agent
decision workflow, login gate, test migration, and disposable development rebuild; no full
Delivery API, admin controller/UI, Phase 3 role-policy wiring, or data-preserving migration

## Constitution Check

| Gate | Status | Design evidence |
|---|---|---|
| Domain dependency boundary | PASS | Domain keeps only `Microsoft.Extensions.Identity.Stores`; `UserManager`, EF, HTTP, and claims remain outside Domain |
| Application dependency boundary | PASS | Application depends on Domain contracts and transport-neutral results only; Identity/EF implementations remain in Infrastructure |
| Single unified user model | PASS | `TalabatDbContext` uses `IdentityDbContext<User, IdentityRole<int>, int>` and legacy account/profile aggregates are deleted |
| Aggregate encapsulation | PASS | Customer/address/agent behavior remains on `User`; `_addresses` stays field-backed; delivery reserve/release remains internal |
| Repository boundary | PASS | `IUserRepository` handles business loads; `UserManager<User>` appears only in the capability workflow and Identity host |
| Capability/role synchronization | PASS | Every user-role mutation is server-owned and occurs with its flag mutation in the shared context transaction |
| Identity key generation | PASS | Identity maps `User.Id` as SQL IDENTITY; `UserConfiguration` does not call `ConfigureIdentityKey` |
| Audit, deletion, and concurrency | PASS | Interface-based mapping/filtering covers `User`; `RowVersion` is SQL rowversion; `UnitOfWork` maps concurrency to a Domain conflict |
| Thin hosts | PASS | Controllers delegate to Application/capability services; current-user and sign-in classes contain host-specific authentication translation only |
| Database integrity | PASS | Eight NULL-tolerant `CK_Users_*` checks, the active-default address index, and retained restricted FKs are frozen in the schema contract |
| Phase sequencing | **BLOCKED before implementation** | Current dirty Phase 1 tree and open T029/T030 gate must be resolved first; planning itself is non-runtime and allowed |
| Destructive-operation safety | PASS by design | Migration source is regenerated before SQL tests, but the named development database is not dropped until full green tests, exact connection validation, a committed checkpoint, and clean status |

No constitution exception is requested. The migration-source/development-database sequence is a
necessary clarification of the governing plan, documented in [research.md](research.md): the SQL
fixtures use `MigrateAsync`, so the new migration must exist before those tests can become green.
This changes no production-data policy and retains the destructive database gate.

## Project Structure

### Documentation

```text
specs/005-unified-user-identity-cutover/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-ports.md
│   ├── identity-api.md
│   └── persistence-schema.md
└── checklists/
    ├── requirements.md
    └── cutover.md
```

`tasks.md` is deliberately not created by this workflow. Generate it with `speckit-tasks` after
this plan is accepted.

### Source Code

```text
src/Talabat/
├── Talabat.Domain/
│   ├── Aggregates/Users/
│   │   ├── User.cs
│   │   ├── UserAddress.cs
│   │   ├── UserType.cs
│   │   ├── AgentApprovalStatus.cs
│   │   ├── VehicleType.cs                 # moved here
│   │   └── DeliveryAgentStatus.cs          # moved here
│   ├── DomainServices/DeliveryManagement/DeliveryAssignmentDomainService.cs
│   ├── Exceptions/ConcurrencyConflictException.cs
│   └── Interfaces/IUserRepository.cs
├── Talabat.Application/
│   ├── Abstractions/ICurrentUser.cs
│   ├── Abstractions/IUserCapabilityService.cs
│   ├── Common/Results/
│   ├── Customers/
│   └── Ordering/Checkout/CheckoutHandler.cs
├── Talabat.Infrastructure/
│   ├── Identity/
│   │   ├── IdentityDataSeeder.cs
│   │   ├── IdentityRoleNames.cs
│   │   ├── TalabatSignInManager.cs
│   │   └── UserCapabilityService.cs
│   └── Persistence/
│       ├── Configurations/UserConfiguration.cs
│       ├── Repositories/UserRepository.cs
│       ├── Migrations/<timestamp>_InitialUnifiedUser.cs
│       ├── Migrations/<timestamp>_InitialUnifiedUser.Designer.cs
│       └── Migrations/TalabatDbContextModelSnapshot.cs
├── Talabat.Identity/
├── Talabat.API/                          # Customer API assembly
└── Talabat.Delivery.API/                 # compile-only scaffold

tests/
├── Talabat.Application.Tests/
├── Talabat.Infrastructure.Tests/
├── Talabat.Identity.Tests/
└── Talabat.Customer.API.Tests/
```

The following production files are removed during the compile-break window:

```text
src/Talabat/Talabat.Domain/Aggregates/Customer/Customer.cs
src/Talabat/Talabat.Domain/Aggregates/Customer/CustomerAddress.cs
src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/DeliveryAgent.cs
src/Talabat/Talabat.Domain/Interfaces/ICustomerRepository.cs
src/Talabat/Talabat.Domain/Interfaces/IDeliveryAgentRepository.cs
src/Talabat/Talabat.Infrastructure/Identity/ApplicationUser.cs
src/Talabat/Talabat.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs
src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryAgentConfiguration.cs
src/Talabat/Talabat.Infrastructure/Persistence/Repositories/CustomerRepository.cs
src/Talabat/Talabat.Infrastructure/Persistence/Repositories/DeliveryAgentRepository.cs
```

## Phase 0: Research

All technical unknowns are resolved in [research.md](research.md). The binding decisions are:

| ID | Decision |
|---|---|
| R1 | Phase 2 implementation stops until Phase 1 is accepted, committed, clean, green, and vulnerability-free. |
| R2 | Use `IdentityDbContext<User, IdentityRole<int>, int>` and let Identity configure the generated user key. |
| R3 | Regenerate migration source before SQL integration tests; defer the actual authorized development DB drop/update until after the full green clean checkpoint. |
| R4 | Capability methods share the scoped `TalabatDbContext`; own a transaction when none exists and use a savepoint when joining an existing transaction. |
| R5 | Follow the exact six workflow sequences and failure mappings, including duplicate/missing targets, applicant normalization, idempotent deactivation, rollback, and tracker cleanup. |
| R6 | Use four server-owned role constants and idempotent role-definition seeding; fail startup if a missing role cannot be created. |
| R7 | Override `CanSignInAsync`, refresh the security stamp after role changes/deactivation, and configure an exact five-minute cookie validator; live-cookie elapsed-time proof remains Phase 3. |
| R8 | Resolve a positive integer preferred subject and query only persisted `UserType`; malformed/non-positive subjects are unauthenticated for `/api/me/*`. |
| R9 | Preserve the governing available-agent query; approval and capability follow from the aggregate state machine, while the query filter excludes deleted users. |
| R10 | Map the unified User, eight checks, owned addresses, retained FKs, and SQL rowversion without reconfiguring the Identity key. |
| R11 | Extract interface-based audit/query-filter mapping without changing existing entity behavior. |
| R12 | Preserve current address removal behavior; the shadow `IsDeleted` column supports the specified index but Phase 2 does not invent an address soft-delete workflow. |
| R13 | Preserve existing login/logout and `ProfileNotCreated` bytes; expose two server-owned registration contracts with numeric user IDs. |
| R14 | Integration fixtures must use the new migration, interceptor, integer users, and seeded roles; exact compatibility assertions replace permissive status ranges. |
| R15 | Parse both destructive connection targets exactly and reject six negative server/catalog/integrated-security cases before an EF process starts. |
| R16 | Use host-specific Identity bootstrap: one full `AddIdentity` chain in Identity, and a shared `AddIdentityCore`/roles/stores extension in Customer and Delivery hosts. |

## Phase 1: Design And Contracts

### Data model

[data-model.md](data-model.md) defines:

- the unified `User` persisted in `AspNetUsers`;
- the owned `UserAddress` table and active-default uniqueness rule;
- customer, application, approval, operational, activation, deletion, and concurrency state;
- retained Cart/Order/Delivery business-key relationships;
- legal state transitions and capability/role projection boundaries; and
- the exact audit, query-filter, and rowversion behavior.

### Application and repository ports

[contracts/application-ports.md](contracts/application-ports.md) freezes the exact Phase 2 members
of `ICurrentUser`, `IUserRepository`, and `IUserCapabilityService`, the server-owned role map, and
the `UseCaseResult` failure categories. Implementations may add private helpers but must not add a
caller-provided role argument or expose EF/Identity types through these contracts.

### HTTP contract

[contracts/identity-api.md](contracts/identity-api.md) freezes the two registration requests,
numeric success response, unchanged login/logout shapes, numeric `/account/me` response, and exact
Customer API `ProfileNotCreated` bodies. No admin approval endpoint is created in Phase 2.

### Persistence contract

[contracts/persistence-schema.md](contracts/persistence-schema.md) freezes tables, columns, eight
checks, index filters, FKs, role rows, and the one-migration history target. Generated migration
code must be reviewed against this contract before a database is touched.

## Phase 2: Planning Handoff

The complete solution is expected to be temporarily uncompilable from WP1 through WP8A. Do not
change the schema-affecting foundation order to chase intermediate compiler errors, do not build
after deleting each legacy type, and do not touch the development database in that window.

### WP0 — Hard preflight (STOP if any check fails)

**Inspect**: branch, `git status --short`, Phase 1 tasks T029/T030, Phase 1 implementation commit,
solution build/tests, and the vulnerable-package report.

**Required state**:

- branch is `feature/user-aggregate-refactor`;
- Phase 1 T001–T030 are accepted;
- the Delivery API's inherited `Microsoft.OpenApi` vulnerability is resolved under approved scope;
- `dotnet build src/Talabat/Talabat.slnx` passes;
- `dotnet test src/Talabat/Talabat.slnx` passes;
- `dotnet list ... --vulnerable --include-transitive` reports no vulnerable packages; and
- Phase 1 is committed and `git status --short` is empty.

**Done gate**: record the Phase 1 commit. Planning completion does not satisfy this gate.

### WP1 — Switch the Identity persistence root

**Edit**:

- `TalabatDbContext.cs`: derive from `IdentityDbContext<User, IdentityRole<int>, int>`, remove
  `Customers`/`DeliveryAgents`, retain all other DbSets, and keep `base.OnModelCreating` first.
- Delete `Infrastructure/Identity/ApplicationUser.cs`.

**Do not** add a duplicate `DbSet<User>`; use Identity's inherited `Users`. Do not build yet.

**Done gate**: no production DbContext generic references `ApplicationUser`, string Identity roles,
or a string Identity key.

### WP2 — Remove the retired aggregates and move shared agent enums

**Delete** the nine legacy aggregate/interface/configuration/repository files listed in Project
Structure (the tenth deletion, `ApplicationUser`, occurred in WP1).

**Move** `VehicleType.cs` and `DeliveryAgentStatus.cs` from `Aggregates/DeliveryManagement/` to
`Aggregates/Users/`; change their namespace and update `User.cs`, `IUserCapabilityService`, delivery
code, and tests.

**Done gate**: there is one Domain `User` aggregate, no account/profile linkage type, and no
duplicate delivery-agent state enum. Do not build yet.

### WP3 — Retarget delivery behavior without changing semantics

**Edit** `DeliveryAssignmentDomainService.cs`:

- change all four `DeliveryAgent agent` parameters and the private helper to `User agent`;
- compare `agent.DeliveryAgentStatus` to `DeliveryAgentStatus.Busy`;
- preserve `agent.Id`, `IsAvailable`, internal `MarkBusy`/`MarkAvailable`, exception types, and
  delivery transition order.

**Done gate**: no delivery Domain service references the removed class; names `AssignedAgentId` and
`DeliveryAgentStatus` remain intact. Do not build yet.

### WP4 — Map the unified User and retarget FKs

**Edit** `MappingConventions.cs`:

- extract all existing audit/deletion column and query-filter mapping into
  `ConfigureAuditing<TEntity>() where TEntity : class, IAuditable, ISoftDeletable`;
- keep `ConfigureAuditableEntity<TEntity>()` and make it delegate to the new method; and
- do not change existing column types, lengths, defaults, or filters.

**Add** `UserConfiguration.cs` exactly as defined in the persistence contract. It must configure
all eight checks, full-name/flags/activation/enums/location/rowversion, interface audit mapping,
query filter, `_addresses` owned rows, and the filtered default index. It must not call
`ConfigureIdentityKey`.

**Edit** `CartConfiguration.cs`, `OrderConfiguration.cs`, and `DeliveryConfiguration.cs` so their
customer and agent principals are `User`; retain property names, indexes, and `DeleteBehavior.Restrict`.

**Done gate**: a model review accounts for every `User` field and every old Customer/Agent FK. Do
not scaffold a migration yet.

### WP5 — Implement repository and concurrency translation

**Add** `UserRepository.cs` with the exact query semantics in the Application port contract,
including the literal backing-field include `Include("_addresses")`.

**Edit** `UnitOfWork.cs` to catch `DbUpdateConcurrencyException` and throw the existing Domain
`ConcurrencyConflictException`.

**Edit** `ApplicationErrorCodes.cs` and `DomainExceptionMapper.cs`:

- add `ConcurrencyConflict`, `IdentityOperationFailed`, and `UserNotFound` codes;
- map `ConcurrencyConflictException` explicitly to Conflict/HTTP 409; and
- retain every existing error mapping.

All handlers whose `SaveChangesAsync` is inside a `try` must catch/map `DomainException` as well as
their existing argument failures; do not let concurrency fall into the API's generic 400 handler.

**Done gate**: repository signatures expose no EF types and a business rowversion conflict has one
deterministic Application error code.

### WP6 — Implement the capability workflow and host-neutral Identity services

**Add** under `Infrastructure/Identity/`:

- `IdentityRoleNames.cs`: internal constants for the exact four role names;
- `UserCapabilityService.cs`: all six port methods and one private transaction helper;
- `IdentityDataSeeder.cs`: idempotent role definition creation with fail-fast errors; and
- `UnifiedUserIdentityServiceCollectionExtensions.cs`: public
  `AddUnifiedUserIdentityCore(IServiceCollection)` returning the exact
  `AddIdentityCore<User>().AddRoles<IdentityRole<int>>()
  .AddEntityFrameworkStores<TalabatDbContext>()` builder for non-Identity hosts.

**Edit** `Infrastructure/DependencyInjection.cs`:

- remove dead Customer/DeliveryAgent repository registrations;
- register `IUserRepository`, `IUserCapabilityService`, and `IdentityDataSeeder`; and
- do not call `AddIdentityCore` or `AddIdentity` inside `AddInfrastructure`; hosts own exactly one
  bootstrap choice from R16.

Follow the exact method sequences and result mappings in `research.md` and
`contracts/application-ports.md`. The service may use only the scoped `TalabatDbContext` and
`UserManager<User>`; it must not inject `IUserRepository`, accept role names, or grant an applicant
the DeliveryAgent role/flag before approval.

**Done gate**: every user-role membership call in production is inside `UserCapabilityService`,
role seeding creates definitions only, and Infrastructure exposes one core-store extension without
registering authentication schemes.

### WP7 — Retarget Application use cases

**Edit**:

- `ICurrentUser` to the exact v2 contract;
- `CreateCustomerProfileCommand` from string linkage ID to integer `UserId`;
- `CreateCustomerProfileHandler` to delegate only to
  `IUserCapabilityService.GrantCustomerCapabilityAsync`;
- Get/Update/Add/Remove/SetDefault handlers from `ICustomerRepository` to `IUserRepository`;
- `CustomerMapper` from `Customer` to `User`, retaining DTOs and handling nullable `Age` with an
  explicit invariant failure rather than a fabricated value; and
- `CheckoutHandler` from `ICustomerRepository` to `IUserRepository`.

Keep all non-create business command properties named `CustomerId`. Keep response models named
`CustomerProfile` and `CustomerAddressDetails`. Do not inject `UserManager` into Application.

**Done gate**: `Talabat.Application` has zero retired repository/aggregate/linkage references and
no Identity/EF/web package or type.

### WP8A — Retarget schema-neutral host foundations

**Identity host foundation before the first build/migration**:

- call exactly one full `AddIdentity<User, IdentityRole<int>>()` chain with EF stores and default
  token providers; do not call `AddUnifiedUserIdentityCore` in this host;
- use Duende `.AddAspNetIdentity<User>()`;
- run `IdentityDataSeeder` in a startup scope before serving requests; and
- retarget login/logout/Me to integer `User`, remove the generic registration route, preserve
  login/logout shapes, and make `/account/me.id` a JSON number.

The foundation may use the framework's default `SignInManager<User>` so the compile window closes.
US3 replaces that registration with `TalabatSignInManager` and adds the five-minute validator before
full acceptance.

**Customer API foundation**:

- call `AddUnifiedUserIdentityCore` after `AddInfrastructure`, and do not call full `AddIdentity`;
- implement cached positive-int subject resolution and one no-tracking scalar `UserType` lookup;
- make a malformed/non-positive subject an empty 401 on `/api/me/*` even if authentication produced
  a principal;
- change only `HasProfile` reads to `HasCustomerCapability` in the existing anonymous
  `ProfileNotCreated` branches; and
- pass `UserId` only for customer-profile creation and retain `CustomerId` elsewhere.

**Delivery API foundation**: call `AddUnifiedUserIdentityCore` after `AddInfrastructure`, make no
business/API change, and verify only that it compiles and its dependency audit is clean.

**Done gate**: every host has exactly one R16 bootstrap path, hosts compile conceptually against
integer users, the generic registration route is absent, and exact compatibility strings remain
unedited.

### WP8B — Complete schema-neutral user stories after migration generation

- **US1** adds `/account/register/customer` and its Application/real-SQL/Identity tests.
- **US2** adds `/account/register/delivery-agent` plus service-level approval/rejection tests; no
  approval transport is added.
- **US3** adds `TalabatSignInManager`, replaces the default manager registration through
  `.AddSignInManager<TalabatSignInManager>()`, configures the exact five-minute validator, and proves
  login rejection plus atomic session-validity refresh. Live-cookie elapsed-time testing remains
  Phase 3.
- **US4** completes compatibility, persistence, concurrency, and delivery behavior tests.

These changes are schema-neutral and may follow WP11. Execute stories in task order because they
overlap Identity and capability-service files.

**Done gate**: both frozen registration routes, sign-in policy, five-minute configuration, and all
P1 compatibility behavior pass without caller roles, approval endpoints, or response drift.

### WP9A — Migrate structural foundations in all four test projects

Use the file inventory in `research.md` before the first build:

- replace `FakeCustomerRepository` with `FakeUserRepository` and add
  `FakeUserCapabilityService`;
- mechanically migrate existing customer/checkout unit tests without weakening assertions;
- replace separate customer/agent persistence fixtures with unified-user arrangements;
- register integer Identity core/roles/stores and the audit interceptor in SQL test providers;
- retarget Identity test factories to integer users and migration-backed databases; and
- seed isolated integer unified users plus four role definitions in Customer API test databases.

**Done gate**: test projects compile conceptually against the foundation and contain no removed
type or string-linkage reference. Story-specific assertions may still be pending.

### WP9B — Add story acceptance tests after migration generation

- add real-SQL `UserCapabilityServiceTests` for customer registration/onboarding, applicant state,
  approval/rejection, duplicate rollback, deactivation/stamps, and missing-role rollback;
- retarget Identity endpoint tests to both registration routes and numeric IDs;
- add inactive and soft-deleted login rejection plus five-minute configuration inspection;
- support customer, non-customer, conflicting preferred claim, and malformed/non-positive subject
  cases without test-order coupling;
- assert both exact raw `ProfileNotCreated` bodies; and
- complete persistence, concurrency, available-agent, and delivery-domain behavior coverage.

Tests may directly arrange aggregate state only when the behavior under test is not the capability
workflow. Production code must never copy that shortcut.

Within one story, tests in disjoint files may be parallelized. Tests sharing a file are sequential.

**Done gate**: all story acceptance tests pass against the reviewed migration and external response
assertions were not rewritten to accommodate production regressions.

### WP10 — Close the compile-break window

Run restore/build and fix only foundation compile errors from WP1–WP9A. Then run fast Application
tests. Do not run SQL integration tests yet because the old migration chain is incompatible with
the new integer-key model. Schema-neutral WP8B/WP9B story behavior follows migration generation. Do
not touch either configured development database.

**Done gate**: solution build and Application tests pass with the replacement runtime model.

### WP11 — Regenerate migration source for disposable test databases

This is the sole ordered correction to the governing task block:

1. Remove the current three migration pairs, old snapshot, and old idempotent script from source.
2. Run `dotnet ef migrations add InitialUnifiedUser` with Infrastructure as project and Customer API
   as startup project.
3. Review the generated migration and snapshot against `contracts/persistence-schema.md`.
4. Confirm the migration contains no legacy Customers/DeliveryAgents tables, string user keys, or
   `IdentityUserId` linkage.

These steps change repository files only; they do **not** drop or update the configured development
database. SQL integration fixtures need this migration because they call `Database.MigrateAsync`
against isolated disposable databases.

**Done gate**: the migration directory contains only the `InitialUnifiedUser` pair and snapshot,
and `dotnet ef migrations has-pending-model-changes` reports no pending change.

### WP12 — Complete WP8B/WP9B and run full Phase 2 acceptance

Implement and independently validate US1, US2, US3, and US4 in that order using the reviewed
migration for disposable SQL fixtures. The story grouping does not change the EF model produced by
WP11.

Run, in order:

1. solution build;
2. all four test projects/whole solution;
3. removed-symbol sweeps excluding generated/`obj` output;
4. package vulnerability audit with transitive dependencies; and
5. migration count/model-pending checks.

Any failure returns to its owning work package. Do not drop the development database and do not
weaken expected customer response bodies.

**Done gate**: all commands in the pre-rebuild section of `quickstart.md` pass.

### WP13 — Checkpoint before destructive work

Review the Phase 2 diff for scope and generated schema correctness. Update tracked task progress
through the checkpoint task, then commit the Phase 2 code, planning artifacts, tests, and
`InitialUnifiedUser` migration using the repository's approved workflow. Do not write that commit's
hash back into a tracked file before the rebuild; capture it in the execution transcript. Require
`git status --short` to be empty.

From the clean checkpoint through WP14 and the remaining read-only acceptance commands, defer
checkbox/evidence edits in `tasks.md`. This exception prevents progress bookkeeping from defeating
the mandatory clean-tree gate. Final task evidence is written and committed only in WP15.

**Done gate**: a recoverable Phase 2 commit exists and the worktree is clean. If the user does not
authorize a commit, stop before the rebuild and report that the code/test portion is ready.

### WP14 — Exact-gated disposable development rebuild

Follow `quickstart.md` exactly:

1. parse both development connection strings and require normalized data source
   `DESKTOP-5IHGJ9F\\SQLEXPRESS` and catalog `Talabat`;
2. require integrated security and run the six R15 negative cases—server, catalog, and integrated
   security for each configuration source—without starting an EF process;
3. recheck empty status plus green build/tests immediately before destruction;
4. drop the database through EF using the Customer API startup project;
5. apply the already-reviewed `InitialUnifiedUser` migration;
6. start the Identity host twice to prove idempotent role seeding; and
7. query tables, checks, indexes, FKs, roles, users, and migration history.

If apply/seeding/inspection fails after the disposable database was dropped, fix the committed
source in a new change, re-run code gates, checkpoint again, then repeat drop/update. There is no
data restoration path by design.

**Done gate**: schema inspection matches the persistence contract exactly.

### WP15 — Phase 2 handoff and final evidence commit

Run the final build/test/audit/schema and symbol gates without changing tracked files. Obtain final
evidence-commit authorization before editing tracked evidence. If authorized, update all deferred
task checkboxes and evidence, including the pre-rebuild checkpoint hash, and create the final
evidence commit. The last task marks itself complete before this commit and does not write the final
evidence commit's own hash into the repository, avoiding a circular dirty-tree record. Require
`git status --short` to be empty after the commit. If authorization is unavailable, leave tracked
files unchanged and hand off the complete evidence transcript.

Do not start Phase 3 work, add Customer API role-policy wiring, implement a Delivery API, or update
the superseded Phase 7 documents in this increment.

**Done gate**: all Phase 2 acceptance criteria are met and the remaining work is explicitly Phase 3.

## Dependency Order

```text
WP0
  -> WP1 -> WP2 -> WP3 -> WP4 -> WP5 -> WP6 -> WP7 -> WP8A -> WP9A
  -> WP10 (first whole-solution build)
  -> WP11 (migration source only)
  -> WP8B/US1 -> WP8B/US2 -> WP8B/US3 -> WP8B/US4 with WP9B tests
  -> WP12 (full SQL-backed acceptance)
  -> WP13 (commit + clean tree)
  -> WP14 (six negative gates + authorized dev DB drop/update/inspect)
  -> WP15 (final evidence commit + clean tree)
```

Only disjoint test files within WP9A/WP9B may be parallelized; shared files remain sequential.
Production WP1–WP8A share foundation symbols and must remain ordered. WP8B stories are
schema-neutral but remain sequential because their Identity/controller/service files overlap.
Migration generation depends on the final EF model, and SQL-backed story tests depend on the
generated migration.

## Complexity Tracking

| Design choice | Why needed | Simpler alternative rejected |
|---|---|---|
| Savepoint when joining an existing transaction | A returned failure after an internal `SaveChanges` must not leave partial workflow changes that an outer caller can commit | Merely avoiding commit/rollback of the ambient transaction does not guarantee atomic failure results |
| Empty 401 for malformed `/api/me/*` subjects | `[Authorize]` may accept a principal even when the application cannot derive a valid integer user; controllers currently dereference customer IDs | Setting only `ICurrentUser.IsAuthenticated = false` can allow the action to continue |
| Migration source before full SQL test gate | Both SQL fixtures call `MigrateAsync`; the legacy migration chain creates incompatible string-key/legacy tables | Waiting until after all tests creates an impossible circular gate; changing fixtures to `EnsureCreated` would stop testing migrations |
| Exact parsed connection safety check | A substring such as `Database=Talabat` can also match an unsafe catalog such as `TalabatProd` | Text-only matching is insufficient before an authorized destructive operation |

## Post-Design Constitution Check

| Gate | Result after design |
|---|---|
| Dependency direction | PASS—hosts → Infrastructure → Application → Domain remains intact |
| Single model/no linkage | PASS—every runtime, persistence, and test path targets `User`; deletion/sweeps are explicit |
| Capability mutation boundary | PASS—Application and controllers delegate; one Infrastructure workflow owns flag/role membership changes |
| Authentication boundary | PASS—Identity owns one full `AddIdentity`/custom sign-in chain; Customer and Delivery use only the R16 core-store extension; current-user translation remains host-specific |
| Compatibility | PASS—retained names and exact raw bodies are contractual test fixtures |
| Persistence integrity | PASS—mapping and generated migration have an independent review contract |
| Concurrency | PASS—rowversion, Domain exception, Application conflict, and HTTP 409 chain is complete |
| Testability | PASS—unit, real-SQL workflow, Identity, Customer API, schema, symbol, package, five-minute configuration, and six negative connection gates are identified |
| Destructive safety | PASS—source regeneration is non-database; actual drop remains after six exact negative checks and a clean committed green state; tracked evidence is deferred until the final commit |
| Scope | PASS—no Phase 3 policy/ownership work, Delivery API implementation, admin UI/controller, frontend, discounts, or data preservation |

The plan is ready for task generation, but implementation remains blocked by WP0.
