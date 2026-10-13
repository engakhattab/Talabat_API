# Phase 9b — Duende Interactive Login/Logout UI (dev testing)

> **For opencode.** Implementation plan, not full source. Add a minimal server-rendered login/logout UI to the `Talabat.Identity` host so the OpenID Connect **Authorization Code + PKCE** flow can complete in a browser. This is the "approved temporary interactive UI" that unblocks token acquisition until the Angular SPA replaces it. Follow the plan exactly; the gotchas listed are the parts that break if done casually.

## Fixed context (already true in the repo — do not change)

- Host: `src/Talabat/Talabat.Identity`, `Microsoft.NET.Sdk.Web`, `net10.0`.
- Packages: `Duende.IdentityServer` 8.0.2, `Duende.IdentityServer.AspNetIdentity` 8.0.2. No new packages needed.
- Identity: `AddIdentity<User, IdentityRole<int>>()` is registered; `SignInManager<User>` / `UserManager<User>` are available (`User : IdentityUser<int>` — the merged aggregate stays as-is, Option 1).
- Clients (`IdentityServerConfig.cs`): `talabat-customer-spa` and `talabat-delivery-spa`, `GrantTypes.Code`, `RequirePkce = true`, `RequireClientSecret = false`, `RequireConsent` unset (defaults to `false`, so **no consent page is needed**).
- Host URL: `https://localhost:7237`.
- The current cookie config returns **401** on `OnRedirectToLogin`, which is exactly what prevents `/connect/authorize` from showing a login page. Fixing that is part of this task.

**Out of scope / do not touch:** `Talabat.Domain`, `Talabat.Application`, the User aggregate, EF migrations, Duende in-memory config (do NOT add Duende EF config/operational stores), the two business APIs. (Separately outstanding but NOT part of this task: gitignoring/rotating the committed signing keys, and adding the Delivery API connection string.)

---

## What to build

Add ASP.NET Core **Razor Pages** to the Identity host with two pages under `/Account`: **Login** and **Logout** (plus a tiny **Error** page). Wire the cookie + IdentityServer interaction so an unauthenticated `/connect/authorize` redirects to `/Account/Login`, authenticates via `SignInManager`, and resumes the authorize request.

### Files to create

```
src/Talabat/Talabat.Identity/Pages/
├─ _ViewImports.cshtml
├─ _ViewStart.cshtml
├─ Shared/_Layout.cshtml
└─ Account/
   ├─ Login.cshtml
   ├─ Login.cshtml.cs
   ├─ Logout.cshtml
   ├─ Logout.cshtml.cs
   ├─ Error.cshtml
   └─ Error.cshtml.cs
```

Keep the markup plain HTML with minimal inline CSS. No Bootstrap, no external CDN, no JS frameworks. This is a throwaway dev UI.

---

## Program.cs changes (4 edits)

### 1. Register Razor Pages
Add near the other service registrations:
```csharp
builder.Services.AddRazorPages();
```

### 2. Fix the cookie redirect — THE critical edit
Replace the current `ConfigureApplicationCookie` block. The JSON `/account/*` and `/api/*` endpoints must keep returning 401; only interactive endpoints (e.g. `/connect/authorize`) should redirect to the login page:
```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Error";

    options.Events.OnRedirectToLogin = context =>
    {
        // API-style paths: return 401 instead of an HTML redirect.
        if (context.Request.Path.StartsWithSegments("/account") ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        // Interactive paths (/connect/authorize, etc.): redirect to the login page.
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/account") ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
```
> Why: IdentityServer's `/connect/authorize` triggers a cookie challenge when there's no session. The current unconditional 401 kills the redirect to a login form. Path-scoping preserves the existing 401 behavior for the JSON account/api endpoints (and their Phase 6 tests) while enabling the interactive flow.

### 3. Set IdentityServer interaction URLs (explicit, matches the pages)
On the existing `AddIdentityServer(options => ...)` call, extend the options:
```csharp
builder.Services.AddIdentityServer(options =>
{
    options.EmitStaticAudienceClaim = true;
    options.UserInteraction.LoginUrl = "/Account/Login";
    options.UserInteraction.LogoutUrl = "/Account/Logout";
    options.UserInteraction.ErrorUrl = "/Account/Error";
})
// ... keep the existing .AddInMemory*/.AddAspNetIdentity/.AddProfileService/.AddDeveloperSigningCredential chain unchanged
```

### 4. Pipeline — add Razor Pages mapping (order matters)
The middleware order must be:
```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();          // add this (harmless even with no wwwroot)
app.UseRouting();
app.UseCors("SpaCorsPolicy");
app.UseIdentityServer();       // calls UseAuthentication internally — must stay before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();           // add this
```

---

## Login page logic (`Login.cshtml.cs`) — the important correctness parts

Decorate the PageModel `[AllowAnonymous]`. Inject `SignInManager<User>` and `Duende.IdentityServer.Services.IIdentityServerInteractionService`.

- `[BindProperty] public InputModel Input { get; set; }` with `Email`, `Password`, `RememberLogin`.
- `public string? ReturnUrl { get; set; }`
- `OnGet(string? returnUrl)`: set `ReturnUrl = returnUrl`.
- `OnPostAsync()`:
  1. `if (!ModelState.IsValid) return Page();`
  2. `var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberLogin, lockoutOnFailure: true);`
  3. On `result.Succeeded`, resolve where to go **safely** (open-redirect protection):
     ```csharp
     var authContext = await _interaction.GetAuthorizationContextAsync(ReturnUrl);
     if (authContext is not null)         // ReturnUrl is a genuine /connect/authorize request
         return Redirect(ReturnUrl!);
     if (Url.IsLocalUrl(ReturnUrl))       // safe local path
         return Redirect(ReturnUrl!);
     return Redirect("~/");               // fallback — never redirect to an untrusted absolute URL
     ```
  4. `if (result.IsLockedOut)` → add a model error "Account locked out." and `return Page();`
  5. otherwise → `ModelState.AddModelError(string.Empty, "Invalid email or password.");` and `return Page();`

> Security: never `Redirect(ReturnUrl)` without the `GetAuthorizationContextAsync` / `IsLocalUrl` guard — that's an open-redirect. `lockoutOnFailure: true` uses the existing Identity lockout config.

`Login.cshtml`: a plain `<form method="post">` with email, password, a "remember me" checkbox, a hidden `ReturnUrl`, an antiforgery token (Razor Pages emits it automatically inside a form tag helper — do not disable it), and a validation summary.

---

## Logout page logic (`Logout.cshtml.cs`)

Inject `SignInManager<User>` and `IIdentityServerInteractionService`.
- `OnGet(string? logoutId)` / `OnPostAsync(string? logoutId)`:
  1. `await _signInManager.SignOutAsync();`
  2. `var ctx = await _interaction.GetLogoutContextAsync(logoutId);`
  3. `if (!string.IsNullOrEmpty(ctx?.PostLogoutRedirectUri)) return Redirect(ctx.PostLogoutRedirectUri);`
  4. otherwise render a simple "You are signed out." page.

`Logout.cshtml`: minimal confirmation markup.

---

## Error page

`Error.cshtml.cs`: inject `IIdentityServerInteractionService`, `OnGet(string? errorId)` → `await _interaction.GetErrorContextAsync(errorId)`, expose `Error`/`ErrorDescription` to the view. `Error.cshtml`: render them. This is what IdentityServer shows when something in the protocol fails — very useful while testing.

---

## `_ViewImports.cshtml` / `_ViewStart.cshtml` / `_Layout.cshtml`

- `_ViewImports.cshtml`:
  ```cshtml
  @namespace Talabat.Identity.Pages
  @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
  ```
- `_ViewStart.cshtml`: `@{ Layout = "_Layout"; }`
- `_Layout.cshtml`: minimal `<html>` with `@RenderBody()`, a `<title>`, and inline CSS for a centered card. Include `@RenderSection("Scripts", required: false)`.

---

## Acceptance / manual verification

1. Solution builds; the Identity host still starts and `/.well-known/openid-configuration` returns 200.
2. `GET https://localhost:7237/Account/Login` renders the form.
3. Existing behavior preserved: an unauthenticated `GET /account/me` still returns **401** (not a redirect); the JSON `/account/register/*` and `/account/login` endpoints are unchanged.
4. Full flow (use a PKCE-capable client — see below): hitting `/connect/authorize` unauthenticated now shows `/Account/Login`; submitting valid seeded credentials resumes the authorize request and returns a `code`.
5. Bad credentials show an inline error and do not redirect.
6. Logout signs out and honors a client `PostLogoutRedirectUri`.

---

## How you'll actually get a token to test the business APIs

The login page enables the browser step; you still need a PKCE client to complete `code → token`. Two easy options (pick one — no more code needed):

- **Postman / Insomnia** → new OAuth 2.0 "Authorization Code (With PKCE)" request:
  - Auth URL `https://localhost:7237/connect/authorize`, Token URL `https://localhost:7237/connect/token`
  - Client ID `talabat-customer-spa`, no secret, scopes `openid profile roles customer.api offline_access`
  - Redirect URL: use Postman's callback (`https://oauth.pstmn.io/v1/callback`) and **add that exact URL to the `talabat-customer-spa` `RedirectUris` in `IdentityServerConfig.cs`** (dev only). Click "Get New Access Token" → the new login page appears → sign in → Postman receives the token.
  - Paste the token as `Authorization: Bearer <jwt>` into the business API's Swagger, or call the API directly.
- For the delivery API, repeat with `talabat-delivery-spa` / `delivery.api`.

> Note: the SPA redirect URIs `http://localhost:4200/...` won't resolve until Angular is running, which is why the manual test uses the client's own callback URL above. Later, Angular reuses the same login page unchanged.

**Optional follow-up (not required now):** wire the two business APIs' Swagger UIs as OAuth2 PKCE clients so you can click "Authorize" directly in their Swagger and log in via this page — cleaner, but more setup. Ask for that as a separate task if you want it.
