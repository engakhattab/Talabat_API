# Phase 6 Manual Setup Guide: Talabat.Identity Web API

> Documentation-only phase summary. The detailed learner procedure is `docs/identity/duende-aspnet-identity-setup-guide.md`. No code, package, project, solution entry, database, or migration was created while updating this guide.

## 1. Approved Host Direction

Phase 6 creates `Talabat.Identity` as an ASP.NET Core Web API host, not an ASP.NET Core Web App/Razor Pages host.

It uses:

- Duende IdentityServer for OIDC/OAuth2 discovery, authorize, token, end-session, and related protocol behavior.
- ASP.NET Core Identity for accounts, password hashing, roles, lockout, and the authentication cookie.
- SQL Server for Identity persistence.
- JSON account-interaction endpoints for initial manual testing.
- A future Angular SPA for the interactive login/register/logout UI.

The future Angular application will be a public OIDC client using Authorization Code with PKCE and no client secret.

## 2. Phase 6 Scope

### Implement manually

- Create `Talabat.Identity` from `dotnet new webapi` after confirming `net10.0` and package compatibility.
- Add Duende IdentityServer and ASP.NET Core Identity integration.
- Add `ApplicationUser` to an Identity-specific Infrastructure namespace.
- Extend Infrastructure's existing `TalabatDbContext` from the appropriate IdentityDbContext base.
- Add the one-way `Talabat.Identity` -> `Talabat.Infrastructure` project reference.
- Use the existing `TalabatDb` connection and Infrastructure migration history.
- Add `POST /api/account/register` using `UserManager`.
- Add `POST /api/account/login` using `SignInManager` to establish the Identity cookie.
- Add `POST /api/account/logout` using `SignInManager` to end the local Identity session.
- Enable Duende discovery/protocol endpoints.
- Test register/login/logout manually while retaining the cookie between requests.
- Preserve Domain/Application independence.

### Explicitly defer

- Angular implementation.
- Complete OIDC redirect/code/token/end-session testing.
- `Talabat.Customer.API` and `Talabat.DeliveryAgent.API`.
- Account-to-profile linkage.
- Final clients, audiences, scopes, claims, roles, and policies.
- Refresh-token tuning, external login, password reset, email confirmation, 2FA, admin UI, advanced consent/custom grants, and production signing/secrets hardening.

## 3. Protocol Guardrails

- The JSON login endpoint establishes the Identity authentication cookie.
- It must not generate or return a custom JWT.
- It must not implement Resource Owner Password Credentials.
- An access token later comes from the Duende Authorization Code + PKCE flow.
- A login cookie is not an API access token.
- A browser/Angular client cannot safely hold a client secret.
- Interactive OIDC login/logout requires UI; Angular supplies it later, or an explicitly approved temporary test UI must be used.

## 4. Architecture Boundaries

- `ApplicationUser`, Identity EF models, and the single `TalabatDbContext` live in Infrastructure.
- Duende configuration and account HTTP endpoints live in `Talabat.Identity`.
- `Talabat.Identity` references Infrastructure; Infrastructure never references the Identity host.
- `Talabat.Domain` never references Identity or ASP.NET Core.
- `Customer` never inherits from `IdentityUser`/`ApplicationUser`.
- `DeliveryAgent` never inherits from `IdentityUser`/`ApplicationUser`.
- Registration creates an account only.
- `TalabatDbContext` explicitly owns both business and Identity tables, while Domain entities remain independent.
- The new Infrastructure migration must add the expected Identity schema without unexpected business-table changes.
- One DbContext must not create EF navigation properties or inheritance between `ApplicationUser` and Domain profiles.

## 5. Phase 6 Acceptance Checklist

- [ ] Phase 6 spec and constitution scope are active.
- [ ] Supported Duende/.NET versions and licensing were checked.
- [ ] `Talabat.Identity` was created manually from the Web API template.
- [ ] The project was added to the solution manually.
- [ ] Identity has the one-way reference to Infrastructure and no circular dependency exists.
- [ ] Duende packages/types remain inside `Talabat.Identity`; ASP.NET Core Identity EF types are limited to Infrastructure and the Identity host.
- [ ] Identity EF packages/types are limited to Infrastructure and the Identity host; Domain/Application remain clean.
- [ ] `TalabatDbContext` is the single application context and uses the IdentityDbContext base correctly.
- [ ] The reviewed Infrastructure migration preserves the existing business schema while adding Identity tables.
- [ ] Register creates an account only.
- [ ] Login establishes the Identity cookie and returns no custom JWT.
- [ ] Logout clears the same manual client's Identity session.
- [ ] Password and `PasswordHash` are never returned/logged.
- [ ] Duende discovery is available.
- [ ] Full Angular OIDC interaction is explicitly deferred.
- [ ] Domain/Application remain free of Identity dependencies.
- [ ] Existing business tests remain green.
- [ ] No business API or Angular project was created.

## 6. Required Review Checkpoints

Stop for review after:

1. SDK/template/package/license discovery.
2. Web API project creation.
3. Infrastructure `ApplicationUser`, single `TalabatDbContext`, and dependency-direction design.
4. Duende/Identity service registration.
5. Identity migration generation and review.
6. Register endpoint.
7. Login endpoint and cookie inspection.
8. Logout using the same cookie jar.
9. Final architecture/security checks.

Use `docs/identity/duende-aspnet-identity-setup-guide.md` for the detailed manual procedure and troubleshooting.
