# Phase 9b — Required Fixes (post-review of commit 80d06c6)

The login/logout UI builds but cannot complete the OIDC flow at runtime. Apply these fixes in order. Do not touch `Talabat.Domain`, `Talabat.Application`, migrations, or the two business APIs' logic.

---

## FIX 1 (blocker) — bind `ReturnUrl` on POST

File: `src/Talabat/Talabat.Identity/Pages/Account/Login.cshtml.cs`

`ReturnUrl` is a plain property with no `[BindProperty]`, and `OnPostAsync()` takes no parameter, so the hidden form field is never bound. On POST it is `null`, the authorization context lookup fails, and login redirects to `~/` instead of resuming `/connect/authorize`. No token is ever issued.

Change:
```csharp
[BindProperty(SupportsGet = true)]
public string? ReturnUrl { get; set; }

public void OnGet() { }   // drop the string? returnUrl parameter
```
Keep the existing `GetAuthorizationContextAsync` / `Url.IsLocalUrl` open-redirect guard exactly as written. Keep the hidden `<input type="hidden" name="ReturnUrl" ... />` in `Login.cshtml`.

**Verify:** after login from a `/connect/authorize` redirect, the browser returns to the client callback with a `code` query parameter.

---

## FIX 2 (blocker) — resolve the route collision with `AccountController`

`AccountController` is `[Route("account")]` with `[HttpPost("login")]` and `[HttpPost("logout")]`. The Razor pages are at `/Account/Login` and `/Account/Logout`. Routing is case-insensitive and Razor Page endpoints have no HTTP-method constraint, so `POST /account/login` matches both endpoints at equal precedence → `AmbiguousMatchException` (500). This breaks the existing JSON login endpoint *and* the new form submit.

Move the pages to a `/auth` prefix (non-breaking for the JSON API):

1. Add explicit page routes:
   - `Login.cshtml`  → first line `@page "/auth/login"`
   - `Logout.cshtml` → first line `@page "/auth/logout"`
   - `Error.cshtml`  → first line `@page "/auth/error"`
2. `Program.cs` — `ConfigureApplicationCookie`:
   ```csharp
   options.LoginPath = "/auth/login";
   options.LogoutPath = "/auth/logout";
   options.AccessDeniedPath = "/auth/error";
   ```
3. `Program.cs` — `AddIdentityServer` options:
   ```csharp
   options.UserInteraction.LoginUrl  = "/auth/login";
   options.UserInteraction.LogoutUrl = "/auth/logout";
   options.UserInteraction.ErrorUrl  = "/auth/error";
   ```
4. Leave the `StartsWithSegments("/account") || StartsWithSegments("/api")` 401/403 branch unchanged — with the pages on `/auth`, interactive redirects now work correctly and the JSON endpoints keep returning 401/403.

**Verify:** `GET /auth/login` renders the form; `POST /account/login` (JSON, Swagger) still returns its normal result and does **not** 500.

---

## FIX 3 (blocker) — Delivery API connection string

File: `src/Talabat/Talabat.Delivery.API/appsettings.Development.json`

`AddInfrastructure(configuration)` throws when `ConnectionStrings:TalabatDb` is missing, so the host does not start. Copy the exact same connection string already used in `src/Talabat/Talabat.API/appsettings.Development.json` — same `Talabat` database, same single `TalabatDbContext`. Do not create a second database and do not add migrations from this project.

Also add the Identity authority if not present:
```json
"Identity": { "Authority": "https://localhost:7237" }
```

**Verify:** `dotnet run --project src/Talabat/Talabat.Delivery.API` starts and serves Swagger.

---

## FIX 4 (security) — stop tracking signing keys

1. Add to `.gitignore`:
   ```
   **/keys/
   *.jwk
   ```
2. `git rm --cached src/Talabat/Talabat.Identity/keys/is-signing-key-*.json src/Talabat/Talabat.Identity/tempkey.jwk`
3. Delete the local files so Duende regenerates a fresh dev key on next start.

These are private token-signing keys; anything signed with the committed key must be considered untrusted.

---

## FIX 5 — add the Postman callback to both clients (dev only)

File: `src/Talabat/Talabat.Identity/IdentityServerConfig.cs`

The SPA redirect URIs (`http://localhost:4200/...`, `:4300`) do not resolve until Angular exists, so manual token testing needs a working callback. Add to **both** `talabat-customer-spa` and `talabat-delivery-spa`:
```csharp
RedirectUris = { "http://localhost:4200/signin-callback", "https://oauth.pstmn.io/v1/callback" },
```
Add a `// dev only — remove before production` comment.

---

## FIX 6 (minor) — logout hardening

File: `Pages/Account/Logout.cshtml[.cs]`

Currently `OnGetAsync` signs the user out immediately, so any GET (including an `<img>` tag) can terminate the session, and the page has no form so `OnPostAsync` is unreachable.

- On GET: call `GetLogoutContextAsync(logoutId)`. If the context is not verified (`ShowSignoutPrompt` is true / context is null), do **not** sign out — render a confirmation `<form method="post">` with a hidden `logoutId` and a "Sign out" button.
- Move the actual `SignOutAsync` + `PostLogoutRedirectUri` handling into `OnPostAsync` only.
- Add `[BindProperty(SupportsGet = true)] public string? LogoutId { get; set; }` so the id survives the round trip.

---

## FIX 7 (housekeeping)

Move `phase-9b-duende-dev-login-ui-plan.md` from the repo root into `docs/`.

---

## Acceptance — run these before declaring Phase 9 done

1. All three hosts start: Identity (7237), Customer API, Delivery API.
2. `GET https://localhost:7237/.well-known/openid-configuration` → 200.
3. `POST /account/register/customer` via Identity Swagger → 200, account created.
4. `POST /account/login` via Swagger → normal result, **no 500**.
5. `GET /account/me` unauthenticated → **401** (not an HTML redirect).
6. Postman PKCE flow (client `talabat-customer-spa`, scopes `openid profile roles customer.api offline_access`) → login page appears → sign in → **access token received**.
7. Decode the token: `aud = talabat.customer.api`, `scope` contains `customer.api`, `role` contains `Customer`, `customer_id` present.
8. Call a protected Customer API endpoint with that token → success.
9. **Audience isolation:** send the same customer token to the Delivery API → **401**. Repeat inverted with a `talabat-delivery-spa` token.
10. Logout → session cleared, re-running the authorize flow prompts for login again.
