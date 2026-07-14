# Talabat.Identity Web API Setup Guide

> Learning guide only. `Talabat.Identity` is planned as an ASP.NET Core Web API host. The future Angular SPA will supply the interactive user interface. No project, package, source file, solution entry, database, or migration was created while updating this guide.

## 1. Goal

Create `Talabat.Identity` as a centralized ASP.NET Core Web API host using:

- Duende IdentityServer for OIDC/OAuth2 protocol endpoints and token issuance.
- ASP.NET Core Identity for users, password hashes, roles, lockout, and the Identity authentication cookie.
- SQL Server for Identity persistence.
- The existing Infrastructure `TalabatDbContext` as the single EF Core context for business and Identity tables.
- JSON account endpoints for initial manual learning:
  - `POST /api/account/register`
  - `POST /api/account/login`
  - `POST /api/account/logout`

Later:

- Angular supplies the login/register/logout UI.
- Angular authenticates through Authorization Code with PKCE.
- `Talabat.Customer.API` and `Talabat.DeliveryAgent.API` trust access tokens issued by `Talabat.Identity`.

The Phase 6 login endpoint establishes an Identity authentication cookie. It must not generate or return a custom JWT.

Approved dependency direction:

```text
Talabat.Identity -> Talabat.Infrastructure -> Talabat.Application -> Talabat.Domain
```

`Talabat.Infrastructure` must never reference the `Talabat.Identity` Web API host.

## 2. Important Protocol Constraint

Duende implements the OIDC/OAuth2 protocol engine, but interactive login and logout still require user-interaction UI.

Because Angular does not exist yet, Phase 6 can prove:

- Account registration.
- Credential validation.
- Identity cookie creation.
- Cookie-based logout.
- Identity persistence.
- Duende discovery/protocol endpoint availability.

Phase 6 cannot prove the complete Angular OIDC redirect/code/token/logout journey without either:

- The future Angular interaction UI; or
- A separately approved temporary OIDC test client/UI.

Do not work around the missing UI by implementing Resource Owner Password Credentials or a custom `/login` endpoint that returns tokens.

## 3. Prerequisites

Before creating anything:

1. Confirm the repository target framework.
   - The current projects target `net10.0`.
2. Confirm the installed .NET SDK supports `net10.0`.
3. Verify the supported Duende version for that target.
4. Verify local Duende template availability.
5. Verify package versions are compatible with each other and the target framework.
6. Verify current Duende licensing and Community Edition eligibility.
7. Confirm the approved single-DbContext direction: extend Infrastructure's existing `TalabatDbContext` with ASP.NET Core Identity EF storage.
8. Activate a Phase 6 spec and constitution scope before implementation.

Pause if any of these decisions is unresolved.

## 4. Inspect Duende Templates

Run these yourself:

```powershell
dotnet new list
dotnet new list duende
dotnet new list identityserver
```

If required:

```powershell
dotnet new install Duende.Templates
```

Then repeat the filtered template listing.

The screenshot appears to show an ASP.NET Identity integration template similar to `isaspid`. Current Duende documentation refers to a template such as `duende-is-aspid`, but you must confirm the exact short name locally.

### Why the ASP.NET Identity template is not the final project template

The Duende ASP.NET Identity template normally includes interactive login/logout pages. It is valuable as a reference for:

- `AddAspNetIdentity<ApplicationUser>()`.
- IdentityServer configuration.
- `ApplicationUser` and Identity DbContext setup.
- Correct `SignInManager`/`UserManager` usage.
- Login/logout interaction handling.

Your approved target is a Web API host with Angular UI later. Therefore:

- Use the Duende template/reference solution to study the correct integration.
- Create the actual `Talabat.Identity` host from the ASP.NET Core Web API template.
- Port only the required IdentityServer/ASP.NET Identity setup after understanding it.
- Do not copy Razor Pages into the final host unless you explicitly approve a temporary server-rendered interaction UI.

## 5. Create The Web API Host

Reference command to run yourself:

```powershell
dotnet new webapi --name Talabat.Identity --framework net10.0 --output src/Talabat/Talabat.Identity
```

Then add `src/Talabat/Talabat.Identity/Talabat.Identity.csproj` to `src/Talabat/Talabat.slnx` manually.

Add the approved one-way project reference:

```powershell
dotnet add src/Talabat/Talabat.Identity reference src/Talabat/Talabat.Infrastructure/Talabat.Infrastructure.csproj
```

Before adding packages, review:

- The generated project SDK and target framework.
- `Program.cs`.
- OpenAPI setup.
- HTTPS launch settings.
- Existing solution project paths.

Do not create either business API during this step.

## 6. Package Direction

Verify exact versions immediately before installation. Expected package responsibilities:

| Package and owner | Purpose |
|---|---|
| `Talabat.Identity`: `Duende.IdentityServer.AspNetIdentity` | Connects Duende IdentityServer to ASP.NET Core Identity |
| `Talabat.Infrastructure`: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Adds Identity EF models/stores to `TalabatDbContext` |
| Existing Infrastructure SQL Server/Design packages | Continue to own provider and migration tooling |
| `Duende.IdentityServer.EntityFramework` | Do not add in the minimal one-DbContext phase; its built-in configuration/operational stores use additional DbContexts |

Reference-only commands:

```powershell
dotnet add src/Talabat/Talabat.Identity package Duende.IdentityServer.AspNetIdentity
dotnet add src/Talabat/Talabat.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore
```

Do not install IdentityServer4. Do not add Identity packages to Domain or Application.

Add a project reference from `Talabat.Identity` to `Talabat.Infrastructure`. Never add the reverse reference.

## 7. Project Boundaries

- `Talabat.Identity` is a separate ASP.NET Core Web API host that references `Talabat.Infrastructure` for persistence.
- `Talabat.Domain` must not reference `Talabat.Identity`.
- `Talabat.Application` must not reference `Talabat.Identity`.
- `Talabat.Infrastructure` must not reference `Talabat.Identity`.
- `Talabat.Identity` must not access business aggregates directly through `TalabatDbContext`; business behavior still goes through Application use cases/repositories when later required.
- Identity framework types must not leak into Domain.
- `Customer` must not inherit from `IdentityUser` or `ApplicationUser`.
- `DeliveryAgent` must not inherit from `IdentityUser` or `ApplicationUser`.
- Customer and DeliveryAgent remain business profiles.
- Account-to-profile linkage remains deferred.
- Future APIs trust Identity at runtime through issuer/signing keys/audience/scope validation, not project references.

## 8. ASP.NET Core Identity Model

Because the single `TalabatDbContext` lives in Infrastructure, `ApplicationUser` must also live in an Identity-specific Infrastructure namespace. Otherwise Infrastructure would need to reference the Web API host and create an invalid outward dependency.

Suggested location:

```text
src/Talabat/Talabat.Infrastructure/Identity/ApplicationUser.cs
```

Minimal model:

```csharp
public sealed class ApplicationUser : IdentityUser
{
}
```

Keep it empty until a real account requirement appears. Do not add Customer or DeliveryAgent IDs yet.

`ApplicationUser` is an Infrastructure authentication persistence model. It is not a Domain entity.

Password rules:

- Give the plain password only to `UserManager`/`SignInManager`.
- Let ASP.NET Core Identity hash and verify passwords.
- Never compare passwords manually.
- Never return or log the password or `PasswordHash`.
- Never expose `ApplicationUser` directly from an endpoint.

Initial role names may be reserved as:

- `Customer`
- `DeliveryAgent`

Role creation, assignment, and token emission are separate operations. Refine them only when the APIs need them.

## 9. DbContext Direction

The approved choice is one physical database and one EF Core context: Infrastructure's existing `TalabatDbContext`.

Required direction:

1. Add ASP.NET Core Identity EF support to `Talabat.Infrastructure`.
2. Place `ApplicationUser` under an Infrastructure Identity namespace.
3. Change `TalabatDbContext` to derive from the appropriate `IdentityDbContext<ApplicationUser, IdentityRole, string>` base.
4. Preserve the existing business `DbSet` properties and entity configurations.
5. Call `base.OnModelCreating(modelBuilder)` before applying custom business/Identity mapping changes.
6. Register ASP.NET Core Identity with `.AddEntityFrameworkStores<TalabatDbContext>()` from the Identity host composition root.
7. Continue using `ConnectionStrings:TalabatDb`.
8. Generate Identity schema changes as a new Infrastructure migration, using `Talabat.Identity` as the startup project.

Expected dependency direction:

```text
Talabat.Identity -> Talabat.Infrastructure -> Talabat.Application -> Talabat.Domain
```

Forbidden direction:

```text
Talabat.Infrastructure -X-> Talabat.Identity
```

One DbContext does not mean one model boundary. Do not create EF navigation properties or foreign keys between `ApplicationUser` and `Customer`/`DeliveryAgent` in Phase 6.

Migration review requirements:

- Preserve the existing migration history.
- Add the standard Identity tables, indexes, keys, and constraints expected by the configured Identity model.
- Confirm no existing business table is dropped, renamed, or unexpectedly altered.
- Confirm no Customer/DeliveryAgent profile-link column is added.
- Keep the migration and snapshot under Infrastructure's existing migrations folder.
- Test the migration against a clean database and an upgraded Phase 5 database before accepting it.

Reference-only migration commands:

```powershell
dotnet ef migrations add AddIdentityAuth --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.Identity --context TalabatDbContext --output-dir Persistence/Migrations

dotnet ef database update --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.Identity --context TalabatDbContext
```

This single-context choice is simpler for the learning monolith but increases Infrastructure coupling. Reassess it before independently deploying or scaling Identity persistence.

Be able to answer before migration:

- Which context owns `AspNetUsers`?
- Which connection string does Identity use?
- Where do Identity migrations live?
- Did `base.OnModelCreating` run before custom mappings?
- Did the new migration preserve every existing business table?

## 10. Minimal Account Endpoints

Use controllers or minimal API handlers consistently. These endpoints are account-interaction endpoints, not OAuth token endpoints.

### Register

`POST /api/account/register` should:

1. Accept a small request DTO such as email, password, and confirmation.
2. Create `ApplicationUser`.
3. Call `UserManager<ApplicationUser>.CreateAsync(user, password)`.
4. Return safe validation errors.
5. Return no password, password hash, security stamp, or full Identity entity.
6. Create no Customer or DeliveryAgent profile.

### Login

`POST /api/account/login` should:

1. Accept the minimum credential DTO.
2. Validate through `SignInManager<ApplicationUser>`.
3. Apply lockout behavior if configured.
4. Establish the ASP.NET Core Identity/Duende authentication cookie.
5. Return a small success/failure response.
6. Return no custom JWT, refresh token, password data, or full Identity entity.

For manual testing, the HTTP client must retain cookies between login and logout requests.

### Logout

`POST /api/account/logout` should:

1. Use `SignInManager<ApplicationUser>.SignOutAsync` or the template-equivalent IdentityServer-aware sign-out flow.
2. End the local Identity authentication session.
3. Return a small success response.

When Angular/OIDC exists, logout must also use the IdentityServer end-session flow and the registered post-logout redirect URI. Merely deleting a browser token does not invalidate every issued token.

## 11. Duende Configuration

Register ASP.NET Core Identity before calling `AddAspNetIdentity<ApplicationUser>()`.

Conceptual composition-root order in `Talabat.Identity`:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<TalabatDbContext>();

builder.Services
    .AddIdentityServer()
    .AddAspNetIdentity<ApplicationUser>();
```

The exact calls/options must match the reviewed supported package versions. Do not register a second DbContext in the Identity host.

Keep Phase 6 minimal:

- Enable discovery and required protocol endpoints.
- Use development signing credentials only in Development.
- Do not fall back to development keys in another environment.
- Do not add speculative clients, scopes, audiences, claims, or grants.
- Do not add Duende's EF configuration/operational stores while the strict one-DbContext rule is active; those built-in stores introduce additional contexts.
- Do not expose the account endpoints as replacements for `/connect/authorize` or `/connect/token`.

If you add a temporary OIDC client for learning, it must be explicitly Development-only and use Authorization Code with PKCE.

## 12. Future Angular Flow

Angular will be a public client because browser code cannot keep a secret.

Planned flow:

1. Angular starts an Authorization Code request with PKCE against `Talabat.Identity`.
2. Duende determines that user interaction is required.
3. The browser is directed to the Angular login route or approved SPA-style interaction UI.
4. The UI calls the Identity account interaction endpoint and establishes the secure Identity cookie.
5. The browser resumes the authorize request.
6. Duende returns an authorization code to an exact registered Angular redirect URI.
7. Angular exchanges the code using the PKCE verifier.
8. Angular receives an access token intended for a specific Talabat API.

Later Angular concerns:

- Exact CORS origins; never `AllowAnyOrigin` with credentials.
- Cookie `Secure`, `HttpOnly`, and `SameSite` behavior.
- `credentials: include` where cross-origin cookie requests are intentionally required.
- CSRF/login-CSRF protection on cookie-establishing endpoints.
- Exact redirect and post-logout redirect URIs.
- No client secret in source, configuration, storage, or build output.

Follow Duende's SPA-style login UI guidance rather than inventing a password-to-token protocol.

## 13. Minimal Token Direction

Begin with:

- `sub` — stable Identity account identifier.
- `name`/username — only when consumed.
- `role` — only after roles are configured, assigned, and emitted.

Reserve future API resource/audience names:

- `talabat.customer-api`
- `talabat.deliveryagent-api`

Refine scopes, audiences, claims, and account/profile mapping after the APIs exist.

Remember:

- `sub` is an account ID.
- `Customer.Id` is a domain profile ID.
- `DeliveryAgent.Id` is a domain profile ID.
- They are not interchangeable.

## 14. What Not To Do

- Do not use the Web App template as the final host if the approved target is Web API + Angular.
- Do not copy template Razor Pages without explicitly choosing a temporary server-rendered UI.
- Do not return a JWT from the custom login endpoint.
- Do not use Resource Owner Password Credentials.
- Do not put a client secret in Angular.
- Do not add Identity types to Domain.
- Do not create profiles during account registration.
- Do not implement either business API or Angular in Phase 6.
- Do not implement refresh-token tuning, external login, password reset, email confirmation, 2FA, admin UI, advanced consent/custom grants, or production signing/secrets hardening.

## 15. Acceptance Checklist

- [ ] Repository target and supported package versions were confirmed.
- [ ] Duende licensing/Community eligibility was reviewed.
- [ ] Duende ASP.NET Identity template was inspected as a reference.
- [ ] `Talabat.Identity` was created manually from `dotnet new webapi`.
- [ ] The project was added to the solution manually.
- [ ] `Talabat.Identity` has the one-way project reference to `Talabat.Infrastructure`.
- [ ] `Talabat.Infrastructure` does not reference the Identity host.
- [ ] `ApplicationUser` lives in an Infrastructure Identity namespace, not Domain.
- [ ] `TalabatDbContext` is the only application EF Core context and uses the appropriate IdentityDbContext base.
- [ ] The Web API starts over HTTPS.
- [ ] Duende discovery is available.
- [ ] Register persists an Identity account.
- [ ] Login validates through `SignInManager` and establishes the Identity cookie.
- [ ] Login does not return a custom JWT.
- [ ] Logout clears the Identity session using the same manual client's cookie jar.
- [ ] Password and `PasswordHash` are never returned or logged.
- [ ] The reviewed Infrastructure migration adds Identity tables without unexpected changes to existing business tables.
- [ ] Domain/Application remain free of Identity dependencies.
- [ ] Customer and DeliveryAgent remain unchanged.
- [ ] No business API or Angular project was created.
- [ ] Complete OIDC Angular login/logout is recorded as deferred, not falsely marked tested.

## 16. Troubleshooting

### Duende template not found

- Re-run the template listing commands.
- Verify `Duende.Templates` installation and version.
- Confirm NuGet source access.
- Do not assume the short name.

### Web API template lacks login pages

That is expected. A Web API template is headless. Use JSON interaction endpoints for Phase 6 manual cookie testing. Add Angular or an approved temporary interaction UI before claiming the complete OIDC interactive journey works.

### Version mismatch

- Confirm `net10.0` and the installed SDK.
- Verify the Duende, Identity, and EF package version lines.
- Stop rather than forcing incompatible restores.

### Duende license warning

- Determine whether it is expected for Development/evaluation.
- Recheck current production licensing and Community eligibility.
- Keep licensing as a deployment gate.

### DbContext confusion

- Confirm `TalabatDbContext` is the only registered application context.
- Confirm its IdentityDbContext generic user type matches `ApplicationUser`.
- Confirm `base.OnModelCreating` is called before custom mappings.
- Confirm `.AddEntityFrameworkStores<TalabatDbContext>()` uses that context.
- Confirm Identity uses `TalabatDb` and the existing Infrastructure migration assembly.
- Check for a circular reference; only Identity may reference Infrastructure.
- Create no migration until this wiring is reviewed.

### Login succeeds but there is no access token

That is expected for the Phase 6 account endpoint. It establishes a cookie. An access token requires an OIDC client plus an authorize/code/token flow. Do not “fix” this by generating a JWT in the login endpoint.

### Logout appears not to work

- Ensure the manual client sends the login cookie on logout.
- Confirm the correct Identity cookie scheme is signed out.
- Distinguish local account-cookie logout from full OIDC client/session logout.

### Roles are missing

- Confirm the role exists and the user is assigned.
- Confirm the profile/claims service emits it.
- Confirm the client/resource requests and permits it.
- Obtain a new token after changes.

### Angular CORS/cookie failure later

- Use an exact allowed Angular origin.
- Enable credentials only for that explicit origin.
- Verify `Secure`/`SameSite` cookie behavior.
- Use HTTPS and exact redirect URIs.
- Add CSRF protection; do not solve the issue with permissive CORS.

## 17. Learning Checkpoints

Stop for review after each checkpoint:

1. SDK, Duende version, template, and license check.
2. Web API project creation and solution entry.
3. Generated project/package review.
4. Infrastructure `ApplicationUser`, single `TalabatDbContext`, and dependency-direction design.
5. Duende/Identity service registration.
6. Identity migration review.
7. Register endpoint.
8. Login endpoint and cookie inspection.
9. Logout with the same cookie jar.
10. Architecture and security review.

Share the relevant diff, command output, or error at each checkpoint before continuing.

## 18. Official References

- [Duende ASP.NET Core Identity integration](https://docs.duendesoftware.com/identityserver/aspnet-identity/)
- [Duende user interaction requirements](https://docs.duendesoftware.com/identityserver/ui/)
- [Duende SPA-style login UI sample index](https://docs.duendesoftware.com/identityserver/samples/ui/)
- [Duende interactive application quickstart](https://docs.duendesoftware.com/identityserver/quickstarts/2-interactive/)
- [Duende supported versions](https://docs.duendesoftware.com/general/support-and-issues/)
- [Duende Community Edition](https://duendesoftware.com/products/communityedition)

Recheck official documentation immediately before implementation because template names, supported versions, defaults, and licensing terms can change.
