# Phase 6 — Minimal Identity/Auth: Technical Implementation Plan

Audience: an implementer model (e.g. opencode). Execute tasks **in order**. Each task lists exact
files, exact intent, and a verification step. Do not improvise beyond the task text. If a task's
verification fails, stop and report — do not proceed.

Authority: `.specify/memory/constitution.md` → "Current Phase Scope: Phase 6". This plan must not
exceed that scope. When in doubt, the constitution wins over this plan.

---

## 0. Current state (already done — DO NOT redo)

Verified in the repo at commit `72458dc`:

- `TalabatDbContext` is already `IdentityDbContext<ApplicationUser, IdentityRole, string>` and already
  calls `base.OnModelCreating(modelBuilder)` first. File:
  `src/Talabat/Talabat.Infrastructure/Persistence/TalabatDbContext.cs`. **Leave as-is.**
- `ApplicationUser : IdentityUser` exists: `src/Talabat/Talabat.Infrastructure/Identity/ApplicationUser.cs`.
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.9 is already in
  `src/Talabat/Talabat.Infrastructure/Talabat.Infrastructure.csproj`.
- `Talabat.Identity` project exists and is in `src/Talabat/Talabat.slnx`, references Infrastructure.

**Still missing (this is the Phase 6 work):** Duende packages, Identity + Duende DI wiring, a
connection string in the Identity host, register/login/logout endpoints, the Identity EF migration,
tests, and template cleanup. The commit title says "Duende identityserver" but Duende is **not**
actually referenced yet — treat it as not done.

## Target end state (one paragraph)

The `Talabat.Identity` host registers ASP.NET Core Identity (**cookie sign-in — this is the working
auth mechanism for Phase 6**) backed by the existing `TalabatDbContext`. It exposes
`POST /account/register`, `POST /account/login`, `POST /account/logout`, and a protected
`GET /account/me` (JSON, cookie session, **no JWT**). Register/login/logout and `[Authorize]` work
**without Duende** — Duende is added only in minimal in-memory dev configuration so the discovery
document at `/.well-known/openid-configuration` responds; token/OIDC flows and JWT are a **later**
phase. One reviewed migration adds the standard `AspNet*` tables to the existing `Talabat` database
without touching business tables. Focused tests cover register/login/logout, the protected endpoint,
and confirm no secret leaks. `Talabat.Domain` and `Talabat.Application` remain free of any Identity
package or type.

## How this auth is used — now vs later (read this first)

- **Now (Phase 6):** ASP.NET Core Identity issues an authentication **cookie** on login. Any endpoint
  marked `[Authorize]` is protected by that cookie. `GET /account/me` proves it end-to-end
  (401 when not logged in → 200 with the user's email after login). This is the "auth the controllers"
  capability you asked for.
- **What it protects today:** controllers **in a host that shares this Identity cookie** (same host, or
  same site/data-protection key ring). That is enough to learn and test `[Authorize]` now.
- **Later (separate phase, deferred):** when the business APIs are separate hosts
  (`Talabat.Customer.API`, `Talabat.DeliveryAgent.API`), cross-host protection uses **JWT bearer**
  tokens issued by Duende. That is exactly the "return to the Identity project and add JWT + advanced
  things" step — out of scope here per the constitution (no JWT in Phase 6). The cookie work now is
  not throwaway: register/login/logout, the user store, and Duende hosting all carry forward; only the
  token-issuing + bearer-validation layer is added later.

## Hard guardrails (from the constitution — violating any = stop)

- No Identity/Duende/JWT/`ClaimsPrincipal`/`ApplicationUser`/`IdentityUser` types in `Talabat.Domain`
  or `Talabat.Application`. Do not add packages to those two projects.
- Do **not** make `Customer` or `DeliveryAgent` inherit from an Identity type. Do **not** add any
  account↔profile link column or `IdentityUserId` anywhere. Registration creates an **account only**.
- **No JWT.** Login uses the Identity cookie only. No custom token endpoint, no ROPC, no hand-built JWT.
- Single `DbContext` only (`TalabatDbContext`). Do not add a second `DbContext`. Do not use Duende
  `AddConfigurationStore`/`AddOperationalStore` — use in-memory + `AddDeveloperSigningCredential`.
- Do not create `Talabat.Customer.API` or `Talabat.DeliveryAgent.API`, and do not add business
  endpoints. No Angular/frontend.
- Defer (do NOT implement): refresh-token tuning, external login, password reset, email confirmation,
  2FA, admin UI, custom grants, production signing/secrets.
- Never return or log a password, `PasswordHash`, `SecurityStamp`, or a full Identity entity.

## Dependency direction (must stay this way)

`Talabat.Identity → Talabat.Infrastructure → Talabat.Application → Talabat.Domain`. Never reverse.

---

## Packages to add

| Project | Package | Version |
|---|---|---|
| `Talabat.Identity` | `Duende.IdentityServer` | latest stable that restores on net10.0 (see Task 1) |
| `Talabat.Identity` | `Duende.IdentityServer.AspNetIdentity` | same version as `Duende.IdentityServer` |

Everything else needed (ASP.NET Core Identity, EF stores) already flows transitively from
Infrastructure. Do **not** add EF or Identity packages to Domain/Application.

---

## Tasks

### T1 — Add Duende packages to the Identity host
- File: `src/Talabat/Talabat.Identity/Talabat.Identity.csproj`.
- Run: `dotnet add src/Talabat/Talabat.Identity package Duende.IdentityServer` then
  `dotnet add src/Talabat/Talabat.Identity package Duende.IdentityServer.AspNetIdentity`.
- If restore fails on net10.0, pin the newest version that restores (mirror how the team handled the
  earlier IdentityServer spike) and **record the resolved version in this file under Task 1**.
- Verify: `dotnet restore src/Talabat/Talabat.slnx` succeeds; both packages appear in the csproj.
- Note: Duende Community Edition is free under the revenue threshold — fine for this project.

### T2 — Add the connection string to the Identity host
- File: `src/Talabat/Talabat.Identity/appsettings.Development.json`.
- Add the **same** connection string the API host uses (same physical DB):
  `"ConnectionStrings": { "TalabatDb": "Server=(localdb)\\MSSQLLocalDB;Database=Talabat;Trusted_Connection=True;TrustServerCertificate=True" }`.
- Verify: file is valid JSON and the `TalabatDb` key matches
  `src/Talabat/Talabat.API/appsettings.Development.json` exactly.

### T3 — Clean the template out of the Identity host
- Delete `src/Talabat/Talabat.Identity/WeatherForecast.cs` and
  `src/Talabat/Talabat.Identity/Controllers/WeatherForecastController.cs`.
- Verify: `dotnet build` still succeeds after T4 (they are unused).

### T4 — Wire Identity + Duende in the host composition root
- File: `src/Talabat/Talabat.Identity/Program.cs`. Keep it a thin composition root only.
- Registrations, in this order:
  1. `builder.Services.AddInfrastructure(builder.Configuration);` (registers `TalabatDbContext`).
  2. `builder.Services.AddIdentity<ApplicationUser, IdentityRole>()` `.AddEntityFrameworkStores<TalabatDbContext>()` `.AddDefaultTokenProviders();`
     (usings: `Microsoft.AspNetCore.Identity`, `Talabat.Infrastructure`, `Talabat.Infrastructure.Identity`, `Talabat.Infrastructure.Persistence`).
  3. **API cookie behavior (required — makes `[Authorize]` return JSON 401/403 instead of redirecting
     to a non-existent login page):**
     ```
     builder.Services.ConfigureApplicationCookie(options =>
     {
         options.Events.OnRedirectToLogin = context =>
         {
             context.Response.StatusCode = StatusCodes.Status401Unauthorized;
             return Task.CompletedTask;
         };
         options.Events.OnRedirectToAccessDenied = context =>
         {
             context.Response.StatusCode = StatusCodes.Status403Forbidden;
             return Task.CompletedTask;
         };
     });
     ```
     Without this, a protected endpoint returns a 302 redirect to `/Account/Login` — wrong for an API.
  4. `builder.Services.AddControllers();`
  5. `builder.Services.AddIdentityServer(options => options.EmitStaticAudienceClaim = true)` `.AddInMemoryIdentityResources(IdentityConfig.IdentityResources)` `.AddInMemoryApiScopes(IdentityConfig.ApiScopes)` `.AddInMemoryClients(IdentityConfig.Clients)` `.AddAspNetIdentity<ApplicationUser>()` `.AddDeveloperSigningCredential();`
- Pipeline order (middleware):
  `app.UseRouting();` → `app.UseIdentityServer();` → `app.UseAuthorization();` → `app.MapControllers();`
  (`UseIdentityServer` already calls `UseAuthentication`; do not add a second `UseAuthentication`. The
  default authenticate/challenge scheme is the Identity application cookie set up by `AddIdentity`, so
  `[Authorize]` uses the cookie — no Duende involvement in protecting endpoints.)
- Keep `AddOpenApi()`/`MapOpenApi()` in Development if already present.
- Verify: `dotnet build src/Talabat/Talabat.Identity` succeeds.

### T5 — Add the minimal Duende dev configuration
- New file: `src/Talabat/Talabat.Identity/IdentityConfig.cs`, `internal static class IdentityConfig`.
- Contents:
  - `IdentityResources` → `new IdentityResource[] { new IdentityResources.OpenId(), new IdentityResources.Profile() }`.
  - `ApiScopes` → `new ApiScope[] { new ApiScope("talabat.customer-api"), new ApiScope("talabat.deliveryagent-api") }` (reserved names; no clients bound yet).
  - `Clients` → `Array.Empty<Client>()` (interactive clients are deferred until a UI exists).
  - usings: `Duende.IdentityServer.Models`.
- Verify: builds; discovery works in T7.

### T6 — Add the account endpoints (cookie session, no JWT)
- New file: `src/Talabat/Talabat.Identity/Controllers/AccountController.cs`,
  `[ApiController]` `[Route("account")]` `AccountController : ControllerBase`.
- Constructor injects `UserManager<ApplicationUser>` and `SignInManager<ApplicationUser>`.
- Request records (in the same file or a `Contracts` folder):
  `RegisterRequest(string Email, string Password)`, `LoginRequest(string Email, string Password)`.
- `POST /account/register`:
  - `var user = new ApplicationUser { UserName = req.Email, Email = req.Email };`
  - `var result = await _userManager.CreateAsync(user, req.Password);`
  - on failure: `return BadRequest(new { errors = result.Errors.Select(e => e.Description) });`
  - on success: `return Ok(new { user.Id, user.Email });` (return **only** Id + Email).
- `POST /account/login`:
  - `var result = await _signInManager.PasswordSignInAsync(req.Email, req.Password, isPersistent: false, lockoutOnFailure: false);`
  - `return result.Succeeded ? Ok(new { message = "logged in" }) : Unauthorized();`
- `POST /account/logout`: `await _signInManager.SignOutAsync(); return Ok(new { message = "logged out" });`
- `GET /account/me` — **the "protect a controller" demonstration**, decorated `[Authorize]`:
  - `var user = await _userManager.GetUserAsync(User);`
  - `return user is null ? Unauthorized() : Ok(new { user.Id, user.Email });`
  - Because the endpoint is `[Authorize]`, an unauthenticated request returns 401 (via the cookie
    config in T4); a request carrying the login cookie returns 200. This is the pattern every future
    business controller will reuse.
- Never serialize the `ApplicationUser` entity directly; return only the anonymous shapes above.
- Verify: builds; behavior checked by tests in T9.

### T7 — Build and smoke-check discovery
- Run the Identity host (dev). `GET /.well-known/openid-configuration` must return HTTP 200 JSON whose
  `issuer` is set and whose `scopes_supported` contains `talabat.customer-api` and
  `talabat.deliveryagent-api`.
- Verify: capture the status code + the two scope names. Stop if 500 (usually a missing signing
  credential or DI ordering error in T4).

### T8 — Create and review the Identity migration
- The `TalabatDbContext` model already includes the Identity tables but no migration exists yet.
  Use the **already-wired** API host as the EF startup project (it has `AddInfrastructure` + the
  connection string; do not build endpoints in it).
- Run:
  `dotnet ef migrations add AddIdentitySchema --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API --output-dir Persistence/Migrations`
- **Review the generated migration before applying.** It MUST only create `AspNetUsers`, `AspNetRoles`,
  `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`.
  It MUST NOT alter or drop `Restaurants`, `Products`, `Carts`, `CartItems`, `Orders`, `OrderItems`,
  `Customers`, `CustomerAddresses`, `Deliveries`, `DeliveryAgents`, and MUST NOT add any
  profile-link column. If it touches business tables, stop and report.
- Apply:
  `dotnet ef database update --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API`
- Verify: migration file exists under `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/`;
  review passed; `database update` succeeds.

### T9 — Identity tests
- New test project: `tests/Talabat.Identity.Tests` (xUnit), add it to `src/Talabat/Talabat.slnx`.
  Reference `Talabat.Identity`. Reuse the SQL Server fixture pattern already in
  `tests/Talabat.Infrastructure.Tests/Persistence/SqlServerDatabaseFixture.cs` (Testcontainers with
  LocalDB fallback) — copy/adapt it; do not invent a new DB strategy.
- Make the host testable: add `public partial class Program { }` at the end of
  `src/Talabat/Talabat.Identity/Program.cs`, and drive it with
  `WebApplicationFactory<Program>` pointed at a migrated test database.
- Required tests (each must pass):
  1. Register with a new email → 200; an `AspNetUsers` row exists for that email.
  2. Register with a duplicate email → 400.
  3. Login with correct credentials → 200 and a `Set-Cookie` auth cookie is present.
  4. Login with wrong password → 401.
  5. Logout after login → 200.
  6. `GET /account/me` **without** logging in → 401 (proves `[Authorize]` + cookie config work).
  7. `GET /account/me` **after** login (reuse the cookie from the login response via a shared
     `HttpClient`/handler) → 200 and body contains the registered email.
  8. No response body across the above contains the substrings `PasswordHash` or `SecurityStamp`.
  9. `GET /.well-known/openid-configuration` → 200 and lists both api scopes.
- Tip: use a single `HttpClient` created from the `WebApplicationFactory` with cookie handling so the
  login cookie flows into the `GET /account/me` call automatically.
- Verify: `dotnet test tests/Talabat.Identity.Tests` all green.

### T10 — Full regression + vulnerability gate
- Run `dotnet build src/Talabat/Talabat.slnx` → 0 errors.
- Run `dotnet test src/Talabat/Talabat.slnx` → all previous suites (Application 45, Infrastructure 19)
  still green, plus the new Identity tests.
- Run `dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive` → report must
  show **no known vulnerabilities**. If Duende pulls a vulnerable transitive package, stop and report
  the package + advisory (do not silently downgrade).

### T11 — Record outcome
- Create `docs/phase-6-identity.md`: list the resolved Duende version, the endpoints added, the
  migration name, and confirm the guardrails held (no Domain/Application identity packages, no JWT,
  no profile linkage, business tables untouched).
- Update the roadmap Status Snapshot row for Phase 6 to Completed, pointing at `docs/phase-6-identity.md`.

---

## Final acceptance checklist (constitution gate)

- [ ] Solution builds; all tests green (Application + Infrastructure + Identity).
- [ ] `Talabat.Domain` and `Talabat.Application` csproj contain no Identity/Auth/Duende packages.
- [ ] Dependency direction unchanged: `Talabat.Identity → Infrastructure → Application → Domain`.
- [ ] Register, login (cookie), logout tested; no password/hash/security-stamp in any response or log.
- [ ] `GET /account/me` returns 401 when logged out and 200 when logged in (proves `[Authorize]` works).
- [ ] Login returns no JWT; only the Identity cookie is set.
- [ ] Migration reviewed; only `AspNet*` tables added; business tables and data untouched; no
      profile-link column.
- [ ] `dotnet list package --vulnerable` is clean.
- [ ] `Customer` / `DeliveryAgent` unchanged (still pure domain profiles, no account linkage).

## The one real uncertainty

Duende IdentityServer's net10.0 support is the only unknown. Task 1 handles it: add the latest stable,
verify restore/build, and if it fails, pin the newest version that restores and record it here. Do not
switch frameworks (OpenIddict/IS4) — the framework decision is fixed to Duende.
