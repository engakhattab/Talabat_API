# Phase 10 Review — Findings & Required Fixes

**Reviewed:** commit `d604f1e` "Complete Phase 10" on `feature/user-aggregate-refactor`
**Verdict:** core authorization design is correct and the ownership vulnerability is genuinely fixed. Two high-severity defects and several quality-gate weaknesses must be resolved before Phase 10 can be signed off.

---

## What is correct (do not change)

- `ScopeRequirement` / `ScopeHandler` handle both space-delimited and multi-claim `scope` formats; registered as `IAuthorizationHandler` singletons in both hosts.
- Policies `CustomerAccess`, `CustomerScopeOnly`, `DeliveryAgentAccess` require authentication + scope + role as designed.
- Policy attributes applied to all controllers; `CatalogController` and `/health` remain anonymous.
- **Delivery ownership is genuinely fixed.** All six lifecycle commands carry `AgentId`; handlers load via `IDeliveryRepository.GetByIdForAgentAsync(deliveryId, agentId)`, whose implementation filters `delivery.Id == deliveryId && delivery.AssignedAgentId == agentId` and returns `404` on mismatch. This closes the vulnerability.
- `AssignDeliveryBody` removed; assignment is self-assignment from the token.
- `GoOnlineHandler`, `GoOfflineHandler`, `UpdateLocationHandler` resolve the agent from `ICurrentUser` with proper null/capability guards.
- Customer-side `OwnershipTests` are strong (foreign address/order → 404, no route/body `CustomerId`, stale-role gating).
- Documentation is complete: `authorization-strategy.md`, `authorization-endpoint-matrix.md`, old matrix marked **SUPERSEDED** with links.
- Solution file includes all seven test projects, so CI compiles and runs them.

---

## FIX 1 — HIGH — `_currentUser.AgentId!.Value` throws 500 on a stale-capability token

**File:** `src/Talabat/Talabat.Delivery.API/Controllers/DeliveriesController.cs` — lines 102, 116, 128, 140, 152, 164, 177 (7 occurrences).

```csharp
new OutForDeliveryCommand(deliveryId, _currentUser.AgentId!.Value),
```

**Why this is reachable, not theoretical.** The `DeliveryAgentAccess` policy checks the **`role` claim in the token**. `ICurrentUser.AgentId` is derived from a **live database read** of `User.UserType`. These two sources can disagree:

- An agent's `DeliveryAgent` capability is revoked or suspended, but their already-issued token (valid up to 1 hour) still carries `role: DeliveryAgent`.
- Policy passes → controller runs → `AgentId` is `null` → `!.Value` throws `InvalidOperationException` → **HTTP 500**.

A revoked user should get `401`/`403`, never a server error. This is also inconsistent with `GoOnlineHandler` and `UpdateLocationHandler`, which already guard correctly.

**Fix.** Replace the null-forgiving operator with an explicit guard in every one of the seven actions:

```csharp
[HttpPost("{deliveryId:int}/out-for-delivery")]
public async Task<IActionResult> OutForDelivery(int deliveryId, CancellationToken cancellationToken)
{
    if (!_currentUser.HasDeliveryAgentCapability || _currentUser.AgentId is null)
        return Forbid();

    var result = await _outForDeliveryHandler.Handle(
        new OutForDeliveryCommand(deliveryId, _currentUser.AgentId.Value),
        cancellationToken);

    return result.ToActionResult(id => Ok(id));
}
```

Prefer extracting a small private helper to avoid repeating the guard seven times:

```csharp
private bool TryGetAgentId(out int agentId)
{
    agentId = 0;
    if (!_currentUser.HasDeliveryAgentCapability || _currentUser.AgentId is null) return false;
    agentId = _currentUser.AgentId.Value;
    return true;
}
```

Use `Forbid()` (403) — the caller is authenticated but lacks the live capability. Document this in the strategy doc's claim-freshness section.

**Test to add** (`tests/Talabat.Delivery.API.Tests`): token carrying `role: DeliveryAgent` for a user whose stored `UserType` lacks the `DeliveryAgent` flag → lifecycle endpoint returns **403**, not 500.

---

## FIX 2 — HIGH — delivery ownership is implemented but never tested

`tests/Talabat.Delivery.API.Tests/DeliveriesAuthorizationTests.cs` has 9 tests, all covering policy gates (missing token / wrong scope / wrong role / correct policy). **None tests ownership.**

The primary Phase 10 acceptance criterion — *"a delivery agent cannot read or progress another agent's delivery"* — is therefore unproven. The implementation looks right, but an untested security control is one refactor away from silently regressing, which is exactly what quality gates exist to prevent.

**Fix.** Add `tests/Talabat.Delivery.API.Tests/DeliveryOwnershipTests.cs` covering:

| Case | Expected |
|---|---|
| Agent B calls `POST /api/agent/deliveries/{id}/out-for-delivery` on a delivery assigned to agent A | 404 |
| Same for `arrived-at-restaurant`, `picked-up`, `delivered`, `cancel`, `fail` | 404 |
| Agent A calls the same endpoint on their **own** assigned delivery | 200 |
| `GET /api/agent/deliveries/active` as agent B does not return agent A's delivery | agent A's delivery absent |
| `GET /api/agent/deliveries/history` is agent-scoped | only own deliveries |
| No lifecycle endpoint accepts an agent id from route/query/body | reflection assertion, mirroring the existing customer-side `No_controller_accepts_route_body_CustomerId` test |

Seed two agents and at least one delivery assigned to agent A. `TestAuthHandler` already supports a subject header, so switching identities between requests is straightforward.

---

## FIX 3 — MEDIUM — the CI vulnerability gate never fails the build

**File:** `.github/workflows/ci.yml`

```yaml
- name: Check vulnerable packages
  run: dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
```

`dotnet list package --vulnerable` **exits 0 even when vulnerabilities are found** — it only prints them. As written, this step can never fail, so the gate is decorative.

**Fix:**

```yaml
- name: Check vulnerable packages
  run: |
    output=$(dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive 2>&1)
    echo "$output"
    if echo "$output" | grep -q "has the following vulnerable packages"; then
      echo "::error::Vulnerable packages detected"
      exit 1
    fi
```

While editing the workflow, also add coverage collection, which the plan specified and is currently missing:

```yaml
- name: Test
  run: dotnet test src/Talabat/Talabat.slnx --no-build --configuration Release
       --collect:"XPlat Code Coverage"
       --logger "trx;LogFileName=test-results.trx"
```

---

## FIX 4 — MEDIUM — audience isolation is not covered by any automated test

Both `tests/Talabat.Customer.API.Tests/Infrastructure/CustomWebApplicationFactory.cs` and the Delivery equivalent replace the JWT bearer scheme with `TestAuthHandler`:

```csharp
options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
```

This bypasses JWT validation entirely — signature, issuer, **and audience**. So the headline security property from Phase 9 ("a customer token is rejected by the Delivery API and vice versa") has **zero** automated coverage, despite being an explicit Phase 10 acceptance criterion.

Using `TestAuthHandler` for policy/ownership tests is fine and should stay — it keeps those tests fast and focused. The gap is that *nothing* exercises the real bearer pipeline.

**Fix.** Add one small dedicated test class per API (e.g. `JwtAudienceIsolationTests.cs`) that uses a factory variant which **keeps the real `AddJwtBearer` pipeline** and points it at a symmetric test signing key trusted only in the Test environment:

- Build a JWT with `iss` = configured authority, `aud = talabat.delivery.api`, `role = Customer`, `scope = customer.api`, signed with the test key.
- Send it to the **Customer** API → expect **401**.
- Send a correctly-audienced token → expect non-401.
- Mirror both cases on the Delivery API.
- Add: expired token → 401; tampered signature → 401.

Keep this to one focused class per host; it is the proof that Phase 9's core control works.

---

## FIX 5 — MEDIUM — `Domain_AllowedException_IdentityStores` asserts nothing

**File:** `tests/Talabat.ArchitectureTests/DomainArchitectureTests.cs`

```csharp
Assert.True(identityStores.Count <= 1, "Identity.Stores reference count unexpected");
```

`Count` is 0 or 1, so this passes unconditionally. It documents the exception in its name but enforces nothing — and would keep passing if the reference disappeared or if the intent changed.

**Fix.** Make it assert the documented reality:

```csharp
[Fact]
public void Domain_Has_Exactly_The_One_Documented_Identity_Exception()
{
    var identityRefs = GetReferencedAssemblyNames()
        .Where(a => a.Name?.Contains("Identity", StringComparison.OrdinalIgnoreCase) == true)
        .Select(a => a.Name!)
        .ToList();

    // Option 1 (documented): User : IdentityUser<int> requires Microsoft.Extensions.Identity.Stores.
    // Any OTHER Identity assembly is an unapproved leak.
    Assert.Equal(new[] { "Microsoft.Extensions.Identity.Stores" }, identityRefs.Distinct().Order());
}
```

This now fails both if the exception is removed (so the test gets revisited deliberately) and if a *new* Identity assembly creeps in.

---

## FIX 6 — MEDIUM — architecture tests miss unused package references

All rules use `Assembly.GetReferencedAssemblies()`, which lists only assemblies whose types the compiler actually emitted references to. A `<PackageReference Include="Microsoft.EntityFrameworkCore" />` added to `Talabat.Domain.csproj` but not yet *used* would **not** be detected — so the tests pass right up until someone writes the first line of offending code.

The project constitution's rule is about the **project files** ("MUST contain no web or Identity/Auth packages"), not just emitted IL.

**Fix.** Add a complementary test that parses the `.csproj` files directly:

```csharp
[Fact]
public void Domain_And_Application_Csproj_Have_No_Forbidden_PackageReferences()
{
    var forbiddenPrefixes = new[]
    {
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
        "Duende", "IdentityServer", "Swashbuckle", "Microsoft.Extensions.Http"
    };
    var allowed = new[] { "Microsoft.Extensions.Identity.Stores" }; // documented Option 1 exception

    foreach (var csproj in new[] { "Talabat.Domain", "Talabat.Application" })
    {
        var path = LocateCsproj(csproj);              // walk up from AppContext.BaseDirectory
        var packages = XDocument.Load(path)
            .Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .Where(p => !allowed.Contains(p));

        var violations = packages
            .Where(p => forbiddenPrefixes.Any(f => p.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(violations.Count == 0, $"{csproj} has forbidden packages: {string.Join(", ", violations)}");
    }
}
```

Keep the existing reflection tests — the two approaches catch different failure modes.

---

## FIX 7 — MEDIUM — `Talabat.Domain.Tests` is missing the aggregates the roadmap named

Present: `UserCapabilityTests` (17), `ValueObjectsTests` (17), `AddressTests` (11). Good coverage of `User` and value objects.

Missing, and explicitly listed as Phase 10 backfill ("Domain unit tests… aggregate state machines… Checkout and delivery lifecycle have regression tests"):

- **`CartTests`** — add/update/remove item, quantity invariants, clearing, and the rule that cart items never store price.
- **`OrderTests`** — creation from checkout, immutable item snapshots, address snapshot, status transitions and rejection of invalid ones.
- **`DeliveryTests`** — the full lifecycle state machine: `Assign → OutForDelivery → ArrivedAtRestaurant → PickedUp → Delivered`, plus `Cancel` / `Fail` with reason, and every invalid transition throwing the correct `DomainException`.

These are pure unit tests — no EF, no HTTP.

---

## FIX 8 — LOW — review, then decide: `CustomerScopeOnly` on `GET`/`PUT /api/me/profile`

Code and `docs/authorization-endpoint-matrix.md` agree (so this is deliberate, not a mismatch), but it deviates from the plan, which reserved `CustomerScopeOnly` for the bootstrap `POST` only.

**There is a good argument for keeping it:** right after `POST /api/me/profile`, the caller's token still lacks the `Customer` role until it expires or is refreshed. Requiring `CustomerAccess` on `GET` would make the natural "create then read" sequence fail confusingly.

**The cost:** the role gate is skipped on those two routes, so `ProfileEnforcementFilter` carries the whole load (returning 404/409).

**Action:** no code change required — but add one sentence to the claim-freshness section of `docs/authorization-strategy.md` stating this is intentional and why, so a future reviewer doesn't read it as an oversight. If you would rather enforce the role, switch `GET`/`PUT` to `CustomerAccess` and document that clients must refresh the token after profile creation.

---

## Implementation order

1. **FIX 1** — controller guards (highest severity, smallest change).
2. **FIX 2** — delivery ownership tests (proves the phase's main deliverable).
3. **FIX 4** — audience isolation tests.
4. **FIX 3** — CI gate + coverage.
5. **FIX 5**, **FIX 6** — architecture test hardening.
6. **FIX 7** — Domain test backfill.
7. **FIX 8** — one documentation sentence.

## Acceptance after fixes

- No `!.Value` on any nullable `ICurrentUser` member anywhere in either API.
- Stale-capability token → 403, never 500 (test).
- Agent B cannot read or progress agent A's delivery (test, all six lifecycle endpoints).
- Cross-audience tokens rejected in both directions through the real JWT pipeline (test).
- CI fails on a seeded vulnerable package; coverage artifact produced.
- Architecture tests fail if a forbidden `PackageReference` is added to Domain or Application, even when unused.
- `Cart`, `Order`, and `Delivery` state machines have domain unit tests.
- Full solution build green; all seven test projects green.
