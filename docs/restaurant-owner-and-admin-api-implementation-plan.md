# Talabat — Restaurant Owner API & Admin API

## Technical Implementation Plan

**Target branch base:** `feature/user-aggregate-refactor`
**Solution:** `src/Talabat/Talabat.slnx` — .NET 10, EF Core 10.0.9, ASP.NET Identity 10.0.9, Duende IdentityServer 8.0.2
**Audience:** an implementing coding agent
**Document status:** execution plan — every section is intended to be actioned, not read as background

---

## 0. Scope statement (read first)

Restaurant-owner and admin workflows are listed as **Phase 11 / advanced features** in
`PROJECT_IMPLEMENTATION_ROADMAP.md`, and `docs/phase-4.5-identity-auth-foundation-plan.md` says:

> Reserve `DeliveryOperations`, `Admin`, and `RestaurantOwner` only as candidate names. Do not create
> roles with no approved use cases. In particular, do not introduce `RestaurantOwner` until restaurant
> ownership exists in the business model.

This plan is the explicit scope extension that lifts that hold. It does two things the docs require
before the roles are allowed to exist:

1. It **models restaurant ownership in the business model** (§4 D2, §7 Phase 2).
2. It gives each role **approved use cases** before its policy is created (§11).

Everything outside those two roles stays deferred: no payment, no notifications, no coupons, no
reviews, no analytics/BI, no real-time tracking, no restaurant-staff sub-roles, no fine-grained admin
permission system. §16 is the explicit deferred backlog.

---

## 1. Verified repository baseline

This section is fact, read from the branch. Do not re-derive it; do not contradict it.

### 1.1 Projects

| Project | Folder | Notes |
|---|---|---|
| `Talabat.Domain` | `src/Talabat/Talabat.Domain` | References **`Microsoft.Extensions.Identity.Stores` 10.0.9** (sanctioned exception, see §4 D0) |
| `Talabat.Application` | `src/Talabat/Talabat.Application` | Only `Microsoft.Extensions.DependencyInjection.Abstractions`. CQRS-lite, no MediatR |
| `Talabat.Infrastructure` | `src/Talabat/Talabat.Infrastructure` | EF Core SqlServer, Identity.EntityFrameworkCore, `FrameworkReference Microsoft.AspNetCore.App` |
| `Talabat.Customer.API` | `src/Talabat/**Talabat.API**` | ⚠ folder name ≠ assembly name |
| `Talabat.Delivery.API` | `src/Talabat/Talabat.Delivery.API` | |
| `Talabat.Identity` | `src/Talabat/Talabat.Identity` | Duende + ASP.NET Identity, Razor login pages |

Test projects: `Talabat.Application.Tests`, `Talabat.Domain.Tests`, `Talabat.Infrastructure.Tests`,
`Talabat.Customer.API.Tests`, `Talabat.Delivery.API.Tests`, `Talabat.Identity.Tests`,
`Talabat.ArchitectureTests`.

### 1.2 Domain aggregates as they exist today

```
Aggregates/
  Users/            User (root), UserAddress (child), UserType[Flags], VehicleType,
                    DeliveryAgentStatus, AgentApprovalStatus
  Catalog/          Restaurant (root), Product (child)
  Basket/           Cart (root), CartItem (child), CartStatus
  Ordering/         Order (root), OrderItem (child)
  DeliveryManagement/ Delivery (root), DeliveryStatus
```

Repository contracts in `Talabat.Domain/Interfaces/`: `IUserRepository`, `IRestaurantRepository`,
`ICartRepository`, `IOrderRepository`, `IDeliveryRepository`, `IUnitOfWork`. No child repositories.

**Five facts that shape this entire plan:**

1. **`UserType` already declares the flags we need.**
   ```csharp
   [Flags] public enum UserType { None = 0, Customer = 1, DeliveryAgent = 2, Admin = 4, RestaurantOwner = 8 }
   ```
   `Admin` and `RestaurantOwner` are declared but **no code sets them and no behaviour reads them**.

2. **`IdentityRoleNames` already declares all four roles** and `IdentityDataSeeder.SeedRolesAsync`
   already seeds `Admin` and `RestaurantOwner`. **No user is ever put in them.**

3. **`UserCapabilityService.SynchronizeCapabilityRolesAsync` already projects all four flags to
   roles**, inside a transaction, and calls `UpdateSecurityStampAsync`. The write path for the new
   roles already exists — only the grant/revoke entry points are missing.

4. **`Restaurant` has no owner.** No `OwnerUserId`, no `RowVersion`, no `UpdateProfile`, no
   `UpdateOpeningHours`, no product-detail update. `Product`'s mutators are `internal`, so the
   aggregate boundary is compiler-enforced.

5. **`Order` is immutable and has no status.** `CreateFromCheckout` + `GetTotal()` only. There is no
   restaurant-side order lifecycle anywhere in the system.

### 1.3 Application patterns in force

- **CQRS-lite, no MediatR.** One folder per use case: `XxxCommand.cs`/`XxxQuery.cs` + `XxxHandler.cs`
  with a single `Handle(...)` method. Registered explicitly in
  `Talabat.Application/DependencyInjection.cs`.
- **Transport-neutral results.** `UseCaseResult<T>` + `ApplicationError(code, category, message)`.
  `ApplicationErrorCategory { Validation, NotFound, Conflict, Unavailable, OwnershipMismatch }`.
  Domain exceptions are converted by `DomainExceptionMapper.Map(exception)`.
- **Ownership is a repository concern.** The established pattern is a scoped read that returns `null`
  for non-owned resources: `IOrderRepository.GetByIdForCustomerAsync`,
  `IDeliveryRepository.GetByIdForAgentAsync`. Handlers then return `NotFound` — never `403` — so
  existence is not disclosed.
- **One commit per use case.** `IUnitOfWork.SaveChangesAsync`. Repositories never save.
- **Capability changes never go through `IUnitOfWork`.** They go through `IUserCapabilityService`,
  which owns its own transaction/savepoint and role synchronisation.

### 1.4 Host patterns in force

Both existing hosts are the same template:

```
Auth/AuthorizationPolicies.cs      // const policy names
Auth/CurrentUser.cs                // ICurrentUser impl, reads sub + resolves capability from DB
Auth/ScopeHandler.cs               // AuthorizationHandler<ScopeRequirement>, splits "scope" claim
Auth/ScopeRequirement.cs
Contracts/**                       // host-owned request/response DTOs
Controllers/**                     // thin; delegate to handlers; map UseCaseResult
Extensions/UseCaseResultExtensions.cs   // ApplicationErrorCategory -> HTTP + ProblemDetails
Middleware/DomainExceptionHandler.cs    // IExceptionHandler for DomainException/ArgumentException
Program.cs                         // AddApplication + AddInfrastructure + AddUnifiedUserIdentityCore
                                   // + JwtBearer(Authority=Identity, Audience=<api>) + policies
```

`Program.cs` ends with `public partial class Program { }` so `WebApplicationFactory<Program>` works.

`Talabat.API/Middleware/RequireCustomerProfileAttribute.cs` is an `IAsyncActionFilter` that turns
"authenticated but capability not granted" into `409 ProfileNotCreated`. This is the template for the
new capability gates.

### 1.5 Identity host baseline

`IdentityServerConfig.cs` currently declares:

- Scopes: `customer.api`, `delivery.api`
- Resources: `talabat.customer.api` (claims `role`, `customer_id`), `talabat.delivery.api` (claims `role`, `delivery_agent_id`)
- Clients: `talabat-customer-spa` (localhost:4200), `talabat-delivery-spa` (localhost:4300) — Code + PKCE, no secret, 900s access token, sliding refresh
- `TalabatProfileService` emits `sub`, `role` per Identity role, and `customer_id`/`delivery_agent_id` when the flag is set; `IsActiveAsync` denies inactive/soft-deleted users

`AccountController` has **development-only** agent approve/reject endpoints marked
`// TODO(Phase 9): replace with AdminAccess policy`. **This plan discharges that TODO.**

### 1.6 Architecture tests already enforcing boundaries

`tests/Talabat.ArchitectureTests`:

- `Domain_ShouldNotReference_EntityFrameworkCore | AspNetCore | DuendeOrIdentityServer | HttpAbstractions`
- `Domain_AllowedException_IdentityStores` — the single sanctioned exception
- `Application_ShouldNotReference_EntityFrameworkCore | AspNetCore | Duende | HttpContext | ClaimsPrincipal` (+ csproj check)
- `Customer_API_Types_ShouldNotReference_TalabatDbContext_Directly`, same for Delivery API
- `Domain_ShouldNotReference_ICurrentUser | ClaimOrRoleTypes`

**Every new project must be added to these tests (§14.5). If a change makes an architecture test fail,
the change is wrong — not the test.**

---

## 2. Gap analysis — what is actually missing

| # | Gap | Impact | Resolved in |
|---|---|---|---|
| G1 | No restaurant ownership in the model | Cannot scope any owner endpoint | Phase 2 |
| G2 | No `Admin`/`RestaurantOwner` grant/revoke path | Roles seeded but unreachable | Phase 3 |
| G3 | No restaurant-side order lifecycle | Owner cannot accept/reject/prepare | Phase 5 |
| G4 | `Restaurant` has no profile/hours/product-detail mutators | Owner cannot edit anything | Phase 2 |
| G5 | No `IRestaurantRepository.AddAsync` | Admin cannot create restaurants | Phase 2 |
| G6 | No cross-aggregate paged read models | Admin lists would abuse aggregate repos | Phase 4 |
| G7 | No admin bootstrap | First admin cannot exist | Phase 3 |
| G8 | Agent approve/reject is a dev-only Identity endpoint | Not production-usable, wrong host | Phase 6 |
| G9 | Host plumbing duplicated 2× → would become 4× | Real maintenance problem at 4 hosts | Phase 1 |
| G10 | `Restaurant`/`Product` have no `RowVersion` | Concurrent owner price edits silently last-write-wins | Phase 2 |
| G11 | `ICurrentUser` has no admin/owner capability | Gates cannot be written | Phase 3 |
| G12 | `CurrentUser.EnsureResolved()` does sync-over-async DB I/O in a property getter | Pre-existing deadlock/perf risk; must not be copied into 2 more hosts | Phase 1 |

---

## 3. Target architecture after this work

### 3.1 Project graph (arrows = project references)

```
Talabat.Domain
   ^
   |
Talabat.Application
   ^                    ^
   |                    |
Talabat.Infrastructure  Talabat.Api.Shared        (NEW — plumbing only, no Infrastructure ref)
   ^        ^      ^        ^   ^   ^   ^
   |        |      |        |   |   |   |
Customer  Delivery Identity  |   |   |   |
  .API      .API             |   |   |   |
   ^---------^---------------+   |   |   |
                                 |   |   |
              Talabat.RestaurantOwner.API   (NEW)
              Talabat.Admin.API             (NEW)
```

Rules that must not break:

- No host references another host.
- `Talabat.Api.Shared` references **`Talabat.Application` only** (+ `FrameworkReference Microsoft.AspNetCore.App`). It must never reference `Talabat.Infrastructure`.
- `Talabat.Infrastructure` never references any host.
- Hosts reference `Talabat.Infrastructure` **only** to call `AddInfrastructure(...)` and
  `AddUnifiedUserIdentityCore()`; no controller/handler may take `TalabatDbContext`.

### 3.2 Bounded contexts after this work

| Context | Owns | New in this work |
|---|---|---|
| Catalog | Restaurant, Product, availability, prices, opening hours | **Restaurant ownership link (`OwnerUserId`)** |
| Basket | Cart, CartItem | — |
| Ordering | Order, OrderItem (immutable) | — |
| Users / Identity profile | `User`, `UserAddress`, capability flags | Admin + RestaurantOwner capability behaviour |
| Delivery Management | Delivery, agent availability, assignment | Admin-initiated assignment/cancel (reuses existing handlers) |
| **Restaurant Operations** (NEW) | `OrderFulfillment` — the restaurant-side lifecycle of one order | Entire context |

**Restaurant Operations is a new bounded context, not an extension of Ordering.** It is introduced
for exactly the same reason `Delivery` was split from `Order`: `Order` is historical purchase data,
fulfillment is mutable operational state; they change for different reasons and protect different
invariants. See §4 D3.

### 3.3 Context map delta

```
Ordering ──(OrderId, RestaurantId)──> Restaurant Operations
Ordering ──(OrderId, ...)───────────> Delivery Management        (existing)
Restaurant Operations ──rejection──> Delivery Management         (NEW, Application-coordinated)
Catalog  ──OwnerUserId──────────────> Users
```

No aggregate navigates to another aggregate. All links are scalar IDs. All coordination is in the
Application layer, exactly as `CheckoutHandler` → `CreateDeliveryForOrderHandler` does today.

---

## 4. Key design decisions

Each decision states what to do, why, and what was rejected. **Do not re-litigate these mid-implementation.**

### D0 — Accept, but do not widen, the existing Domain/Identity coupling

`User : IdentityUser<int>` and `Talabat.Domain` references `Microsoft.Extensions.Identity.Stores`.
This contradicts the constitution's original wording but is a deliberate, documented,
architecture-test-sanctioned override from `user-aggregate-refactor-plan.md` (D6).

**Do:** treat it as frozen. New domain code may read `UserType` flags.
**Do not:** add `UserManager`, `RoleManager`, `SignInManager`, `IdentityRole`, `ClaimsPrincipal`,
`Claim`, or any Duende type to Domain or Application. Role names live in
`Talabat.Infrastructure/Identity/IdentityRoleNames.cs` and **stay there**.
**Rule of thumb:** Domain branches on `UserType`; hosts branch on roles; nothing branches on both.

### D1 — Two new hosts, not new controllers on existing hosts

Create `Talabat.RestaurantOwner.API` and `Talabat.Admin.API`.

**Why:** each host has its own JWT audience and scope. Putting admin endpoints behind the customer
audience would mean a customer token is *structurally* able to reach admin routes and only a role
check stands between them. Separate audiences make cross-role token replay impossible at the
authentication layer, before authorization runs. It also matches the existing
one-website-one-host shape and keeps blast radius per deploy small.

**Rejected:** a single `Talabat.Backoffice.API` serving both roles — admin and owner have different
trust levels, different token lifetimes, and different CORS origins; merging them creates one host
where an owner-scoped bug becomes an admin-scoped bug.

### D2 — Ownership = `Restaurant.OwnerUserId` (nullable scalar), not a junction aggregate

```csharp
public int? OwnerUserId { get; private set; }
```

**Why:** one restaurant has at most one owner in this scope. Ownership is an invariant *of the
restaurant* (a restaurant may be unowned; it may not have two owners), so it belongs on the
`Restaurant` root. A nullable column keeps the migration purely additive, so the existing
`CatalogSeedData.HasData` rows stay valid with zero seed changes. One owner may own many restaurants —
that works with no schema change.

**Rejected:**
- A `RestaurantOwnership` junction table / aggregate — needed only for multi-owner or staff roles,
  which are out of scope. Migration path preserved in §16.
- Putting `OwnedRestaurantIds` on `User` — the collection would be unbounded, would break the
  aggregate's cohesion, and would duplicate a fact Catalog already owns.
- An `owned_restaurant_ids` JWT claim — see D6.

### D3 — `Order` stays immutable; add an `OrderFulfillment` aggregate

**Never add `Status`, `AcceptedAt`, or any mutator to `Order`.** `docs/bounded-contexts.md` and the
aggregate design both state Order stores immutable snapshots; several tests assume it.

Add `OrderFulfillment` (root, one-per-Order) in `Aggregates/RestaurantOperations/`:

```
AwaitingRestaurant ──Accept──> Accepted ──MarkPreparing──> Preparing ──MarkReady──> ReadyForPickup
        └──Reject──> Rejected (terminal)
        (any non-terminal) ──CancelByAdmin──> CancelledByAdmin (terminal)
```

**Why:** exact mirror of the `Order` → `Delivery` split already in the codebase and already
documented in `docs/delivery/delivery-bounded-context.md`. It gives the restaurant-side lifecycle its
own invariants, its own `RowVersion`, and its own repository without touching order history.

**Rejected:** reusing `DeliveryStatus` for kitchen states — conflates courier lifecycle with kitchen
lifecycle, and `Delivery` is agent-guarded (`MarkPickedUp(int agentId, ...)`) which makes no sense for
a restaurant actor.

### D4 — Fulfillment is created at checkout, alongside Delivery

`CheckoutHandler`'s caller already creates a `Delivery` via `CreateDeliveryForOrderHandler`. Add a
symmetric `CreateOrderFulfillmentForOrderHandler` invoked at the same point.

**Why:** the restaurant must see the order the moment it is placed. Creating fulfillment lazily on
first owner read would make the "incoming orders" list a write endpoint.

**Consequence to handle explicitly:** a `Delivery` now exists in `PendingAssignment` for an order the
restaurant may still reject. `RejectOrderHandler` therefore **also cancels the linked delivery in the
same use case** (§9.3). This is an Application-coordinated cross-aggregate workflow, identical in shape
to checkout.

**Deferred (do not implement now):** gating agent assignment on `ReadyForPickup`. That changes
existing delivery-agent behaviour and belongs in its own increment (§16).

### D5 — Authorization is three-layered; ownership is never a policy

| Layer | Mechanism | Answers | Failure |
|---|---|---|---|
| 1. Transport | ASP.NET policy: authenticated + scope + role | "is this token for this API and this role?" | `401` / `403` |
| 2. Capability | `IAsyncActionFilter` reading `ICurrentUser` | "does this *account* still hold the capability?" | `409` |
| 3. Resource ownership | Scoped repository read inside the handler | "does this actor own *this* row?" | `404` |

Layer 3 is the only one that can answer ownership, and it must be done with a scoped read
(`GetByIdForOwnerAsync`) that returns `null`, **not** a load-then-compare that returns `403`.
Returning `404` prevents restaurant/order enumeration by a valid but non-owning owner account.

**Layer 2 exists because tokens go stale.** A JWT minted before an admin revoked
`RestaurantOwner` is still cryptographically valid for up to its lifetime. `ICurrentUser` re-reads
`UserType` from the database per request, which is why `CurrentUserCapabilityResolver` exists.
Never trust the role claim alone for a state-changing operation.

### D6 — No restaurant IDs, no permissions, in the token

`TalabatProfileService` emits `sub` + `role` (+ existing `customer_id`/`delivery_agent_id`). For the
new APIs emit **only `role`**.

**Why:** an `owned_restaurant_ids` claim is stale the moment an admin reassigns a restaurant, is
unbounded in size, and would let a replayed token act on a restaurant the owner no longer owns.
Ownership is cheap to resolve server-side and is already the pattern for delivery
(`delivery_agent_id` is only an identity echo, never an authorization decision).

### D7 — Admin is one coarse role now, with a named seam for later granularity

Create policy `AdminAccess` (authenticated + `admin.api` scope + `Admin` role). Also declare
`AdminUserManagement`, `AdminCatalogManagement`, `AdminDeliveryOperations` as **separate policy names
that currently carry identical requirements**, and annotate controllers with the specific names.

**Why:** the roadmap explicitly forbids speculative claims/permissions. But routes annotated with
`AdminAccess` everywhere would need to be re-annotated across every controller the day granularity
arrives. Declaring the names now costs three lines and makes future tightening a
composition-root-only change.

**Do not** build a permission table, permission claims, or a `[HasPermission]` attribute.

### D8 — All capability changes go through `IUserCapabilityService`; never through `IUnitOfWork`

Extend the existing interface (§8.2). The service already owns transaction/savepoint handling, role
synchronisation, and `UpdateSecurityStampAsync`.

**The trap to avoid:** a handler that calls `_userRepository.Update(user)` +
`_unitOfWork.SaveChangesAsync()` **and** `_capabilityService.GrantAdminCapabilityAsync(...)` commits
twice and can leave flags and roles divergent. **Rule:** a handler either delegates entirely to
`IUserCapabilityService`, or it touches no capability at all.

### D9 — Cross-aggregate admin/owner lists use dedicated read-model query services, not repositories

New Application contracts, implemented in `Talabat.Infrastructure/Persistence/Queries/`:

```
Talabat.Application/Admin/Abstractions/IAdminQueries.cs
Talabat.Application/RestaurantOwner/Abstractions/IRestaurantOwnerQueries.cs
```

They return Application read-model records built by EF `AsNoTracking().Select(...)` projections.

**Why:** admin needs paged, filtered, joined, cross-aggregate lists. Forcing those into
`IUserRepository`/`IOrderRepository` would either (a) return whole aggregates for list screens, or
(b) push `IQueryable` outward — both are explicitly forbidden by `docs/repository-interfaces-design.md`.
A separate read side keeps aggregate repositories aligned to write use cases. This is the CQRS read/write
split the codebase already half-implements (handlers return read models, never aggregates).

**Hard constraints on query services:** never return a Domain aggregate; never accept an expression
tree from Application; never write.

### D10 — No domain events in this increment

There is no dispatcher, no outbox, no event base type in the repo.

**Why not now:** adding event infrastructure is a cross-cutting change with its own transactional
semantics (in-transaction vs outbox), its own tests, and its own failure modes. Two coordinated
workflows (`reject → cancel delivery`, `checkout → create fulfillment`) do not justify it, and the
codebase already coordinates in Application handlers.

**Where it would pay off later** (§16): fulfillment/delivery status fan-out to notifications, and
admin audit trail. The smallest future seam is a `List<IDomainEvent>` on `AuditableEntity` drained by
`UnitOfWork.SaveChangesAsync` — do not build it now.

### D11 — Extract `Talabat.Api.Shared` before adding hosts

`ScopeHandler`, `ScopeRequirement`, `UseCaseResultExtensions`, `DomainExceptionHandler` are already
byte-for-byte duplicated across two hosts. At four hosts, a fix to the error-mapping table has to
land in four places.

Move them to `Talabat.Api.Shared` (namespace `Talabat.Api.Shared.*`) **as a mechanical, behaviour-
preserving refactor in its own commit**, with the existing Customer/Delivery API tests green before
any new host exists. Host-specific `AuthorizationPolicies` constants stay per host.

**Why first:** doing it after the new hosts exist means four migrations instead of two.

### D12 — Fix the sync-over-async `ICurrentUser` for the new hosts

`CurrentUser.EnsureResolved()` calls `_resolver.GetUserTypeAsync(...).GetAwaiter().GetResult()` inside
a property getter. Do not replicate that.

**New-host pattern:** the capability action filter (`RequireRestaurantOwnerCapabilityAttribute` /
`RequireAdminCapabilityAttribute`) resolves `UserType` **asynchronously** in
`OnActionExecutionAsync`, stores it in `HttpContext.Items["talabat.usertype"]`, and `CurrentUser`
reads the cached value with a synchronous fallback only when the filter did not run (anonymous routes).

**Do not** refactor the two existing hosts' `CurrentUser` in this workstream — flag it in §16.

### D13 — Concurrency

`User` and `Delivery` already carry `RowVersion`. Add `RowVersion` to `Restaurant`, `Product`, and
`OrderFulfillment`. Map `DbUpdateConcurrencyException` to the existing
`ConcurrencyConflictException` → `ApplicationErrorCodes.ConcurrencyConflict` → `409`.

**Why Restaurant/Product need it now:** two staff members on one owner account editing prices
concurrently is a realistic first-week bug, and price is money.

### D14 — Validation stays three-tier, no new packages

1. **Host DTOs** — `[Required]`, `[Range]`, `[StringLength]` + `ApiController` automatic `400`.
2. **Handlers** — cross-aggregate/business preconditions returning `UseCaseResult.Failure(...)`
   (e.g. "cannot revoke owner capability while the user still owns restaurants").
3. **Domain** — `Guard.*` and domain exceptions, mapped by `DomainExceptionMapper`.

**Do not add FluentValidation.** No validation package exists in the solution; adding one is a
separate approved decision.

### D15 — Migration ownership

All migrations continue to be generated from `Talabat.Infrastructure` with
**`src/Talabat/Talabat.API` (the Customer API) as the startup project**, so `__EFMigrationsHistory`
has exactly one generator and snapshot conflicts are impossible.

Three separate reviewed migrations, in strict order (§10.2). Never squash them into one.

---

## 5. Phased implementation plan — order of execution

Each phase is independently shippable and leaves the solution building with all tests green.
**Do not start a phase before the previous one is green.**

| Phase | Name | Deliverable | Blocks |
|---|---|---|---|
| **0** | Baseline & guard rails | Green build + full test run recorded; new architecture-test assertions added *first* (they will fail until the code exists — mark `[Fact(Skip=...)]` and unskip in the phase that satisfies them) | all |
| **1** | `Talabat.Api.Shared` extraction (D11) | Shared plumbing library; Customer + Delivery hosts refactored to use it; zero behaviour change | 6, 7 |
| **2** | Catalog ownership + Restaurant write behaviour (D2) | `Restaurant.OwnerUserId`, `RowVersion`, `UpdateProfile`, `UpdateOpeningHours`, `UpdateProductDetails`, repo `AddAsync`/`Update`/`GetOwnedByAsync`/`GetByIdForOwnerAsync`; **Migration 1** | 5, 6, 7 |
| **3** | User capabilities: Admin + RestaurantOwner (D8) | `User` grant/revoke methods, `IUserCapabilityService` extensions, `ICurrentUser` extensions, admin bootstrap | 6, 7 |
| **4** | Read models (D9) | `IAdminQueries`, `IRestaurantOwnerQueries` + EF implementations + `PagedResult<T>` | 6, 7 |
| **5** | Restaurant Operations context (D3, D4) | `OrderFulfillment` aggregate, repository, exceptions, create-at-checkout wiring, reject→cancel-delivery coordination; **Migration 2** | 6 |
| **6** | `Talabat.RestaurantOwner.API` | New host + Identity client/scope/resource + tests | — |
| **7** | `Talabat.Admin.API` | New host + Identity client/scope/resource + tests; retire Identity host's dev-only approve/reject | — |
| **8** | Hardening & documentation | `docs/authorization-matrix.md` updated, architecture tests unskipped, vulnerability audit, full-solution regression | — |

**Parallelisation:** Phases 2, 3, 4 touch disjoint files and may be done concurrently by separate
agents *if* Phase 1 is merged first. Phase 5 depends on 2. Phases 6 and 7 depend on 2+3+4 (and 6 also
on 5). Do not parallelise anything that edits
`Talabat.Application/DependencyInjection.cs` or `Talabat.Infrastructure/DependencyInjection.cs`.

---

## 6. Domain layer changes

All under `src/Talabat/Talabat.Domain/`.

### 6.1 `Aggregates/Users/User.cs` — capability grants (Phase 3)

Append to the existing class. **No new fields, no new dependencies** — `UserType` already exists.

```csharp
    public void GrantAdminCapability()
    {
        UserType |= UserType.Admin;
    }

    public void RevokeAdminCapability()
    {
        UserType &= ~UserType.Admin;
    }

    public void GrantRestaurantOwnerCapability()
    {
        UserType |= UserType.RestaurantOwner;
    }

    public void RevokeRestaurantOwnerCapability()
    {
        UserType &= ~UserType.RestaurantOwner;
    }

    public bool IsAdmin() => UserType.HasFlag(UserType.Admin);

    public bool IsRestaurantOwner() => UserType.HasFlag(UserType.RestaurantOwner);
```

**Deliberately absent:**
- No "cannot revoke the last admin" guard — that is a *set-wide* rule the aggregate cannot see. It
  lives in `RevokeAdminCapabilityHandler` (§7.3).
- No "cannot revoke owner while restaurants are owned" guard — cross-aggregate, lives in
  `RevokeRestaurantOwnerCapabilityHandler`.
- No `Guard` on grant: granting twice is idempotent by construction (`|=`).

### 6.2 `Aggregates/Catalog/Restaurant.cs` — ownership + write behaviour (Phase 2)

```csharp
    public int? OwnerUserId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void AssignOwner(int ownerUserId)
    {
        OwnerUserId = Guard.Positive(ownerUserId, nameof(ownerUserId));
    }

    public void ClearOwner()
    {
        OwnerUserId = null;
    }

    public bool IsOwnedBy(int userId) => OwnerUserId is not null && OwnerUserId.Value == userId;

    public void UpdateProfile(string name, string description, string? imageUrl)
    {
        Name = Guard.RequiredText(name, nameof(name));
        Description = Guard.RequiredText(description, nameof(description));
        ImageUrl = Guard.OptionalText(imageUrl);
    }

    public void UpdateOpeningHours(TimeRange openingHours)
    {
        OpeningHours = openingHours ?? throw new ArgumentNullException(nameof(openingHours));
    }

    public void UpdateProductDetails(
        int productId,
        string name,
        string description,
        string? imageUrl)
    {
        var product = GetRequiredProduct(productId);
        var normalizedName = Guard.RequiredText(name, nameof(name));

        // Re-assert the aggregate's duplicate-name invariant, excluding the product being renamed.
        if (_products.Any(other =>
                other.Id != productId &&
                string.Equals(other.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateProductException();
        }

        product.UpdateDetails(normalizedName, description, imageUrl);
    }
```

`IsOwnedBy` is a scalar comparison, not a role check — it does **not** violate "no auth in Domain".
It is defence-in-depth; the primary enforcement is the scoped repository read.

### 6.3 `Aggregates/Catalog/Product.cs` (Phase 2)

Add one `internal` mutator, preserving the compiler-enforced boundary:

```csharp
    internal void UpdateDetails(string name, string description, string? imageUrl)
    {
        Name = Guard.RequiredText(name, nameof(name));
        Description = Guard.RequiredText(description, nameof(description));
        ImageUrl = Guard.OptionalText(imageUrl);
    }

    public byte[] RowVersion { get; private set; } = [];
```

**Do not make it public. Do not add `IProductRepository`.**

### 6.4 New: `Aggregates/RestaurantOperations/` (Phase 5)

**`OrderFulfillmentStatus.cs`**

```csharp
namespace Talabat.Domain.Aggregates.RestaurantOperations;

public enum OrderFulfillmentStatus
{
    AwaitingRestaurant = 1,
    Accepted = 2,
    Preparing = 3,
    ReadyForPickup = 4,
    Rejected = 5,
    CancelledByAdmin = 6
}
```

**`OrderFulfillment.cs`**

```csharp
using Talabat.Domain.Common;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.Exceptions;

namespace Talabat.Domain.Aggregates.RestaurantOperations;

public sealed class OrderFulfillment : AuditableEntity
{
    public int Id { get; private set; }

    public int OrderId { get; private set; }

    public int RestaurantId { get; private set; }

    public OrderFulfillmentStatus Status { get; private set; }

    public DateTime? AcceptedAt { get; private set; }
    public DateTime? PreparingAt { get; private set; }
    public DateTime? ReadyAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public int? EstimatedPreparationMinutes { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    private OrderFulfillment() { }

    public OrderFulfillment(int orderId, int restaurantId, DateTime createdAt)
    {
        OrderId = Guard.Positive(orderId, nameof(orderId));
        RestaurantId = Guard.Positive(restaurantId, nameof(restaurantId));
        CreatedAt = Guard.Utc(createdAt, nameof(createdAt));
        Status = OrderFulfillmentStatus.AwaitingRestaurant;
    }

    public bool IsTerminal() =>
        Status is OrderFulfillmentStatus.Rejected or OrderFulfillmentStatus.CancelledByAdmin;

    public void Accept(int restaurantId, int? estimatedPreparationMinutes, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        EnsureStatus(OrderFulfillmentStatus.AwaitingRestaurant);

        if (estimatedPreparationMinutes is { } minutes)
        {
            Guard.Positive(minutes, nameof(estimatedPreparationMinutes));
            EstimatedPreparationMinutes = minutes;
        }

        Status = OrderFulfillmentStatus.Accepted;
        AcceptedAt = Guard.Utc(currentTime, nameof(currentTime));
    }

    public void Reject(int restaurantId, string reason, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        EnsureStatus(OrderFulfillmentStatus.AwaitingRestaurant);

        RejectionReason = Guard.RequiredText(reason, nameof(reason));
        Status = OrderFulfillmentStatus.Rejected;
        RejectedAt = Guard.Utc(currentTime, nameof(currentTime));
    }

    public void MarkPreparing(int restaurantId, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        EnsureStatus(OrderFulfillmentStatus.Accepted);

        Status = OrderFulfillmentStatus.Preparing;
        PreparingAt = Guard.Utc(currentTime, nameof(currentTime));
    }

    public void MarkReadyForPickup(int restaurantId, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);

        if (Status is not (OrderFulfillmentStatus.Accepted or OrderFulfillmentStatus.Preparing))
        {
            throw new InvalidOrderFulfillmentStatusTransitionException(
                $"Cannot mark ready from status '{Status}'.");
        }

        Status = OrderFulfillmentStatus.ReadyForPickup;
        ReadyAt = Guard.Utc(currentTime, nameof(currentTime));
    }

    public void CancelByAdmin(DateTime currentTime)
    {
        if (IsTerminal())
        {
            throw new OrderFulfillmentTerminalStateException();
        }

        Status = OrderFulfillmentStatus.CancelledByAdmin;
        CancelledAt = Guard.Utc(currentTime, nameof(currentTime));
    }

    private void EnsureRestaurant(int restaurantId)
    {
        if (RestaurantId != restaurantId)
        {
            throw new OrderFulfillmentRestaurantMismatchException();
        }
    }

    private void EnsureStatus(OrderFulfillmentStatus expected)
    {
        if (IsTerminal())
        {
            throw new OrderFulfillmentTerminalStateException();
        }

        if (Status != expected)
        {
            throw new InvalidOrderFulfillmentStatusTransitionException(
                $"Expected status '{expected}' but was '{Status}'.");
        }
    }
}
```

Note the `restaurantId` guard parameter — this is the same defence-in-depth shape as
`Delivery.MarkPickedUp(int agentId, ...)`. Ownership is still enforced first at the repository.

### 6.5 New domain exceptions (Phase 5)

All in `Talabat.Domain/Exceptions/`, all inheriting `DomainException`, business language only:

- `InvalidOrderFulfillmentStatusTransitionException(string message)`
- `OrderFulfillmentTerminalStateException()`
- `OrderFulfillmentRestaurantMismatchException()`
- `OrderFulfillmentNotFoundException()`
- `RestaurantOwnershipRequiredException()` — used only if a future non-repository path needs it

### 6.6 Repository contract changes (`Talabat.Domain/Interfaces/`)

**`IRestaurantRepository`** — add (Phase 2):

```csharp
    Task<IReadOnlyCollection<Restaurant>> GetOwnedByAsync(
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<Restaurant?> GetByIdForOwnerAsync(
        int restaurantId,
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<Restaurant?> GetByIdWithProductsForOwnerAsync(
        int restaurantId,
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<int> CountOwnedByAsync(
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Restaurant restaurant, CancellationToken cancellationToken = default);

    void Update(Restaurant restaurant);
```

`CountOwnedByAsync` exists for exactly one business rule: refusing to revoke `RestaurantOwner` from a
user who still owns restaurants. Every method here maps to a named use case, per
`docs/repository-interfaces-design.md`.

**`IOrderRepository`** — add (Phase 5):

```csharp
    Task<Order?> GetByIdForRestaurantAsync(
        int orderId,
        int restaurantId,
        CancellationToken cancellationToken = default);
```

**New `IOrderFulfillmentRepository`** (Phase 5):

```csharp
using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Domain.Interfaces;

public interface IOrderFulfillmentRepository
{
    Task<OrderFulfillment?> GetByIdAsync(int fulfillmentId, CancellationToken cancellationToken = default);

    Task<OrderFulfillment?> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);

    Task<OrderFulfillment?> GetByOrderIdForRestaurantAsync(
        int orderId,
        int restaurantId,
        CancellationToken cancellationToken = default);

    Task AddAsync(OrderFulfillment fulfillment, CancellationToken cancellationToken = default);

    void Update(OrderFulfillment fulfillment);
}
```

**Do not add:** `IProductRepository`, `IOrderItemRepository`, any paged/filtered admin list method,
or anything returning `IQueryable`.

---

## 7. Application layer changes

All under `src/Talabat/Talabat.Application/`.

### 7.1 `Abstractions/ICurrentUser.cs` — extend (Phase 3)

```csharp
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }

    bool HasCustomerCapability { get; }
    int? CustomerId { get; }

    bool HasDeliveryAgentCapability { get; }
    int? AgentId { get; }

    // NEW
    bool HasAdminCapability { get; }
    bool HasRestaurantOwnerCapability { get; }
}
```

**Deliberately no `OwnedRestaurantIds`** (D6). Both existing host implementations
(`Talabat.API/Auth/CurrentUser.cs`, `Talabat.Delivery.API/Auth/CurrentUser.cs`) must be updated to
satisfy the interface — return the flags from the already-fetched `UserType`. This is a two-line
change per host, no behaviour change.

### 7.2 `Common/Results/` additions (Phase 4)

**`PagedResult.cs`**

```csharp
namespace Talabat.Application.Common.Results;

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

**`ApplicationErrorCodes.cs`** — add:

```csharp
    public const string RestaurantOwnershipRequired = nameof(RestaurantOwnershipRequired);
    public const string OwnerCapabilityNotGranted   = nameof(OwnerCapabilityNotGranted);
    public const string AdminCapabilityNotGranted   = nameof(AdminCapabilityNotGranted);
    public const string LastAdminCannotBeRevoked    = nameof(LastAdminCannotBeRevoked);
    public const string OwnerStillOwnsRestaurants   = nameof(OwnerStillOwnsRestaurants);
    public const string RestaurantAlreadyOwned      = nameof(RestaurantAlreadyOwned);
    public const string OrderFulfillmentNotFound    = nameof(OrderFulfillmentNotFound);
    public const string InvalidFulfillmentTransition = nameof(InvalidFulfillmentTransition);
    public const string AgentApplicationNotPending  = nameof(AgentApplicationNotPending);
```

**`DomainExceptionMapper`** — add cases for the new domain exceptions:
`InvalidOrderFulfillmentStatusTransitionException` → `Conflict`/`InvalidFulfillmentTransition`;
`OrderFulfillmentTerminalStateException` → `Conflict`;
`OrderFulfillmentRestaurantMismatchException` → `NotFound` (never leak that the row exists);
`AgentApplicationNotPendingException` → `Conflict`/`AgentApplicationNotPending`.

### 7.3 `Abstractions/IUserCapabilityService.cs` — extend (Phase 3)

```csharp
    Task<UseCaseResult<int>> GrantAdminCapabilityAsync(int userId, CancellationToken ct = default);

    Task<UseCaseResult<int>> RevokeAdminCapabilityAsync(int userId, CancellationToken ct = default);

    Task<UseCaseResult<int>> GrantRestaurantOwnerCapabilityAsync(int userId, CancellationToken ct = default);

    Task<UseCaseResult<int>> RevokeRestaurantOwnerCapabilityAsync(int userId, CancellationToken ct = default);

    Task<UseCaseResult<int>> ActivateUserAsync(int userId, CancellationToken ct = default);
```

(`ActivateUserAsync` is the missing counterpart to the existing `DeactivateUserAsync`.)

### 7.4 New Application folders

```
Talabat.Application/
  Fulfillment/
    CreateForOrder/           CreateOrderFulfillmentCommand.cs, CreateOrderFulfillmentHandler.cs
  RestaurantOwner/
    Abstractions/             IRestaurantOwnerQueries.cs
    Models/                   OwnedRestaurantSummary.cs, OwnedRestaurantDetails.cs,
                              OwnerMenuProduct.cs, RestaurantOrderSummary.cs,
                              RestaurantOrderDetails.cs, RestaurantOrderFilter.cs
    GetOwnedRestaurants/
    GetOwnedRestaurantDetails/
    UpdateRestaurantProfile/
    UpdateRestaurantOpeningHours/
    SetRestaurantActiveState/
    AddMenuProduct/
    UpdateMenuProductDetails/
    UpdateMenuProductPrice/
    SetMenuProductAvailability/
    GetRestaurantOrders/
    GetRestaurantOrderDetails/
    AcceptOrder/
    RejectOrder/
    MarkOrderPreparing/
    MarkOrderReady/
  Admin/
    Abstractions/             IAdminQueries.cs
    Models/                   AdminUserSummary.cs, AdminUserDetails.cs, AdminUserFilter.cs,
                              AdminRestaurantSummary.cs, AdminOrderSummary.cs,
                              AdminOrderDetails.cs, AdminDeliverySummary.cs,
                              AgentApplicationSummary.cs
    Users/                    SearchUsers/, GetUserDetails/, ActivateUser/, DeactivateUser/,
                              SoftDeleteUser/, GrantAdminCapability/, RevokeAdminCapability/,
                              GrantRestaurantOwnerCapability/, RevokeRestaurantOwnerCapability/
    DeliveryAgents/           GetAgentApplications/, ApproveAgentApplication/, RejectAgentApplication/
    Restaurants/              CreateRestaurant/, AssignRestaurantOwner/, ClearRestaurantOwner/,
                              SetRestaurantActiveState/, SearchRestaurants/
    Orders/                   SearchOrders/, GetAdminOrderDetails/
    Deliveries/               SearchDeliveries/, GetAdminDeliveryDetails/
```

Every new handler must be registered in `Talabat.Application/DependencyInjection.cs`.

### 7.5 Read-model contracts (Phase 4)

**`RestaurantOwner/Abstractions/IRestaurantOwnerQueries.cs`**

```csharp
using Talabat.Application.Common.Results;
using Talabat.Application.RestaurantOwner.Models;

namespace Talabat.Application.RestaurantOwner.Abstractions;

public interface IRestaurantOwnerQueries
{
    Task<PagedResult<RestaurantOrderSummary>> GetRestaurantOrdersAsync(
        int restaurantId,
        RestaurantOrderFilter filter,
        CancellationToken ct = default);

    Task<RestaurantOrderDetails?> GetRestaurantOrderDetailsAsync(
        int restaurantId,
        int orderId,
        CancellationToken ct = default);
}
```

`restaurantId` is supplied by the handler **after** an ownership check has already succeeded — the
query service never performs authorization.

**`Admin/Abstractions/IAdminQueries.cs`**

```csharp
public interface IAdminQueries
{
    Task<PagedResult<AdminUserSummary>> SearchUsersAsync(AdminUserFilter filter, CancellationToken ct = default);
    Task<AdminUserDetails?> GetUserDetailsAsync(int userId, bool includeDeleted, CancellationToken ct = default);
    Task<PagedResult<AgentApplicationSummary>> GetAgentApplicationsAsync(AgentApplicationFilter filter, CancellationToken ct = default);
    Task<PagedResult<AdminRestaurantSummary>> SearchRestaurantsAsync(AdminRestaurantFilter filter, CancellationToken ct = default);
    Task<PagedResult<AdminOrderSummary>> SearchOrdersAsync(AdminOrderFilter filter, CancellationToken ct = default);
    Task<AdminOrderDetails?> GetOrderDetailsAsync(int orderId, CancellationToken ct = default);
    Task<PagedResult<AdminDeliverySummary>> SearchDeliveriesAsync(AdminDeliveryFilter filter, CancellationToken ct = default);
}
```

Filters are plain records with nullable properties and `Page`/`PageSize`. Clamp `PageSize` to
`[1, 100]` **in the handler**, not in the query implementation.

### 7.6 Representative handlers

**Owner write with ownership scoping** — `RestaurantOwner/UpdateMenuProductPrice/`:

```csharp
public sealed record UpdateMenuProductPriceCommand(
    int OwnerUserId,
    int RestaurantId,
    int ProductId,
    decimal Amount);

public sealed class UpdateMenuProductPriceHandler
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMenuProductPriceHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<int>> Handle(
        UpdateMenuProductPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        // Ownership scoping: a non-owned restaurant is indistinguishable from a missing one.
        var restaurant = await _restaurantRepository.GetByIdWithProductsForOwnerAsync(
            command.RestaurantId, command.OwnerUserId, cancellationToken);

        if (restaurant is null)
        {
            return UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.RestaurantNotFound, "Restaurant was not found."));
        }

        try
        {
            restaurant.UpdateProductPrice(command.ProductId, new Money(command.Amount));
            _restaurantRepository.Update(restaurant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<int>.Success(command.ProductId);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
```

Note: **`OwnerUserId` is a handler input supplied by the host from `ICurrentUser.UserId`, never from
the request body or route.** This mirrors how `CartController` passes `_currentUser.CustomerId!.Value`.

**Cross-aggregate coordination** — `RestaurantOwner/RejectOrder/RejectOrderHandler.cs` (D4):

```csharp
public sealed record RejectOrderCommand(int OwnerUserId, int RestaurantId, int OrderId, string Reason);

public sealed class RejectOrderHandler
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    // ctor omitted for brevity — same null-guard style as existing handlers

    public async Task<UseCaseResult<int>> Handle(
        RejectOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await _restaurantRepository.GetByIdForOwnerAsync(
            command.RestaurantId, command.OwnerUserId, cancellationToken);

        if (restaurant is null)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.NotFound(
                ApplicationErrorCodes.RestaurantNotFound, "Restaurant was not found."));
        }

        var fulfillment = await _fulfillmentRepository.GetByOrderIdForRestaurantAsync(
            command.OrderId, command.RestaurantId, cancellationToken);

        if (fulfillment is null)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.NotFound(
                ApplicationErrorCodes.OrderFulfillmentNotFound, "Order was not found."));
        }

        var now = _clock.UtcNow;

        try
        {
            fulfillment.Reject(command.RestaurantId, command.Reason, now);
            _fulfillmentRepository.Update(fulfillment);

            // A Delivery was created at checkout (D4). Rejection must release it.
            var delivery = await _deliveryRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
            if (delivery is not null && !delivery.IsTerminal())
            {
                delivery.Cancel(now);
                _deliveryRepository.Update(delivery);
            }

            // ONE commit for both aggregates.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<int>.Success(fulfillment.Id);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
```

> **If `Delivery.Cancel` currently throws when an agent is already assigned**, use the existing
> internal `CancelAssigned(agentId, now)` path via the `DeliveryAssignmentDomainService`, so the
> agent is released back to `Available`. Verify the exact signature before implementing; do not
> invent a new cancellation path.

**Business rule that must not be in Domain** — `Admin/Users/RevokeAdminCapability/`:

```csharp
public async Task<UseCaseResult<int>> Handle(
    RevokeAdminCapabilityCommand command,
    CancellationToken cancellationToken = default)
{
    if (command.ActingAdminUserId == command.TargetUserId)
    {
        return UseCaseResult<int>.Failure(new ApplicationError(
            ApplicationErrorCodes.LastAdminCannotBeRevoked,
            ApplicationErrorCategory.Conflict,
            "An administrator cannot revoke their own admin capability."));
    }

    var adminCount = await _adminQueries.CountActiveAdminsAsync(cancellationToken);
    if (adminCount <= 1)
    {
        return UseCaseResult<int>.Failure(new ApplicationError(
            ApplicationErrorCodes.LastAdminCannotBeRevoked,
            ApplicationErrorCategory.Conflict,
            "The last remaining administrator cannot be revoked."));
    }

    // Single delegation — no IUnitOfWork here (D8).
    return await _capabilityService.RevokeAdminCapabilityAsync(command.TargetUserId, cancellationToken);
}
```

Similarly, `RevokeRestaurantOwnerCapabilityHandler` calls
`IRestaurantRepository.CountOwnedByAsync(userId)` and fails with `OwnerStillOwnsRestaurants` when
non-zero, before delegating.

---

## 8. Infrastructure layer changes

All under `src/Talabat/Talabat.Infrastructure/`.

### 8.1 Persistence

**`Persistence/Configurations/RestaurantConfiguration.cs`** (Phase 2) — extend `Configure(EntityTypeBuilder<Restaurant>)`:

```csharp
        builder.Property(restaurant => restaurant.OwnerUserId);
        builder.Property(restaurant => restaurant.RowVersion).IsRowVersion();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(restaurant => restaurant.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);   // users are soft-deleted; never cascade a restaurant away

        builder.HasIndex(restaurant => restaurant.OwnerUserId)
            .HasDatabaseName("IX_Restaurants_OwnerUserId")
            .HasFilter("[OwnerUserId] IS NOT NULL");
```

and in `Configure(EntityTypeBuilder<Product>)`:

```csharp
        builder.Property(product => product.RowVersion).IsRowVersion();
```

`HasOne<User>().WithMany()` creates the FK **without** a navigation property in either direction —
required, because `User` must not gain a `Restaurants` collection (that would break the aggregate).

**New `Persistence/Configurations/OrderFulfillmentConfiguration.cs`** (Phase 5):

```csharp
internal sealed class OrderFulfillmentConfiguration : IEntityTypeConfiguration<OrderFulfillment>
{
    public void Configure(EntityTypeBuilder<OrderFulfillment> builder)
    {
        builder.ToTable("OrderFulfillments", table =>
        {
            table.HasCheckConstraint("CK_OrderFulfillments_Status", "[Status] IN (1,2,3,4,5,6)");
            table.HasCheckConstraint(
                "CK_OrderFulfillments_RejectionReason",
                "([Status] <> 5) OR ([RejectionReason] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_OrderFulfillments_EstimatedPreparationMinutes",
                "([EstimatedPreparationMinutes] IS NULL OR [EstimatedPreparationMinutes] > 0)");
        });

        builder.ConfigureIdentityKey();
        builder.ConfigureAuditableEntity();

        builder.Property(f => f.OrderId).IsRequired();
        builder.Property(f => f.RestaurantId).IsRequired();
        builder.Property(f => f.Status).HasConversion<int>().IsRequired();
        builder.Property(f => f.RejectionReason).HasMaxLength(500);
        builder.Property(f => f.RowVersion).IsRowVersion();

        builder.Property(f => f.AcceptedAt).HasColumnType("datetime2");
        builder.Property(f => f.PreparingAt).HasColumnType("datetime2");
        builder.Property(f => f.ReadyAt).HasColumnType("datetime2");
        builder.Property(f => f.RejectedAt).HasColumnType("datetime2");
        builder.Property(f => f.CancelledAt).HasColumnType("datetime2");

        builder.HasIndex(f => f.OrderId)
            .IsUnique()
            .HasDatabaseName("UX_OrderFulfillments_OrderId")
            .HasFilter("[IsDeleted] = CAST(0 AS bit)");

        builder.HasIndex(f => new { f.RestaurantId, f.Status })
            .HasDatabaseName("IX_OrderFulfillments_RestaurantId_Status");

        builder.HasOne<Order>().WithMany().HasForeignKey(f => f.OrderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Restaurant>().WithMany().HasForeignKey(f => f.RestaurantId).OnDelete(DeleteBehavior.NoAction);
    }
}
```

`ConfigureAuditableEntity()` applies the global `!IsDeleted` query filter — consistent with every
other aggregate.

**`Persistence/TalabatDbContext.cs`** (Phase 5) — add:

```csharp
    public DbSet<OrderFulfillment> OrderFulfillments => Set<OrderFulfillment>();
```

Configurations are auto-applied by the existing `ApplyConfigurationsFromAssembly` call — nothing else.

### 8.2 Repositories

**`Persistence/Repositories/RestaurantRepository.cs`** (Phase 2) — implement the new contract methods:

```csharp
    public async Task<IReadOnlyCollection<Restaurant>> GetOwnedByAsync(
        int ownerUserId, CancellationToken ct = default) =>
        await _dbContext.Restaurants
            .AsNoTracking()
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

    public Task<Restaurant?> GetByIdForOwnerAsync(
        int restaurantId, int ownerUserId, CancellationToken ct = default) =>
        _dbContext.Restaurants
            .SingleOrDefaultAsync(r => r.Id == restaurantId && r.OwnerUserId == ownerUserId, ct);

    public Task<Restaurant?> GetByIdWithProductsForOwnerAsync(
        int restaurantId, int ownerUserId, CancellationToken ct = default) =>
        _dbContext.Restaurants
            .Include("_products")
            .SingleOrDefaultAsync(r => r.Id == restaurantId && r.OwnerUserId == ownerUserId, ct);

    public Task<int> CountOwnedByAsync(int ownerUserId, CancellationToken ct = default) =>
        _dbContext.Restaurants.CountAsync(r => r.OwnerUserId == ownerUserId, ct);

    public async Task AddAsync(Restaurant restaurant, CancellationToken ct = default) =>
        await _dbContext.Restaurants.AddAsync(restaurant, ct);

    public void Update(Restaurant restaurant) => _dbContext.Restaurants.Update(restaurant);
```

The two ownership reads are **tracked** (they feed writes). `GetOwnedByAsync` is `AsNoTracking`
(list screen). `Include("_products")` matches the existing string-based private-field include
convention.

**New `Persistence/Repositories/OrderFulfillmentRepository.cs`** (Phase 5) — same shape.

**`Persistence/Repositories/OrderRepository.cs`** (Phase 5) — add `GetByIdForRestaurantAsync`
mirroring the existing `GetByIdForCustomerAsync` (include `_items`).

### 8.3 New: `Persistence/Queries/` (Phase 4)

`AdminQueries.cs` implementing `IAdminQueries`, `RestaurantOwnerQueries.cs` implementing
`IRestaurantOwnerQueries`. Both take `TalabatDbContext`.

Mandatory implementation rules:

1. `AsNoTracking()` on every query.
2. Project straight into the Application read-model record inside `.Select(...)` — never materialise
   an aggregate and map afterwards.
3. **Admin user/restaurant/order searches must call `.IgnoreQueryFilters()` when
   `filter.IncludeDeleted` is true**, otherwise the global soft-delete filter silently hides the exact
   rows an admin came to see. Re-apply `!x.IsDeleted` manually when `IncludeDeleted` is false.
4. Count and page in one round trip:
   `var total = await q.CountAsync(ct); var items = await q.Skip((page-1)*size).Take(size).ToListAsync(ct);`
5. Always `OrderBy` a deterministic key before `Skip/Take` (SQL Server paging is undefined otherwise).
   Use `OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)`.
6. No authorization logic. No writes. No `SaveChanges`.

Sketch:

```csharp
public async Task<PagedResult<AdminUserSummary>> SearchUsersAsync(
    AdminUserFilter filter, CancellationToken ct = default)
{
    var query = _dbContext.Users.AsNoTracking();

    if (filter.IncludeDeleted)
    {
        query = _dbContext.Users.AsNoTracking().IgnoreQueryFilters();
    }

    if (filter.UserType is { } userType)
    {
        query = query.Where(u => (u.UserType & userType) == userType);
    }

    if (filter.IsActive is { } isActive)
    {
        query = query.Where(u => u.IsActive == isActive);
    }

    if (!string.IsNullOrWhiteSpace(filter.Search))
    {
        var term = filter.Search.Trim();
        query = query.Where(u => u.FullName.Contains(term) || u.Email!.Contains(term));
    }

    var total = await query.CountAsync(ct);

    var items = await query
        .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
        .Skip((filter.Page - 1) * filter.PageSize)
        .Take(filter.PageSize)
        .Select(u => new AdminUserSummary(
            u.Id, u.FullName, u.Email, u.UserType, u.IsActive, u.IsDeleted,
            u.AgentApprovalStatus, u.DeliveryAgentStatus, u.CreatedAt))
        .ToListAsync(ct);

    return new PagedResult<AdminUserSummary>(items, filter.Page, filter.PageSize, total);
}
```

> `AdminUserSummary` exposes `UserType` (a Domain enum). That is acceptable — the existing
> `MenuProduct` read model already exposes the Domain `Money` value object. Host DTOs still convert
> it to strings for the wire.

### 8.4 `Identity/UserCapabilityService.cs` (Phase 3)

Implement the five new methods by copying the exact structural template of the existing
`ApproveDeliveryAgentAsync`:

```
EnsureTransactionAsync → load user via _userManager.Users → null ⇒ NotFound + rollback
→ call the domain method → _dbContext.SaveChangesAsync
→ SynchronizeCapabilityRolesAsync → UpdateSecurityStampAsync
→ CommitOrReleaseAsync → Success(user.Id)
```

`SynchronizeCapabilityRolesAsync` already handles `Admin` and `RestaurantOwner` — **do not modify it.**

`UpdateSecurityStampAsync` is not optional on revoke: it is what makes an outstanding refresh token
stop producing tokens with the removed role.

### 8.5 New: `Identity/AdminBootstrapper.cs` (Phase 3, G7)

The system has no admin and no way to create one.

```csharp
public static class AdminBootstrapper
{
    public static async Task EnsureInitialAdminAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var email    = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;   // not configured => no-op. Never invent credentials.
        }

        var userManager = services.GetRequiredService<UserManager<User>>();

        // Idempotent AND single-shot: only bootstrap when no admin exists at all.
        var anyAdmin = await userManager.Users.AnyAsync(u => (u.UserType & UserType.Admin) != 0, ct);
        if (anyAdmin)
        {
            return;
        }

        var capabilityService = services.GetRequiredService<IUserCapabilityService>();
        var existing = await userManager.FindByEmailAsync(email);

        if (existing is null)
        {
            var user = User.Register(email, email, "Platform Administrator");
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Admin bootstrap failed: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            }
            existing = user;
        }

        var result = await capabilityService.GrantAdminCapabilityAsync(existing.Id, ct);
        if (result.IsFailure)
        {
            throw new InvalidOperationException("Admin bootstrap failed: " + result.Error!.Message);
        }
    }
}
```

**Rules:**
- Called only from `Talabat.Admin.API/Program.cs` at startup.
- `Bootstrap:AdminPassword` comes from **user-secrets in development and a secret store in
  production**. It must never appear in `appsettings.json`, `appsettings.Development.json`, or git.
- If either value is missing the method is a no-op — a misconfigured deploy must not create a
  guessable admin.
- Add a startup log line stating whether bootstrap ran, for auditability.

### 8.6 `DependencyInjection.cs` (Phases 2–5)

Add to `AddInfrastructure`:

```csharp
        services.AddScoped<IOrderFulfillmentRepository, OrderFulfillmentRepository>();
        services.AddScoped<IAdminQueries, AdminQueries>();
        services.AddScoped<IRestaurantOwnerQueries, RestaurantOwnerQueries>();
```

Nothing else changes. `AddInfrastructure` must still register **no** controllers, endpoints,
authentication, or authorization — that contract is stated in
`specs/002-persistence-infrastructure/contracts/persistence-boundary.md`.

---

## 9. Integration with existing customer and delivery flows

**Reuse is mandatory. Do not duplicate a single existing handler.**

### 9.1 Checkout (customer flow) — one addition

Today: `CheckoutHandler` creates the `Order`; the caller invokes `CreateDeliveryForOrderHandler`.

Change: at the same call site, also invoke `CreateOrderFulfillmentForOrderHandler`
`(orderId, restaurantId)`. Fulfillment creation is idempotent-by-constraint — the unique index on
`OrderId` means a retry produces a `409`, not a duplicate.

**Customer-visible behaviour is unchanged in this increment.** The customer order-details response
does **not** gain a fulfillment status field yet (§16) — that is a Customer API contract change and
belongs to its own increment with its own tests.

### 9.2 Delivery flow — unchanged for agents

`AssignDeliveryAgentHandler`, `GoOnline/GoOffline`, the six lifecycle handlers, `UpdateLocation`,
and all three delivery queries are **reused as-is** by the Admin API. No new delivery handlers.

Admin manual assignment = `AssignDeliveryAgentHandler` behind `POST /api/admin/deliveries/{id}/assign`.
It already loads `Delivery` + `User` and delegates to `DeliveryAssignmentDomainService`, which marks
the agent `Busy`. The only thing the Admin API adds is a different authorization policy in front of
the same use case.

> ⚠ `Talabat.Delivery.API` exposes `POST /api/agent/deliveries/{id}/assign` behind
> `DeliveryAgentAccess` — i.e. self-assignment. The admin route is a *second caller of the same
> handler*, not a fork of it. Keep both.

### 9.3 Rejection ⇄ delivery cancellation (the one new cross-flow rule)

Because `Delivery` is created at checkout (D4), a restaurant rejection must release it. Implemented
inside `RejectOrderHandler` in one `SaveChangesAsync` (§7.6). Admin cancellation of a fulfillment does
the same.

**Ordering matters:** mutate `OrderFulfillment` first, then `Delivery`, then commit once. If the
delivery is already terminal, skip it silently — a delivered order cannot be un-delivered by a late
rejection, and the fulfillment transition guard already prevents rejecting a non-`AwaitingRestaurant`
fulfillment.

### 9.4 Catalog visibility (customer flow) — unchanged contract, new writers

`BrowseRestaurantsHandler` filters on `IsActive`; `GetRestaurantMenuHandler` returns products with
availability flags. Owner and admin edits flow through the same `Restaurant` aggregate, so customer
reads pick them up with **no customer-side change**.

Consequence to test explicitly: an owner deactivating a restaurant must remove it from
`GET /api/catalog/restaurants`, and an owner marking a product unavailable must make checkout fail
with the existing `CheckoutProductsUnavailable` outcome. These are regression tests in
`Talabat.Customer.API.Tests`, not new features.

### 9.5 Capability grants and live customer sessions

Granting `RestaurantOwner` to an account that is also a `Customer` must not disturb the customer
session beyond the security-stamp rotation that `UserCapabilityService` already performs. The user
re-authenticates once and keeps both roles. `UserType` is `[Flags]` precisely for this.

**Test to add:** a user with `Customer | RestaurantOwner` can still complete a full checkout on the
Customer API, and can still manage their restaurant on the Owner API, with two different tokens.

---

## 10. Persistence, migrations, seed data, constraints

### 10.1 Schema delta

| Table | Change |
|---|---|
| `Restaurants` | `+ OwnerUserId INT NULL` FK → `AspNetUsers(Id)` `NO ACTION`; `+ RowVersion rowversion`; `+ IX_Restaurants_OwnerUserId` (filtered `IS NOT NULL`) |
| `Products` | `+ RowVersion rowversion` |
| `OrderFulfillments` **(new)** | `Id` IDENTITY PK; `OrderId` INT NOT NULL FK → `Orders(Id)` `NO ACTION`; `RestaurantId` INT NOT NULL FK → `Restaurants(Id)` `NO ACTION`; `Status` INT NOT NULL; five nullable `datetime2` timestamps; `RejectionReason NVARCHAR(500) NULL`; `EstimatedPreparationMinutes INT NULL`; audit + soft-delete columns; `RowVersion rowversion`; `UX_OrderFulfillments_OrderId` (unique, filtered `IsDeleted = 0`); `IX_OrderFulfillments_RestaurantId_Status`; 3 CHECK constraints |

**Every FK is `NO ACTION`.** `AspNetUsers` → `Restaurants` → `OrderFulfillments` ← `Orders` →
`AspNetUsers` creates multiple paths to the same table; SQL Server rejects multiple cascade paths.
Deletion is soft everywhere, so cascade is not needed.

### 10.2 Migration order and commands

Run from repo root. **Review the generated `.cs` before applying — never auto-apply.**

```powershell
# Migration 1 — Phase 2
dotnet ef migrations add AddRestaurantOwnership `
  --project src\Talabat\Talabat.Infrastructure `
  --startup-project src\Talabat\Talabat.API `
  --output-dir Persistence\Migrations

# Migration 2 — Phase 5
dotnet ef migrations add AddOrderFulfillment `
  --project src\Talabat\Talabat.Infrastructure `
  --startup-project src\Talabat\Talabat.API `
  --output-dir Persistence\Migrations

dotnet ef database update `
  --project src\Talabat\Talabat.Infrastructure `
  --startup-project src\Talabat\Talabat.API
```

**Migration review checklist (blocking):**

- [ ] No existing table dropped, renamed, or retyped.
- [ ] No `AspNetUsers` / `AspNetRoles` / Identity table altered.
- [ ] No change to the `CatalogSeedData` `HasData` rows (`OwnerUserId` is nullable, so there must be
      zero `UpdateData` operations against seeded restaurants — **if any appear, stop and fix the
      configuration**).
- [ ] `RowVersion` columns are `rowversion`, not `varbinary(max)`.
- [ ] All new FKs are `ReferentialAction.NoAction`.
- [ ] Filtered unique indexes carry the intended filters.
- [ ] Applies cleanly to (a) a fresh database and (b) a database already at `AddDeliveryRowVersion`.

### 10.3 Seed data

- **Roles** — no change. `IdentityDataSeeder.SeedRolesAsync` already seeds all four; call it from both
  new hosts' startup.
- **Catalog** — no change. Seeded restaurants stay unowned (`OwnerUserId = NULL`).
- **Admin** — runtime bootstrap only (§8.5). **Never `HasData` a user**: password hashes, security
  stamps, and concurrency stamps churn migrations and would commit a credential.
- **Development owner fixture** — optional, environment-gated, in the Owner API host only: create a
  demo owner account, grant the capability, and assign it seeded restaurant `Id = 1`. Guard with
  `if (app.Environment.IsDevelopment())` and make it idempotent. Do **not** put it in a migration.

---

## 11. Authentication and authorization

### 11.1 `Talabat.Identity/IdentityServerConfig.cs` additions

```csharp
    public static IEnumerable<ApiScope> ApiScopes =>
    new ApiScope[]
    {
        new ApiScope("customer.api",   "Customer API Access"),
        new ApiScope("delivery.api",   "Delivery Agent API Access"),
        new ApiScope("restaurant.api", "Restaurant Owner API Access"),   // NEW
        new ApiScope("admin.api",      "Admin API Access")               // NEW
    };

    public static IEnumerable<ApiResource> ApiResources =>
    new ApiResource[]
    {
        // ... existing two ...
        new ApiResource("talabat.restaurant.api", "Talabat Restaurant Owner API")
        {
            Scopes = { "restaurant.api" },
            UserClaims = { "role" }              // NO restaurant ids — see D6
        },
        new ApiResource("talabat.admin.api", "Talabat Admin API")
        {
            Scopes = { "admin.api" },
            UserClaims = { "role" }
        }
    };
```

Clients:

```csharp
        new Client
        {
            ClientId = "talabat-restaurant-spa",
            ClientName = "Talabat Restaurant Owner Portal",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "http://localhost:4400/signin-callback" },
            PostLogoutRedirectUris = { "http://localhost:4400/signout-callback" },
            AllowedCorsOrigins = { "http://localhost:4400" },
            AllowedScopes = { "openid", "profile", "roles", "restaurant.api", "offline_access" },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 900,
            UpdateAccessTokenClaimsOnRefresh = true,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 1296000
        },
        new Client
        {
            ClientId = "talabat-admin-spa",
            ClientName = "Talabat Admin Console",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "http://localhost:4500/signin-callback" },
            PostLogoutRedirectUris = { "http://localhost:4500/signout-callback" },
            AllowedCorsOrigins = { "http://localhost:4500" },
            AllowedScopes = { "openid", "profile", "roles", "admin.api" },
            AllowOfflineAccess = false,        // deliberate: no long-lived admin refresh tokens
            AccessTokenLifetime = 300,         // deliberate: 5 minutes
            UpdateAccessTokenClaimsOnRefresh = true
        }
```

The admin client's shorter lifetime and absent refresh token are the deliberate blast-radius control
for the highest-privilege token in the system. **Do not "make it consistent" with the others.**

Also remove the `https://oauth.pstmn.io/...` Postman redirect URIs from the two **new** clients —
they exist on the old clients as a dev shortcut and should not spread.

`TalabatProfileService` needs **no change**: it already emits every Identity role the user holds,
and `SynchronizeCapabilityRolesAsync` now puts `Admin`/`RestaurantOwner` in that set.

### 11.2 Host policies

`Talabat.RestaurantOwner.API/Auth/AuthorizationPolicies.cs`:

```csharp
public static class AuthorizationPolicies
{
    public const string RestaurantOwnerAccess = nameof(RestaurantOwnerAccess);
}
```

`Talabat.Admin.API/Auth/AuthorizationPolicies.cs`:

```csharp
public static class AuthorizationPolicies
{
    public const string AdminAccess             = nameof(AdminAccess);
    // Seams for future granularity (D7) — identical requirements today.
    public const string AdminUserManagement     = nameof(AdminUserManagement);
    public const string AdminCatalogManagement  = nameof(AdminCatalogManagement);
    public const string AdminDeliveryOperations = nameof(AdminDeliveryOperations);
}
```

Registration (Admin host `Program.cs`):

```csharp
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

void AdminPolicy(AuthorizationPolicyBuilder policy)
{
    policy.RequireAuthenticatedUser();
    policy.AddRequirements(new ScopeRequirement("admin.api"));
    policy.RequireRole(IdentityRoleNames.Admin);   // "Admin"
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.AdminAccess,             AdminPolicy)
    .AddPolicy(AuthorizationPolicies.AdminUserManagement,     AdminPolicy)
    .AddPolicy(AuthorizationPolicies.AdminCatalogManagement,  AdminPolicy)
    .AddPolicy(AuthorizationPolicies.AdminDeliveryOperations, AdminPolicy);
```

> `IdentityRoleNames` is `internal` to `Talabat.Infrastructure`. Either make it `public` (smallest
> change, and hosts already reference Infrastructure) or duplicate the four literals as host
> constants. **Recommended: make it `public`** — one `internal`→`public` edit beats four copies of a
> magic string. Add an architecture test asserting the literals match.

### 11.3 Capability gates

`Talabat.RestaurantOwner.API/Middleware/RequireRestaurantOwnerCapabilityAttribute.cs` — modelled on
`RequireCustomerProfileAttribute`, but **async-resolving** (D12):

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireRestaurantOwnerCapabilityAttribute : Attribute, IAsyncActionFilter
{
    public const string UserTypeItemKey = "talabat.usertype";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var currentUser = services.GetRequiredService<ICurrentUser>();

        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            await next();      // the [Authorize] policy already produced 401
            return;
        }

        var resolver = services.GetRequiredService<ICurrentUserCapabilityResolver>();
        var userType = await resolver.GetUserTypeAsync(userId, context.HttpContext.RequestAborted);
        context.HttpContext.Items[UserTypeItemKey] = userType;

        if (userType.HasFlag(UserType.RestaurantOwner))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title  = "Conflict",
            Detail = "This account does not hold the restaurant-owner capability.",
            Type   = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Extensions = { ["errorCode"] = ApplicationErrorCodes.OwnerCapabilityNotGranted }
        }) { StatusCode = StatusCodes.Status409Conflict };
    }
}
```

`Talabat.Admin.API/Middleware/RequireAdminCapabilityAttribute.cs` is identical with
`UserType.Admin` / `AdminCapabilityNotGranted`.

The host's `CurrentUser` reads `HttpContext.Items[UserTypeItemKey]` when present, avoiding a second
DB round trip and the sync-over-async getter.

**Why the gate exists on top of the role policy:** the role claim is baked into a token that may be
up to 15 minutes old (5 for admin). A revoked capability must stop working immediately for writes.

### 11.4 Authorization matrix — Restaurant Owner API

Base: `https://localhost:7xxx`. Audience `talabat.restaurant.api`, scope `restaurant.api`, role `RestaurantOwner`.
All routes carry `[Authorize(Policy = RestaurantOwnerAccess)]` + `[RequireRestaurantOwnerCapability]`.

| Method | Route | Handler | Ownership | Success |
|---|---|---|---|---|
| GET | `/api/me/restaurants` | `GetOwnedRestaurantsHandler` | scoped read | 200 |
| GET | `/api/me/restaurants/{restaurantId:int}` | `GetOwnedRestaurantDetailsHandler` | `GetByIdWithProductsForOwnerAsync` | 200 / 404 |
| PUT | `/api/me/restaurants/{restaurantId:int}/profile` | `UpdateRestaurantProfileHandler` | ✔ | 204 |
| PUT | `/api/me/restaurants/{restaurantId:int}/opening-hours` | `UpdateRestaurantOpeningHoursHandler` | ✔ | 204 |
| POST | `/api/me/restaurants/{restaurantId:int}/activate` | `SetRestaurantActiveStateHandler(true)` | ✔ | 204 |
| POST | `/api/me/restaurants/{restaurantId:int}/deactivate` | `SetRestaurantActiveStateHandler(false)` | ✔ | 204 |
| GET | `/api/me/restaurants/{restaurantId:int}/products` | `GetOwnedRestaurantDetailsHandler` | ✔ | 200 |
| POST | `/api/me/restaurants/{restaurantId:int}/products` | `AddMenuProductHandler` | ✔ | 201 / 409 `DuplicateProduct` |
| PUT | `/api/me/restaurants/{restaurantId:int}/products/{productId:int}` | `UpdateMenuProductDetailsHandler` | ✔ | 204 |
| PUT | `/api/me/restaurants/{restaurantId:int}/products/{productId:int}/price` | `UpdateMenuProductPriceHandler` | ✔ | 204 |
| PUT | `/api/me/restaurants/{restaurantId:int}/products/{productId:int}/availability` | `SetMenuProductAvailabilityHandler` | ✔ | 204 |
| GET | `/api/me/restaurants/{restaurantId:int}/orders` | `GetRestaurantOrdersHandler` | ✔ then read model | 200 |
| GET | `/api/me/restaurants/{restaurantId:int}/orders/{orderId:int}` | `GetRestaurantOrderDetailsHandler` | ✔ | 200 / 404 |
| POST | `/api/me/restaurants/{restaurantId:int}/orders/{orderId:int}/accept` | `AcceptOrderHandler` | ✔ | 204 / 409 |
| POST | `/api/me/restaurants/{restaurantId:int}/orders/{orderId:int}/reject` | `RejectOrderHandler` | ✔ | 204 / 409 |
| POST | `/api/me/restaurants/{restaurantId:int}/orders/{orderId:int}/preparing` | `MarkOrderPreparingHandler` | ✔ | 204 / 409 |
| POST | `/api/me/restaurants/{restaurantId:int}/orders/{orderId:int}/ready` | `MarkOrderReadyHandler` | ✔ | 204 / 409 |
| GET | `/health` | — | anonymous | 200 |

`{restaurantId}` in the route is **not** an ownership claim — it is a resource selector, validated
against the token-derived `UserId` inside the handler. This is the same shape as
`/api/agent/deliveries/{deliveryId}` in the Delivery API.

### 11.5 Authorization matrix — Admin API

Audience `talabat.admin.api`, scope `admin.api`, role `Admin`.

| Method | Route | Policy | Handler / reuse |
|---|---|---|---|
| GET | `/api/admin/users` | `AdminUserManagement` | `SearchUsersHandler` → `IAdminQueries` |
| GET | `/api/admin/users/{userId:int}` | `AdminUserManagement` | `GetUserDetailsHandler` |
| POST | `/api/admin/users/{userId:int}/activate` | `AdminUserManagement` | `ActivateUserHandler` → `IUserCapabilityService` |
| POST | `/api/admin/users/{userId:int}/deactivate` | `AdminUserManagement` | `DeactivateUserHandler` → existing `DeactivateUserAsync` |
| DELETE | `/api/admin/users/{userId:int}` | `AdminUserManagement` | `SoftDeleteUserHandler` → existing `SoftDeleteUserAsync` |
| POST | `/api/admin/users/{userId:int}/capabilities/admin` | `AdminUserManagement` | `GrantAdminCapabilityHandler` |
| DELETE | `/api/admin/users/{userId:int}/capabilities/admin` | `AdminUserManagement` | `RevokeAdminCapabilityHandler` (last-admin + self guard) |
| POST | `/api/admin/users/{userId:int}/capabilities/restaurant-owner` | `AdminUserManagement` | `GrantRestaurantOwnerCapabilityHandler` |
| DELETE | `/api/admin/users/{userId:int}/capabilities/restaurant-owner` | `AdminUserManagement` | `RevokeRestaurantOwnerCapabilityHandler` (owns-restaurants guard) |
| GET | `/api/admin/delivery-agents/applications` | `AdminUserManagement` | `GetAgentApplicationsHandler` |
| POST | `/api/admin/delivery-agents/{userId:int}/approve` | `AdminUserManagement` | **existing** `ApproveDeliveryAgentAsync` |
| POST | `/api/admin/delivery-agents/{userId:int}/reject` | `AdminUserManagement` | **existing** `RejectDeliveryAgentAsync` |
| GET | `/api/admin/restaurants` | `AdminCatalogManagement` | `SearchRestaurantsHandler` |
| POST | `/api/admin/restaurants` | `AdminCatalogManagement` | `CreateRestaurantHandler` |
| PUT | `/api/admin/restaurants/{restaurantId:int}/owner` | `AdminCatalogManagement` | `AssignRestaurantOwnerHandler` |
| DELETE | `/api/admin/restaurants/{restaurantId:int}/owner` | `AdminCatalogManagement` | `ClearRestaurantOwnerHandler` |
| POST | `/api/admin/restaurants/{restaurantId:int}/activate` \| `/deactivate` | `AdminCatalogManagement` | `AdminSetRestaurantActiveStateHandler` |
| GET | `/api/admin/orders` | `AdminAccess` | `SearchOrdersHandler` |
| GET | `/api/admin/orders/{orderId:int}` | `AdminAccess` | `GetAdminOrderDetailsHandler` |
| GET | `/api/admin/deliveries` | `AdminDeliveryOperations` | `SearchDeliveriesHandler` |
| GET | `/api/admin/deliveries/{deliveryId:int}` | `AdminDeliveryOperations` | `GetAdminDeliveryDetailsHandler` |
| POST | `/api/admin/deliveries/{deliveryId:int}/assign` | `AdminDeliveryOperations` | **existing** `AssignDeliveryAgentHandler` |
| POST | `/api/admin/deliveries/{deliveryId:int}/cancel` | `AdminDeliveryOperations` | **existing** `CancelDeliveryHandler` |
| GET | `/health` | — | anonymous |

`AssignRestaurantOwnerHandler` must validate that the target user holds `UserType.RestaurantOwner`
before assignment, and fail `Validation`/`RestaurantOwnershipRequired` otherwise. Assigning a
restaurant to a user without the capability produces an invisible restaurant.

### 11.6 Retire the Identity host's dev-only admin endpoints (Phase 7, G8)

Delete `ApproveDeliveryAgent` / `RejectDeliveryAgent` from
`src/Talabat/Talabat.Identity/Controllers/AccountController.cs`, together with the
`// TODO(Phase 9): replace with AdminAccess policy` comments and the `IHostEnvironment` dependency if
it becomes unused. Update `tests/Talabat.Identity.Tests` accordingly.

**Do this only after the Admin API equivalents pass their integration tests** — otherwise there is a
window with no approval path at all.

---

## 12. New API host composition roots

### 12.1 `Talabat.Api.Shared` (Phase 1, D11)

`src/Talabat/Talabat.Api.Shared/Talabat.Api.Shared.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Talabat.Application\Talabat.Application.csproj" />
  </ItemGroup>

</Project>
```

Contents — moved verbatim, namespace changed to `Talabat.Api.Shared.*`:

```
Talabat.Api.Shared/
  Auth/ScopeRequirement.cs           // from both hosts (identical)
  Auth/ScopeHandler.cs               // from both hosts (identical)
  Errors/DomainExceptionHandler.cs   // from both hosts (identical)
  Results/UseCaseResultExtensions.cs // from both hosts (identical)
  Paging/PageQuery.cs                // NEW: page/pageSize binding + clamping helper
  OpenApi/BearerSecuritySchemeTransformer.cs  // the duplicated AddDocumentTransformer lambda
```

**Explicitly NOT moved:** `AuthorizationPolicies` (host-specific names), `CurrentUser` (host-specific
claim handling), controllers, contracts. Keeping `CurrentUser` per host preserves the ability to fix
D12 in the new hosts without touching the old ones.

**Phase 1 acceptance:** `Talabat.Customer.API.Tests` and `Talabat.Delivery.API.Tests` pass unchanged
except for `using` statements. Zero behavioural diff. Add `Talabat.Api.Shared` to `Talabat.slnx`.

### 12.2 `Talabat.RestaurantOwner.API` (Phase 6)

```
src/Talabat/Talabat.RestaurantOwner.API/
  Auth/AuthorizationPolicies.cs
  Auth/CurrentUser.cs
  Contracts/Restaurants/RestaurantContracts.cs
  Contracts/Menu/MenuContracts.cs
  Contracts/Orders/RestaurantOrderContracts.cs
  Contracts/Common/MoneyDto.cs           // reuse the existing shape
  Controllers/OwnedRestaurantsController.cs
  Controllers/MenuController.cs
  Controllers/RestaurantOrdersController.cs
  Middleware/RequireRestaurantOwnerCapabilityAttribute.cs
  Properties/launchSettings.json         // https port 7xxx, http 5xxx — pick free ports
  appsettings.json
  appsettings.Development.json
  Program.cs
```

`.csproj` — copy `Talabat.Delivery.API.csproj` and add the shared reference:

```xml
  <ItemGroup>
    <ProjectReference Include="..\Talabat.Infrastructure\Talabat.Infrastructure.csproj" />
    <ProjectReference Include="..\Talabat.Api.Shared\Talabat.Api.Shared.csproj" />
  </ItemGroup>
```

Packages: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`,
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Microsoft.OpenApi`,
`Swashbuckle.AspNetCore.SwaggerUI` — same versions (10.0.9 / 2.7.5 / 10.2.3) as the existing hosts.
**Do not introduce a different version of anything.**

`Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddBearerSecurityScheme());   // Api.Shared
builder.Services.AddHttpContextAccessor();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddUnifiedUserIdentityCore();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.RestaurantOwnerAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ScopeRequirement("restaurant.api"));
        policy.RequireRole(IdentityRoleNames.RestaurantOwner);
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    var authority = builder.Configuration["Identity:Authority"] ?? "https://localhost:7237";

    options.Authority           = authority;
    options.Audience            = "talabat.restaurant.api";
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidIssuer              = authority,
        ValidateAudience         = true,
        ValidAudience            = "talabat.restaurant.api",
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        RoleClaimType            = "role",
        NameClaimType            = "sub"
    };
});

builder.Services.AddCors(options =>
    options.AddPolicy("SpaCorsPolicy", policy =>
        policy.WithOrigins("http://localhost:4400")
              .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddHealthChecks().AddDbContextCheck<TalabatDbContext>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();   // Api.Shared

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "Talabat Restaurant Owner API v1");
        o.RoutePrefix = "swagger";
    });
}

app.UseExceptionHandler(_ => { });
app.UseHttpsRedirection();
app.UseCors("SpaCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }   // required for WebApplicationFactory<Program>
```

Representative controller — note how thin it is and where `OwnerUserId` comes from:

```csharp
[ApiController]
[Route("api/me/restaurants/{restaurantId:int}/products")]
[Authorize(Policy = AuthorizationPolicies.RestaurantOwnerAccess)]
[RequireRestaurantOwnerCapability]
public sealed class MenuController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly AddMenuProductHandler _addMenuProduct;
    private readonly UpdateMenuProductPriceHandler _updatePrice;
    private readonly SetMenuProductAvailabilityHandler _setAvailability;

    // ctor omitted

    [HttpPost]
    public async Task<IActionResult> AddProduct(
        int restaurantId,
        [FromBody] AddMenuProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddMenuProductCommand(
            _currentUser.UserId!.Value,        // <-- token-derived, never from the body
            restaurantId,
            request.Name,
            request.Description,
            request.Price,
            request.ImageUrl,
            request.IsAvailable);

        var result = await _addMenuProduct.Handle(command, cancellationToken);

        return result.ToActionResult(productId =>
            CreatedAtAction(nameof(AddProduct), new { restaurantId, productId }, new { id = productId }));
    }

    [HttpPut("{productId:int}/price")]
    public async Task<IActionResult> UpdatePrice(
        int restaurantId,
        int productId,
        [FromBody] UpdateProductPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updatePrice.Handle(
            new UpdateMenuProductPriceCommand(_currentUser.UserId!.Value, restaurantId, productId, request.Amount),
            cancellationToken);

        return result.ToActionResult(_ => NoContent());
    }
}
```

Controller rules (enforced by review and by `SC-007`-style tests):
no `if`/`switch` on domain state, no `TalabatDbContext`, no repository, no Domain aggregate in a
response body, no `ownerUserId` accepted from route/query/body.

### 12.3 `Talabat.Admin.API` (Phase 7)

Identical structure. Differences:

- Audience `talabat.admin.api`, scope `admin.api`, role `Admin`, CORS origin `http://localhost:4500`.
- `Middleware/RequireAdminCapabilityAttribute.cs`.
- Four policies registered (§11.2).
- Startup extras, in this order, wrapped in a startup scope:
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      await IdentityDataSeeder.SeedRolesAsync(scope.ServiceProvider);
      await AdminBootstrapper.EnsureInitialAdminAsync(scope.ServiceProvider, builder.Configuration);
  }
  ```
  Roles must exist before a capability grant tries to add a user to one.
- Controllers: `AdminUsersController`, `AdminDeliveryAgentsController`, `AdminRestaurantsController`,
  `AdminOrdersController`, `AdminDeliveriesController`.

---

## 13. Shared vs split — decision table

| Concern | Shared | Split per host | Rationale |
|---|---|---|---|
| Domain aggregates, invariants, exceptions | ✅ `Talabat.Domain` | | One model, many actors |
| Use-case handlers | ✅ `Talabat.Application` | | Admin assign = agent assign; forking would double the bug surface |
| Repositories, EF config, migrations, UoW | ✅ `Talabat.Infrastructure` | | One database, one schema owner |
| `IUserCapabilityService` | ✅ | | Single writer for flags+roles (D8) |
| Read-model query services | ✅ (interfaces in Application, impl in Infrastructure) | | Shared paging/projection mechanics |
| `UseCaseResult` → HTTP mapping | ✅ `Talabat.Api.Shared` | | Error semantics must be identical across APIs |
| `ScopeHandler` / `ScopeRequirement` | ✅ `Talabat.Api.Shared` | | Pure mechanics |
| `DomainExceptionHandler` | ✅ `Talabat.Api.Shared` | | Same mapping table |
| OpenAPI bearer transformer | ✅ `Talabat.Api.Shared` | | Identical today |
| `AuthorizationPolicies` constants | | ✅ | Names, scopes, roles differ per host |
| `ICurrentUser` implementation | | ✅ | Lets new hosts fix D12 without touching old ones |
| Capability action filters | | ✅ | Different flag, different error code |
| Request/response DTOs | | ✅ | An owner's "restaurant" and an admin's "restaurant" are different projections; sharing them couples two API contracts that must evolve independently |
| CORS origins, ports, token lifetimes | | ✅ | Different clients, different risk |

**Anti-pattern to avoid:** a shared `Talabat.Api.Shared.Contracts` DTO library. The moment the admin
console needs a field the owner portal must not see, a shared DTO forces either a leak or a fork.
Duplicated DTO records are the cheaper failure mode.

---

## 14. Test strategy

Existing frameworks only: **xUnit**, `WebApplicationFactory<Program>`, real SQL Server per-test
database (`CustomWebApplicationFactory` creates `TalabatTest_API_{guid}` and drops it on dispose).
**Do not add Moq, FluentAssertions, NSubstitute, Testcontainers, or any new test package** — the
existing suites use hand-written fakes and plain `Assert`.

### 14.1 Domain tests — `tests/Talabat.Domain.Tests/`

`OrderFulfillmentTests.cs` — one test per transition edge:

- new fulfillment starts `AwaitingRestaurant`
- `Accept` from `AwaitingRestaurant` → `Accepted`, sets `AcceptedAt`
- `Accept` twice → `InvalidOrderFulfillmentStatusTransitionException`
- `Accept` with a foreign `restaurantId` → `OrderFulfillmentRestaurantMismatchException`
- `Reject` with blank reason → `ArgumentException`
- `Reject` then `MarkPreparing` → `OrderFulfillmentTerminalStateException`
- `MarkReadyForPickup` valid from both `Accepted` and `Preparing`, invalid from `AwaitingRestaurant`
- `CancelByAdmin` from each non-terminal state; from terminal → throws
- non-UTC `currentTime` → `Guard.Utc` throws
- `EstimatedPreparationMinutes = 0` → throws

`RestaurantOwnershipTests.cs`:

- `AssignOwner(0)` / negative → throws; valid → `IsOwnedBy(id)` true
- `ClearOwner` → `IsOwnedBy` false for everyone
- `UpdateProfile` trims and rejects blank name/description
- `UpdateProductDetails` renaming to another existing product's name → `DuplicateProductException`
- `UpdateProductDetails` renaming a product to **its own** current name → succeeds (regression guard
  for the `other.Id != productId` predicate)
- `UpdateProductDetails` on unknown id → `ProductNotFoundException`

`UserCapabilityTests.cs`:

- grant/revoke Admin and RestaurantOwner set and clear only their own flag
- granting twice is idempotent
- granting `Admin` to a `Customer` preserves `Customer` (flags composition)
- revoking a flag the user never had is a no-op

### 14.2 Application tests — `tests/Talabat.Application.Tests/`

Extend `TestDoubles/` with `FakeOrderFulfillmentRepository`, `FakeAdminQueries`,
`FakeRestaurantOwnerQueries`, `FakeUserCapabilityService`, and add the ownership methods to the
existing `FakeRestaurantRepository`.

**The single most important test class** — `RestaurantOwner/OwnershipScopingTests.cs`. For **every**
owner write handler, assert that a command carrying a foreign `OwnerUserId`:

1. returns `IsFailure`,
2. with category `NotFound` (**not** `OwnershipMismatch`/`403` — no existence disclosure),
3. and that the fake repository recorded **zero** writes and `SaveChangesAsync` was **not** called.

Table-drive it so a newly added owner handler that forgets the scoped read fails immediately.

Other required Application tests:

- `RejectOrderTests`: rejection cancels a `PendingAssignment` delivery; leaves a terminal delivery
  untouched; commits exactly once (assert the fake UoW's call count == 1).
- `AcceptOrderTests`: accept on an already-accepted fulfillment → `Conflict`, no write.
- `RevokeAdminCapabilityTests`: self-revoke → `Conflict`; last admin → `Conflict`; second admin →
  delegates to the capability service and never touches `IUnitOfWork` (assert UoW call count == 0).
- `RevokeRestaurantOwnerCapabilityTests`: user still owns ≥1 restaurant → `Conflict`
  `OwnerStillOwnsRestaurants`.
- `AssignRestaurantOwnerTests`: target without `RestaurantOwner` flag → `Validation` failure.
- `SearchUsersTests`: `PageSize` clamped to `[1, 100]` in the handler, not the query service.

### 14.3 Infrastructure tests — `tests/Talabat.Infrastructure.Tests/`

- `RestaurantOwnershipPersistenceTests`: `OwnerUserId` round-trips; FK rejects a non-existent user;
  `GetByIdForOwnerAsync` returns `null` for a foreign owner; `CountOwnedByAsync` is accurate.
- `OrderFulfillmentPersistenceTests`: generated `Id` > 0 after save; unique index rejects a second
  fulfillment for one `OrderId`; each CHECK constraint rejects its invalid value; status enum stored
  as `int`; soft-delete filter hides deleted rows.
- `ConcurrencyTests`: two `Restaurant` contexts editing the same product price → second
  `SaveChangesAsync` throws `DbUpdateConcurrencyException` (proves `RowVersion` is wired).
- `AdminQueriesTests`: `IncludeDeleted = true` surfaces soft-deleted users;
  `IncludeDeleted = false` hides them; paging returns a stable order across pages; `TotalCount`
  matches an unpaged count.
- `MigrationTests`: `Database.Migrate()` on a clean database succeeds and produces the expected
  indexes/constraints (the suite already migrates per test database — assert on
  `INFORMATION_SCHEMA` / `sys.indexes`).

### 14.4 API tests — two new projects

`tests/Talabat.RestaurantOwner.API.Tests/` and `tests/Talabat.Admin.API.Tests/`, each copying
`Infrastructure/CustomWebApplicationFactory.cs` + `Infrastructure/TestAuthHandler.cs` from
`Talabat.Customer.API.Tests` and adapting the seeded fixture.

Owner fixture must seed: an owner user with `RestaurantOwner`; a **second** owner user; a restaurant
owned by the first; a restaurant owned by the second; an unowned restaurant; an order + fulfillment
against the first restaurant.

Required test classes:

| Class | Asserts |
|---|---|
| `AuthEnforcementTests` | every route returns `401` with no token; `403` with a valid token missing the scope; `403` with the right scope but wrong role |
| `AudienceIsolationTests` | **a token minted for `talabat.customer.api` is rejected by the Owner API and the Admin API** — this is the test that proves D1 |
| `CapabilityGateTests` | a user in the `RestaurantOwner` role whose `UserType` flag has been revoked in the DB gets `409 OwnerCapabilityNotGranted` — proves D5 layer 2 |
| `OwnershipTests` | owner A gets `404` (never `403`) for every route on owner B's restaurant and on the unowned restaurant |
| `MenuEndpointTests` | full CRUD happy paths + `409 DuplicateProduct` |
| `OrderLifecycleEndpointTests` | accept → preparing → ready; reject sets the linked delivery to `Cancelled`; invalid transitions → `409` |
| `ErrorMappingTests` | each `ApplicationErrorCategory` maps to its documented status with `errorCode` in `ProblemDetails.Extensions` |

Admin fixture adds: a pending agent applicant, a soft-deleted user, an extra admin (so
last-admin tests can pass and fail deterministically).

Required admin test classes: `AdminAuthEnforcementTests`, `AudienceIsolationTests`,
`UserManagementEndpointTests` (incl. last-admin and self-revoke `409`s),
`AgentApprovalEndpointTests` (approve grants role + flag + `DeliveryAgentStatus = Offline`),
`RestaurantOwnershipEndpointTests` (assign to a non-owner-capable user → `400`),
`DeliveryOperationsEndpointTests` (admin assign reuses the same handler as agent self-assign).

### 14.5 Architecture tests — `tests/Talabat.ArchitectureTests/`

Add in Phase 0 as skipped facts; unskip in the phase that satisfies them.

```csharp
[Fact] public void RestaurantOwner_API_Types_ShouldNotReference_TalabatDbContext_Directly()
[Fact] public void Admin_API_Types_ShouldNotReference_TalabatDbContext_Directly()
[Fact] public void ApiShared_ShouldNotReference_Infrastructure()
[Fact] public void Hosts_ShouldNotReference_EachOther()          // 4 business hosts + Identity
[Fact] public void Domain_ShouldNotReference_UserManagerOrRoleManager()
[Fact] public void Application_ShouldNotReference_UserManagerOrRoleManager()
[Fact] public void Application_ShouldNotExpose_IQueryable_OnPublicContracts()
[Fact] public void IdentityRoleNames_MustMatch_UserTypeFlagNames()
```

`Application_ShouldNotExpose_IQueryable_OnPublicContracts` is worth the ten lines: it is the guard
that stops a future agent from "simplifying" `IAdminQueries` into `IQueryable<User> Users { get; }`.

### 14.6 Command to run everything

```powershell
dotnet build src\Talabat\Talabat.slnx
dotnet test  src\Talabat\Talabat.slnx
dotnet list  src\Talabat\Talabat.slnx package --vulnerable --include-transitive
```

All three must be clean before a phase is accepted.

---

## 15. Risks, trade-offs, and things that must not be done

### 15.1 Risks

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | **Privilege escalation via a self-grant path.** A bug that lets any admin-capable request reach `GrantAdminCapabilityAsync` with an attacker-controlled id is a total compromise. | Critical | Grant/revoke live behind `AdminUserManagement` on a separate audience; `RevokeAdminCapabilityHandler` blocks self-revoke; integration tests assert `401/403` for every non-admin token; audience-isolation test proves a customer token cannot reach the route |
| R2 | **Stale token after capability revoke.** | High | `UpdateSecurityStampAsync` on every capability change (already in `UserCapabilityService`); capability action filter re-reads `UserType` per request; admin access token = 300s, no refresh |
| R3 | **Flag/role drift.** Two stores describe the same fact. | High | Single writer (`IUserCapabilityService`); no controller or handler may call `AddToRoleAsync`; architecture test forbids `UserManager`/`RoleManager` outside Infrastructure |
| R4 | **Admin bootstrap credential leaks into git.** | High | Config-only, no default, no-op when absent, user-secrets in dev; explicit review-gate item |
| R5 | **Soft-delete filter hides data from admin screens**, making the console silently lie. | Medium | `IgnoreQueryFilters()` on admin reads when `IncludeDeleted`; dedicated infrastructure test |
| R6 | **Delivery orphaned by rejection** (a `PendingAssignment` delivery for a rejected order stays in the agent queue). | Medium | `RejectOrderHandler` cancels it in the same commit; integration test asserts the delivery status |
| R7 | **Multiple-cascade-path migration failure** on `AspNetUsers → Restaurants → OrderFulfillments ← Orders → AspNetUsers`. | Medium | All new FKs `NoAction`; migration review checklist item |
| R8 | **Seed-data churn.** A non-nullable `OwnerUserId` would force `UpdateData` on seeded restaurants. | Medium | `int?`; checklist asserts zero `UpdateData` in Migration 1 |
| R9 | **Sync-over-async `ICurrentUser` copied into two more hosts** (D12) → thread-pool starvation under load. | Medium | New hosts resolve in the async action filter and cache in `HttpContext.Items` |
| R10 | **Duplicated host plumbing drifts** (an error-mapping fix lands in 2 of 4 hosts). | Medium | Phase 1 extraction happens *before* the new hosts exist |
| R11 | **Order immutability quietly broken** by a future "just add a status column". | Medium | Domain test asserting `Order` exposes no public mutator; explicit §15.3 entry |
| R12 | **Migration snapshot conflict** from generating migrations with different startup projects. | Low | D15: `Talabat.API` is the only startup project for `dotnet ef` |
| R13 | Folder/assembly mismatch (`Talabat.API` ↔ `Talabat.Customer.API`) confuses path-based tooling. | Low | Do **not** rename in this workstream (§15.3); note it in §16 |
| R14 | Scope creep into analytics/notifications on the admin console. | Low | §16 backlog; reject at review |

### 15.2 Trade-offs consciously accepted

1. **`Restaurant.OwnerUserId` instead of a membership aggregate.** Buys a trivial, additive
   migration and a one-column ownership check. Costs a future migration when restaurant staff/managers
   arrive. Accepted because staff roles are firmly out of scope and the migration path (§16) is
   mechanical.
2. **A new `OrderFulfillment` aggregate instead of an `Order.Status` column.** Costs a table, a
   repository, and a coordination step. Buys order immutability, a `RowVersion` that does not contend
   with order history, and a lifecycle that can gain kitchen states without touching Ordering.
3. **Coarse admin role now.** Costs a future refactor if per-permission admin is needed. Buys zero
   speculative claims, which the roadmap explicitly demands. Mitigated by the named policy seams (D7).
4. **Read-model query services alongside repositories.** Costs a second persistence abstraction.
   Buys aggregate repositories that stay small and use-case-shaped instead of accreting
   `SearchXxxPagedAsync` methods.
5. **Duplicated DTOs per host.** Costs copy-paste. Buys independently evolvable API contracts.
6. **Fulfillment created eagerly at checkout.** Costs a delivery-cancellation coordination step.
   Buys an incoming-orders screen that is a pure read.

### 15.3 Do not do these

**Domain**
- Do **not** add `Status`, `AcceptedAt`, or any mutator to `Order`.
- Do **not** add navigation properties between `User` and `Restaurant`, or between `Order` and
  `OrderFulfillment`. Scalar IDs only.
- Do **not** add `UserManager`, `RoleManager`, `SignInManager`, `IdentityRole`, `Claim`,
  `ClaimsPrincipal`, or Duende types to Domain or Application.
- Do **not** put role or permission checks inside aggregates. `IsOwnedBy` is an identity comparison,
  not authorization.
- Do **not** widen the Domain → Identity coupling beyond the existing `User : IdentityUser<int>`.
- Do **not** create `IProductRepository`, `IOrderItemRepository`, `ICartItemRepository`, or
  `IUserAddressRepository`.

**Application**
- Do **not** call `IUnitOfWork.SaveChangesAsync` in a handler that also calls
  `IUserCapabilityService` (D8).
- Do **not** accept `ownerUserId`, `customerId`, `agentId`, or `adminUserId` from a route, query, or
  body. Always from `ICurrentUser`.
- Do **not** return `403` for a non-owned resource. Return `404`.
- Do **not** expose `IQueryable`, `DbContext`, or EF types on any Application contract.
- Do **not** add MediatR, AutoMapper, or FluentValidation.

**Infrastructure**
- Do **not** register controllers, authentication, or authorization inside `AddInfrastructure`.
- Do **not** modify `SynchronizeCapabilityRolesAsync` — it already handles all four flags.
- Do **not** seed users via `HasData`.
- Do **not** put a default admin password in any `appsettings*.json`.
- Do **not** squash the two migrations, and do **not** edit an already-applied migration.

**Hosts**
- Do **not** reuse `talabat.customer.api` / `talabat.delivery.api` audiences or scopes.
- Do **not** add admin or owner controllers to the Customer or Delivery hosts.
- Do **not** reference one host from another.
- Do **not** inject `TalabatDbContext` into a controller.
- Do **not** return Domain aggregates or Application read models directly as response bodies.
- Do **not** scatter `[Authorize(Roles = "Admin")]`; use the named policies.
- Do **not** rename the `Talabat.API` folder in this workstream.
- Do **not** delete the Identity host's approve/reject endpoints before the Admin API replacements
  are green.

**Process**
- Do **not** start a phase before the previous one builds green with all tests passing.
- Do **not** auto-apply a migration without reading the generated file.
- Do **not** "fix" a failing architecture test by weakening the test.

---

## 16. Deferred backlog — explicitly not now

| Item | Why deferred | Trigger to revisit |
|---|---|---|
| Gate delivery assignment on `ReadyForPickup` | Changes existing agent behaviour and the pending-deliveries queue | After owner order lifecycle is in production use |
| Fulfillment status in the customer order-details response | Customer API contract change with its own tests | When the customer app needs order tracking |
| Restaurant staff / manager sub-roles, multi-owner restaurants | Needs a `RestaurantMembership` aggregate; ownership column becomes a compatibility shim | When a restaurant needs more than one login |
| Fine-grained admin permissions | Roadmap forbids speculative claims | When two admin personas provably differ |
| Domain events / outbox | No dispatcher exists; own increment | When notifications or an audit trail is approved |
| Admin audit log (who changed what, when) | Needs the event seam above; a naive interceptor would miss capability changes made through `UserManager` | Alongside domain events |
| Restaurant-owner self-registration | Currently admin-granted only, which is safer | When onboarding volume justifies it |
| Owner analytics / revenue dashboards | Reporting concern, likely a separate read store | Post-MVP |
| Refactor the Customer/Delivery `CurrentUser` sync-over-async (D12/R9) | Pre-existing; touching it now widens this change set | Its own small PR |
| Rename `src/Talabat/Talabat.API` → `Talabat.Customer.API` | Churns `.slnx`, launch settings, EF startup-project paths, CI | Its own mechanical PR |
| Notifications, payment, coupons, reviews, real-time tracking | Roadmap Phase 11, each with its own design doc | Per roadmap |

---

## 17. Definition of done, per phase

A phase is complete only when **all** of these hold.

**Every phase**
- [ ] `dotnet build src\Talabat\Talabat.slnx` succeeds with no new warnings.
- [ ] `dotnet test src\Talabat\Talabat.slnx` is fully green — including the pre-existing suites.
- [ ] `dotnet list package --vulnerable --include-transitive` reports nothing new.
- [ ] No architecture test was weakened or skipped to make the phase pass.

**Phase 1** — `Talabat.Api.Shared` exists, is in `Talabat.slnx`, references only `Talabat.Application`;
both existing hosts compile against it; the only diffs in the existing hosts are `using` lines and
deleted files.

**Phase 2** — `Restaurant.OwnerUserId` and `RowVersion` persist and round-trip; Migration 1 reviewed
against the §10.2 checklist; **zero** `UpdateData` operations touching `CatalogSeedData` rows;
ownership repository methods covered by infrastructure tests.

**Phase 3** — an account can be granted and revoked `Admin` and `RestaurantOwner`; `UserType` flags
and `AspNetUserRoles` agree after every operation (assert both in one test); security stamp rotates on
every change; bootstrap is a no-op without configuration and creates exactly one admin with it.

**Phase 4** — every `IAdminQueries` / `IRestaurantOwnerQueries` method has a paging test proving
stable ordering and an accurate `TotalCount`; `IncludeDeleted` behaviour tested both ways.

**Phase 5** — Migration 2 reviewed; the unique `OrderId` index rejects a duplicate fulfillment; every
`OrderFulfillment` transition edge has a domain test; checkout creates exactly one fulfillment and one
delivery in one workflow.

**Phase 6** — every route in §11.4 returns `401` unauthenticated, `403` with a wrong-scope token,
`404` for a foreign restaurant, `409` when the capability flag is revoked; a
`talabat.customer.api` token is rejected; OpenAPI renders; `/health` is green.

**Phase 7** — every route in §11.5 enforced as above; the Identity host's dev-only approve/reject
endpoints are removed and `Talabat.Identity.Tests` updated; last-admin and self-revoke guards are
proven by integration tests.

**Phase 8** — `docs/authorization-matrix.md` extended with both new hosts (same table shape as the
existing Customer section, marking what remains provisional); `PROJECT_IMPLEMENTATION_ROADMAP.md`
status snapshot updated; all Phase-0 skipped architecture tests unskipped and passing; a short
`docs/restaurant-operations-bounded-context.md` written in the style of
`docs/delivery/delivery-bounded-context.md`.

---

## 18. Open questions to confirm before Phase 5

These change the plan if answered differently. Ask before implementing, do not guess.

1. **Can a restaurant reject an order after accepting it?** This plan says no (`Reject` requires
   `AwaitingRestaurant`). If cancellation-after-accept is required, add a
   `CancelByRestaurant(reason, now)` transition valid from `Accepted`/`Preparing` and extend §9.3.
2. **Is there a time limit on `AwaitingRestaurant`?** Auto-reject after N minutes needs a background
   job — nothing in the repo has one. Currently: no timeout.
3. **`Delivery.Cancel` vs the assigned-agent path.** Confirm the exact signature that releases an
   assigned agent back to `Available` (`DeliveryAssignmentDomainService` internal path) so
   `RejectOrderHandler` does not leave an agent stuck `Busy`.
4. **Should an admin be able to act as a restaurant owner** (edit any restaurant's menu)? This plan
   says no — admins assign owners; owners edit menus. If yes, it is additional admin catalog
   endpoints reusing the owner handlers with an ownership-bypass overload, which must be designed
   explicitly rather than by loosening the scoped reads.
