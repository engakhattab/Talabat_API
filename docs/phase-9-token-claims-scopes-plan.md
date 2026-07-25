# Phase 9 – Centralized Auth: Token, Claims & Scopes Refinement

> **Purpose of this document:** implementation *plan and strategy* only, to be handed to a code-generation model (opencode). It explains the logic, the target design, where each piece lives, and the key techniques with small illustrative snippets. It is **not** the full implementation.

> **Scope guardrail:** This is a deliberate, documented extension of MVP scope (auth), sequenced as Phase 9 in `PROJECT_IMPLEMENTATION_ROADMAP.md`. It does **not** add payment, notifications, coupons, reviews, real delivery tracking, or admin dashboards. Endpoint-level authorization *policies* and the endpoint→policy matrix belong to **Phase 10**, not here. Phase 9 stops at: issuing correct tokens, defining resources/scopes/claims/roles, and making the APIs validate JWTs.

---

## 0. What already exists (Phase 6) vs. what Phase 9 adds

**Already done in Phase 6 (do not rebuild):**
- `Talabat.Identity` ASP.NET Core Web API host exists.
- Duende IdentityServer is integrated with ASP.NET Core Identity.
- `ApplicationUser` lives in an Identity-specific Infrastructure namespace.
- `TalabatDbContext` derives from `IdentityDbContext<ApplicationUser, IdentityRole, string>` (single DbContext for business + Identity tables).
- Cookie-based `POST /api/account/register|login|logout` exist. **Login returns a cookie, never a JWT.**
- Dependency direction: `Talabat.Identity → Talabat.Infrastructure → Talabat.Application → Talabat.Domain`. Never reverse.

**Phase 9 adds:**
1. OIDC **clients** for the Angular Customer SPA and Angular Delivery SPA (Authorization Code + PKCE, no secret).
2. **API resources** (audiences) and **API scopes** — one audience per business API.
3. **Roles** (generic, seedable) and **custom claims** (profile-link claims) emitted into tokens via a custom `IProfileService`.
4. **Account → domain-profile linkage** strategy (account → `Customer` / `DeliveryAgent`).
5. **JWT Bearer validation** wired into `Talabat.Customer.API` and `Talabat.DeliveryAgent.API`.
6. CORS + signing-key strategy for real token flows.

**Hard invariant carried from all prior phases:** `Talabat.Domain` and `Talabat.Application` stay independent from Duende, `IdentityUser`/`ApplicationUser`, `ClaimsPrincipal`, `HttpContext`, and JWT. `Customer` and `DeliveryAgent` are **domain profiles**, they never inherit from Identity account types.

---

## 1. Actors and the real request flow (correcting the "round-trip" idea)

There are two kinds of caller, and they behave differently:

- **Angular SPAs (browser)** = OIDC **clients**. They talk to `Talabat.Identity` to *get* tokens.
- **`Talabat.Customer.API` / `Talabat.DeliveryAgent.API`** = **resource servers**. They *receive* and *validate* tokens. They do **not** call Identity per request.

### Login (happens once, redirect-based — Authorization Code + PKCE)
```
Browser (Customer SPA)
  --(1) redirect to /connect/authorize (with PKCE code_challenge)--> Talabat.Identity
  <--(2) interactive login UI (Angular login route) --
  --(3) user submits credentials, Identity cookie established -->
  <--(4) authorization code to exact registered redirect_uri --
  --(5) POST /connect/token with code + PKCE verifier -->
  <--(6) access_token (JWT) [+ id_token, optional refresh_token] --
```

### Every API call afterwards (NO round-trip to Identity)
```
Browser --(Authorization: Bearer <JWT>)--> Talabat.Customer.API
Talabat.Customer.API validates the JWT locally:
   - signature  -> against Identity's JWKS (fetched once from /.well-known, cached)
   - issuer     -> equals Talabat.Identity authority
   - audience   -> equals this API's resource name
   - expiry     -> not expired
   - scope/role -> present as required
```

Identity is contacted again only for: token **refresh**, **logout** (end-session), or a fresh **login**. This is why key rotation/JWKS caching matters, not per-request calls.

> Your phrasing "request goes to Identity then back" is only true for step (1)–(6), the login. It is **not** true for normal API traffic.

---

## 2. Token model design (generic and multi-API from day one)

Because you have two APIs now and more roles later, isolate by **audience** and keep everything data-driven.

### 2.1 API Resources = audiences (one per API)
| Resource name (audience) | Belongs to |
|---|---|
| `talabat.customer.api` | `Talabat.Customer.API` |
| `talabat.delivery.api` | `Talabat.DeliveryAgent.API` |

A token minted for the customer SPA carries `aud: talabat.customer.api`. The Delivery API rejects it because the audience doesn't match. **Audience isolation prevents a customer token from working on the delivery API and vice-versa.**

### 2.2 API Scopes = what an operation is allowed to touch
Start coarse; you can split later without breaking clients.
- `customer.api` (grants access to customer-facing operations: catalog read, basket, ordering)
- `delivery.api` (grants access to delivery-agent operations)

> Keep scope names stable. Splitting `customer.api` into `basket.write` / `ordering.write` later is additive. Renaming an existing scope is a breaking change for every client and token — avoid.

### 2.3 Identity resources (OIDC standard)
`openid`, `profile`, and a custom `roles` identity resource (so role claims can flow into the id_token when needed).

### 2.4 Clients (one per SPA, public, PKCE)
| Client | Grant | Secret | Allowed scopes |
|---|---|---|---|
| `talabat-customer-spa` | Authorization Code + PKCE | none | `openid profile roles customer.api [offline_access]` |
| `talabat-delivery-spa`  | Authorization Code + PKCE | none | `openid profile roles delivery.api [offline_access]` |

Each client also declares exact `RedirectUris`, `PostLogoutRedirectUris`, and `AllowedCorsOrigins` (the SPA origins). `offline_access` only if you want refresh tokens (see §6).

### 2.5 Roles (generic, seed-driven)
Use ASP.NET `IdentityRole` (already in `TalabatDbContext`). Seed the known roles and leave room for more:
```
Customer, DeliveryAgent            // needed now
DeliveryOperations, Admin, RestaurantOwner   // future candidates — just seed names when needed
```
Nothing in Domain/Application knows about these strings. Roles are an Identity + token concern only.

### 2.6 Custom claims (profile-link — the important one)
The token must carry a stable link from the **account** to the **domain profile**, so the API can do ownership checks **without trusting route IDs**:
- Customer token → claim `customer_id: <CustomerId>`
- Delivery token → claim `delivery_agent_id: <DeliveryAgentId>`

This is emitted by a custom `IProfileService` (§4).

---

## 3. Where each piece lives (files / projects)

```
src/Talabat/
├─ Talabat.Identity/                         (Phase 6 host — extend here)
│  ├─ IdentityServer/
│  │   ├─ IdentityServerConfig.cs            <-- ApiResources, ApiScopes, IdentityResources, Clients (in-memory/config)
│  │   └─ TalabatProfileService.cs           <-- IProfileService: emits roles + customer_id/delivery_agent_id claims
│  ├─ Seeding/
│  │   └─ IdentitySeeder.cs                  <-- seed roles; (dev) seed a test account + assign role + profile link
│  ├─ Account/
│  │   └─ AccountController.cs               <-- existing register/login/logout; register may create+link profile (§5)
│  └─ Program.cs                             <-- AddIdentityServer(...).AddAspNetIdentity(...).AddProfileService(...)
│
├─ Talabat.Customer.API/
│  └─ Program.cs                             <-- AddAuthentication().AddJwtBearer(authority, audience=talabat.customer.api)
│                                                CORS for customer SPA origin; UseAuthentication/UseAuthorization
│
├─ Talabat.DeliveryAgent.API/
│  └─ Program.cs                             <-- same, audience=talabat.delivery.api, delivery SPA origin
│
├─ Talabat.Infrastructure/
│  └─ Identity/ApplicationUser.cs            (already exists — Phase 6)
│     Persistence/TalabatDbContext.cs        (already IdentityDbContext — Phase 6)
│
├─ Talabat.Application/                      (UNCHANGED for auth; may expose a "create customer profile" use case if you choose §5 option B)
└─ Talabat.Domain/                           (UNCHANGED — no auth types)
```

> **Single-DbContext rule (from Phase 6, still active):** keep clients/resources/scopes as **in-memory/config** (`AddInMemoryClients`, `AddInMemoryApiResources`, etc.). **Do NOT** add Duende's EF **configuration store** or **operational store** yet — those introduce additional DbContexts and break the one-context constraint. Moving config to a DB is a separate, later decision.

---

## 4. Key technique #1 — the `IProfileService` (roles + profile-link claims)

This is the generic extension point that keeps you future-proof. Duende calls it when building tokens; you decide which claims go in.

```csharp
// Talabat.Identity/IdentityServer/TalabatProfileService.cs  (ILLUSTRATIVE — verify against Duende docs)
public sealed class TalabatProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _users;

    public TalabatProfileService(UserManager<ApplicationUser> users) => _users = users;

    public async Task GetProfileDataAsync(ProfileDataRequestContext ctx)
    {
        var user = await _users.GetUserAsync(ctx.Subject);
        if (user is null) return;

        // roles -> generic, works for any future role
        foreach (var role in await _users.GetRolesAsync(user))
            ctx.IssuedClaims.Add(new Claim(JwtClaimTypes.Role, role));

        // profile-link claims -> stored as ASP.NET Identity user claims (see §5 option A)
        foreach (var c in await _users.GetClaimsAsync(user))
            if (c.Type is "customer_id" or "delivery_agent_id")
                ctx.IssuedClaims.Add(c);
    }

    public Task IsActiveAsync(IsActiveContext ctx) { ctx.IsActive = true; return Task.CompletedTask; }
}
```
Register with `.AddProfileService<TalabatProfileService>()`. Make sure the ApiResource lists these as `UserClaims` (e.g. `JwtClaimTypes.Role`, `"customer_id"`) so Duende requests them.

---

## 5. Key technique #2 — account → domain-profile linkage (a required Phase 9 decision)

Phase 6 intentionally created **account-only** registration with **no** profile and **no** link column. Phase 9 must decide the link. Two viable options — pick one and tell opencode which:

**Option A (recommended, low-friction, no new tables): store the link as an ASP.NET Identity user claim.**
- When a `Customer` domain profile is created for an account, add user claim `customer_id = <CustomerId>` and assign the `Customer` role.
- Same pattern for delivery: `delivery_agent_id` + `DeliveryAgent` role.
- Pros: no schema migration, `AspNetUserClaims` already exists, generic for future profile types, flows straight into the token via `GetClaimsAsync`.

**Option B: dedicated link columns/table.**
- Add nullable `CustomerId` / `DeliveryAgentId` FK-style columns via an Infrastructure migration.
- Pros: relational integrity. Cons: a migration on the shared `TalabatDbContext`, more coupling. Only choose if you need DB-level joins.

**Where the profile gets created:** the Identity host already references Application (via Infrastructure), so registration can call an Application use case (e.g. `CreateCustomerProfile`) to create the `Customer` aggregate, get its ID back, then write the `customer_id` claim. Keep the *domain* creation in Application/Domain; keep the *claim writing* in the Identity host. Domain still knows nothing about accounts.

> If you want to keep registration truly account-only for now, you can instead create+link the profile lazily on first authenticated call — but that pushes linkage logic into the APIs. Prefer eager linkage at registration for clarity.

---

## 6. Key technique #3 — issuing tokens (Identity host `Program.cs`)

```csharp
// Talabat.Identity/Program.cs  (ILLUSTRATIVE — order and options must match your Duende version)
builder.Services.AddInfrastructure(builder.Configuration); // brings TalabatDbContext + Identity stores

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<TalabatDbContext>();

builder.Services
    .AddIdentityServer(/* options: issuer uri, etc. */)
    .AddAspNetIdentity<ApplicationUser>()
    .AddInMemoryIdentityResources(IdentityServerConfig.IdentityResources)
    .AddInMemoryApiScopes(IdentityServerConfig.ApiScopes)
    .AddInMemoryApiResources(IdentityServerConfig.ApiResources)
    .AddInMemoryClients(IdentityServerConfig.Clients)
    .AddProfileService<TalabatProfileService>();
    // Development ONLY: .AddDeveloperSigningCredential();
    // Non-dev: real signing certificate — never fall back to dev keys outside Development.
```

- **Refresh tokens (optional):** add `offline_access` to the client's `AllowedScopes` and set `AllowOfflineAccess = true`. Use rotating refresh tokens. Note: refresh-token *tuning* is flagged as a later concern — enable only if the SPA UX needs silent renewal now.
- **Signing keys:** dev credential in Development only; a persisted certificate elsewhere. The APIs pull the public key via JWKS automatically.

---

## 7. Key technique #4 — validating tokens (each business API `Program.cs`)

```csharp
// Talabat.Customer.API/Program.cs  (ILLUSTRATIVE)
builder.Services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
      options.Authority = builder.Configuration["Auth:Authority"]; // https://localhost:<identity-port>
      options.Audience  = "talabat.customer.api";                  // must match the ApiResource name
      options.TokenValidationParameters.ValidateAudience = true;
      // options.RequireHttpsMetadata = true; // keep true except pure local dev
  });

builder.Services.AddAuthorization(); // policies themselves = Phase 10

var app = builder.Build();
app.UseCors(/* exact SPA origin, AllowCredentials, never AllowAnyOrigin with credentials */);
app.UseAuthentication();
app.UseAuthorization();
```
`Talabat.DeliveryAgent.API` is identical with `Audience = "talabat.delivery.api"` and the delivery SPA origin.

> Phase 9 goal is that a valid token is *accepted* and an invalid/wrong-audience token is *rejected*. Fine-grained `[Authorize(Roles=...)]` / policy rules and the ownership matrix are **Phase 10** — but the *inputs* those policies need (role claim + `customer_id`/`delivery_agent_id` claim) are produced here.

---

## 8. Ownership: trust the token, not the route (sets up Phase 10)

Your roadmap explicitly warns: **do not trust route IDs for ownership.** After Phase 9, every authenticated request carries `customer_id` (or `delivery_agent_id`). The rule the APIs will enforce (starting Phase 10):

> The `CustomerId` used to load a cart/order must come from the **token claim**, not from a route/query parameter. If a route id is present, it must be checked *against* the claim, never trusted on its own.

Phase 9 just guarantees the claim exists and is trustworthy (signed by Identity).

---

## 9. Implementation sequence for opencode (do in this order)

1. **Config class** `IdentityServerConfig.cs`: define `IdentityResources`, `ApiScopes` (`customer.api`, `delivery.api`), `ApiResources` (`talabat.customer.api`, `talabat.delivery.api` with `UserClaims` = role + link claims), `Clients` (`talabat-customer-spa`, `talabat-delivery-spa`; PKCE; redirect/cors placeholders from config).
2. **Wire Duende** in `Talabat.Identity/Program.cs`: add the four `AddInMemory*` calls + `AddProfileService`. Dev signing credential guarded by `IsDevelopment()`.
3. **`TalabatProfileService`**: emit role claims + `customer_id`/`delivery_agent_id` claims.
4. **Role seeding** (`IdentitySeeder`): ensure `Customer`, `DeliveryAgent` roles exist; make it a loop over a list so adding roles later is one-line.
5. **Profile linkage** (choose Option A): on account creation for a customer, create the `Customer` profile via the Application use case, then add role + `customer_id` user claim. (Delivery equivalent when a delivery account is provisioned.)
6. **Customer.API JWT bearer** + CORS.
7. **DeliveryAgent.API JWT bearer** + CORS.
8. **Smoke test** the full code+PKCE flow with a dev OIDC test client or the Angular dev app; confirm `aud`, `role`, and link claim appear in the decoded JWT and that the wrong-audience token is rejected by the other API.

---

## 10. Guardrails / pitfalls (from the docs — enforce these)

- **Never** issue a JWT from `/api/account/login`. Tokens come only from `/connect/token` via code+PKCE.
- **Never** implement Resource Owner Password Credentials or a custom password→token endpoint.
- **No client secret** in the SPA clients (public clients).
- **Single DbContext:** do **not** add Duende EF config/operational stores yet.
- **Domain purity:** no auth types leak into `Talabat.Domain` or `Talabat.Application`. `Customer`/`DeliveryAgent` never inherit Identity types.
- **CORS:** exact origins only; never `AllowAnyOrigin` together with credentials.
- **Audience isolation:** customer token must be rejected by delivery API and vice-versa — test this explicitly.
- **Signing:** dev keys in Development only; never fall back to dev keys elsewhere.
- **Scope stability:** don't rename existing scopes; only add.
- **Re-check Duende docs** for exact API names/versions right before coding — template names, defaults, and licensing change.

---

## 11. Testing checklist

- Register → account created, no token returned.
- Full code+PKCE flow → access token issued with correct `iss`, `aud`, `scope`.
- Decoded token contains `role` and `customer_id` (or `delivery_agent_id`).
- Customer.API accepts customer token; rejects delivery token (audience).
- DeliveryAgent.API accepts delivery token; rejects customer token.
- Expired/tampered token rejected.
- Domain & Application unit tests still green (no auth dependency crept in).
- Existing business regression tests still pass.
- `dotnet list package --vulnerable` clean.

---

## 12. What is explicitly deferred (do NOT let opencode build these here)

- Endpoint→policy authorization matrix and `[Authorize]` policies → **Phase 10**.
- Refresh-token hardening, external logins, password reset, email confirmation, 2FA, admin UI, production signing/secret hardening → later.
- Angular SPA implementation (this phase only registers its client config and CORS).
- RestaurantOwner domain model / restaurant-ownership rules → not in scope until decided.
- Any Delivery↔Ordering integration → out of scope.
