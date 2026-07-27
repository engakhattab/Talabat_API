# Phase 10 — Authorization Strategy And Quality Gates

> **For opencode.** Implementation *plan and strategy*, not full source. Follow the sequence; the "why" notes exist because several items are security-critical and easy to get subtly wrong.

> **Goal.** Phase 9 proved *who the caller is* and produced trustworthy claims. Phase 10 decides and enforces *what they may do*, binds ownership to the token instead of caller-supplied ids, and puts the quality gates in place that prevent regression.

---

## 0. Current state (verified in commit `50dff68`)

**Working:**
- Audience isolation: Customer API validates `aud = talabat.customer.api`, Delivery API validates `aud = talabat.delivery.api`, both with `ValidateAudience = true`, `RoleClaimType = "role"`.
- `TalabatProfileService` emits `role` + `customer_id` / `delivery_agent_id`.
- Roles seeded: `Customer`, `DeliveryAgent`, `Admin`, `RestaurantOwner`.
- `ICurrentUser` (Application abstraction) → `CurrentUser` (API) resolves `sub` → `UserId`, `CustomerId`, `AgentId`, capability flags.
- `ProfileEnforcementFilter` blocks `/api/me/*` when no customer profile exists.
- Customer API controllers carry bare `[Authorize]`; catalog is anonymous.
- Delivery API controllers carry `[Authorize(Roles = "DeliveryAgent")]`.
- Repository contracts already expose owner-scoped reads: `IOrderRepository.GetByIdForCustomerAsync`, `GetByCustomerIdAsync`, `ICartRepository.GetActiveCartByCustomerIdAsync`, `IDeliveryRepository.GetActiveByAgentIdAsync`.

**Gaps this phase closes:**
1. **No scope enforcement anywhere.** A token missing `customer.api` still passes `[Authorize]`.
2. **No role requirement on the Customer API.** Any authenticated caller with a customer-audience token reaches the controller; only `ProfileEnforcementFilter` stops them, and it returns 401/404 rather than 403.
3. **Delivery ownership is unenforced.** Lifecycle commands (`OutForDeliveryCommand`, `PickUpOrderCommand`, `DeliverOrderCommand`, `ArrivedAtRestaurantCommand`, `CancelDeliveryCommand`, `FailDeliveryCommand`) carry only `DeliveryId`. Any agent can progress any delivery.
4. **`AssignDelivery` trusts `body.AgentId`** — caller-supplied identity, forbidden by the roadmap.
5. `docs/authorization-matrix.md` covers only the Customer API and is dated Phase 3; the roadmap requires `docs/authorization-strategy.md` + `docs/authorization-endpoint-matrix.md`.
6. Missing test projects: `Talabat.Domain.Tests`, `Talabat.Delivery.API.Tests`, architecture tests.
7. No CI workflow.

**Out of scope — do not build:** `Admin`, `DeliveryOperations`, `RestaurantOwner` policies (candidate names only, no approved use cases); payment, notifications, coupons, reviews; Angular; API versioning; rate limiting; production key hardening; refresh-token tuning; external login / 2FA / password reset.

---

## 1. Decision: keep the implemented names, correct the docs

`docs/phase-4.5-identity-auth-foundation-plan.md` specifies different identifiers than Phase 9 shipped. **The code is the source of truth** — renaming would invalidate every issued token and both client registrations for no benefit.

| Concept | Old doc | **Authoritative (keep)** |
|---|---|---|
| Customer audience | `talabat.customer-api` | `talabat.customer.api` |
| Delivery audience | `talabat.deliveryagent-api` | `talabat.delivery.api` |
| Customer scope | `talabat.customer-api.access` | `customer.api` |
| Delivery scope | `talabat.deliveryagent-api.access` | `delivery.api` |
| Customer policy | `CustomerAccess` | `CustomerAccess` (create) |
| Delivery policy | `DeliveryAgentAccess` | `DeliveryAgentAccess` (create) |

Add a short "Naming reconciliation" note to `docs/authorization-strategy.md` recording this decision and superseding the Phase 4.5 table.

---

## 2. The layered authorization model (explain this in the strategy doc)

Four independent gates. Each catches something the others cannot. **None replaces another.**

```
1. AUTHENTICATION   valid signature + issuer + not expired      → else 401
2. AUDIENCE         aud matches this API                        → else 401   [done in Phase 9]
3. POLICY           required scope AND required role            → else 403   [Phase 10, §3]
4. OWNERSHIP        resource belongs to the caller              → else 404   [Phase 10, §4]
```

Why each is necessary:
- Scope without role: a customer-scoped token from a user who is only a delivery agent would pass.
- Role without scope: a token issued to some other client for a different purpose would pass.
- Policy without ownership: customer A could read customer B's order — both are legitimate `Customer`s.

**Status-code rule (already established, keep it consistent):**
- `401` — no/invalid token, or wrong audience.
- `403` — authenticated, but missing scope or role.
- `404` — authenticated and authorized by role, but the resource is not theirs. **Never 403 here** — a 403 confirms the resource exists, leaking information. Return the same 404 as for a genuinely missing resource.
- `409` — `ProfileNotCreated` on owner-scoped writes (existing behavior; do not change).

---

## 3. Policies

### 3.1 Create a shared requirement for scope

ASP.NET Core has no built-in "has scope" check. The `scope` claim may arrive as one space-delimited string **or** as multiple claim entries depending on the token — handle both or the check silently fails.

Create in each API host (`Auth/ScopeRequirement.cs` + `ScopeHandler.cs`), or a small shared helper per host:

```csharp
// ILLUSTRATIVE
public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}

public sealed class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var scopes = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (scopes.Contains(requirement.Scope, StringComparer.Ordinal))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

### 3.2 Register the policies

`Talabat.API/Program.cs`:
```csharp
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CustomerAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ScopeRequirement("customer.api"));
        policy.RequireRole("Customer");
    });
```

`Talabat.Delivery.API/Program.cs`:
```csharp
.AddPolicy("DeliveryAgentAccess", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.AddRequirements(new ScopeRequirement("delivery.api"));
    policy.RequireRole("DeliveryAgent");
});
```

Define policy names as constants (`Auth/AuthorizationPolicies.cs`) — no magic strings in controllers.

### 3.3 Apply to controllers

Replace `[Authorize]` → `[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]` on `CustomerController`, `AddressController`, `CartController`, `CheckoutController`, `OrderController`.

Replace `[Authorize(Roles = "DeliveryAgent")]` → `[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)]` on `DeliveriesController`, `StatusController`, `LocationController`.

`CatalogController` stays anonymous. `/health` stays anonymous.

**Careful with `POST /api/me/profile`:** it is the endpoint that *creates* the customer capability, so the caller does not yet have the `Customer` role. It must **not** require `CustomerAccess`. Use `[Authorize]` + scope only (add a separate `CustomerScopeOnly` policy: authenticated + `customer.api` scope, no role). Applying the full policy here creates a deadlock — you can never get the role because you can never call the endpoint that grants it. `ProfileEnforcementFilter` already special-cases this path; keep them consistent.

> **Claim-freshness note.** A user who gains the `Customer` capability mid-session still holds a token without the `Customer` role until it expires (≤1 hour) or is refreshed. Document this in the strategy doc: after `POST /api/me/profile` the client must refresh its token before customer endpoints succeed. Do not work around it by weakening the policy.

---

## 4. Ownership hardening — the security core of this phase

### 4.1 Customer side (verify, likely already correct)

Audit every `/api/me/*` handler and confirm the customer id comes from `ICurrentUser.CustomerId` and never from route, query, or body. Repository calls must use the owner-scoped methods (`GetByIdForCustomerAsync`, `GetByCustomerIdAsync`, `GetActiveCartByCustomerIdAsync`). Any handler taking a `customerId` parameter that a controller fills from user input is a defect — fix it. Add a test proving customer A gets 404 (not 403) for customer B's order.

### 4.2 Delivery side — **must fix**

**Problem A — lifecycle commands have no agent identity.**
`OutForDeliveryCommand(int DeliveryId)` and its siblings let any authenticated agent progress any delivery.

Fix, for each of `OutForDelivery`, `ArrivedAtRestaurant`, `PickUpOrder`, `DeliverOrder`, `CancelDelivery`, `FailDelivery`:
1. Add `int AgentId` to the command record: `record OutForDeliveryCommand(int DeliveryId, int AgentId)`.
2. In the controller, populate it from `ICurrentUser.AgentId` — **never from route or body**:
   ```csharp
   var agentId = _currentUser.AgentId;
   if (agentId is null) return Unauthorized();
   var result = await _handler.Handle(new OutForDeliveryCommand(deliveryId, agentId.Value), ct);
   ```
3. In the handler, load the delivery **scoped to that agent** (add a repository method such as `GetByIdForAgentAsync(int deliveryId, int agentId, CancellationToken)` mirroring the existing customer-scoped pattern), and return the not-found result when it doesn't match. Do **not** load then compare in the controller.

**Problem B — `AssignDelivery` trusts `body.AgentId`.**
`AssignDeliveryCommand(int DeliveryId, int AgentId)` with `AgentId` from the request body lets an agent assign work to any agent id.

Decide and document one of:
- **(a) Self-assignment (recommended for MVP):** the agent claims a pending delivery for themselves. Drop `AgentId` from the body entirely; take it from `ICurrentUser.AgentId`. Remove `AssignDeliveryBody` or reduce it to whatever else it carries.
- **(b) Operations-assigned:** a dispatcher assigns work to an agent. This requires the `DeliveryOperations` role, which has **no approved use case** and is explicitly out of scope. Do not build it now.

Choose (a) unless told otherwise. Record the decision in the strategy doc.

**Problem C — `UpdateLocation` / `GoOnline` / `GoOffline`.**
These act on the caller's own agent record. Confirm each resolves the agent from `ICurrentUser.AgentId` and not from any request field. `UpdateLocationCommand(decimal Latitude, decimal Longitude)` has no agent id — verify the handler obtains it from `ICurrentUser` (Application-side) or add `AgentId` to the command and populate it in the controller, consistent with the approach chosen above.

> **Keep the Domain clean.** Ownership is an Application/API concern. Do **not** add role checks, claims, or `ICurrentUser` calls inside aggregates. The `User` and `Delivery` aggregates keep enforcing their own state-machine invariants; ownership is enforced by scoping the *load*.

---

## 5. Documentation deliverables

### `docs/authorization-strategy.md` (new)
- The four-gate model from §2 and why each layer exists.
- Status-code policy (401 / 403 / ownership-404 / 409) with rationale for 404-over-403.
- Naming reconciliation table from §1.
- Role inventory: `Customer`, `DeliveryAgent` (active); `DeliveryOperations`, `Admin`, `RestaurantOwner` (candidates, no policies, no use cases — `RestaurantOwner` blocked until restaurant ownership exists in the domain model).
- Claim-freshness caveat (§3.3).
- The self-assignment decision (§4.2 Problem B).
- Statement that Domain remains free of roles/claims.

### `docs/authorization-endpoint-matrix.md` (new — supersedes `docs/authorization-matrix.md`)
One table per host. Reuse the existing Customer table as the base (it is accurate); add the policy column and the Delivery API section.

Columns: `Endpoint | Method | Anonymous | Policy | Role | Scope | Ownership rule | Failure codes`.

Delivery API rows to add:
| Endpoint | Method | Policy | Ownership |
|---|---|---|---|
| `/api/agent/status/online` | PUT | DeliveryAgentAccess | self (`ICurrentUser.AgentId`) |
| `/api/agent/status/offline` | PUT | DeliveryAgentAccess | self |
| `/api/agent/location` | PUT | DeliveryAgentAccess | self |
| `/api/agent/deliveries/active` | GET | DeliveryAgentAccess | agent-scoped read |
| `/api/agent/deliveries/pending` | GET | DeliveryAgentAccess | unassigned pool (document why unscoped) |
| `/api/agent/deliveries/history` | GET | DeliveryAgentAccess | agent-scoped read |
| `/api/agent/deliveries/{id}/assign` | POST | DeliveryAgentAccess | self-assign; agent id from token |
| `/api/agent/deliveries/{id}/out-for-delivery` | POST | DeliveryAgentAccess | must be assigned to caller → else 404 |
| `…/arrived-at-restaurant`, `…/picked-up`, `…/delivered`, `…/cancel`, `…/fail` | POST | DeliveryAgentAccess | same |

Mark `docs/authorization-matrix.md` as superseded (add a header pointing to the new file) rather than deleting it.

---

## 6. Quality gates

Per the roadmap, Phase 10 *consolidates* testing; it does not invent it. Existing: `Talabat.Application.Tests`, `Talabat.Customer.API.Tests`, `Talabat.Identity.Tests`, `Talabat.Infrastructure.Tests`.

### 6.1 `tests/Talabat.Domain.Tests` (new — roadmap backfill)
Pure unit tests, no EF, no Identity beyond what `User` already inherits:
- `User` capability transitions: `InitializeCustomerProfile`, `SubmitDeliveryAgentApplication` → `Approve`/`Reject`, `GoOnline`/`GoOffline`/`Suspend`/`MarkBusy`/`MarkAvailable`, including every invalid transition throwing the right exception.
- Address rules: duplicate rejection, default-address switching, `RequireCustomer` guard.
- `Cart`, `Order`, `Delivery` state machines and invariants.
- Value objects: `Address`, `GeoLocation`, `Money` (range validation).

### 6.2 `tests/Talabat.Delivery.API.Tests` (new)
Mirror the `Talabat.Customer.API.Tests` host/fixture pattern. Cover the authorization matrix (§6.4).

### 6.3 `tests/Talabat.ArchitectureTests` (new)
Use NetArchTest or a simple reflection assertion. Assert:
- `Talabat.Domain` references no `Microsoft.EntityFrameworkCore`, no `Microsoft.AspNetCore.*` **except** `Microsoft.Extensions.Identity.Stores` (the documented, deliberate Option-1 exception — encode it as an explicit allow so a *new* framework leak still fails).
- `Talabat.Application` references no EF Core, no `HttpContext`, no `ClaimsPrincipal`, no Duende.
- `Talabat.Domain` references no `Talabat.Application` / `Infrastructure` / any API project.
- No API project references `Talabat.Identity`.
- No aggregate type references `ICurrentUser` or any claim/role type.

### 6.4 Authorization integration tests (the acceptance evidence)
For **both** APIs, using test-minted JWTs trusted only in the Test environment (existing pattern in `Talabat.Customer.API.Tests`):

| Case | Expected |
|---|---|
| No token → protected endpoint | 401 |
| Valid token, wrong audience (customer token → Delivery API, and inverse) | 401 |
| Valid audience, **missing scope** | 403 |
| Valid audience + scope, **wrong role** | 403 |
| Full valid token | 200 |
| Anonymous → `/api/catalog/*`, `/health` | 200 |
| Customer A requests customer B's order / address / cart item | 404 (never 403) |
| Agent A calls a lifecycle endpoint on a delivery assigned to agent B | 404 |
| Agent attempts to assign a delivery to another agent id | rejected / body field absent |
| Authenticated, no customer profile → `/api/me/cart` | 409 `ProfileNotCreated` |
| Authenticated, no profile → `POST /api/me/profile` | allowed (no deadlock) |

### 6.5 CI — `.github/workflows/ci.yml` (new)
Steps: checkout → setup .NET 10 → `dotnet restore` → `dotnet build -c Release --no-restore` → `dotnet test --no-build --collect:"XPlat Code Coverage"` → `dotnet list package --vulnerable --include-transitive` (fail the job on any finding) → publish coverage. Run on push and PR.

Document the local equivalent command set in the strategy doc.

---

## 7. Implementation sequence

1. `docs/authorization-strategy.md` — write the decisions **first**; they drive the code.
2. `ScopeRequirement` + `ScopeHandler` + `AuthorizationPolicies` constants in both hosts.
3. Register `CustomerAccess` / `CustomerScopeOnly` / `DeliveryAgentAccess` policies.
4. Apply policy attributes to controllers (mind the `POST /api/me/profile` exception).
5. Delivery ownership fix: add `AgentId` to lifecycle commands, populate from `ICurrentUser`, add the agent-scoped repository read, update handlers.
6. `AssignDelivery` → self-assignment; remove the body-supplied agent id.
7. Audit customer handlers for caller-supplied ids; fix any found.
8. `docs/authorization-endpoint-matrix.md`; mark the old matrix superseded.
9. `tests/Talabat.Domain.Tests`.
10. `tests/Talabat.Delivery.API.Tests`.
11. `tests/Talabat.ArchitectureTests`.
12. Authorization integration tests in both API test projects.
13. `.github/workflows/ci.yml`.
14. Full run: build + all tests + vulnerability scan green.

---

## 8. Guardrails

- **Do not** put role/claim/`ICurrentUser` checks inside aggregates.
- **Do not** return 403 for owner-scoped misses — 404 only.
- **Do not** accept a customer id or agent id from route, query, or body on any `/api/me/*` or `/api/agent/*` endpoint.
- **Do not** create `Admin`, `DeliveryOperations`, or `RestaurantOwner` policies — candidates only.
- **Do not** weaken a policy or a test to make something pass; fix the cause.
- **Do not** rename audiences or scopes (§1).
- **Do not** add Duende EF configuration/operational stores (single-DbContext rule still active).
- **Do not** let `Talabat.Domain` or `Talabat.Application` gain web/EF/Duende packages; the architecture test enforces this.
- Keep the `Microsoft.Extensions.Identity.Stores` reference in Domain as an **explicit, documented** exception — do not silently broaden it.

---

## 9. Acceptance criteria

- Every implemented endpoint appears in `docs/authorization-endpoint-matrix.md` with policy, role, scope, ownership rule, and failure codes.
- Every protected endpoint enforces authentication **and** audience **and** scope **and** role.
- No endpoint derives caller identity from route, query, or body.
- A delivery agent cannot read or progress another agent's delivery (proved by test).
- A customer cannot read another customer's cart, address, or order (proved by test).
- Cross-audience tokens are rejected in both directions (proved by test).
- Domain contains no roles, claims, or identity-framework types beyond the documented Identity base class.
- All test projects green; architecture tests green; `dotnet list package --vulnerable` clean; CI runs on push and PR.
