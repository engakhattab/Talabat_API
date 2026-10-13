# Research: Unified User Domain Model (Phase 1)

**Date**: 2026-07-18  
**Spec**: [spec.md](spec.md)  
**Governing plan**: [user-aggregate-refactor-plan.md](../../user-aggregate-refactor-plan.md)  
**Status**: Complete; no unresolved clarifications

## R1: Unified Account Type and Domain Dependency

**Decision**: Introduce `User : IdentityUser<int>` in `Talabat.Domain` and add exactly one direct
package reference there: `Microsoft.Extensions.Identity.Stores` 10.0.9. The version matches the
solution's existing ASP.NET Identity and EF Core 10.0.9 line.

**Rationale**: The approved architecture makes the Identity account and business user one aggregate.
`Microsoft.Extensions.Identity.Stores` is the smallest package that supplies `IdentityUser<int>`;
it does not require adding ASP.NET Core web, EF Core, or host dependencies to Domain. `Id` remains a
database-generated `int` and therefore remains `0` in Phase 1 objects until later persistence.

**Alternatives considered**:

- Keep `ApplicationUser` plus separate Customer and DeliveryAgent aggregates: rejected by the
  instructor-approved unified-user decision.
- Add an `IdentityUserId` linkage property: rejected because the account is the profile.
- Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` or a web package to Domain: rejected as a
  constitution violation.

## R2: Capability Representation

**Decision**: Represent capabilities with `[Flags] UserType` values `None = 0`, `Customer = 1`,
`DeliveryAgent = 2`, `Admin = 4`, and `RestaurantOwner = 8`. Domain behavior adds flags with bitwise
OR and never replaces the whole set.

**Rationale**: One person may hold several capabilities at once. A flags value gives Domain code a
framework-free source of truth while preserving Customer when DeliveryAgent is granted and vice
versa. Admin and RestaurantOwner are introduced as available classifications only; their grant
workflows are not part of Phase 1.

**Alternatives considered**:

- Single-value enum: rejected because it cannot represent multi-role users.
- Domain role lookups: rejected because authorization roles are an Infrastructure projection and
  are not referenced in Phase 1.
- A second Domain capability table: rejected as duplicate state with no Phase 1 persistence need.

## R3: Registration and Customer Initialization

**Decision**: `User.Register(userName, email, fullName)` assigns the inherited username/email,
requires a non-blank full name, sets `IsActive = true`, and starts with `UserType.None`. It does not
perform Identity credential or email-policy validation; that belongs to the Phase 2 workflow using
Identity services. `InitializeCustomerProfile` validates full name, positive age, and optional phone,
then adds the Customer flag. Calling it again directly replaces the valid customer profile values;
the future capability workflow owns the `ProfileAlreadyExists` idempotency check.

**Rationale**: Phase 1 provides a behavior-rich aggregate without prematurely reproducing Identity
validation or workflow concerns in Domain. This also follows the governing plan's explicit rule
that direct customer initialization is not idempotence-safe.

**Alternatives considered**:

- Validate passwords/email policy in Domain: rejected because that would duplicate Identity rules.
- Make Domain initialization silently idempotent: rejected because it would hide duplicate
  onboarding; the workflow must return the existing `ProfileAlreadyExists` contract later.

## R4: Address Ownership and Equality

**Decision**: Port `CustomerAddress` to `UserAddress` without changing behavior. `User` owns a
private list and exposes a read-only view. Duplicate detection uses the existing `Address` value
equality: case-insensitive comparison of Street, City, BuildingNumber, and Floor. At most one
address is default; removing the default may leave no default. Non-positive address IDs fail
`Guard.Positive` with `ArgumentOutOfRangeException`; unknown positive IDs produce
`AddressNotFoundException`.

**Rationale**: Exact behavioral porting reduces refactor risk and keeps `UserAddress` inside the User
aggregate boundary. It also preserves the delivery snapshot contract and existing exception
messages.

**Alternatives considered**:

- Give `UserAddress` its own repository: rejected by aggregate ownership rules.
- Auto-select another default after removal: rejected because it changes existing behavior.
- Compare only a subset of address fields: rejected because it changes `Address` value semantics.

## R5: Delivery-Agent Application and Operational Lifecycles

**Decision**: Keep application approval separate from operational status. A supported vehicle is one
of Bike, Motorcycle, or Car. Initial or rejected applications become `PendingApproval` with no
DeliveryAgent flag and no operational status. A pending submission may be submitted again to refresh
the vehicle while staying pending; only an already approved application is rejected at submission.
Approval adds the flag and starts `Offline`; rejection leaves the flag absent and status null.
Approval/rejection from any non-pending state throws `AgentApplicationNotPendingException`.

The existing Offline/Available/Busy/Suspended state machine is ported verbatim. `MarkBusy` and
`MarkAvailable` stay internal. Every agent operation first requires both the DeliveryAgent flag and
non-null operational status; location updates are permitted in every initialized agent status.

**Rationale**: Approval state answers whether the person may be an agent; operational state answers
whether an approved agent can receive work. Keeping them separate prevents applicants from entering
delivery operations and preserves the current assignment invariants.

**Alternatives considered**:

- Give applicants an Offline status: rejected because it makes an unapproved user look operational.
- Grant DeliveryAgent at application time: rejected by the approval requirement.
- Make reservation/release public: rejected because only controlled delivery coordination may call
  those transitions.

## R6: Audit and Soft-Deletion Abstractions

**Decision**: Extract `IAuditable` and `ISoftDeletable` contracts whose members exactly match
`AuditableEntity`; make `AuditableEntity` implement both without changing its methods. `User`
implements both directly by copying the seven properties and four method bodies. Generalize the
existing save interceptor from `Entries<AuditableEntity>()` to `Entries<IAuditable>()`.

**Rationale**: `User` must inherit `IdentityUser<int>` and cannot also inherit `AuditableEntity`.
Interface-based audit discovery supports both lineages while preserving existing auditing,
soft-delete idempotency, UTC guards, and modified-audit updates.

**Alternatives considered**:

- Duplicate the interceptor for User: rejected as behavior duplication.
- Add a second base class through composition wrappers: rejected because C# has single inheritance
  and wrappers would not match Identity's entity requirements.
- Change existing audit behavior: rejected because Phase 1 is additive.

## R7: Repository and Capability Workflow Contracts

**Decision**: Add `IUserRepository` in Domain for tracked, read-only, and address-inclusive loads,
available-agent listing, and update marking. It deliberately has no add method because account
creation belongs to Identity's user workflow. Add `IUserCapabilityService` in Application with the
six exact operations fixed by the governing plan; it accepts credentials/profile values or a user
ID, never caller-supplied role names. Phase 1 defines but does not implement either contract.

**Rationale**: Freezing the boundaries now prevents Phase 2 handlers, hosts, and Infrastructure from
inventing conflicting shapes. It also keeps `UserManager` out of Domain and out of ordinary business
repositories.

**Alternatives considered**:

- Add `IUserRepository.AddAsync`: rejected because it would compete with Identity account creation.
- Put `UserManager` in Application or Domain: rejected by dependency rules.
- Accept arbitrary roles in capability methods: rejected as an authorization escalation risk.

## R8: Concurrency Preparation

**Decision**: Add `byte[] RowVersion` to User and initialize it to `[]`. Add a Domain
`ConcurrencyConflictException`, but do not map or throw it from persistence in Phase 1.

**Rationale**: The later cutover needs a SQL rowversion that covers Identity and business writes.
Adding the Domain surface and failure type now prevents another aggregate edit during Phase 2 while
keeping Phase 1 persistence-free.

**Alternatives considered**:

- Use `ConcurrencyStamp`: rejected because it does not change for all repository-driven business
  writes.
- Implement conflict mapping now: rejected because there is no unified-user persistence in Phase 1.

## R9: Test Placement and Internal Transition Coverage

**Decision**: Put four xUnit files under
`tests/Talabat.Application.Tests/Domain/Users/`: `UserCustomerCapabilityTests`,
`UserAddressInvariantTests`, `UserAgentLifecycleTests`, and `UserAccountStateTests`. Reuse the
existing reflection-based test-ID helper pattern for `UserAddress.Id`. Add an SDK
`InternalsVisibleTo` item for `Talabat.Application.Tests` in the already-modified Domain project so
`UserAgentLifecycleTests` can exercise internal reservation/release transitions without making them
public. Phase 1 does not yet retarget the delivery assignment service to User.

**Rationale**: No Domain test project exists, and Application.Tests already references Domain. A
friend-test assembly preserves the production API boundary, keeps transition tests compile-time
checked, and changes no additional production file beyond the Domain project already touched for
the approved package.

**Alternatives considered**:

- Make `MarkBusy`/`MarkAvailable` public for tests: rejected because it weakens the aggregate API.
- Invoke internal transitions through reflection: rejected because a supported friend-assembly
  declaration is clearer and compile-time checked.
- Add a separate assembly metadata file: rejected because the SDK project item avoids expanding the
  exact Phase 1 file set.
- Retarget `DeliveryAssignmentDomainService` in Phase 1: rejected because that is a Phase 2 cutover
  task.

## R10: Additive Sequencing and Acceptance

**Decision**: Execute the governing plan's Phase 1 steps in order: checkpoint and establish a green
baseline; add the one Domain package; introduce audit interfaces; generalize audit discovery; add
enums/exceptions; add UserAddress and User; define repository/workflow contracts; add tests; then run
the full build, full test suite, and dependency symbol sweeps. Do not delete or retarget any existing
Customer/DeliveryAgent production component.

**Rationale**: The old runtime remains the rollback path and keeps Phase 1 independently shippable.
Ordered, additive changes minimize compile-break windows and isolate any regression.

**Alternatives considered**:

- Combine Phase 1 with Identity/persistence cutover: rejected because it removes the safe additive
  checkpoint and expands the failure surface.
- Start the database rebuild early: rejected; database work is explicitly Phase 2.
