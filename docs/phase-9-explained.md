# Phase 9 Explained — Centralized Authentication with Duende IdentityServer

**Project:** Talabat API (DDD + Clean Architecture, .NET 10)
**Phase:** 9 — Auth, Tokens, Claims & Scopes
**Purpose of this document:** explain what was built, *why* each decision was made, and how the pieces fit — at a level you can present to a mentor and defend under questioning.

---

## Table of contents

1. [The problem Phase 9 solves](#1-the-problem-phase-9-solves)
2. [Vocabulary you must own before explaining anything](#2-vocabulary-you-must-own-before-explaining-anything)
3. [The three roles in our system](#3-the-three-roles-in-our-system)
4. [The flow, end to end](#4-the-flow-end-to-end)
5. [Why Authorization Code + PKCE (and why not the alternatives)](#5-why-authorization-code--pkce-and-why-not-the-alternatives)
6. [Anatomy of our access token](#6-anatomy-of-our-access-token)
7. [The four configuration concepts](#7-the-four-configuration-concepts)
8. [Audience isolation — the key security property](#8-audience-isolation--the-key-security-property)
9. [How custom claims get into the token: IProfileService](#9-how-custom-claims-get-into-the-token-iprofileservice)
10. [The validation side: what the business APIs do](#10-the-validation-side-what-the-business-apis-do)
11. [Why we needed a login page (Phase 9b)](#11-why-we-needed-a-login-page-phase-9b)
12. [Where every file lives](#12-where-every-file-lives)
13. [Design decisions and their trade-offs](#13-design-decisions-and-their-trade-offs)
14. [Questions your mentor will ask — with answers](#14-questions-your-mentor-will-ask--with-answers)
15. [Known gaps and what Phase 10 adds](#15-known-gaps-and-what-phase-10-adds)
16. [Glossary](#16-glossary)

---

## 1. The problem Phase 9 solves

### Before Phase 9

Phases 1–8 built the business logic: Catalog, Basket, Ordering, Customer, Delivery. Every endpoint answered the question *"what should happen?"* but nobody answered *"who is asking?"*

Concretely, the API had endpoints like `GET /api/me/orders`. Without authentication there was no way to know whose orders "me" referred to. The temporary workaround in earlier phases was to pass an ID in the route or a header — which is not merely incomplete, it is **unsafe**: any caller could pass any customer's ID and read their data.

### The requirement

We have (and will have) multiple front-ends:

- `Talabat.API` — the Customer backend (Angular customer site will call it)
- `Talabat.Delivery.API` — the Delivery-agent backend (Angular delivery app will call it)
- more later (admin, restaurant owner)

Three bad options and one good one:

| Option | Why it fails |
|---|---|
| Each API implements its own login | Passwords duplicated in N places, N places to get security wrong, no single sign-on, users re-register per app |
| Share a database of users, each API validates passwords | Every API touches password hashes; a bug in one leaks all credentials |
| One API logs in and forwards credentials to others | Credentials travel through services that shouldn't see them |
| **One central authority issues signed tokens; APIs only verify signatures** | ✅ This is Phase 9 |

### The one-sentence summary for your mentor

> "Phase 9 introduces a single authentication authority — `Talabat.Identity`, built on Duende IdentityServer — that is the only component that ever sees a password. It issues short-lived, digitally signed JWT access tokens. Each business API independently verifies those tokens offline using the authority's public key, so authentication is centralized while authorization enforcement stays distributed and stateless."

---

## 2. Vocabulary you must own before explaining anything

Mixing these terms up is the fastest way to lose credibility in a review. Learn these six.

**Authentication (AuthN)** — *Who are you?* Proving identity. Handled by `Talabat.Identity`.

**Authorization (AuthZ)** — *What are you allowed to do?* Handled by each business API. Phase 9 does AuthN properly and lays the groundwork for AuthZ; Phase 10 completes AuthZ.

**OAuth 2.0** — an *authorization* framework. It defines how a client obtains a token to access an API. It says nothing about who the user is.

**OpenID Connect (OIDC)** — a thin *identity* layer on top of OAuth 2.0. It adds the `id_token` and the `/userinfo` endpoint, so the client learns *who logged in*, not just *that it has access*. We use OIDC because we need both.

**JWT (JSON Web Token)** — a token format: three Base64URL-encoded parts (`header.payload.signature`) joined by dots. The payload is **readable by anyone** — it is signed, not encrypted. Never put secrets in a JWT.

**Claim** — one fact about the subject, as a key/value pair inside the token. `sub=42`, `role=Customer`, `customer_id=42`.

> **Say this to your mentor:** "OAuth 2.0 answers *may this app call that API*; OpenID Connect answers *and who is the human behind it*. We need both, so we use OIDC, which is OAuth 2.0 plus identity."

---

## 3. The three roles in our system

Every OAuth/OIDC discussion has exactly three actors. Name yours precisely.

```
┌──────────────────────┐
│   1. CLIENT          │   Angular Customer SPA  (localhost:4200)
│   (wants a token)    │   Angular Delivery SPA  (localhost:4300)
└──────────────────────┘   Postman (for manual testing today)
            │
            │ asks for a token
            ▼
┌──────────────────────┐
│   2. AUTHORIZATION   │   Talabat.Identity  (https://localhost:7237)
│      SERVER          │   Duende IdentityServer + ASP.NET Core Identity
│   (issues tokens)    │   ► the ONLY component that sees passwords
└──────────────────────┘   ► the ONLY component that signs tokens
            │
            │ token
            ▼
┌──────────────────────┐
│   3. RESOURCE SERVER │   Talabat.API          (Customer business API)
│   (validates tokens) │   Talabat.Delivery.API (Delivery business API)
│                      │   ► never sees a password
└──────────────────────┘   ► never calls Identity per request
```

**The most common misunderstanding — kill it early.** People assume each API call is forwarded to the Identity server for checking. It is not. The API validates the token **locally, offline**, using a public key it downloaded once. Identity is contacted only at login, token refresh, and logout.

Why this matters, in mentor-friendly terms:

- **Performance** — no extra network hop on every request.
- **Availability** — if Identity is down, already-issued tokens keep working until they expire. Existing users are not locked out.
- **Scale** — APIs are stateless; add instances freely, no shared session store.

---

## 4. The flow, end to end

### 4.1 Login (happens once)

```
   Browser (Angular / Postman)                    Talabat.Identity
   ───────────────────────────                    ────────────────
1. generate code_verifier (random)
   code_challenge = SHA256(verifier)

2. GET /connect/authorize?
        client_id=talabat-customer-spa
        &response_type=code
        &scope=openid profile roles customer.api
        &redirect_uri=...
        &code_challenge=<hash>
        &code_challenge_method=S256           ──────────►
                                                          no session cookie yet
                                              ◄────────── 302 redirect to /auth/login

3. GET /auth/login  (our Razor Page)          ──────────►
                                              ◄────────── HTML login form

4. POST email + password                      ──────────►
                                                          SignInManager verifies
                                                          the password hash,
                                                          issues an Identity COOKIE
                                              ◄────────── 302 back to /connect/authorize

5. /connect/authorize retried (now with cookie)──────────►
                                                          user is authenticated,
                                                          consent not required
                                              ◄────────── 302 to redirect_uri?code=ABC123

6. POST /connect/token
        grant_type=authorization_code
        code=ABC123
        code_verifier=<original random>       ──────────►
                                                          SHA256(verifier) == challenge?
                                                          ✅ then build the token:
                                                             • ProfileService adds claims
                                                             • sign with private key
                                              ◄────────── { access_token, id_token,
                                                            refresh_token, expires_in }
```

The awkward-looking two-step (`/authorize` returns a *code*, then `/token` exchanges it) is deliberate — see §5.

### 4.2 Every API call afterwards (no Identity involved)

```
Browser ──── GET /api/me/orders
             Authorization: Bearer eyJhbGciOi... ────►  Talabat.API

                                                        Validates locally:
                                                        1. signature vs cached JWKS  ✅
                                                        2. iss == https://localhost:7237 ✅
                                                        3. aud == talabat.customer.api  ✅
                                                        4. exp not passed              ✅
                                                        5. role / scope present        ✅
        ◄──────────────────────────────────────────     200 OK
```

**How the API gets the public key.** On its first request it fetches `https://localhost:7237/.well-known/openid-configuration`, which points to the JWKS endpoint holding the **public** key. It caches this. The **private** key never leaves the Identity host. This is asymmetric cryptography: Identity signs with the private key, anyone can verify with the public key, nobody else can forge a signature.

---

## 5. Why Authorization Code + PKCE (and why not the alternatives)

### The threat PKCE defeats

In step 5 above, the authorization code arrives back at the browser via a URL redirect. URLs leak — browser history, server logs, a malicious app registered for the same custom URI scheme on a phone. If an attacker steals the code, could they redeem it for a token?

Without PKCE and without a client secret: **yes**. And a public client (an Angular SPA whose JavaScript anyone can read) *cannot* hold a secret. That's the gap.

### How PKCE closes it

**PKCE** = Proof Key for Code Exchange (pronounced "pixy"), RFC 7636.

1. Before starting, the client invents a large random string: the **code_verifier**. It never leaves the client.
2. It sends only `SHA256(code_verifier)` — the **code_challenge** — with the authorize request. Identity stores it alongside the code.
3. When redeeming the code, the client must present the original **code_verifier**.
4. Identity hashes it and compares. Match → issue token. No match → reject.

An attacker who steals the code from a URL does not have the verifier, and cannot derive it from the challenge (SHA-256 is one-way). The stolen code is useless.

> **Analogy for your mentor:** the code is a claim ticket, the verifier is the torn half of it you kept in your pocket. Stealing the ticket doesn't help; you must produce the matching half.

### Why we rejected the alternatives

| Grant type | Why not |
|---|---|
| **Implicit flow** | Returns the token directly in the URL fragment. Tokens land in browser history and referrer headers. Deprecated by OAuth 2.1. |
| **Resource Owner Password Credentials (ROPC)** | The SPA collects the password and posts it to `/connect/token`. This defeats the entire point of centralization: the client handles raw passwords, no SSO, no MFA path, no external identity providers. Deprecated by OAuth 2.1 and **forbidden by our project constitution**. |
| **Client Credentials** | Machine-to-machine. There is no user, so no `sub`, no `role`, no `customer_id` — useless for "show me *my* orders". |

Our config enforces the right choice:

```csharp
AllowedGrantTypes  = GrantTypes.Code,   // authorization code only
RequirePkce        = true,              // PKCE mandatory
RequireClientSecret = false,            // public client — cannot keep a secret
```

---

## 6. Anatomy of our access token

A JWT has three dot-separated parts. Paste one into jwt.io to show your mentor.

### Header
```json
{ "alg": "RS256", "kid": "110C1F51...", "typ": "at+jwt" }
```
- `alg: RS256` — RSA signature with SHA-256. **Asymmetric**: private key signs, public key verifies. (Symmetric HS256 would require sharing one secret with every API — a much weaker design.)
- `kid` — key id, so the API knows which key from the JWKS to use. This is what makes key rotation possible.

### Payload (the claims) — a real customer token from our system
```json
{
  "iss": "https://localhost:7237",        // issuer — who minted this
  "aud": "talabat.customer.api",          // audience — who may accept it
  "exp": 1769345678,                      // expiry (unix seconds) — 1 hour out
  "iat": 1769342078,                      // issued at
  "client_id": "talabat-customer-spa",    // which app requested it
  "sub": "42",                            // SUBJECT — the user id. The most important claim.
  "scope": ["openid","profile","roles","customer.api","offline_access"],
  "role": "Customer",                     // ← added by our ProfileService
  "customer_id": "42"                     // ← added by our ProfileService
}
```

### Signature
`RSA-SHA256(base64(header) + "." + base64(payload), privateKey)`

Change one byte of the payload and the signature no longer verifies. This is **integrity**, not secrecy — anyone can *read* a JWT; nobody can *alter* one.

### The claims that matter to us

| Claim | Meaning | Who consumes it |
|---|---|---|
| `sub` | The user's primary key in our `Users` table | `CurrentUser` resolves the caller from this |
| `aud` | Which API this token is for | JWT bearer middleware — see §8 |
| `scope` | What the client was granted | Phase 10 policies |
| `role` | `Customer`, `DeliveryAgent`, `Admin`, `RestaurantOwner` | Phase 10 `[Authorize(Roles=...)]` |
| `customer_id` / `delivery_agent_id` | Capability marker | Ownership checks |
| `exp` | Expiry — 3600s (1 hour) | Bearer middleware |

**Why a 1-hour lifetime?** A JWT cannot be revoked once issued (nobody calls Identity to check it). So the window during which a stolen token is useful must be short. One hour is the standard compromise between security and not forcing constant re-login. Refresh tokens (`offline_access`) let the client silently obtain a new access token without prompting the user — and *those* can be revoked, because redeeming one does hit Identity.

---

## 7. The four configuration concepts

All in `src/Talabat/Talabat.Identity/IdentityServerConfig.cs`. Beginners conflate these constantly; the distinction is worth memorizing.

### 7.1 IdentityResources — information *about the user*
```csharp
new IdentityResources.OpenId(),                                  // gives 'sub'
new IdentityResources.Profile(),                                 // name, etc.
new IdentityResource("roles", "User capability roles", new[] { JwtClaimTypes.Role })
```
These shape the **id_token** (who the user is). `openid` is mandatory — requesting it is what turns a plain OAuth request into an OIDC request.

### 7.2 ApiScopes — *permissions* a client may request
```csharp
new ApiScope("customer.api", "Customer API Access"),
new ApiScope("delivery.api", "Delivery Agent API Access")
```
A scope is a verb-ish permission: "this token is allowed to do customer things." Scopes are what a user would consent to ("this app wants to read your orders").

### 7.3 ApiResources — *the APIs themselves* (audiences)
```csharp
new ApiResource("talabat.customer.api", "Talabat Customer Business API")
{
    Scopes    = { "customer.api" },
    UserClaims = { JwtClaimTypes.Role, "role", "customer_id" }
}
```
An ApiResource is a physical API. Its **name becomes the `aud` claim**. `UserClaims` declares *which extra claims this API needs* — Identity only asks the ProfileService for claims some resource actually wants. If you forget to list `customer_id` here, your ProfileService can add it and it will still be dropped from the token. **This is the #1 "my custom claim disappeared" bug.**

### 7.4 Clients — *the applications* allowed to request tokens
```csharp
new Client
{
    ClientId = "talabat-customer-spa",
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false,
    RedirectUris = { "http://localhost:4200/signin-callback" },
    PostLogoutRedirectUris = { "http://localhost:4200/signout-callback" },
    AllowedCorsOrigins = { "http://localhost:4200" },
    AllowedScopes = { "openid", "profile", "roles", "customer.api", "offline_access" },
    AllowOfflineAccess = true,
    AccessTokenLifetime = 3600
}
```

`RedirectUris` is a security control, not configuration trivia: Identity refuses to send a code to any URL not on this exact list. Without it, an attacker could start a flow with `redirect_uri=https://evil.com` and receive your users' codes.

### The relationship in one picture
```
Client  ──requests──►  Scope  ──belongs to──►  ApiResource  ──becomes──►  aud claim
"talabat-customer-spa"  "customer.api"      "talabat.customer.api"    aud=talabat.customer.api
```

---

## 8. Audience isolation — the key security property

**This is the single most important thing to demonstrate to your mentor.**

### The problem

A delivery agent is a legitimate, authenticated user with a valid token. What stops them from pointing that token at the Customer API and reading customers' orders? "The token is valid" is true but insufficient — valid *for what*?

### The mechanism

Each API declares exactly one acceptable audience:

```csharp
// Talabat.API (Customer)
options.Audience = "talabat.customer.api";
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateAudience = true,
    ValidAudience    = "talabat.customer.api",
    ...
};

// Talabat.Delivery.API
ValidAudience = "talabat.delivery.api";
```

A token minted for `talabat-delivery-spa` carries `aud: talabat.delivery.api`. When the Customer API validates it, the audience doesn't match, and it returns **401 before any controller code runs**. Not a policy, not a role check — a cryptographic-layer rejection.

```
customer token  →  Customer API  →  200 ✅
customer token  →  Delivery API  →  401 ❌  (aud mismatch)
delivery token  →  Delivery API  →  200 ✅
delivery token  →  Customer API  →  401 ❌  (aud mismatch)
```

`ValidateAudience = true` is the line that makes this real. Setting it to `false` (a common tutorial shortcut, and what our earlier phase had) would silently let any token from our issuer work on any of our APIs.

> **Demo this live.** Get both tokens, send each to both APIs, show two 200s and two 401s. It is the most convincing 30 seconds of the presentation.

---

## 9. How custom claims get into the token: IProfileService

Duende knows about ASP.NET Identity users, but it does not know your domain. `IProfileService` is the extension point where you inject application-specific claims.

`src/Talabat/Talabat.Identity/TalabatProfileService.cs`:

```csharp
public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken ct = default)
{
    var user = await _userManager.FindByIdAsync(context.Subject.GetSubjectId());
    if (user is null || !user.IsActive || user.IsDeleted) return;   // emit nothing

    var claims = new List<Claim> { new(JwtClaimTypes.Subject, user.Id.ToString()) };

    foreach (var role in await _userManager.GetRolesAsync(user))
        claims.Add(new Claim(JwtClaimTypes.Role, role));

    if (user.UserType.HasFlag(UserType.Customer))
        claims.Add(new Claim("customer_id", user.Id.ToString()));

    if (user.UserType.HasFlag(UserType.DeliveryAgent))
        claims.Add(new Claim("delivery_agent_id", user.Id.ToString()));

    context.IssuedClaims.AddRange(claims);
}
```

Two methods, two jobs:

- **`GetProfileDataAsync`** — called while *building* a token. Decides what goes in.
- **`IsActiveAsync`** — called on token issuance *and* refresh. Returning `false` blocks issuance. This is how a deactivated or soft-deleted user stops getting new tokens. Note it cannot invalidate an already-issued access token; that only expires.

### The `UserType` flags design

```csharp
[Flags]
public enum UserType { None = 0, Customer = 1, DeliveryAgent = 2, Admin = 4, RestaurantOwner = 8 }
```

A `[Flags]` enum stores multiple capabilities in one integer via bitwise OR. One person can be `Customer | DeliveryAgent = 3` — a customer who also delivers. Adding a role later is `Admin = 4`, no schema change. `UserType.None` is the initial state: an account exists but has no capability yet.

**Remember the wiring requirement from §7.3:** these claims only survive if `"customer_id"` is listed in the ApiResource's `UserClaims`.

---

## 10. The validation side: what the business APIs do

`src/Talabat/Talabat.API/Program.cs`:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = "https://localhost:7237";   // where to fetch keys/metadata
    options.Audience  = "talabat.customer.api";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidIssuer              = "https://localhost:7237",
        ValidateAudience         = true,
        ValidAudience            = "talabat.customer.api",
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        RoleClaimType            = "role",   // ← see below
        NameClaimType            = "sub"
    };
});
```

**Why `RoleClaimType = "role"` matters.** .NET's default role claim type is the long WS-Federation URI `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`. Duende emits the short OIDC-style `role`. Without this line, `User.IsInRole("Customer")` and `[Authorize(Roles = "Customer")]` silently return false even though the claim is right there in the token — a classic afternoon-losing bug.

**Middleware order is not stylistic:**
```csharp
app.UseAuthentication();   // reads the token → builds ClaimsPrincipal
app.UseAuthorization();    // checks that principal against policies
app.MapControllers();
```
Authorization before authentication means there is no identity to authorize yet — everything 401s.

### From token to domain: `ICurrentUser`

`Talabat.API/Auth/CurrentUser.cs` translates the raw `ClaimsPrincipal` into something the application layer can use:

```csharp
var subjectValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
if (!int.TryParse(subjectValue, out var parsedId) || parsedId <= 0) { /* authenticated but unusable */ }

var userType = _dbContext.Users.AsNoTracking()
    .Where(u => u.Id == parsedId).Select(u => u.UserType).FirstOrDefault();

if (userType.HasFlag(UserType.Customer)) { _hasCustomerCapability = true; _customerId = parsedId; }
```

`ICurrentUser` is an **abstraction defined in `Talabat.Application`** and implemented in the API layer. That direction matters for Clean Architecture: the Application layer says *"I need to know who's calling"* without knowing anything about HTTP, JWTs, or `ClaimsPrincipal`. Dependencies point inward.

This is the pattern that kills the "trust the route id" vulnerability. Instead of `GET /api/customers/{id}/orders` (where any id can be typed), we have `GET /api/me/orders` and the id comes from `ICurrentUser.CustomerId`, which came from a cryptographically signed token.

### `ProfileEnforcementFilter`

An account can exist with `UserType.None` — registered but no capability yet. This filter intercepts requests where the caller is authenticated but has no customer profile and returns a clear `404` with `errorCode: ProfileNotCreated`, while explicitly allowing `POST /api/me/profile` through so the profile can be created. It turns a confusing failure into an actionable one.

---

## 11. Why we needed a login page (Phase 9b)

Phase 6 gave us JSON endpoints: `POST /account/login`. Why wasn't that enough?

**Because a cookie is not a JWT.** `POST /account/login` calls `SignInManager` and sets an ASP.NET Identity **cookie** on the Identity host. The business APIs are configured with `AddJwtBearer` — they read `Authorization: Bearer <jwt>` and ignore cookies entirely. So logging in via Swagger produced a credential the business APIs would never look at.

Only `/connect/authorize` → `/connect/token` mints a JWT with the right `aud`, `scope`, `role`, and `customer_id`. And `/connect/authorize` is a **browser redirect flow** — it needs an HTML page where a human types credentials. Swagger's "Try it out" cannot drive that: it can't generate a PKCE verifier, follow redirects, extract the code, and exchange it.

Worse, our cookie configuration returned `401` on `OnRedirectToLogin`, so `/connect/authorize` hit a wall instead of showing a form.

Phase 9b adds Razor Pages (`Login`, `Logout`, `Error`) and makes the cookie middleware redirect interactive paths to the login page while keeping JSON paths returning 401:

```csharp
options.Events.OnRedirectToLogin = context =>
{
    if (context.Request.Path.StartsWithSegments("/account") ||
        context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;  // API style
        return Task.CompletedTask;
    }
    context.Response.Redirect(context.RedirectUri);                        // interactive
    return Task.CompletedTask;
};
```

The login page's `OnPostAsync` contains one security-critical detail — the **open-redirect guard**:

```csharp
var authContext = await _interaction.GetAuthorizationContextAsync(ReturnUrl, CancellationToken.None);
if (authContext is not null) return Redirect(ReturnUrl!);   // genuine /connect/authorize request
if (Url.IsLocalUrl(ReturnUrl))  return Redirect(ReturnUrl!); // safe local path
return Redirect("~/");                                       // fallback
```

Blindly redirecting to a user-supplied `ReturnUrl` would let an attacker send `/auth/login?ReturnUrl=https://evil.com` — the victim logs in on your real domain, then gets bounced to a phishing clone. `GetAuthorizationContextAsync` returns non-null only for a legitimate authorize request Identity itself created; `IsLocalUrl` blocks absolute external URLs.

This page is **temporary scaffolding**. When the Angular apps arrive, they replace it — and nothing else changes, because the OIDC contract is unchanged.

---

## 12. Where every file lives

```
src/Talabat/
│
├── Talabat.Identity/                    ◄── AUTHORIZATION SERVER
│   ├── IdentityServerConfig.cs             clients, scopes, api resources, identity resources
│   ├── TalabatProfileService.cs            injects role + customer_id/delivery_agent_id claims
│   ├── Controllers/AccountController.cs    JSON register / login / logout / me
│   ├── Pages/Account/Login.cshtml[.cs]     interactive login (Phase 9b)
│   ├── Pages/Account/Logout.cshtml[.cs]    interactive logout
│   ├── Pages/Account/Error.cshtml[.cs]     protocol error display
│   └── Program.cs                          AddIdentityServer + cookie events + pipeline
│
├── Talabat.API/                         ◄── RESOURCE SERVER (customer)
│   ├── Program.cs                          AddJwtBearer(aud = talabat.customer.api)
│   ├── Auth/CurrentUser.cs                 ClaimsPrincipal → ICurrentUser
│   └── Middleware/ProfileEnforcementFilter.cs
│
├── Talabat.Delivery.API/                ◄── RESOURCE SERVER (delivery)
│   ├── Program.cs                          AddJwtBearer(aud = talabat.delivery.api)
│   └── Auth/CurrentUser.cs
│
├── Talabat.Infrastructure/
│   └── Identity/
│       ├── UserCapabilityService.cs        registration: create user + capability + role, transactional
│       ├── IdentityDataSeeder.cs           seeds Customer, DeliveryAgent, Admin, RestaurantOwner roles
│       ├── IdentityRoleNames.cs            role name constants
│       └── TalabatSignInManager.cs
│
├── Talabat.Application/
│   └── Abstractions/ICurrentUser.cs        ◄── abstraction only; no HTTP, no JWT
│
└── Talabat.Domain/
    └── Aggregates/Users/User.cs            the user aggregate
```

**Dependency direction:** `Identity → Infrastructure → Application → Domain`. Never reversed.

---

## 13. Design decisions and their trade-offs

Be ready to defend these. A mentor respects "here's the trade-off I accepted" far more than "that's how the tutorial did it."

### Decision 1 — Centralized authority instead of per-API auth
**Chose:** one Identity host issuing tokens.
**Gained:** one place handling passwords; SSO across apps; adding an admin API later requires zero new auth code.
**Cost:** a new deployable service; Identity is a single point of failure *for login* (not for already-authenticated traffic).

### Decision 2 — JWT (stateless) instead of server-side sessions
**Gained:** no shared session store; APIs scale horizontally; no per-request Identity call.
**Cost:** **tokens cannot be revoked before expiry.** This is the real trade-off. Mitigations: short 1-hour lifetime; refresh tokens can be revoked; `IsActiveAsync` blocks new issuance for deactivated users.

### Decision 3 — One audience per API
**Gained:** hard isolation between customer and delivery surfaces, enforced at the middleware layer.
**Cost:** a client needing both APIs must request two tokens. Acceptable — our SPAs are separate apps.

### Decision 4 — In-memory client/scope configuration
**Chose:** `AddInMemoryClients` etc. rather than Duende's EF configuration store.
**Why:** the project has a hard single-`DbContext` constraint. Duende's config/operational stores add extra DbContexts.
**Cost:** changing a client requires a redeploy. Fine for now; revisit when clients are managed dynamically.

### Decision 5 — Merging account and profile into one `User` aggregate
**Chose:** `User : IdentityUser<int>`, with `[Flags] UserType` for capabilities.
**Gained:** registration is one atomic write; no account↔profile synchronization problem; one person can be customer *and* delivery agent naturally.
**Cost — state this openly:** `Talabat.Domain` now references `Microsoft.Extensions.Identity.Stores`, so the domain layer is coupled to a framework, which departs from the strict Clean Architecture rule the earlier phases followed. The aggregate also carries persistence/framework concerns (`PasswordHash`, `SecurityStamp`, `LockoutEnd`) alongside domain behavior, and it spans what were previously two bounded contexts (customer addresses + delivery-agent state machine), guarded at runtime by `RequireCustomer()` / `RequireAgent()`.
**This was a deliberate, documented choice** — pragmatism over purity — not an oversight. The alternative (separate `Customer`/`DeliveryAgent` aggregates linked by a scalar `IdentityUserId`) keeps the domain pure at the cost of cross-store coordination on registration. Say which you chose and why; a mentor will likely probe this hardest.

### Decision 6 — `CurrentUser` reads `sub` and then queries the DB for `UserType`
Note the token carries `customer_id`, but `CurrentUser` derives capability from a database lookup instead. **Trade-off:** the DB is always fresh (a capability revoked one minute ago takes effect immediately, whereas the claim would be stale until the token expires), at the cost of one small indexed query per request. The claim remains useful for clients and for future policy checks. Know this distinction — it's exactly the kind of detail a sharp mentor notices.

---

## 14. Questions your mentor will ask — with answers

**Q: Does every API request go to the Identity server?**
No. Only login, refresh, and logout. Normal requests are validated offline against a cached public key from the JWKS endpoint. That's the main benefit of JWTs over server-side sessions.

**Q: If the token is just Base64, can't a user edit their role to Admin?**
They can edit it, but the signature then fails verification and the API returns 401. The payload is readable but tamper-evident. That's also why we never put secrets in a JWT — Base64 is encoding, not encryption.

**Q: What if a token is stolen?**
It's valid until it expires (max 1 hour). We cannot revoke it — that's the accepted cost of stateless auth. Mitigations: HTTPS everywhere, short lifetime, revocable refresh tokens, and `IsActiveAsync` blocking new issuance for disabled accounts. If we needed instant revocation we'd add reference tokens or a revocation list, at the cost of a per-request lookup.

**Q: Why not just have the SPA post the password to `/connect/token` (ROPC)? It's simpler.**
It puts raw passwords back in the client, which defeats centralization; it can't support MFA or external providers; and it's deprecated in OAuth 2.1. Our constitution forbids it.

**Q: Why does a SPA have no client secret?**
Anyone can read a SPA's JavaScript, so a "secret" there isn't secret. That's precisely why it's a *public client* and why PKCE is mandatory — PKCE replaces the secret with a per-request proof.

**Q: What's the difference between scope and role?**
Scope is what the *application* was authorized to do (`customer.api`); role is what the *user* is (`Customer`). Both end up in the token, and Phase 10 policies will require both — a delivery agent using a customer-scoped client should still fail a customer-only endpoint.

**Q: What stops a customer from reading another customer's orders?**
The id never comes from the URL. Endpoints are `/api/me/...`, and the identity is resolved by `ICurrentUser` from the signed token's `sub`. Phase 10 formalizes this into an ownership policy.

**Q: Why RS256 instead of HS256?**
HS256 is symmetric — every API would need the same shared secret, so any compromised API could forge tokens for all the others. RS256 is asymmetric: only Identity holds the private signing key; APIs only ever get the public key.

**Q: Where's the register page?**
Registration is JSON-only for now: `POST /account/register/customer` and `POST /account/register/delivery-agent`, exercised through Swagger. The interactive UI intentionally covers only login/logout, because only those are required for the OIDC redirect flow to complete. The Angular apps will call the register endpoints directly.

---

## 15. Known gaps and what Phase 10 adds

Be upfront about these — knowing your own gaps reads as competence.

### Open items in Phase 9
- `ReturnUrl` on the login page is not `[BindProperty]`, so it isn't bound on POST and the authorize request isn't resumed after login. Must fix.
- The Razor page path `/Account/Login` collides case-insensitively with `AccountController`'s `POST /account/login`; the pages need moving to `/auth/*`.
- `Talabat.Delivery.API` is missing its `TalabatDb` connection string and won't start.
- Development signing keys were committed to git; they need gitignoring and rotation.
- Logout signs out on GET without confirmation.

### Deliberately deferred (not defects)
- Endpoint→policy authorization matrix — **Phase 10**.
- Refresh-token rotation hardening, external login providers, password reset, email confirmation, MFA.
- Production signing certificate (currently `AddDeveloperSigningCredential`).
- Angular front-ends.

### What Phase 10 does
Phase 9 proved *who you are* and produced trustworthy claims. Phase 10 uses them: define named policies (e.g. `CustomerOnly` = role `Customer` **and** scope `customer.api`), apply them per endpoint, enforce ownership through `ICurrentUser` rather than route parameters, and add integration tests plus CI gates for the whole matrix.

> **The clean handoff sentence:** "Phase 9 answers *who is this?* and guarantees the answer is trustworthy. Phase 10 answers *what may they do?*"

---

## 16. Glossary

| Term | Meaning |
|---|---|
| **Access token** | Short-lived credential presented to an API in the `Authorization` header |
| **ID token** | OIDC token describing *who logged in*; consumed by the client, not the API |
| **Refresh token** | Long-lived, revocable token used to obtain a new access token without re-login |
| **Audience (`aud`)** | Which API a token is intended for |
| **Issuer (`iss`)** | Which authority minted the token |
| **Subject (`sub`)** | The user's unique id |
| **Scope** | A permission the *client* requested |
| **Claim** | A key/value fact inside a token |
| **JWKS** | JSON Web Key Set — the public keys, published at a well-known URL |
| **PKCE** | Proof Key for Code Exchange; protects the code from interception |
| **Public client** | An app that cannot keep a secret (SPA, mobile) |
| **Confidential client** | A server-side app that can keep a secret |
| **Discovery document** | `/.well-known/openid-configuration` — metadata describing all endpoints |
| **Bearer token** | "Whoever bears it may use it" — hence HTTPS and short lifetimes |
| **Resource server** | An API that consumes and validates tokens |
| **Authorization server** | The service that authenticates users and issues tokens |
