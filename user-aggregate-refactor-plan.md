# Implementation Plan — Unified `User` Aggregate Refactor

**Branch**: `feature/user-aggregate-refactor`
**Date**: 2026-07-18 (v2 — rebuilt from the final Q1–Q34 decisions; supersedes the v1 plan previously in this file)
**Status**: Approved for implementation (instructor-directed architecture override)
**Governance**: `.specify/memory/constitution.md` v3.0.0 ("Current Phase Scope: User Aggregate Refactor")
**Execution model**: 3 phases, executed phase by phase with Spec Kit. Each phase becomes its own
Spec Kit specification; the "Spec Kit handoff" block at the end of each phase lists exactly what to
pass in. Do not create the Spec Kit artifacts before a phase starts.

**Repo facts**: repo root `D:\link-dev\talabat`; solution `src/Talabat/Talabat.slnx`
(6 src projects, 4 test projects); .NET 10; EF Core / ASP.NET Identity 10.0.9; Duende 8.0.2;
dev DB `Server=DESKTOP-5IHGJ9F\SQLEXPRESS;Database=Talabat` (in
`src/Talabat/Talabat.API/appsettings.Development.json` and
`src/Talabat/Talabat.Identity/appsettings.Development.json`).

---

## Key design decisions (owned by this plan, not the implementation model)

**D1 — `UserType` is a `[Flags]` enum.** One person holds several types at once (an agent who shops
as a customer). A single-value enum cannot represent `Customer | DeliveryAgent`; a separate junction
table would duplicate what `AspNetUserRoles` already stores. Flags give: one column, no joins for
Domain checks, cheap extension (next value = 16), and a natural Domain-side mirror of the role set:

```csharp
[Flags]
public enum UserType { None = 0, Customer = 1, DeliveryAgent = 2, Admin = 4, RestaurantOwner = 8 }
```

**D2 — `UserType` is the Domain source of truth; Identity roles are a synchronized authorization
projection.** Domain code may branch on flags but never on `UserManager`/role lookups. Roles power
`[Authorize]`/policies in hosts. Because two stores describing the same fact will drift, **every**
change to flags or roles goes through one workflow service (`IUserCapabilityService`, implemented in
Infrastructure) that mutates both inside one DB transaction. No controller or handler touches
`AddToRoleAsync` or `UserType` directly.

**D3 — Concurrency token is a SQL `rowversion` column (`User.RowVersion`), not `ConcurrencyStamp`.**
`ConcurrencyStamp` only rotates when `UserManager` performs an update — business writes through
`IUserRepository`/`SaveChangesAsync` would not rotate it, so two concurrent business writers
(location ping vs. address edit) would **not** conflict. `rowversion` is bumped by SQL Server on
every UPDATE regardless of writer, covering Identity writes, business writes, and the capability
workflow with zero code obligations. `ConcurrencyStamp` remains (Identity uses it internally) but is
not the business mechanism.

**D4 — Naming.** Aggregate `User` (property `UserType UserType`); `UserAddress` / table
`UserAddresses` / FK `UserId`; enum names `DeliveryAgentStatus`, `VehicleType`,
`AgentApprovalStatus` kept business-specific (no vague `UserStatus`); `CustomerId` and
`AssignedAgentId` kept — they name the *role the User plays in the operation*, while the FK targets
`AspNetUsers.Id`; existing DTO/error names (`CustomerProfile`, `ProfileNotCreated`) kept as business
language.

**D5 — Pending agent approval** is a nullable `AgentApprovalStatus` enum on `User`
(`PendingApproval = 1, Approved = 2, Rejected = 3`). It is the smallest state that keeps agent data
inside the aggregate: an applicant has `VehicleType` + `PendingApproval` but **no** `DeliveryAgent`
flag, **no** role, and `DeliveryAgentStatus == null` until approval (then `Offline`).

**D6 — Domain package** = `Microsoft.Extensions.Identity.Stores` 10.0.9 — the smallest package
containing `IdentityUser<TKey>`; no ASP.NET web stack enters Domain.

**Global assumptions**
- No `Talabat.Domain.Tests` project exists — new Domain behavior tests live in
  `tests/Talabat.Application.Tests` under a `Domain/Users/` folder (avoids new-project churn).
- Capability **revocation** (removing Customer/DeliveryAgent from an account that owns orders or
  deliveries) is intentionally **not implemented** in this refactor — the only disablement
  mechanisms are `Deactivate()` and soft delete.
- The dev database is disposable; the destructive rebuild in Phase 2 is authorized, gated by the
  connection-string safety check.

---

# Phase 1 — Unified User Domain Model (additive; old model still running)

### 1. Business goal
Establish "one person = one account with multiple capabilities" as a fully-behaved Domain aggregate,
proven by unit tests — without changing any running behavior yet.

### 2. Business capabilities introduced
The `User` aggregate can: be registered as a person; be initialized as a Customer (profile +
addresses); apply to be a DeliveryAgent; be approved/rejected; operate as an agent
(online/offline/suspend/busy/location); be deactivated. None of this is wired to hosts yet.

### 3. Business rules and invariants that must remain valid
All existing rules, now expressed on `User`: required `FullName`; positive `Age` when
customer-initialized; duplicate-address rejection; at most one default address; agent status machine
(`Offline/Available/Busy/Suspended` with the exact transition rules in
`src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/DeliveryAgent.cs`); vehicle required to
apply; approval precedes any agent operation; `DeliveryAgentStatus` starts `Offline` on approval and
is `null` before.

### 4. User journeys affected
None at runtime — Customer API, Delivery scaffold, and Identity host behave exactly as today. (This
is what makes the phase independently shippable.)

### 5. Domain behaviors preserved / moved / replaced
- **Moved (verbatim bodies)** from `Customer`: `UpdateProfile`→`UpdateCustomerProfile`,
  `AddAddress`, `RemoveAddress`, `SetDefaultAddress`, `CreateDeliveryAddressSnapshot`, plus the
  private `GetRequiredAddress`/`MarkAllAddressesAsNonDefault` helpers. From `DeliveryAgent`:
  `IsAvailable`, `GoOnline`, `GoOffline`, `Suspend`, `internal MarkBusy`, `internal MarkAvailable`,
  `UpdateLocation` — same exceptions (`AgentNotAvailableException`,
  `InvalidDeliveryAgentStatusTransitionException`, `DuplicateAddressException`,
  `AddressNotFoundException`), same messages.
- **Replaced**: `Customer.CreateForAccount` → `User.Register` + `InitializeCustomerProfile`
  (the linkage-key concept disappears; the account *is* the profile). `DeliveryAgent` constructor →
  `SubmitDeliveryAgentApplication` + `ApproveDeliveryAgentApplication`.
- **New**: capability guards `RequireCustomer()` / `RequireAgent()`, `Activate`/`Deactivate`,
  approval lifecycle.

### 6–7. Cross-layer / authorization contracts established
`IUserRepository` (Domain interface) and `IUserCapabilityService` (Application abstraction) are
defined now so Phases 2–3 cannot invent their own shapes. Roles are *not* referenced anywhere in
Domain.

### 8–9. Invalid states, failures, edge cases handled in the aggregate
- Customer operation without the Customer flag → `CustomerProfileNotInitializedException`.
- Agent operation when `DeliveryAgentStatus == null` or flag missing →
  `DeliveryAgentNotInitializedException`.
- `ApproveDeliveryAgentApplication`/`Reject…` when status isn't `PendingApproval` →
  `AgentApplicationNotPendingException` (covers double-approval and approve-after-reject).
- Re-submitting after rejection: allowed — `SubmitDeliveryAgentApplication` may run again from
  `Rejected` (sets `PendingApproval` again); it must throw if already `Approved`.
- `InitializeCustomerProfile` on a user already holding the flag: the aggregate call is not
  idempotence-safe by design — the workflow checks `ProfileAlreadyExists` first (documented so the
  implementation model does not "fix" it in Domain).
- Busy agent must never be suspendable/offline-able (existing rules preserved verbatim).

### 10. Database constraints
None yet (Phase 2). Phase 1 is persistence-free by design.

### 11. Transactions & concurrency
None yet; the `RowVersion` property exists on the aggregate (initialized `[]`) so Phase 2 can map it
without touching Domain again.

### Ordered tasks (each small and deterministic)

1. **STOP-AND-VERIFY**: `git branch --show-current` = `feature/user-aggregate-refactor`; commit the
   entire uncommitted working tree:
   `git add -A; git commit -m "Checkpoint before unified User refactor"`.
   Run `dotnet build src/Talabat/Talabat.slnx` and `dotnet test src/Talabat/Talabat.slnx`; record
   both green. Do not proceed on a red baseline.
2. Edit `src/Talabat/Talabat.Domain/Talabat.Domain.csproj`: add
   `<ItemGroup><PackageReference Include="Microsoft.Extensions.Identity.Stores" Version="10.0.9" /></ItemGroup>`.
3. Add `src/Talabat/Talabat.Domain/Common/Abstractions/IAuditable.cs` and `ISoftDeletable.cs`:

   ```csharp
   public interface IAuditable
   {
       DateTime CreatedAt { get; }  string? CreatedBy { get; }
       DateTime? ModifiedAt { get; } string? ModifiedBy { get; }
       void SetCreatedAudit(DateTime createdAt, string? createdBy);
       void SetModifiedAudit(DateTime modifiedAt, string? modifiedBy);
   }
   public interface ISoftDeletable
   {
       bool IsDeleted { get; } DateTime? DeletedAt { get; } string? DeletedBy { get; }
       void SoftDelete(DateTime deletedAt, string? deletedBy);
       void Restore(DateTime restoredAt, string? restoredBy);
   }
   ```

   Change `AuditableEntity` to `public abstract class AuditableEntity : IAuditable, ISoftDeletable`
   — no member changes (they already match).
4. In `AuditableEntitySaveChangesInterceptor.StampAuditFields` change
   `Entries<AuditableEntity>()` → `Entries<IAuditable>()` (makes the interceptor stamp `User` in
   Phase 2 automatically). Build — still green.
5. Add `src/Talabat/Talabat.Domain/Aggregates/Users/UserType.cs` (D1) and
   `AgentApprovalStatus.cs` (D5).
6. Add four exceptions in `src/Talabat/Talabat.Domain/Exceptions/` (one file each,
   `: DomainException`, matching the existing style): `CustomerProfileNotInitializedException`,
   `DeliveryAgentNotInitializedException`, `AgentApplicationNotPendingException`,
   `ConcurrencyConflictException`.
7. Add `src/Talabat/Talabat.Domain/Aggregates/Users/UserAddress.cs`: exact port of
   `CustomerAddress.cs` (int `Id`, `Address Details`, `bool IsDefault`, private materialization
   ctor, `internal` ctor, `internal MarkAsDefault/MarkAsNonDefault`), new namespace.
   Do **not** delete `CustomerAddress` yet.
8. Add `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs` with exactly this surface (bodies per
   §5 above):

   ```csharp
   public sealed class User : IdentityUser<int>, IAuditable, ISoftDeletable
   {
       private readonly List<UserAddress> _addresses = [];
       public string FullName { get; private set; }
       public int? Age { get; private set; }
       public UserType UserType { get; private set; }
       public bool IsActive { get; private set; }
       public VehicleType? VehicleType { get; private set; }
       public DeliveryAgentStatus? DeliveryAgentStatus { get; private set; }
       public GeoLocation? CurrentLocation { get; private set; }
       public AgentApprovalStatus? AgentApprovalStatus { get; private set; }
       public byte[] RowVersion { get; private set; }
       // IAuditable/ISoftDeletable: copy AuditableEntity's seven properties + four method bodies verbatim
       public IReadOnlyCollection<UserAddress> Addresses => _addresses.AsReadOnly();

       private User() { FullName = string.Empty; RowVersion = []; }
       public static User Register(string userName, string email, string fullName);
       // sets UserName/Email, FullName (Guard.RequiredText), IsActive = true, UserType = None
       public void InitializeCustomerProfile(string fullName, int age, string? phoneNumber);
       // Guard age positive; sets fields incl. inherited PhoneNumber; UserType |= UserType.Customer
       public void UpdateCustomerProfile(string fullName, int age, string? phoneNumber);   // RequireCustomer()
       public void AddAddress(Address address, bool makeDefault = false);                  // RequireCustomer()
       public void RemoveAddress(int addressId);                                           // RequireCustomer()
       public void SetDefaultAddress(int addressId);                                       // RequireCustomer()
       public DeliveryAddressSnapshot CreateDeliveryAddressSnapshot(int addressId);        // RequireCustomer()
       public void SubmitDeliveryAgentApplication(VehicleType vehicleType);
       // validates enum; throws if already Approved; sets VehicleType, AgentApprovalStatus = PendingApproval
       public void ApproveDeliveryAgentApplication();
       // requires PendingApproval; sets Approved; UserType |= DeliveryAgent; DeliveryAgentStatus = Offline
       public void RejectDeliveryAgentApplication();       // PendingApproval -> Rejected
       public bool IsAvailable();                          // DeliveryAgentStatus == Available
       public void GoOnline(); public void GoOffline(); public void Suspend();
       internal void MarkBusy(); internal void MarkAvailable();
       public void UpdateLocation(GeoLocation location);   // RequireAgent()
       public void Activate(); public void Deactivate();   // toggles IsActive
       private void RequireCustomer(); // !UserType.HasFlag(Customer) -> CustomerProfileNotInitializedException
       private void RequireAgent();    // status null or flag missing -> DeliveryAgentNotInitializedException
   }
   ```

   `PhoneNumber` writes go to the inherited property via `Guard.OptionalText`.
   `VehicleType`/`DeliveryAgentStatus` still live in `Talabat.Domain.Aggregates.DeliveryManagement`
   this phase — reference them with a `using`; they move in Phase 2.
9. Add `src/Talabat/Talabat.Domain/Interfaces/IUserRepository.cs`:

   ```csharp
   public interface IUserRepository
   {
       Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);               // tracked
       Task<User?> GetByIdReadOnlyAsync(int userId, CancellationToken ct = default);       // AsNoTracking
       Task<User?> GetByIdWithAddressesAsync(int userId, CancellationToken ct = default);  // tracked + addresses
       Task<IReadOnlyCollection<User>> GetAvailableAgentsAsync(CancellationToken ct = default);
       void Update(User user);
   }
   ```

   (No `AddAsync` — creation is `UserManager`'s job.)
10. Add `src/Talabat/Talabat.Application/Abstractions/IUserCapabilityService.cs`:

    ```csharp
    public interface IUserCapabilityService
    {
        Task<UseCaseResult<int>> RegisterCustomerAsync(string email, string password, string fullName, int age, string? phoneNumber, CancellationToken ct = default);
        Task<UseCaseResult<int>> RegisterDeliveryAgentApplicantAsync(string email, string password, string fullName, VehicleType vehicleType, string? phoneNumber, CancellationToken ct = default);
        Task<UseCaseResult<int>> GrantCustomerCapabilityAsync(int userId, string fullName, int age, string? phoneNumber, CancellationToken ct = default);
        Task<UseCaseResult<int>> ApproveDeliveryAgentAsync(int userId, CancellationToken ct = default);
        Task<UseCaseResult<int>> RejectDeliveryAgentAsync(int userId, CancellationToken ct = default);
        Task<UseCaseResult<int>> DeactivateUserAsync(int userId, CancellationToken ct = default);
    }
    ```

11. Add Domain behavior tests in `tests/Talabat.Application.Tests/Domain/Users/`:
    `UserCustomerCapabilityTests.cs` (initialize/update/guards), `UserAddressInvariantTests.cs`
    (duplicate rejected, one default, remove/set-default, snapshot), `UserAgentLifecycleTests.cs`
    (apply→approve→`Offline`; apply→reject→re-apply; approve twice throws; every legal/illegal
    status transition; guards on uninitialized users), `UserAccountStateTests.cs`
    (Register defaults: `IsActive = true`, `UserType.None`; Deactivate/Activate).

### Phase 1 endings

- **Acceptance criteria**: solution builds; **all pre-existing tests unchanged and green**; new
  Domain tests green; no file outside the listed set touched; `Customer`/`DeliveryAgent` still
  fully operational.
- **Required tests**: the four new test files above (business behavior, not coverage padding).
- **Build/validation**: `dotnet build src/Talabat/Talabat.slnx` ·
  `dotnet test src/Talabat/Talabat.slnx`.
- **Searches**: `Microsoft.AspNetCore.Identity` in `Talabat.Domain.csproj` → nothing (only
  `Microsoft.Extensions.Identity.Stores` allowed);
  `UserManager|ClaimsPrincipal|HttpContext` in `src/Talabat/Talabat.Domain` → nothing.
- **Risks / hidden problems**: nullable `RowVersion` warnings (init `[]`); the
  `User.DeliveryAgentStatus` property references an enum still in another namespace this phase —
  if the compiler complains, use the fully-qualified type name in the property declaration.
- **Rollback**: `git revert` Phase-1 commits; nothing depends on them.
- **Spec Kit handoff**: feature name "unified-user-domain-model"; inputs = this section + D1–D6 +
  the exact file list and skeletons; acceptance = the criteria above; constraint = "additive only;
  do not delete or modify Customer/DeliveryAgent".

---

# Phase 2 — Identity + Persistence Cutover and Clean Database Rebuild

### 1. Business goal
One physical account per person becomes real: registration, login, capability grants, and agent
approval operate on the unified `User`; the database holds one user table; the old profile tables
cease to exist.

### 2. Business capabilities introduced/changed
- Customer registration (server assigns Customer flag + role automatically).
- DeliveryAgent applicant registration (account created, **no** role/flag until approval).
- Admin approval/rejection of agent applications at service level (no admin UI; creating
  Admin/RestaurantOwner users is a future admin workflow — only the roles are seeded).
- Existing-account customer onboarding (`GrantCustomerCapabilityAsync`) — the delivery employee who
  wants to shop uses the **same** account.
- Account deactivation that blocks login.

### 3. Business rules and invariants
All Phase-1 aggregate rules now DB-backed (see §10). Plus: flags and roles never diverge (D2); a
caller never chooses their role — the initiating flow only selects *which* server workflow runs; an
unapproved applicant cannot appear in `GetAvailableAgentsAsync` (their `DeliveryAgentStatus` is
`null`, never `Available`).

### 4. User journeys affected
- **Identity host**: `POST /account/register` is replaced by `POST /account/register/customer` and
  `POST /account/register/delivery-agent`; `Login`/`Logout` unchanged in shape; `Me` returns int
  `Id`. Login now *rejects* inactive/soft-deleted users.
- **Customer Website/API**: behaviorally unchanged this phase (the 401/404/409 `ProfileNotCreated`
  contract is preserved) — internally, "has a profile" now means "has the Customer capability".
- **Delivery Website/API**: still a scaffold; must merely compile.

### 5. Domain behaviors preserved/moved/replaced
- Delete aggregates `Customer` (+`CustomerAddress`) and `DeliveryAgent`; their behavior already
  lives on `User` (Phase 1).
- `DeliveryAssignmentDomainService`: all four methods change parameter `DeliveryAgent agent` →
  `User agent`; `EnsureAssignedBusyAgent` compares
  `agent.DeliveryAgentStatus != DeliveryAgentStatus.Busy`; `agent.Id` stays int. Business semantics
  identical, including "one active delivery per agent" (enforced by the Busy state exactly as today).
- Move `VehicleType.cs` and `DeliveryAgentStatus.cs` from `Aggregates/DeliveryManagement/` to
  `Aggregates/Users/` (namespace updates in `User.cs`, Delivery-related files,
  `DeliveryAssignmentDomainService`, tests).

### 6. Cross-aggregate and cross-layer rules
`Cart.CustomerId`, `Order.CustomerId`, `Delivery.CustomerId`, `Delivery.AssignedAgentId` remain
`int` and keep their names; their FKs now target `AspNetUsers.Id`. `Guard.Positive` checks in
`Order`, `Delivery`, `Cart` remain valid (int identity ≥ 1).

### 7. Authorization / ownership / roles
- Role seed: exactly `Customer`, `DeliveryAgent`, `Admin`, `RestaurantOwner` — idempotent, at
  Identity-host startup, via `RoleManager<IdentityRole<int>>.RoleExistsAsync` guard (no `HasData`:
  role rows carry `ConcurrencyStamp` churn in migrations). No seeded users.
- `sub` claim = `User.Id` int (Duende `AddAspNetIdentity<User>` emits it).
  `Talabat.API/Auth/CurrentUser.cs` parses it with `int.TryParse` and resolves capability from the
  DB — so a stale JWT minted before a capability change can never grant business capability.
- Login gate: custom `TalabatSignInManager : SignInManager<User>` overriding `CanSignInAsync` →
  deny when `!user.IsActive || user.IsDeleted`. The soft-delete query filter *also* hides deleted
  users from `UserManager.FindByNameAsync`, but that is defense-in-depth, not the rule (the filter
  would silently miss `IsActive = false`).

### 8. Invalid states and failure cases
- Registration with an existing email → `IdentityResult` failure → validation `UseCaseResult`
  failure; nothing persisted (transaction rollback).
- `GrantCustomerCapabilityAsync` on a user who already has the flag → `ProfileAlreadyExists`
  conflict (reuses `ApplicationErrorCodes.ProfileAlreadyExists`).
- Approve on non-pending application → `AgentApplicationNotPendingException` → conflict category.
- Any role-sync step failing after the domain mutation → the whole transaction rolls back; flags and
  roles both revert (the drift-prevention guarantee; tested in Phase 3).
- Deactivated user with a live cookie: workflow calls `UpdateSecurityStampAsync`; the Identity host
  configures `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.FromMinutes(5)`.

### 9. Edge cases (non-obvious)
- **Role changed while sessions exist**: cookie principals cache role claims —
  `UserCapabilityService` calls `userManager.UpdateSecurityStampAsync(user)` after *any* role delta.
  JWTs cannot be revoked, which is exactly why the Customer API's operative gate is the DB
  capability lookup, not token roles.
- **Query filter vs. Identity internals**: `HasQueryFilter(u => !u.IsDeleted)` on `User` means
  soft-deleted users vanish from *every* `UserManager` path. Intended; do not "fix" with
  `IgnoreQueryFilters`.
- **`UserManager` saves inside a use case**: `CreateAsync`/`AddToRoleAsync` call `SaveChanges`
  internally — which is why they are confined to `UserCapabilityService`'s transaction and the
  Identity host; Application handlers keep `IUserRepository` + `IUnitOfWork` and never inject
  `UserManager`.
- **Auditing of `User`**: the interceptor (retargeted to `IAuditable` in Phase 1) stamps `User`
  automatically — including rows created *by `UserManager`*, because it shares `TalabatDbContext`.
- **Existing carts/orders if capability were revoked**: revocation is out of scope; `Deactivate()`
  blocks login without orphaning data.
- **Dropped indexes/constraints**: `UX_Customers_IdentityUserId` disappears with its table (nothing
  replaces it — linkage is gone); `CK_Customers_Age_Positive` and the five `CK_DeliveryAgents_*`
  checks are recreated as `CK_Users_*` (§10); `UX_CustomerAddresses_CustomerId_Default` is recreated
  as `UX_UserAddresses_UserId_Default`. Any omission is silent data-integrity loss — the schema
  verification in task 12 lists all of them.

### 10. Database constraints (all NULL-tolerant; never join role tables)
On `AspNetUsers`:
- `CK_Users_Age` — `([Age] IS NULL OR [Age] > 0)`
- `CK_Users_VehicleType` — `([VehicleType] IS NULL OR [VehicleType] IN (1, 2, 3))`
- `CK_Users_DeliveryAgentStatus` — `([DeliveryAgentStatus] IS NULL OR [DeliveryAgentStatus] IN (1, 2, 3, 4))`
- `CK_Users_AgentApprovalStatus` — `([AgentApprovalStatus] IS NULL OR [AgentApprovalStatus] IN (1, 2, 3))`
- `CK_Users_UserType_Range` — `([UserType] >= 0 AND [UserType] <= 15)`
- `CK_Users_CurrentLocation_PairedNull`, `CK_Users_CurrentLatitude_Range`,
  `CK_Users_CurrentLongitude_Range` — ported verbatim from `DeliveryAgentConfiguration`.

On `UserAddresses`: filtered unique `UX_UserAddresses_UserId_Default`
(`[IsDefault] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit)`).

Capability-conditional rules that would need role joins (e.g., "Age required for customers") are
enforced in Domain/Application only.

### 11. Transaction and concurrency boundaries
- **Capability workflow**: one transaction per public method. Helper pattern: if
  `_dbContext.Database.CurrentTransaction is null` begin one; domain mutation →
  `SaveChangesAsync` → role deltas via `UserManager` (enlist automatically — same scoped context) →
  security-stamp update → commit; any failure → rollback + failure result.
- **Business use cases**: unchanged single `IUnitOfWork.SaveChangesAsync` boundary.
- **Concurrency**: `RowVersion` mapped `IsRowVersion()` (D3). `UnitOfWork.SaveChangesAsync` catches
  `DbUpdateConcurrencyException` → throws Domain `ConcurrencyConflictException` → new
  `DomainExceptionMapper` case → `ApplicationErrorCategory.Conflict` → HTTP 409 via the existing
  ProblemDetails pipeline. Address-only edits touch `UserAddresses` rows (not the user row) —
  concurrent edits of *different* addresses don't conflict; accepted behavior.

### Ordered tasks

*(Temporary broken state warning: steps 2–10 form the compile-break window — the solution will not
build between deleting the old aggregates and finishing the handler/API/test retargeting. Complete
the whole sequence, then build. Never start the DB rebuild (step 12) before the build is green.)*

1. **STOP-AND-VERIFY**: Phase 1 accepted and committed; clean `git status`; baseline build + tests
   green.
2. `TalabatDbContext`: base → `IdentityDbContext<User, IdentityRole<int>, int>`; delete
   `DbSet<Customer>` and `DbSet<DeliveryAgent>` properties. Delete
   `src/Talabat/Talabat.Infrastructure/Identity/ApplicationUser.cs`.
3. Delete files: `Aggregates/Customer/Customer.cs`, `Aggregates/Customer/CustomerAddress.cs`,
   `Aggregates/DeliveryManagement/DeliveryAgent.cs`, `Interfaces/ICustomerRepository.cs`,
   `Interfaces/IDeliveryAgentRepository.cs`,
   `Persistence/Configurations/CustomerConfiguration.cs`,
   `Persistence/Configurations/DeliveryAgentConfiguration.cs`,
   `Persistence/Repositories/CustomerRepository.cs`,
   `Persistence/Repositories/DeliveryAgentRepository.cs`.
   Move `VehicleType.cs`/`DeliveryAgentStatus.cs` to `Aggregates/Users/` (§5).
4. Retarget `DeliveryAssignmentDomainService` per §5.
5. Add `Persistence/Configurations/UserConfiguration.cs` implementing §10 plus: `FullName` max 200
   required; `UserType` `HasConversion<int>()`; `IsActive` default `true`; nullable int conversions
   for the three nullable enums; `CurrentLocation` via existing `ConfigureGeoLocation`; `RowVersion`
   `IsRowVersion()`; audit mapping via a new
   `MappingConventions.ConfigureAuditing<TEntity>() where TEntity : class, IAuditable, ISoftDeletable`
   (extract the body of `ConfigureAuditableEntity`, which now delegates to it), including the
   `!IsDeleted` query filter; the `OwnsMany("_addresses", …)` block ported from
   `CustomerConfiguration` with table `UserAddresses`, FK `UserId`, soft-delete shadow property, the
   filtered default index, `Ignore(u => u.Addresses)`, field access mode.
   Do **not** call `ConfigureIdentityKey` for `User` (Identity maps the int key as SQL IDENTITY).
6. Retarget FK configs: in `CartConfiguration`, `OrderConfiguration`, `DeliveryConfiguration`:
   `HasOne<CustomerAggregate>()` → `HasOne<User>()` (3 places) and Delivery's agent relation
   `HasOne<DeliveryAgent>()` → `HasOne<User>()`. Property names untouched.
7. Add `Persistence/Repositories/UserRepository.cs` (implements `IUserRepository`; patterns copied
   from the deleted `CustomerRepository` — `Include("_addresses")` for the addresses load;
   `GetAvailableAgentsAsync` =
   `Where(u => u.DeliveryAgentStatus == DeliveryAgentStatus.Available).OrderBy(u => u.FullName).AsNoTracking()`).
8. Add `Infrastructure/Identity/UserCapabilityService.cs` (per §7, §8, §11; role map
   `Customer→"Customer"`, `DeliveryAgent→"DeliveryAgent"`, `Admin→"Admin"`,
   `RestaurantOwner→"RestaurantOwner"`), `Infrastructure/Identity/TalabatSignInManager.cs`,
   `Infrastructure/Identity/IdentityDataSeeder.cs` (§7). Update
   `Infrastructure/DependencyInjection.cs`: remove the two dead repository registrations; add
   `IUserRepository`→`UserRepository`, `IUserCapabilityService`→`UserCapabilityService`, and
   `services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<TalabatDbContext>();`
   so `UserManager`/`RoleManager` resolve in every host.
9. Application rework:
   - `ICurrentUser` → `bool IsAuthenticated; int? UserId; bool HasCustomerCapability; int? CustomerId;`
     (`CustomerId` = `UserId` when capable, else null — keeps consumers compiling with business
     language intact).
   - `Customers/CreateProfile/*`: command becomes
     `(int UserId, string FullName, int Age, string? PhoneNumber)`; handler delegates to
     `IUserCapabilityService.GrantCustomerCapabilityAsync` (repository/UoW usage removed —
     capability changes only via the workflow, D2).
   - `GetProfile`/`UpdateProfile`/`AddAddress`/`RemoveAddress`/`SetDefaultAddress` handlers +
     commands: `ICustomerRepository`→`IUserRepository`, `GetByIdentityUserIdAsync(string)`→
     `GetByIdAsync/GetByIdWithAddressesAsync(int)`, `string IdentityUserId`→`int UserId`.
     `CustomerMapper` maps `User`→`CustomerProfile`/`CustomerAddressDetails` (DTO names kept, D4).
   - `Ordering/Checkout/CheckoutHandler`: `ICustomerRepository`→`IUserRepository`; the
     `RequireCustomer()` guard inside `CreateDeliveryAddressSnapshot` enforces "no checkout without
     Customer capability".
   - Add `ApplicationErrorCodes.ConcurrencyConflict` + `DomainExceptionMapper` case (§11) and
     `ApplicationErrorCodes.IdentityOperationFailed` for mapped `IdentityResult` errors.
10. Host fixes:
    - **Identity** `Program.cs` —
      `AddIdentity<User, IdentityRole<int>>()…AddEntityFrameworkStores<TalabatDbContext>().AddDefaultTokenProviders().AddSignInManager<TalabatSignInManager>()`;
      `.AddAspNetIdentity<User>()`; stamp-validation interval (§8); seeder invocation in a scope
      before `app.Run()`. `AccountController` — replace `Register` with the two endpoints (§4)
      delegating to `IUserCapabilityService`; `Me` returns int id.
    - **Customer API** — `CurrentUser` parses int `sub` (`ClaimTypes.NameIdentifier` then `"sub"`),
      single `Select(u => u.UserType)` lookup; `ProfileEnforcementFilter` reads
      `HasCustomerCapability` (response bodies and status codes byte-identical); controllers pass
      `UserId`/`CustomerId` per new command shapes.
    - **Delivery API** — verify it still compiles; change nothing.
11. Test compile-and-pass migration (whole suite must be green at phase end):
    `FakeCustomerRepository`→`FakeUserRepository`; new `FakeUserCapabilityService`;
    `TestData`/`PersistenceTestData` build `User.Register(...)` + capability calls;
    `CustomerPersistenceTests` + `DeliveryAgentPersistenceTests` → `UserPersistenceTests`;
    `AuditAndSoftDeleteTests` extended to `User`; `ConstraintPersistenceTests` uses `CK_Users_*`
    names; `Talabat.Identity.Tests/AccountEndpointTests` targets the two new endpoints;
    `Talabat.Customer.API.Tests/Infrastructure/TestAuthHandler` emits int `sub` + `role` claim,
    `CustomWebApplicationFactory` seeds `User` rows. Existing endpoint assertions (401/404/409,
    payloads) must pass **unchanged** — if one fails, the production change is wrong, not the test.
12. **STOP-AND-VERIFY, then destructive DB rebuild** (only after full green build):

    ```powershell
    git status --short                    # must be clean (commit first)
    Select-String -Path src/Talabat/Talabat.API/appsettings.Development.json, `
      src/Talabat/Talabat.Identity/appsettings.Development.json -Pattern "Database=Talabat"
    # STOP unless both are Server=DESKTOP-5IHGJ9F\SQLEXPRESS;Database=Talabat (local dev only)
    dotnet ef database drop --force --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API
    Remove-Item src/Talabat/Talabat.Infrastructure/Persistence/Migrations/* -Force     # incl. snapshot + InitialPersistence.idempotent.sql
    dotnet ef migrations add InitialUnifiedUser --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API --output-dir Persistence/Migrations
    dotnet ef database update --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API
    dotnet run --project src/Talabat/Talabat.Identity      # seeds roles; Ctrl+C after startup
    sqlcmd -S DESKTOP-5IHGJ9F\SQLEXPRESS -d Talabat -E -Q "SELECT name FROM sys.tables ORDER BY name; SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('AspNetUsers'); SELECT name FROM sys.indexes WHERE object_id=OBJECT_ID('UserAddresses'); SELECT Name FROM AspNetRoles;"
    ```

    Verify: no `Customers`/`DeliveryAgents` tables; the 8 `CK_Users_*` constraints;
    `UX_UserAddresses_UserId_Default`; 4 roles; `Carts`/`Orders`/`Deliveries` FKs reference
    `AspNetUsers`.

### Phase 2 endings

- **Acceptance criteria**: solution builds; **all four test suites green**; schema verification
  passes; customer registration produces flag + role atomically; agent applicant has neither until
  approval; deactivated/soft-deleted users cannot log in.
- **Required tests** (beyond migrated ones): `UserCapabilityServiceTests` (Infrastructure.Tests,
  real SQL fixture): register-customer → flags + roles both present; applicant → neither; approve →
  both + `Offline`; email-duplicate registration → nothing persisted. `LoginRejectionTests`
  (Identity.Tests): inactive + soft-deleted → 401.
- **Build/validation**: `dotnet build src/Talabat/Talabat.slnx` ·
  `dotnet test src/Talabat/Talabat.slnx` · the rebuild block above.
- **Searches** (production code, exclude `obj/`): `ApplicationUser`, `ICustomerRepository`,
  `IDeliveryAgentRepository`, `CustomerRepository`, `DeliveryAgentRepository`, `IdentityUserId`,
  `IdentityDbContext<ApplicationUser`, `class Customer\b`, `class DeliveryAgent\b` → zero hits.
  (`CustomerId`, `AssignedAgentId`, `CustomerProfile` are intentionally present.)
- **Risks / hidden problems**: the compile-break window (mitigated by task order); forgetting
  `UpdateSecurityStampAsync` after role deltas (stale cookie claims); forgetting one CHECK
  constraint (schema verification lists all 8); `Include("_addresses")` string must match the field
  name exactly; EF warning about the query-filtered principal (`User`) with required dependents —
  pre-existing pattern, ignore.
- **Rollback**: `git revert` the Phase-2 range, then re-run the rebuild block against the reverted
  code (the DB is disposable in both directions).
- **Spec Kit handoff**: feature "unified-user-identity-persistence-cutover"; inputs = §§1–11 + the
  ordered tasks + D2/D3; preconditions = Phase 1 accepted; acceptance = the criteria and searches
  above; explicit permission granted for the destructive rebuild with the connection-string safety
  gate.

---

# Phase 3 — Business Behavior Proof, Customer API Hardening, Governance

### 1. Business goal
Prove the multi-role person works end-to-end across the three sites, harden ownership/authorization
at the Customer API, and leave zero contradictory documentation.

### 2–4. Capabilities, rules, journeys
- **Journey A (agent becomes customer)**: delivery applicant registered + approved → logs in on the
  customer site → `POST /api/me/profile` grants Customer capability to the *same* account →
  browses, carts, checks out. The `DeliveryAgent` role stays visible in the principal (future
  employee offers — no discount logic now).
- **Journey B (customer stays customer on delivery site)**: a Customer-only user is *not*
  assignable to deliveries (Application role check + Domain guard).
- **Journey C (blocked account)**: deactivated/deleted users are rejected at login and their
  sessions die on stamp refresh.
- Customer API: every `/api/me/*` operation resolves ownership from `ICurrentUser.CustomerId` only —
  never a route/body id; missing capability keeps returning the documented 404/409
  `ProfileNotCreated`.

### 5–7. Domain / cross-layer / authorization
Customer API adds `options.TokenValidationParameters.RoleClaimType = "role"` so role claims
materialize in the principal; the *operative* business gate remains the DB capability lookup
(stale-JWT defense). Delivery assignment call sites (currently the domain service + tests) document
the two-level rule: Application checks the role, Domain checks profile/status.

### 8–9. Invalid states / edge cases to pin with tests
Drift-on-failure (role step fails → flags rolled back); double-approve; re-apply after reject;
concurrent writes to one user; `sub` claim that is not an int (tampered token → treated as
unauthenticated); user A reading user B's order (404, not 403 — no existence leak, matching current
behavior).

### 10–11. Constraints / transactions
No new schema. Concurrency behavior surfaced to HTTP is verified here (409 ProblemDetails).

### Ordered tasks

1. Customer API: role-claim wiring (§5–7) + confirm no controller accepts a caller-supplied customer
   id (search `FromRoute.*customerId|FromBody.*CustomerId` in `src/Talabat/Talabat.API/Controllers`
   → only DTO-internal reads allowed).
2. Add business tests:
   - `CapabilityRoleDriftTests` (Infrastructure.Tests): after every workflow method,
     `AspNetUserRoles` == role-projection of `UserType`; failure injection (delete the
     `DeliveryAgent` role row, then approve) → rollback proven, flags unchanged.
   - `MultiRoleJourneyTests` (Identity.Tests): Journey A end-to-end at service/endpoint level; one
     `User.Id` throughout; both roles present.
   - `AgentAssignmentAuthorizationTests` (Application.Tests): assignment of a Customer-only `User` →
     `DeliveryAgentNotInitializedException`; approved-but-Offline agent →
     `AgentNotAvailableException`; Busy agent completing/cancelling → releases to `Available`.
   - `ConcurrencyConflictTests` (Infrastructure.Tests): two contexts, conflicting user-row writes →
     `ConcurrencyConflictException`; Customer.API test asserting 409 ProblemDetails.
   - `OwnershipTests` (Customer.API.Tests): cross-user order/cart access → 404; non-int `sub` → 401.
   - `SessionInvalidationTests` (Identity.Tests): deactivate → cookie rejected after stamp
     validation.
3. Documentation: verify `.specify/memory/constitution.md` v3.0.0 still matches the implementation
   (flags, rowversion, capability workflow, retained names — amend only if a Phase 1–2 detail
   diverged); update `AGENTS.md`'s increment section to "Phase 3 of the refactor"; add
   superseded-by notes atop `specs/003-customer-api/spec.md`, `plan.md`, `data-model.md`; rewrite
   `docs/authorization-matrix.md` (`sub` = int `User.Id`; `ProfileNotCreated` = missing Customer
   capability; role list; approval flow); add the same note to `phase-7-architecture-guide.md`.
4. Final gate: `dotnet restore` + `build` + `test` on the slnx;
   `dotnet ef migrations list --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API`
   → exactly `InitialUnifiedUser`; re-run the Phase-2 symbol searches plus
   `grep -ri "MUST NOT inherit" .specify docs AGENTS.md` → nothing active;
   `dotnet list src/Talabat/Talabat.Domain package` → only `Microsoft.Extensions.Identity.Stores`.

### Phase 3 endings

- **Acceptance criteria**: all suites green including the six new test groups; Journey A
  demonstrable end-to-end; docs contain no active contradiction; Delivery API scaffold untouched
  and compiling.
- **Build/validation**: commands in task 4.
- **Risks**: contract drift while adding tests (pin with the existing `AuthEnforcementTests`); doc
  updates silently missed — the grep in task 4 is the guard.
- **Rollback**: revert Phase-3 commits; Phase-2 state remains fully functional.
- **Spec Kit handoff**: feature "unified-user-behavior-and-governance"; inputs = journeys A–C, the
  six test groups with their assertions, the documentation file list; preconditions = Phase 2
  accepted with green suite and rebuilt DB; acceptance = task-4 gate.

---

## Phase dependencies

Phase 1 depends on nothing; Phase 2 depends only on Phase 1's merged types; Phase 3 depends only on
Phase 2's running system. No phase references a decision deferred to a later phase — all decisions
are fixed in D1–D6 above.
