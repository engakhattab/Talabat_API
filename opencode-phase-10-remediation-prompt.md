# OpenCode Task: Phase 10 Remediation (v2 — spec-kit aligned)

You are working in the **Talabat_API** repository on branch `feature/user-aggregate-refactor`
(.NET 10, Clean Architecture, DDD, Duende IdentityServer 8).

An architectural and security audit of Phase 10 found **8 critical blockers** plus a set of
consistency issues. This document is the remediation plan.

> **v2 supersedes v1.** Two things changed after a spec-kit analysis run:
> 1. The remediation is now a **separate spec-kit feature**, not an amendment to Phase 10.
> 2. **Phase 8a is reversed.** v1 proposed the wrong ownership model. See Phase 8a below.

---

## Rules of Engagement

1. **Do not invent requirements.** Everything traces to an existing doc:
   `docs/delivery/delivery-business-rules.md`, `PROJECT_IMPLEMENTATION_ROADMAP.md`,
   `phase-9-token-claims-scopes-plan.md`, `phase-10-authorization-and-quality-gates-plan.md`.
   If a task conflicts with a doc, **stop and ask** — do not guess.
2. **Do not add features.** No payment, notifications, coupons, reviews, admin dashboards,
   GPS/maps, nearest-agent optimisation, or new roles beyond what is specified.
3. **Layer discipline is non-negotiable:**
   - `Talabat.Domain` must not gain any new framework dependency.
   - `Talabat.Application` must not reference EF Core, ASP.NET Core, or `Talabat.Identity`.
   - Ownership/authorisation decisions live in Application use cases and API policies,
     never inside aggregates.
   - Repositories exist for aggregate roots only.
4. **One commit per phase.** Conventional Commits (`fix:`, `feat:`, `test:`, `ci:`, `refactor:`,
   `docs:`). Never commit with a failing gate.
5. **Do not modify `specs/00X-phase-10/spec.md`, `plan.md`, or `tasks.md`** except for the
   single D1 cross-reference edit in Phase 0f. In particular, **do not unmark any of the 58
   completed tasks** — see Phase 0a.

---

## Phase 0 — Spec-kit alignment (commit: `docs:`)

The audit findings have no home in the current spec-kit artifacts. Create that home **before**
writing any code, otherwise `/speckit.analyze` will keep reporting the same coverage gaps.

### 0a. Record the task-status finding (resolves **T1**)

All 58 Phase 10 tasks are marked complete, yet the audit found 8 blockers. The reason is
**not** that tasks were skipped or falsely marked. The tasks were genuinely implemented but
verified against acceptance criteria too weak to detect the defects.

The proof is `TestAuthHandler`: both API test factories strip out `AddJwtBearer` and substitute
a handler that hand-builds raw `"role"` claims. Every four-gate test passed while the real JWT
pipeline was broken by `MapInboundClaims`. T005–T014 were done; the tests certifying them
could not fail.

Record this verbatim as the T1 resolution: **"implemented, inadequately verified."**
Leave all `[x]` marks intact. The gap is verification strength, not execution.

### 0b. Create the remediation feature (resolves **G1**, **G2**)

Create `specs/0XX-phase-10-remediation/` (use the next free number) containing `spec.md`,
`plan.md`, and `tasks.md` in the repository's existing spec-kit format.

Derive functional requirements one-to-one from the blockers:

| FR | Blocker | Remediation phase |
|---|---|---|
| FR-R001 | Real JWT pipeline never exercised; `MapInboundClaims` breaks `RequireRole` | Phase 1 |
| FR-R002 | Pending-delivery capability check + pre-assignment PII reduction (folds in **U1**) | Phase 2 |
| FR-R003 | Delivery never created after checkout (BR-DEL-001 unimplemented) | Phase 3 |
| FR-R004 | No delivery-agent approval path | Phase 4 |
| FR-R005 | Delivery assignment race — no concurrency token | Phase 5 |
| FR-R006 | Identity host hardening (signing credential, redirect URI, token lifetimes, profile service) | Phase 6 |
| FR-R007 | CI quality gates non-functional | Phase 7 |
| FR-R008 | Ownership-model convergence and hardening (folds in **U2**, **U3**) | Phase 8 |

Write one success criterion per FR (SC-R001…SC-R008), each stated as **observable test
evidence**, not as "code exists." Every SC must name the test that proves it and, where the
defect is currently latent, must require that the test **fails before the fix**.

Then generate `tasks.md` from these FRs.

### 0c. Declare the data-model delta (resolves **C1**, **C2**)

Phase 10's `plan.md` says "No changes to EF mappings" and "No new entities are introduced."
Both statements remain **true for Phase 10's scope**. Do not edit them.

The new `plan.md` declares its own delta:
- `Delivery.RowVersion` (`byte[]`, private setter) + `.IsRowVersion()` in `DeliveryConfiguration`
- migration `AddDeliveryRowVersion`
- no new entities

On Principle 8 ("EF mapping MUST NOT weaken encapsulation"): it still **passes**.
`RowVersion` with a private setter is the identical shape `User` already uses; it exposes no
new mutation surface. Record this reasoning in the new `plan.md`'s constitution check rather
than marking Principle 8 as an exception.

### 0d. Amend the constitution Quality Gates (resolves **A1**)

The constitution says "all four test projects MUST pass." That predates the three projects
Phase 10 added; SC-008's list of seven is correct. Change the constitution to
**"all test projects in the solution MUST pass"** so it cannot go stale again.

### 0e. Record the delivery-failure policy as a business rule

Phase 3 depends on a policy decision the spec is silent on. Add to
`docs/delivery/delivery-business-rules.md`, in the existing BR format:

> **BR-DEL-016 — Delivery creation failure does not invalidate a committed order**
>
> Given checkout has succeeded and the order is committed,
> When delivery task creation fails,
> Then the order remains valid and the checkout response still reports success.
>
> The failure is logged at Error severity with the `OrderId`. The delivery is created later by
> retry or reconciliation. The order is never rolled back, and the two operations never share
> a transaction.

Rationale to include: everything needed to build the `Delivery` — `OrderId`, `CustomerId`,
`RestaurantId`, `DeliveryAddressSnapshot` — is already persisted on the committed `Order`, so
nothing is lost by deferring. Rolling the order back would invert the documented dependency
direction (`docs/delivery/README.md`: "Ordering does not directly create or mutate Delivery")
and cannot cleanly undo `cart.MarkCheckedOut(now)`.

Also add a deferred roadmap item: **reconciliation for orders with no delivery row** — a
repository query plus a dev-only backfill endpoint. Note that the eventual correct solution is
a **transactional outbox** (`OrderPlaced` domain event committed in the same transaction,
dispatched at-least-once), which removes this failure mode rather than mitigating it. There is
no domain-event dispatch or outbox table today, so this is explicitly deferred — but record it
so the gap stays a deliberate decision rather than an accident.

### 0f. Cross-reference the authorization test matrix (resolves **D1**)

In Phase 10's `tasks.md`, add explicit `FR-014` scenario references to the descriptions of
**T045** (Delivery) and **T048** (Customer). Description text only — no structural change,
no status change.

**Gate for Phase 0:** re-run `/speckit.analyze`. I1, G1, G2, T1, C1, C2, U1, U2, U3, D1, A1,
and S1 should all clear. Report any residual issues before writing code.

---

## Phase 0.5 — Baseline (no commit)

```bash
dotnet restore src/Talabat/Talabat.slnx
dotnet build   src/Talabat/Talabat.slnx --no-restore -c Release
dotnet test    src/Talabat/Talabat.slnx --no-build   -c Release
```

Record which suites pass, fail, or error on missing SQL Server. Report the baseline before
touching any code. If the build is already broken, fix that first and stop for review.

---

## Phase 1 — 🔴 JWT inbound claim mapping (FR-R001)

### Problem
Both API hosts set `TokenValidationParameters.RoleClaimType = "role"` but leave
`JwtBearerOptions.MapInboundClaims` at its default `true`. Inbound `role` claims are rewritten
to `ClaimTypes.Role`, so `policy.RequireRole("Customer")` and `RequireRole("DeliveryAgent")`
will **fail with a real token**, returning 403 on every protected endpoint. This is invisible
today because all API tests replace the JWT scheme with `TestAuthHandler`.

### Files
- `src/Talabat/Talabat.API/Program.cs`
- `src/Talabat/Talabat.Delivery.API/Program.cs`

### Change
In each `.AddJwtBearer(options => { ... })` block, add as the **first** line:

```csharp
options.MapInboundClaims = false;   // keep "sub", "role", "scope" verbatim from the token
```

Leave `RoleClaimType = "role"` and `NameClaimType = "sub"` as they are. `CurrentUser` already
falls back `ClaimTypes.NameIdentifier ?? "sub"`, so it resolves under both settings —
do not change it.

### Tests (SC-R001)
Create `tests/Talabat.Customer.API.Tests/RealTokenPipelineTests.cs` and
`tests/Talabat.Delivery.API.Tests/RealTokenPipelineTests.cs`.

These must **not** use `TestAuthHandler`. Use a factory variant that keeps the real
`AddJwtBearer` registration and overrides only the signing key:

```csharp
services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
{
    o.Authority = null;
    o.MetadataAddress = null;
    o.RequireHttpsMetadata = false;
    o.TokenValidationParameters.IssuerSigningKey = TestSigningKey;      // symmetric test key
    o.TokenValidationParameters.ValidIssuer = "https://localhost:7237";
    o.TokenValidationParameters.ValidAudience = "talabat.customer.api"; // per host
});
```

Mint tokens with `JsonWebTokenHandler` carrying exactly the claim set the real
`TalabatProfileService` emits: `sub`, `role`, `scope`, `customer_id` / `delivery_agent_id`,
`aud`, `iss`.

Assert, per host:
- Valid token, correct `aud` + `scope` + `role` → **200**
- Token with the other host's `aud` → **401**
- Correct `aud`, missing scope → **403**
- Correct `aud` + scope, wrong role → **403**
- Expired token → **401**
- No `Authorization` header → **401**

**Confirm these tests fail before the `MapInboundClaims` fix and pass after.** Report both runs.
If they pass beforehand, the diagnosis was wrong — stop and report.

---

## Phase 2 — 🔴 Pending-delivery capability check + PII (FR-R002)

### Problem
`GetPendingDeliveriesHandler` checks only `_currentUser.IsAuthenticated`. Every sibling handler
checks `HasDeliveryAgentCapability`. A principal whose agent capability was revoked in the DB
but who still holds a valid token can enumerate **every pending delivery**, including
`CustomerId` and full street address.

> **Note:** Phase 8a will move this guard into the controller. Keep it in the handler for now —
> this is a live security hole and must close immediately and independently. The small rework
> in Phase 8 is an accepted cost.

### Files
- `src/Talabat/Talabat.Application/DeliveryAgents/GetPendingDeliveries/GetPendingDeliveriesHandler.cs`
- `src/Talabat/Talabat.Application/DeliveryAgents/GetPendingDeliveries/PendingDeliveryDto.cs`

### Change
1. Replace the guard with the standard one used everywhere else:

```csharp
if (!_currentUser.IsAuthenticated || !_currentUser.HasDeliveryAgentCapability || _currentUser.AgentId is null)
{
    return UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>.Failure(
        DomainExceptionMapper.OwnershipMismatch(
            ApplicationErrorCodes.AgentRequired,
            "Authenticated delivery agent required."));
}
```

2. Reduce pre-assignment PII. Remove `Street`, `BuildingNumber`, `Floor`, and `CustomerId`
   from `PendingDeliveryDto`; keep `Id`, `OrderId`, `RestaurantId`, `Status`, `City`,
   `CreatedAt`. The full address is already returned by `ActiveDeliveryDto` after assignment,
   which is the correct disclosure point.

3. Update the handler projection and any test doubles.

### Tests (SC-R002)
`tests/Talabat.Application.Tests/Domain/DeliveryAgents/GetPendingDeliveriesHandlerTests.cs`:
- authenticated, no agent capability → failure with `AgentRequired`
- authenticated agent → success
- returned DTO exposes no street/building/floor/customer id

`tests/Talabat.Delivery.API.Tests/DeliveriesAuthorizationTests.cs`:
- token with `DeliveryAgent` role, but user's `UserType` lacks the `DeliveryAgent` flag →
  `GET /api/agent/deliveries/pending` returns **403**, not 200

---

## Phase 3 — 🔴 Delivery is never created after checkout (FR-R003)

### Problem
`IDeliveryRepository.AddAsync` has **zero callers**. `CheckoutHandler` creates the `Order` and
stops. BR-DEL-001 is unimplemented, so `GET /api/agent/deliveries/pending` is permanently empty
and the entire agent lifecycle is unreachable.

### Design constraint
Ordering must not depend on Delivery, and Delivery must not mutate Order. Implement as a
**separate Application-layer use case** invoked by the Customer API's checkout endpoint *after*
`CheckoutHandler` succeeds — not inside `CheckoutHandler`'s domain path, not inside `Order`.

### Files to create
- `src/Talabat/Talabat.Application/Deliveries/CreateForOrder/CreateDeliveryForOrderCommand.cs`
- `src/Talabat/Talabat.Application/Deliveries/CreateForOrder/CreateDeliveryForOrderHandler.cs`

### Files to modify
- `src/Talabat/Talabat.Application/DependencyInjection.cs` (register the handler)
- `src/Talabat/Talabat.API/Controllers/CheckoutController.cs` (invoke after success)
- `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutOutcome.cs` — **only if** it does
  not already expose `OrderId`, `RestaurantId`, and the address snapshot. Read it first.

### Handler shape

```csharp
public async Task<UseCaseResult<int>> Handle(CreateDeliveryForOrderCommand cmd, CancellationToken ct)
{
    if (await _deliveryRepository.GetByOrderIdAsync(cmd.OrderId, ct) is not null)
    {
        return UseCaseResult<int>.Failure(
            new ApplicationError(ApplicationErrorCodes.DeliveryAlreadyExists,
                ApplicationErrorCategory.Conflict, "A delivery already exists for this order."));
    }

    var delivery = new Delivery(
        cmd.OrderId, cmd.CustomerId, cmd.RestaurantId, cmd.DeliveryAddress, _clock.UtcNow);

    await _deliveryRepository.AddAsync(delivery, ct);
    await _unitOfWork.SaveChangesAsync(ct);

    return UseCaseResult<int>.Success(delivery.Id);
}
```

Add `DeliveryAlreadyExists` to `ApplicationErrorCodes`.

### Failure policy — implement BR-DEL-016 exactly
- Checkout **must not fail** if delivery creation fails. Log at **Error** with the `OrderId`
  and return the successful checkout response.
- Do **not** wrap both in one transaction — that couples the two contexts.
- Keep the `GetByOrderIdAsync` pre-check *and* rely on `UX_Deliveries_OrderId` as the real
  guarantee, mapping the unique-index violation to the same `Conflict`. These are what make
  any later retry safe.

### Tests (SC-R003)
`tests/Talabat.Application.Tests/Deliveries/CreateForOrder/CreateDeliveryForOrderHandlerTests.cs`:
- creates a `PendingAssignment` delivery with the order's address snapshot (BR-DEL-002, BR-DEL-012)
- stores `OrderId`, `CustomerId`, `RestaurantId`, `AssignedAgentId == null` (BR-DEL-013)
- second call for the same order → `Conflict` / `DeliveryAlreadyExists`

`tests/Talabat.Customer.API.Tests/CheckoutEndpointTests.cs`:
- after successful checkout, a delivery row exists for the new order with status `PendingAssignment`
- checkout still returns 200 when delivery creation throws (inject a throwing repository) — BR-DEL-016

---

## Phase 4 — 🔴 No delivery-agent approval path (FR-R004)

### Problem
`IUserCapabilityService.ApproveDeliveryAgentAsync` / `RejectDeliveryAgentAsync` are fully
implemented and reachable from **nowhere**. An applicant registered via
`POST /account/register/delivery-agent` stays `PendingApproval` forever, never receives
`UserType.DeliveryAgent`, never gets the `DeliveryAgent` role, and can never be assigned work.

### Files
- `src/Talabat/Talabat.Identity/Controllers/AccountController.cs`, or a new
  `src/Talabat/Talabat.Identity/Controllers/AgentApprovalController.cs`

### Change
Expose:

```
POST /account/delivery-agents/{userId:int}/approve   → 200
POST /account/delivery-agents/{userId:int}/reject    → 200
```

There is no `Admin` policy yet and **you must not invent one**. Gate behind a development-only
guard so these cannot ship enabled:

```csharp
if (!_environment.IsDevelopment())
{
    return NotFound();
}
```

Add `// TODO(Phase 9): replace with AdminAccess policy` referencing
`phase-10-authorization-and-quality-gates-plan.md`. Do not add an `Admin` role, claim, scope,
or policy.

### Tests (SC-R004)
`tests/Talabat.Identity.Tests/AgentApprovalEndpointTests.cs`:
- register applicant → approve → `UserType.DeliveryAgent` set, role `DeliveryAgent` assigned,
  `DeliveryAgentStatus == Offline`, security stamp changed
- approve an already-approved user → 409
- approve a non-existent user → 404
- endpoints return 404 outside Development

---

## Phase 5 — 🔴 Delivery assignment race condition (FR-R005)

### Problem
`Delivery` has no concurrency token (`User` has `RowVersion`, `Delivery` does not). Under READ
COMMITTED, two agents can both read a `PendingAssignment` row, both pass `AssignAgent`'s status
check, and both write — last write wins and the delivery is stolen.
`UX_Deliveries_AssignedAgentId_Active` enforces one active delivery *per agent* (BR-DEL-004);
it does not prevent double-claiming *one delivery* (BR-DEL-005).

### Files
- `src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/Delivery.cs`
- `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryConfiguration.cs`
- `src/Talabat/Talabat.Infrastructure/Persistence/UnitOfWork.cs` (verify only — see step 3)
- new EF migration

### Change
1. Add `public byte[] RowVersion { get; private set; }` to `Delivery`, initialised to `[]` in
   the private constructor — mirror `User` exactly. Plain `byte[]`, so Domain gains no dependency.
2. `builder.Property(d => d.RowVersion).IsRowVersion();` in `DeliveryConfiguration`.
3. **Do not catch `DbUpdateConcurrencyException` in the Application layer** — that would drag
   EF Core across the boundary. Check `UnitOfWork.cs` first: it should already translate
   `DbUpdateConcurrencyException` into `ConcurrencyConflictException`. Reuse that path and map
   to `ApplicationErrorCodes.ConcurrencyConflict` → 409. If the translation is missing, add it
   **in `UnitOfWork`**, not in Application.
4. `dotnet ef migrations add AddDeliveryRowVersion -p src/Talabat/Talabat.Infrastructure -s src/Talabat/Talabat.API`

### Tests (SC-R005)
- `tests/Talabat.Infrastructure.Tests/Persistence/DeliveryPersistenceTests.cs`: two contexts
  load the same delivery, both assign, second `SaveChanges` throws the concurrency exception
- new `tests/Talabat.Delivery.API.Tests/AssignmentConcurrencyTests.cs`: concurrent
  `POST {id}/assign` from two agents yields exactly one 200 and one 409

---

## Phase 6 — 🔴 Identity host hardening (FR-R006)

### Files
- `src/Talabat/Talabat.Identity/Program.cs`
- `src/Talabat/Talabat.Identity/IdentityServerConfig.cs`
- `src/Talabat/Talabat.Identity/TalabatProfileService.cs`
- **delete** `src/Talabat/Talabat.Identity/IdentityConfig.cs` (dead pass-through duplicate)

### Changes

**6a. Gate the developer signing credential.**

```csharp
var idsBuilder = builder.Services.AddIdentityServer(options => { /* unchanged */ })
    .AddInMemoryIdentityResources(IdentityServerConfig.IdentityResources)
    .AddInMemoryApiScopes(IdentityServerConfig.ApiScopes)
    .AddInMemoryApiResources(IdentityServerConfig.ApiResources)
    .AddInMemoryClients(IdentityServerConfig.Clients)
    .AddAspNetIdentity<User>()
    .AddProfileService<TalabatProfileService>();

if (builder.Environment.IsDevelopment())
{
    idsBuilder.AddDeveloperSigningCredential();
}
else
{
    throw new InvalidOperationException(
        "No production signing credential configured. See PROJECT_IMPLEMENTATION_ROADMAP.md Phase 9.");
}
```
Fail loudly rather than silently shipping a dev key. Do **not** implement Key Vault or
certificate loading — that is a later phase decision.

**6b.** Add `https://oauth.pstmn.io/v1/browser-callback` to both clients' `RedirectUris`
(alongside the existing `/v1/callback`). Keep the `// dev only — remove before production`
comment on both.

**6c.** Token lifetime correctness on both clients:
```csharp
AccessTokenLifetime = 900,                         // 15 min for browser clients
UpdateAccessTokenClaimsOnRefresh = true,           // propagate role/capability revocation
RefreshTokenExpiration = TokenExpiration.Sliding,  // makes SlidingRefreshTokenLifetime meaningful
SlidingRefreshTokenLifetime = 1296000,
```

**6d.** `TalabatProfileService` ignores `context.RequestedClaimTypes` and issues `customer_id`
*and* `delivery_agent_id` on every token regardless of resource. Use
`context.AddRequestedClaims(claims)` instead of `context.IssuedClaims.AddRange(claims)`.

**6e.** Remove the duplicate `JwtClaimTypes.Role` / `"role"` entry in each `ApiResource.UserClaims`.

### Tests (SC-R006)
- for a dual-capability user, a customer-scope token carries no `delivery_agent_id`, and a
  delivery-scope token carries no `customer_id`
- both `oauth.pstmn.io` URIs present in each client's `RedirectUris`
- `UpdateAccessTokenClaimsOnRefresh == true` on both clients

---

## Phase 7 — 🔴 CI quality gates are decorative (FR-R007)

### Problem
`.github/workflows/ci.yml` runs `dotnet test` on `ubuntu-latest` with no SQL Server, while four
suites (`Customer.API.Tests`, `Delivery.API.Tests`, `Infrastructure.Tests`, `Identity.Tests`)
build a real connection from `ConnectionStrings:TalabatDb` and call `EnsureCreated()` /
`DROP DATABASE`. The connection string is committed as a personal machine name.
`dotnet list package --vulnerable` prints but always exits 0.

### Files
- `.github/workflows/ci.yml`
- `src/Talabat/*/appsettings.Development.json`
- all `SqlServerDatabaseFixture` / `CustomWebApplicationFactory` classes

### Changes
1. Add a SQL Server service container:

```yaml
    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: "Y"
          MSSQL_SA_PASSWORD: "Your_strong_Passw0rd"
        ports: ["1433:1433"]
        options: >-
          --health-cmd "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P Your_strong_Passw0rd -Q 'SELECT 1'"
          --health-interval 10s --health-timeout 5s --health-retries 10
```

2. Supply the connection string via environment, not a committed file:
```yaml
    env:
      ConnectionStrings__TalabatDb: "Server=localhost,1433;Database=Talabat;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True"
```
Remove the hardcoded `DESKTOP-5IHGJ9F\SQLEXPRESS` string from every
`appsettings.Development.json`; replace with a placeholder and document setup in the README.

3. Make the vulnerability check a real gate:
```yaml
      - name: Check vulnerable packages
        run: |
          dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive 2>&1 | tee vuln.txt
          ! grep -q "has the following vulnerable packages" vuln.txt
```

4. Switch fixtures from `Database.EnsureCreated()` to `Database.Migrate()` so migrations are
   exercised. If a fixture breaks under `Migrate()`, that is a genuine migration bug —
   **report it, do not revert**.

### Acceptance (SC-R007)
CI green on a clean run, all seven test projects reported as executed. Paste the job summary.

---

## Phase 8 — 🟡 Ownership convergence and hardening (FR-R008)

Start only once Phases 1–7 are committed and green.

### 8a. Converge on FR-007 — **direction reversed from v1**

> **v1 got this backwards.** It proposed removing `AgentId` from the lifecycle commands and
> resolving identity inside handlers. That contradicts spec **FR-007** ("lifecycle commands
> include the agent id, populated by the controller from `ICurrentUser.AgentId`") and it
> churns the majority pattern to match the minority.
>
> The Customer API already injects from the controller everywhere:
> `new GetOrderHistoryQuery(_currentUser.CustomerId!.Value)`,
> `new ClearCartCommand(_currentUser.CustomerId!.Value)`, and so on. Roughly 19 handlers follow
> controller-injection versus 6 that self-resolve.
>
> It is also better on the merits: handlers stay pure functions of their inputs, testable
> without an `ICurrentUser` fake and reusable from a background job or future ops action. The
> BOLA protection was never in *where the id comes from* — it is in `GetByIdForAgentAsync`
> scoping the query, which is why cross-agent access already returns 404 correctly.

**Leave the seven lifecycle commands exactly as they are.** Convert the six self-resolving
cases to controller-injection instead:

| Command / Query | Handler | Controller |
|---|---|---|
| `GoOnlineCommand` | `GoOnlineHandler` | `StatusController` |
| `GoOfflineCommand` | `GoOfflineHandler` | `StatusController` |
| `UpdateLocationCommand` | `UpdateLocationHandler` | `LocationController` |
| `GetActiveDeliveryQuery` | `GetActiveDeliveryHandler` | `DeliveriesController` |
| `GetDeliveryHistoryQuery` | `GetDeliveryHistoryHandler` | `DeliveriesController` |
| `GetPendingDeliveriesQuery` | `GetPendingDeliveriesHandler` | `DeliveriesController` |

For each: add `int AgentId` to the record, drop the `ICurrentUser` dependency and its guard
from the handler, and call the existing `TryGetAgentId` guard in the controller action.
`StatusController` and `LocationController` do not currently inject `ICurrentUser` — add it.
Lift `TryGetAgentId` out of `DeliveriesController` into a shared helper in
`src/Talabat/Talabat.Delivery.API/Auth/` so all three controllers use one implementation.

This supersedes the Phase 2 in-handler guard on `GetPendingDeliveriesHandler`. The Phase 2 API
test (403 for a revoked agent) must still pass unchanged after the move.

> **⚠️ BOLA trap — read before touching `UpdateLocationCommand`.**
> `LocationController` currently binds it directly: `[FromBody] UpdateLocationCommand command`.
> Adding `AgentId` to that record would let a caller post `{"agentId": 99, ...}` and act as
> another agent. Introduce a separate request contract and construct the command server-side:
>
> ```csharp
> public sealed record UpdateLocationRequest(decimal Latitude, decimal Longitude);
>
> [HttpPut]
> public async Task<IActionResult> UpdateLocation(
>     [FromBody] UpdateLocationRequest request, CancellationToken cancellationToken)
> {
>     if (!TryGetAgentId(out var agentId)) return Forbid();
>
>     var result = await _updateLocationHandler.Handle(
>         new UpdateLocationCommand(agentId, request.Latitude, request.Longitude),
>         cancellationToken);
>
>     return result.ToActionResult(_ => Ok());
> }
> ```
>
> Add a regression test: posting an `agentId` in the body must not change which agent's
> location is updated.

**8b. Replace `ProfileEnforcementFilter` path matching with an attribute** (resolves U2).
The filter matches `"/api/me/profile"` as a literal and falls back to `StartsWith("/api/me/")`;
a trailing slash silently turns a 404 into a 409, and every new `/api/me/*` route inherits
behaviour by accident. Introduce `[RequireCustomerProfile]` applied per action. Preserve status
codes exactly: `GET /api/me/profile` without a profile → **404** `ProfileNotCreated`; other
`/api/me/*` without a profile → **409** `ProfileNotCreated`; `POST /api/me/profile` always
allowed through.

**8c. Move capability resolution out of the API layer** (resolves U3). `CurrentUser` is
duplicated verbatim in both hosts and injects `TalabatDbContext` directly, issuing a synchronous
DB query inside a property getter. Add `ICurrentUserCapabilityResolver` to
`Talabat.Application/Abstractions/`, implement in `Talabat.Infrastructure/Identity/`, and reduce
each host's `CurrentUser` to claims plus one resolver call, cached per request.
**Keep the DB-backed capability check** — it is what protects against stale tokens.

**8d.** Add `AddExceptionHandler<DomainExceptionHandler>` + `UseExceptionHandler` and a
`/health` endpoint to `Talabat.Delivery.API`, matching the Customer API's ProblemDetails contract.

**8e.** Move `app.UseCors("SpaCorsPolicy")` out of the `IsDevelopment()` block in both API hosts
and drive origins from configuration (`Cors:AllowedOrigins`) instead of hardcoded
`http://localhost:4200` / `:4300`.

**8f.** Add an OAuth2 security scheme to the OpenAPI documents on all three hosts so Swagger UI
renders an Authorize button and can exercise protected endpoints via authorization code + PKCE.

**8g.** Strengthen the architecture tests. They use `Assembly.GetReferencedAssemblies()`, which
the compiler prunes to *used* references — an unused forbidden package passes silently. Assert
on `.csproj` `PackageReference` items or switch to NetArchTest. Add: **no type in either API
assembly may depend on `TalabatDbContext`.** This fails until 8c lands — sequence accordingly.

**8h.** Stop the aggregate-encapsulation leak. `UserCapabilityService` does
`user.PhoneNumber = phoneNumber.Trim();`, mutating state through a public setter inherited from
`IdentityUser`. Add `User.SetPhoneNumber(string?)` with the same `Guard.OptionalText`
normalisation used elsewhere, and call that instead.

**8i.** Replace obsolete `ISystemClock` in both `TestAuthHandler` classes with the `TimeProvider`
constructor overload; remove `#pragma warning disable CS0618`.

---

## Explicitly Out of Scope

Do **not** attempt these.

- **Decoupling `User` from `IdentityUser<int>`.** `Talabat.Domain` references
  `Microsoft.Extensions.Identity.Stores`. The target shape is `ApplicationUser` in
  Infrastructure with a scalar link to a framework-free `User` aggregate. 8h stops the bleeding;
  the full refactor needs its own spec. If you touch this, stop and ask.
- Transactional outbox / domain-event dispatch. Recorded as deferred in Phase 0e.
- Reconciliation job for orders missing a delivery. Recorded as deferred in Phase 0e.
- Production signing-key management, Key Vault, certificate rotation.
- Email confirmation, password reset, lockout policy, 2FA, external login.
- Any `Admin`, `DeliveryOperations`, or `RestaurantOwner` role, policy, scope, or claim.
- Changing `ApplicationErrorCategory.OwnershipMismatch → 403`. It contradicts the documented
  "ownership violations return 404" rule on paper, but cross-agent access already yields 404
  correctly via `GetByIdForAgentAsync` returning null. **Raise as a documentation question;
  do not change the mapping.**
- Unmarking any Phase 10 task, or editing Phase 10's `spec.md` / `plan.md`. Only the D1
  cross-reference edit in `tasks.md` is permitted.

---

## Per-Phase Gate (run before every commit)

```bash
dotnet build src/Talabat/Talabat.slnx -c Release --no-restore
dotnet test  src/Talabat/Talabat.slnx -c Release --no-build
dotnet list  src/Talabat/Talabat.slnx package --vulnerable --include-transitive
```

A phase is done only when **all** hold:

- [ ] Build succeeds with zero new warnings
- [ ] Every test project passes, including the four requiring SQL Server
- [ ] `Talabat.ArchitectureTests` passes with no new exemptions added
- [ ] New tests for this phase exist and demonstrably fail against the pre-fix code
- [ ] No new `PackageReference` in `Talabat.Domain` or `Talabat.Application`
- [ ] No new EF Core / ASP.NET Core / Duende type referenced from Application
- [ ] `--vulnerable` reports nothing
- [ ] The phase's SC-R00x in the remediation spec is satisfied by a named test

Commit naming the phase and the blocker it closes, e.g.
`fix(auth): disable inbound claim mapping so RequireRole works with real JWTs (FR-R001)`.

---

## Reporting

After each phase, report:

1. Files created / modified / deleted
2. Tests added, and the before/after result proving the fix
3. Anything that contradicts this plan or the repository docs
4. Anything you chose **not** to do, and why

Stop and ask rather than guessing if a task requires a product decision not already written
down in the repository docs.
