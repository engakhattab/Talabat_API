# Phase 10 Explained — Authorization Strategy & Quality Gates

**Project:** Talabat API (DDD + Clean Architecture, .NET 10)
**Phase:** 10 — Authorization Strategy And Quality Gates
**Builds on:** Phase 9 (centralized authentication, tokens, claims, scopes)
**Purpose of this document:** explain what was built, *why* each decision was made, and how the pieces fit — at a level you can present to a mentor and defend under questioning.

---

## Table of contents

1. [The problem Phase 10 solves](#1-the-problem-phase-10-solves)
2. [The one-sentence summary](#2-the-one-sentence-summary)
3. [Vocabulary: the three things people conflate](#3-vocabulary-the-three-things-people-conflate)
4. [The four-gate model](#4-the-four-gate-model)
5. [Gate 3 — Policies: how ASP.NET Core authorization actually works](#5-gate-3--policies-how-aspnet-core-authorization-actually-works)
6. [Gate 4 — Ownership: the real vulnerability we fixed](#6-gate-4--ownership-the-real-vulnerability-we-fixed)
7. [Why 404 and not 403](#7-why-404-and-not-403)
8. [The bootstrap paradox](#8-the-bootstrap-paradox)
9. [Two sources of truth: token vs database](#9-two-sources-of-truth-token-vs-database)
10. [Quality gates: making the architecture executable](#10-quality-gates-making-the-architecture-executable)
11. [Where every file lives](#11-where-every-file-lives)
12. [Design decisions and their trade-offs](#12-design-decisions-and-their-trade-offs)
13. [Questions your mentor will ask — with answers](#13-questions-your-mentor-will-ask--with-answers)
14. [Known gaps — honest status](#14-known-gaps--honest-status)
15. [Glossary](#15-glossary)

---

## 1. The problem Phase 10 solves

### What Phase 9 left us with

Phase 9 gave every request a trustworthy identity. After it, an API knew: *this request comes from user 42, who holds the role `Customer`, with scope `customer.api`, and the token is cryptographically proven to come from our Identity server.*

That is **authentication**. It answers *who are you?*

But every protected controller carried only a bare `[Authorize]` attribute. Translated into plain English, that attribute means: **"any successfully authenticated caller may proceed."** Nothing more.

### The three holes that left open

**Hole 1 — no scope enforcement.** `[Authorize]` never inspects the `scope` claim. A token issued for an entirely different purpose, as long as it was signed by our Identity server and carried the right audience, sailed through.

**Hole 2 — no role enforcement on the Customer API.** Any authenticated caller reached the controller body. A user who was *only* a delivery agent could call customer endpoints. A filter caught them afterwards, but the authorization layer itself never said no.

**Hole 3 — and this is the serious one — no ownership enforcement on the Delivery API.** Look at the code as it existed before this phase:

```csharp
// BEFORE — the command carries no agent identity at all
public record OutForDeliveryCommand(int DeliveryId);

[HttpPost("{deliveryId:int}/out-for-delivery")]
public async Task<IActionResult> OutForDelivery(int deliveryId, CancellationToken ct)
{
    var result = await _handler.Handle(new OutForDeliveryCommand(deliveryId), ct);
    return result.ToActionResult(id => Ok(id));
}
```

Read it carefully. The endpoint takes a delivery id from the URL and acts on it. Nothing anywhere asks *"is this delivery yours?"*

So **any authenticated delivery agent could mark any delivery in the system as delivered, cancelled, or failed** — including deliveries assigned to a different agent — simply by changing the number in the URL. Agent A could mark agent B's order delivered and the system would accept it.

It was worse for assignment:

```csharp
// BEFORE — agent id read from the request BODY
public record AssignDeliveryCommand(int DeliveryId, int AgentId);
var result = await _handler.Handle(new AssignDeliveryCommand(deliveryId, body.AgentId), ct);
```

`body.AgentId` is **caller-supplied identity**. The client tells the server who it is, and the server believes it. An agent could assign deliveries to any other agent id they chose.

### This has a name

This vulnerability class is **Broken Object Level Authorization (BOLA)**, also called **IDOR** (Insecure Direct Object Reference). It has been **#1 on the OWASP API Security Top 10** since the list was created.

It is the single most common serious API vulnerability in the wild, and it is dangerous precisely because it is invisible in normal testing: every request is from a legitimate, logged-in, correctly-roled user. Nothing looks wrong. Authentication is perfect. The bug is that *authentication was mistaken for authorization*.

> **Say this to your mentor:** "Phase 9 made sure every caller was who they claimed to be. Phase 10 discovered that being authenticated said nothing about which *objects* you were allowed to touch — and fixed it."

---

## 2. The one-sentence summary

> "Phase 10 replaces blanket `[Authorize]` with a four-layer authorization model — authentication, audience, policy (scope + role), and per-object ownership — closes a Broken Object Level Authorization vulnerability in the Delivery API by deriving the acting agent from the token instead of the request, and makes the architectural rules executable through architecture tests and a CI pipeline so the design cannot silently regress."

---

## 3. Vocabulary: the three things people conflate

If you take one thing from this phase, take this. **Scope, role, and ownership are three orthogonal concepts.** Confusing them is how authorization bugs get written.

| Concept | Question it answers | Attached to | Example |
|---|---|---|---|
| **Scope** | What is *this application* permitted to do on the user's behalf? | The **client app** | `customer.api` |
| **Role** | What *kind of user* is this? | The **person** | `Customer`, `DeliveryAgent` |
| **Ownership** | Does *this specific record* belong to this person? | The **individual object** | order #77 belongs to customer 42 |

### Why you need all three

Remove any one and something breaks:

- **Scope without role:** a token minted for some other purpose but signed by our issuer would be accepted. The *user* was never checked.
- **Role without scope:** a legitimate `Customer` using an application that was never authorized for customer operations gets through.
- **Both, without ownership:** customer 42 reads customer 99's orders. Both are perfectly legitimate `Customer`s with perfect tokens. **Only ownership catches this** — and it is the check people forget.

> **A useful analogy.** Scope is the visitor badge issued to the contractor company. Role is your job title. Ownership is whether *this particular office* is yours. A valid badge and the title "Engineer" still do not entitle you to walk into someone else's office and move their things.

---

## 4. The four-gate model

Phase 10 formalizes authorization as four gates. A request must pass all four. They run in order, and each catches something the others structurally cannot.

```
┌─────────────────────────────────────────────────────────────┐
│ GATE 1 — AUTHENTICATION            [Phase 9]         → 401  │
│   Is the signature valid? Right issuer? Not expired?        │
├─────────────────────────────────────────────────────────────┤
│ GATE 2 — AUDIENCE                  [Phase 9]         → 401  │
│   Was this token minted for THIS API?                       │
├─────────────────────────────────────────────────────────────┤
│ GATE 3 — POLICY (scope + role)     [Phase 10]        → 403  │
│   Right permission AND right kind of user?                  │
├─────────────────────────────────────────────────────────────┤
│ GATE 4 — OWNERSHIP                 [Phase 10]        → 404  │
│   Does this specific object belong to this caller?          │
└─────────────────────────────────────────────────────────────┘
```

Note the status codes carefully — they are not arbitrary:

| Code | Meaning | When |
|---|---|---|
| `401 Unauthorized` | "I don't know who you are." | No token, bad signature, expired, wrong audience |
| `403 Forbidden` | "I know who you are; you may not do this." | Missing scope or wrong role |
| `404 Not Found` | "No such thing — for you." | Object exists but isn't yours (see §7) |
| `409 Conflict` | "Your account isn't set up for this yet." | `ProfileNotCreated` on owner-scoped writes |

Gates 1 and 2 are **stateless and cryptographic** — no database touched. Gates 3 and 4 are **contextual** — gate 3 reads claims, gate 4 reads the database.

This is **defense in depth**: four independent mechanisms, so a mistake in one does not expose everything.

---

## 5. Gate 3 — Policies: how ASP.NET Core authorization actually works

### Why we couldn't just use `[Authorize(Roles = "Customer")]`

Roles are built into ASP.NET Core; scopes are not. There is no `RequireScope(...)`. The framework has no idea what an OAuth scope is — that is an OAuth concept, and ASP.NET Core's authorization system predates and is independent of it.

So we had to extend the framework. Understanding *how* is worth real points in a review.

### The requirement/handler pattern

ASP.NET Core authorization is built from three pieces:

1. **A policy** — a named bundle of requirements.
2. **A requirement** — a marker describing *what* must be true. It carries data but no logic.
3. **A handler** — the code that decides whether the requirement is satisfied.

This separation exists so that one requirement can be satisfied by multiple different handlers (e.g. "is over 18" could be proven by a birthdate claim *or* by a verified-age claim), and so requirements stay serializable and inspectable.

Our requirement — pure data, no behavior:

```csharp
// src/Talabat/Talabat.API/Auth/ScopeRequirement.cs
public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
```

Our handler — the logic:

```csharp
// src/Talabat/Talabat.API/Auth/ScopeHandler.cs
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

### Three subtleties in those eight lines

**(a) Why `FindAll` *and* `Split(' ')`.** The `scope` claim arrives in two different shapes depending on the token and the JWT library:

```
Form A (space-delimited, one claim):   scope: "openid profile customer.api"
Form B (multiple separate claims):     scope: "openid"
                                       scope: "profile"
                                       scope: "customer.api"
```

`FindAll` handles form B. `SelectMany(Split)` handles form A. Handling only one is a bug that appears the moment a token format changes — and it fails *closed* (403 for legitimate users), which is safer than failing open but still an outage.

**(b) `StringComparer.Ordinal`.** Scope comparison is case-sensitive and exact by OAuth spec. `Customer.API` must not match `customer.api`. Ordinal comparison also avoids culture-specific casing surprises (the classic Turkish dotless-ı problem).

**(c) Why there is no `context.Fail()`.** This surprises people. The handler calls `Succeed` on success and simply does *nothing* on failure. That is deliberate: ASP.NET Core requires **every** requirement in a policy to succeed, so silence means failure. `context.Fail()` is a stronger statement — it means "veto this outright, even if another handler would succeed" — which is not what we want here.

### The policies we defined

```csharp
// Talabat.API/Program.cs
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.CustomerAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ScopeRequirement("customer.api"));
        policy.RequireRole("Customer");
    })
    .AddPolicy(AuthorizationPolicies.CustomerScopeOnly, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ScopeRequirement("customer.api"));
    });
```

```csharp
// Talabat.Delivery.API/Program.cs
.AddPolicy(AuthorizationPolicies.DeliveryAgentAccess, policy =>
{
    policy.RequireAuthenticatedUser();
    policy.AddRequirements(new ScopeRequirement("delivery.api"));
    policy.RequireRole("DeliveryAgent");
});
```

Registering `ScopeHandler` as a singleton is safe because it is stateless — everything it needs arrives in the `context` parameter.

Policy names live in constants (`AuthorizationPolicies.cs`), never as magic strings in controllers. A typo in `[Authorize(Policy = "CustomrAccess")]` throws at runtime, not compile time — the constant makes it a compile error instead.

### Applied to controllers

```csharp
[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]   // Address, Cart, Checkout, Order
[Authorize(Policy = AuthorizationPolicies.DeliveryAgentAccess)] // Deliveries, Status, Location
```

`CatalogController` and `/health` stay anonymous — browsing restaurants requires no account.

---

## 6. Gate 4 — Ownership: the real vulnerability we fixed

This is the heart of the phase.

### The principle

> **Identity must come from the token, never from the request.**

Anything the client sends — route parameter, query string, request body, custom header — is **caller-controlled** and therefore untrusted. The token is signed by our Identity server and cannot be forged. Those are categorically different levels of trust, and the entire fix follows from that distinction.

### The fix, in three coordinated parts

**Part 1 — commands now carry the acting agent.**

```csharp
// BEFORE
public record OutForDeliveryCommand(int DeliveryId);
// AFTER
public record OutForDeliveryCommand(int DeliveryId, int AgentId);
```

Applied to all six lifecycle commands: `OutForDelivery`, `ArrivedAtRestaurant`, `PickUpOrder`, `DeliverOrder`, `CancelDelivery`, `FailDelivery`.

**Part 2 — the controller fills it from the token, not the request.**

```csharp
var result = await _outForDeliveryHandler.Handle(
    new OutForDeliveryCommand(deliveryId, _currentUser.AgentId!.Value),
    cancellationToken);
```

`_currentUser.AgentId` traces back to the `sub` claim of a signed JWT. The client cannot influence it. (The `!` here is a defect — see §14.)

**Part 3 — and this is the important design choice — ownership is enforced by scoping the *load*.**

```csharp
// Talabat.Application/DeliveryAgents/ProgressOutForDelivery/OutForDeliveryHandler.cs
var delivery = await _deliveryRepository.GetByIdForAgentAsync(
    command.DeliveryId, command.AgentId, cancellationToken);

if (delivery is null)
    return UseCaseResult<int>.Failure(
        DomainExceptionMapper.NotFound(ApplicationErrorCodes.DeliveryNotFound, "Delivery was not found."));
```

```csharp
// Talabat.Infrastructure/Persistence/Repositories/DeliveryRepository.cs
public Task<Delivery?> GetByIdForAgentAsync(int deliveryId, int agentId, CancellationToken ct)
    => _dbContext.Deliveries.SingleOrDefaultAsync(
        d => d.Id == deliveryId && d.AssignedAgentId == agentId, ct);
```

### Why "scope the load" beats "load then check"

There is an obvious alternative:

```csharp
// The tempting version — DON'T do this
var delivery = await _repo.GetByIdAsync(command.DeliveryId, ct);
if (delivery.AssignedAgentId != command.AgentId)
    return Forbidden();
```

It looks equivalent. It isn't, for four reasons:

1. **Forgetting it is silent.** If a developer adds a new endpoint and forgets the `if`, there is no error — just a security hole. With scoped loading, forgetting means calling a method that doesn't exist, or getting `null` and returning 404. The safe path is the *default* path.
2. **It leaks by timing and by mistake.** The unauthorized object is now in memory, one careless log line or error message away from exposure.
3. **The database does the work.** `WHERE Id = @id AND AssignedAgentId = @agent` uses an index; you never transfer another user's row across the wire.
4. **One place to audit.** Reviewing "are all repository reads owner-scoped?" is a finite, greppable question. Reviewing "did every handler remember its `if`?" is not.

This pattern was already established on the customer side — `GetByIdForCustomerAsync`, `GetByCustomerIdAsync`, `GetActiveCartByCustomerIdAsync` — so Phase 10 extended a proven convention rather than inventing one.

### The assignment endpoint

```csharp
// BEFORE: agent id from the request body — caller-controlled
new AssignDeliveryCommand(deliveryId, body.AgentId)
// AFTER: agent id from the token; AssignDeliveryBody deleted entirely
new AssignDeliveryCommand(deliveryId, _currentUser.AgentId!.Value)
```

This encodes a **business decision**, not just a security fix: assignment is now **self-assignment** — an agent claims a pending delivery for themselves. The alternative (a dispatcher assigns work to others) would require a `DeliveryOperations` role, which has no approved use case and is explicitly out of MVP scope. Deciding this deliberately, and writing it down in `docs/authorization-strategy.md`, is the difference between a design and an accident.

Note that `AssignDeliveryAgentHandler` correctly uses the *unscoped* `GetByIdAsync` — because a pending delivery belongs to nobody yet. Ownership scoping applies to claimed work, not to the open pool. Being able to explain *why* one read is unscoped shows you understand the rule rather than applying it mechanically.

### Keeping the Domain clean

Ownership is enforced in **Application** (scoped loads) and **API** (token resolution). It is **not** in the Domain.

The `Delivery` aggregate still enforces its own state machine — you cannot mark something delivered before it was picked up — and knows nothing about who is asking. That is the correct split: the aggregate protects *business invariants*; the application layer decides *whose data you may load*. Putting `ICurrentUser` or role checks inside an aggregate would couple the domain model to authentication and make it untestable in isolation.

---

## 7. Why 404 and not 403

When agent B requests agent A's delivery, we return **404 Not Found** — not 403 Forbidden. This looks wrong at first and is worth being able to defend.

### The reasoning

**403 means "this exists, but you may not have it."** That sentence contains information.

Consider an attacker enumerating `/api/agent/deliveries/1`, `/2`, `/3`… and comparing responses:

- Mixed `403` and `404` → the `403`s map out exactly which delivery ids exist. Now they know the system's scale, id allocation pattern, and which ids are worth attacking further.
- Uniform `404` → indistinguishable. They learn nothing.

This is a **side-channel information leak**. The response code itself is data.

### The general rule

> Use **403** when the *capability* is denied — that's a fact about the caller, which they already know.
> Use **404** when a *specific object* is denied — because acknowledging existence is itself a disclosure.

Concretely in our system:

| Situation | Code | Why |
|---|---|---|
| Customer calls a delivery-agent endpoint | 403 | They already know they're not an agent. No leak. |
| Customer 42 requests order #77 (owned by 99) | 404 | Confirming #77 exists would leak. |

GitHub uses exactly this pattern for private repositories: not a member? You get a 404, identical to a repo that never existed.

### The honest trade-off

404-for-forbidden makes debugging harder. A legitimate user hitting a genuine bug sees "not found" when the real cause was an ownership mismatch. Mitigation: log the true reason server-side with the caller id and the object id, while returning the opaque 404 to the client. The information exists for operators; it just doesn't cross the network boundary.

---

## 8. The bootstrap paradox

A subtle problem worth presenting, because spotting it demonstrates real understanding.

### The circular dependency

`POST /api/me/profile` is the endpoint that **creates** a customer profile — which is what **grants** the `Customer` role.

If that endpoint required the `CustomerAccess` policy (which demands the `Customer` role), then:

```
To create a profile → you need the Customer role
To get the Customer role → you must create a profile
```

A perfect deadlock. **No user could ever become a customer.** Every new registration would be permanently locked out, and it would look like a mysterious 403 rather than an obvious bug.

### The solution: a second, weaker policy

```csharp
.AddPolicy(AuthorizationPolicies.CustomerScopeOnly, policy =>
{
    policy.RequireAuthenticatedUser();
    policy.AddRequirements(new ScopeRequirement("customer.api"));
    // deliberately NO RequireRole
});
```

The bootstrap endpoint still requires authentication and the correct scope — it is not open. It just doesn't require the role it is about to grant.

### The lesson

This generalizes: **any endpoint that grants a capability cannot require that capability.** The same pattern appears in account activation, first-time onboarding, and role-request flows. Recognizing the shape once means you'll spot it everywhere.

In our implementation `GET` and `PUT /api/me/profile` also use `CustomerScopeOnly`. That is defensible for a related reason explained next — though it deserves an explicit note in the strategy document so a future reader doesn't mistake it for an oversight.

---

## 9. Two sources of truth: token vs database

This is the deepest idea in Phase 10, and the one most likely to earn respect in a review.

### The tension

Our system derives authorization facts from **two independent sources**:

| Source | Read by | Freshness |
|---|---|---|
| **Token claims** (`role`, `scope`) | Gate 3 — policies | Frozen at issuance; stale up to 1 hour |
| **Database** (`User.UserType`) | Gate 4 — `ICurrentUser` | Always current |

Recall `CurrentUser.EnsureResolved()`:

```csharp
var subjectValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
// ...
var userType = _dbContext.Users.AsNoTracking()
    .Where(u => u.Id == parsedId).Select(u => u.UserType).FirstOrDefault();

if (userType.HasFlag(UserType.Customer)) { _hasCustomerCapability = true; _customerId = parsedId; }
```

It takes only the **subject** from the token and re-derives capability from the **database**.

### Why this is a deliberate design, not redundancy

A JWT is a **snapshot**. It records what was true when it was signed. It cannot be revoked, so for up to an hour it may describe a world that no longer exists.

The database is the **present**.

Deriving authorization purely from claims means a revoked agent keeps their access for the remainder of the token's life. Deriving it from the database closes that window instantly, at the cost of one indexed query per request.

Our system uses **both, at different gates**: the fast, stateless claim check screens out obviously wrong callers before any database work; the authoritative database check governs what data is actually touched.

### The two directions of staleness

**Direction A — capability added after issuance.** A user creates their profile. The database now says `Customer`; their token does not. They must refresh the token before role-gated endpoints work. This is exactly why `GET /api/me/profile` uses `CustomerScopeOnly` — so "create profile, then read it back" doesn't fail confusingly for one hour.

**Direction B — capability removed after issuance.** An agent is suspended. Their token still says `role: DeliveryAgent`, so gate 3 passes; the database says otherwise, so `ICurrentUser.AgentId` is `null`.

Direction B is where the current code has a bug — the controller writes `_currentUser.AgentId!.Value`, and that null-forgiving `!` turns a legitimate authorization failure into an unhandled exception and an HTTP 500. It should be an explicit check returning 403. (Details in §14. Notably, `GoOnlineHandler` and `UpdateLocationHandler` already guard this correctly — the inconsistency is what makes it clearly a defect rather than a judgment call.)

> **Say this to your mentor:** "Claims are a cache of authorization state. Like every cache, they can go stale, and the interesting bugs live in the gap between the cache and the source of truth."

---

## 10. Quality gates: making the architecture executable

The second half of Phase 10 is about ensuring the design cannot quietly rot.

### The problem with written rules

Nine phases produced documents saying "Domain must not depend on EF Core," "controllers must not contain business logic," "dependencies point inward." Documents do not enforce anything. Six months and three developers later, a well-meaning `using Microsoft.EntityFrameworkCore;` appears in the Domain because it made one thing convenient, review misses it, and the architecture erodes one reasonable-looking commit at a time.

**Architecture tests turn the constitution into executable code.**

```csharp
[Fact]
public void Domain_ShouldNotReference_EntityFrameworkCore()
{
    var forbidden = GetReferencedAssemblyNames()
        .Where(a => a.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true)
        .Select(a => a.Name!).ToList();

    Assert.True(forbidden.Count == 0,
        $"Domain references forbidden EF Core packages: {string.Join(", ", forbidden)}");
}
```

Now the rule fails a build instead of relying on a reviewer's attention.

`tests/Talabat.ArchitectureTests` covers: Domain free of EF Core, ASP.NET Core, Duende/IdentityServer and HTTP abstractions; Domain free of `ICurrentUser`, `Claim`, and `ClaimsPrincipal`; no API project referencing `Talabat.Identity`; Customer API not referencing Delivery API; Infrastructure not referencing API projects.

### The documented exception

Phase 9's Option 1 decision made `User` inherit `IdentityUser<int>`, which requires `Microsoft.Extensions.Identity.Stores` in the Domain. That is a **deliberate, recorded** exception.

The right way to handle a known exception is to encode it explicitly, so that *this one* reference passes while any *new* framework leak still fails. An allowlist of exactly one entry keeps the exception visible and bounded — the wrong approach is to loosen the rule generally, which quietly licenses the next leak. (The current test is too weak to do this properly; see §14.)

### The test pyramid

Phase 10 filled in missing layers rather than inventing testing (that started in Phase 3):

```
        ┌──────────────────────┐
        │  API integration     │  Customer.API.Tests, Delivery.API.Tests
        │  (endpoints, auth)   │  ← Phase 10 added Delivery
        ├──────────────────────┤
        │  Infrastructure      │  EF mappings, constraints
        ├──────────────────────┤
        │  Application         │  handlers with fake repositories
        ├──────────────────────┤
        │  Domain unit         │  aggregates, value objects  ← Phase 10 added
        └──────────────────────┘
         + Architecture tests (cross-cutting, dependency rules)  ← Phase 10 added
```

Why the shape matters: domain rules belong in fast domain tests, not in slow API tests. If your only proof that "an order cannot be delivered before pickup" is an HTTP test, the feedback loop is slow, the failure message is unhelpful, and the rule is tested through five layers of incidental machinery.

### CI

`.github/workflows/ci.yml` runs on push and pull request: restore → build (Release) → test → vulnerability scan. It is the mechanism that makes all the above non-optional; a gate that only runs when someone remembers is not a gate. (The vulnerability step currently doesn't actually fail the build — §14.)

---

## 11. Where every file lives

```
src/Talabat/
├── Talabat.API/                            ◄── Customer API
│   ├── Auth/
│   │   ├── ScopeRequirement.cs                 marker: which scope is needed
│   │   ├── ScopeHandler.cs                     logic: is that scope present?
│   │   ├── AuthorizationPolicies.cs            policy-name constants
│   │   └── CurrentUser.cs                      ClaimsPrincipal → ICurrentUser (Phase 9)
│   ├── Middleware/ProfileEnforcementFilter.cs  ProfileNotCreated handling
│   ├── Controllers/                            [Authorize(Policy = ...)] applied
│   └── Program.cs                              policy registration + handler DI
│
├── Talabat.Delivery.API/                   ◄── Delivery API (mirrors the above)
│   ├── Auth/{ScopeRequirement,ScopeHandler,AuthorizationPolicies,CurrentUser}.cs
│   ├── Controllers/DeliveriesController.cs     agent id from token, never from request
│   └── Program.cs
│
├── Talabat.Application/
│   ├── Abstractions/ICurrentUser.cs            framework-neutral identity contract
│   └── DeliveryAgents/**/                      commands now carry AgentId; handlers load scoped
│
├── Talabat.Domain/
│   ├── Aggregates/                             unchanged — no roles, no claims
│   └── Interfaces/IDeliveryRepository.cs       + GetByIdForAgentAsync
│
└── Talabat.Infrastructure/
    └── Persistence/Repositories/DeliveryRepository.cs   WHERE Id = @id AND AssignedAgentId = @agent

tests/
├── Talabat.Domain.Tests/          ◄── new: aggregates & value objects
├── Talabat.Delivery.API.Tests/    ◄── new: delivery endpoint authorization
├── Talabat.ArchitectureTests/     ◄── new: dependency rules as tests
├── Talabat.Customer.API.Tests/        incl. OwnershipTests
├── Talabat.Application.Tests/
├── Talabat.Infrastructure.Tests/
└── Talabat.Identity.Tests/

docs/
├── authorization-strategy.md            ◄── new: the four-gate model, decisions, rationale
├── authorization-endpoint-matrix.md     ◄── new: every endpoint × policy × role × scope × ownership
└── authorization-matrix.md                  marked SUPERSEDED, kept for history

.github/workflows/ci.yml            ◄── new
```

**Dependency direction is unchanged:** `API → Application → Domain`, with `Infrastructure` wired only at the composition root.

---

## 12. Design decisions and their trade-offs

### Decision 1 — Four gates instead of one check
**Gained:** defense in depth; each layer independently verifiable; a mistake in one doesn't expose everything.
**Cost:** more moving parts; a developer must understand which gate rejected a request when debugging.

### Decision 2 — Custom `ScopeRequirement` rather than reusing roles
**Gained:** scope and role stay conceptually separate, matching OAuth semantics; either can change without disturbing the other.
**Cost:** custom code where a built-in would have been simpler; ~30 lines duplicated across two hosts (see Decision 6).

### Decision 3 — Ownership by scoped loading, not post-load checks
**Gained:** the safe path is the default; forgetting produces a compile error or a 404, not a silent hole; the database filters using an index.
**Cost:** more repository methods; ownership logic lives in the persistence contract rather than reading as an explicit rule in the handler.

### Decision 4 — 404 instead of 403 for ownership failures
**Gained:** no existence disclosure; enumeration attacks learn nothing.
**Cost:** harder debugging for legitimate users; requires disciplined server-side logging to compensate.

### Decision 5 — Self-assignment instead of dispatcher assignment
**Gained:** eliminates caller-supplied agent ids entirely; needs no new role; matches MVP scope.
**Cost:** no operational oversight of workload distribution. If dispatching is ever required, it needs a `DeliveryOperations` role and a separate endpoint — deliberately deferred, not forgotten.

### Decision 6 — Duplicating `ScopeHandler`/`ScopeRequirement` in both hosts
**Gained:** hosts stay independent and deployable separately; no shared "common" project pulling web concerns into a place they don't belong.
**Cost:** ~30 duplicated lines; a fix must be applied twice. Acceptable at two hosts; revisit at four. Be ready to name the alternative (a small shared `Talabat.Web.Common` library) and say why you deferred it.

### Decision 7 — Policies read claims; `ICurrentUser` reads the database
**Gained:** fast stateless screening plus authoritative, always-current data where it matters.
**Cost:** two sources of truth that can disagree, producing the staleness behavior in §9 — which must be handled explicitly rather than assumed away.

---

## 13. Questions your mentor will ask — with answers

**Q: You already authenticate users. Why is more needed?**
Authentication says *who* you are; it says nothing about *which objects* you may touch. Before this phase, any authenticated delivery agent could mark any delivery in the system as delivered by changing a number in the URL. Everyone involved was legitimately logged in. That's Broken Object Level Authorization — OWASP API Security #1.

**Q: Why both scope and role? Isn't that redundant?**
They describe different subjects. Scope is what the *application* may do on the user's behalf; role is what the *user* is. A legitimate `Customer` using an app never authorized for customer operations should fail — role alone would let them through.

**Q: Why 404 instead of 403 for someone else's order?**
403 confirms the object exists, which is itself information. An attacker enumerating ids can map the database from the 403/404 pattern. Uniform 404 leaks nothing. GitHub does exactly this for private repositories.

**Q: Why scope the query instead of loading and comparing?**
Forgetting the comparison is silent and invisible; forgetting a scoped load produces a 404 or a compile error. The safe path should be the default path. It also means another user's row never leaves the database.

**Q: Ownership rules aren't in the Domain. Isn't that a DDD violation?**
No. Aggregates protect *business invariants* — a delivery cannot be marked delivered before pickup. Ownership is an *application-boundary* concern about which data a request may load. Putting `ICurrentUser` in an aggregate would couple the domain to authentication and make it untestable in isolation.

**Q: If a token says `role: DeliveryAgent` but you revoked them, what happens?**
The policy passes (claims are a one-hour-old snapshot) but `ICurrentUser.AgentId` resolves to null from the live database, so no data is reachable. That's deliberate defense in depth. The current code handles that null unsafely and would return 500 instead of 403 — a known defect I've documented and am fixing.

**Q: Why can `POST /api/me/profile` skip the role check?**
Because it's the endpoint that *grants* the role. Requiring it would deadlock — you'd need a profile to create a profile. It still requires authentication and the correct scope. This pattern applies to any capability-granting endpoint.

**Q: What stops the architecture from decaying?**
Architecture tests. The dependency rules are executable assertions that fail a build, not prose in a document. CI runs them on every push and pull request.

**Q: What's still missing?**
Two things I'd fix before calling the phase done: the null-handling defect above, and tests proving delivery ownership actually holds. The implementation is correct, but an untested security control is one refactor from regressing — which is exactly what a quality-gates phase exists to prevent. (Full list in §14.)

---

## 14. Known gaps — honest status

Stating these yourself is far stronger than having them found for you.

### High severity
1. **`_currentUser.AgentId!.Value` (7 occurrences in `DeliveriesController`).** The null-forgiving operator turns the revoked-capability case (§9, direction B) into an unhandled exception and HTTP 500 instead of 403. `GoOnlineHandler` and `UpdateLocationHandler` already guard correctly, so this is an inconsistency, not a design choice. Fix: explicit null check returning `Forbid()`.
2. **Delivery ownership is implemented but untested.** All nine delivery tests cover policy gates (missing token, wrong scope, wrong role); none verifies that agent B cannot progress agent A's delivery. The primary acceptance criterion of the phase is unproven.

### Medium severity
3. **CI vulnerability gate never fails.** `dotnet list package --vulnerable` exits 0 even when it finds vulnerabilities; the step must grep the output and exit non-zero.
4. **Audience isolation has no automated test.** Both API test factories replace the JWT scheme with a `TestAuthHandler`, which bypasses signature, issuer, *and* audience validation. Phase 9's headline security property is therefore uncovered.
5. **One architecture test asserts nothing.** `Assert.True(identityStores.Count <= 1, ...)` passes whether the count is 0 or 1.
6. **Architecture tests use `GetReferencedAssemblies()`**, which only sees assemblies whose types are actually *used*. An unused forbidden `PackageReference` would pass undetected.
7. **`Talabat.Domain.Tests` is missing `Cart`, `Order`, and `Delivery` state-machine tests** — named explicitly in the roadmap as Phase 10 backfill.

### Deliberately out of scope
`Admin`, `DeliveryOperations`, and `RestaurantOwner` policies (candidate role names only — no approved use cases; `RestaurantOwner` is blocked until restaurant ownership exists in the domain model); payment, notifications, coupons, reviews; Angular front-ends; API versioning; rate limiting; production key hardening.

---

## 15. Glossary

| Term | Meaning |
|---|---|
| **BOLA / IDOR** | Broken Object Level Authorization — an authenticated user reaching another user's objects by changing an identifier. OWASP API Security #1. |
| **Policy** | A named bundle of authorization requirements applied via `[Authorize(Policy = ...)]` |
| **Requirement** | Data describing what must be true (`IAuthorizationRequirement`) |
| **Handler** | Code that decides whether a requirement is satisfied (`AuthorizationHandler<T>`) |
| **Scope** | An OAuth permission granted to a *client application* |
| **Role** | A classification of the *user* |
| **Ownership** | Whether a *specific record* belongs to the caller |
| **Owner-scoped read** | A repository query filtered by owner id so foreign rows are never loaded |
| **Defense in depth** | Multiple independent controls, so one failure isn't total |
| **Claim staleness** | The gap between token contents and current database state |
| **Architecture test** | An automated test asserting dependency/design rules |
| **Quality gate** | An automated check that must pass before code is accepted |
| **Enumeration attack** | Probing sequential ids to discover which resources exist |
| **Fail closed** | On error/ambiguity, deny access (the safe default) |
