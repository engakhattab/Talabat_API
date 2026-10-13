# OpenCode Task: Registration Razor Page (`Talabat.Identity`)

Repository: **Talabat_API**, branch `feature/user-aggregate-refactor`.
Do **not** branch from or target `main`.

The Identity host already serves `Login`, `Logout`, and `Error` Razor Pages, and both SPA clients
(`talabat-customer-spa`, `talabat-delivery-spa`) are registered for authorization-code + PKCE. This
task adds the missing **Register** page so a new user can create an account from the browser
*without leaving the pending authorize request*.

---

## Why this is needed

A first-time user starting the OIDC flow from either Angular app is dead-ended today:

1. Angular calls `/connect/authorize`.
2. Duende stashes the request and redirects to `/auth/login?ReturnUrl=…`.
3. The login page offers no way to create an account.
4. The only registration surface is `AccountController`'s JSON endpoints, which are not reachable
   from a browser form and cannot resume the stashed authorize request.

Result: the end-to-end Angular login journey cannot be exercised for any new user.

---

## Governing constraints (constitution v3.0.1 — do not violate)

- `IUserCapabilityService` is the **only** path for registration and capability grants. Do not call
  `UserManager<User>.CreateAsync`, do not touch `AspNetUserRoles`, do not set `UserType` directly.
- **Callers never supply role names.** The initiating flow selects *which server workflow runs* —
  never *which role is granted*.
- Customer capability is granted at registration. DeliveryAgent capability requires server-side
  approval; registration only creates an **applicant** (`AgentApprovalStatus = PendingApproval`,
  no `DeliveryAgent` flag, no role, `DeliveryAgentStatus == null`).
- One `User` row per person. `sub` is `User.Id` (int).
- `Talabat.Domain` and `Talabat.Application` must remain free of Duende, IdentityServer, JWT, and
  ASP.NET Core Identity web dependencies. All new code lives in the `Talabat.Identity` host.

---

## Scope

**In scope**
- `src/Talabat/Talabat.Identity/Pages/Account/Register.cshtml` and `Register.cshtml.cs`
- Server-side derivation of the registration variant from the initiating client
- Auto sign-in on success and correct `returnUrl` continuation
- Reciprocal links between Login and Register that preserve `ReturnUrl`
- Tests in `tests/Talabat.Identity.Tests`

**Out of scope — do not touch**
- `IUserCapabilityService`, `UserCapabilityService`, the `User` aggregate, `UserType`,
  `AgentApprovalStatus`, any repository, any migration
- `AccountController` JSON endpoints (keep them for Postman/integration tests)
- `IdentityServerConfig` — no client, scope, redirect-URI, or CORS changes
- `TalabatProfileService`, token lifetimes, refresh-token settings
- Admin approval UI or endpoints
- Password reset, email confirmation, 2FA, external login
- Any Angular work
- `specs/`, `.specify/`, `docs/` — except a phase record if the repo process requires one

---

## Task 1 — Derive the registration variant server-side

Add a private enum or equivalent with two values: customer registration and delivery-agent
application.

Resolve it by calling `IIdentityServerInteractionService.GetAuthorizationContextAsync(ReturnUrl)`
and reading `context.Client.ClientId`:

| `client_id` | Variant |
|---|---|
| `talabat-customer-spa` | Customer |
| `talabat-delivery-spa` | Delivery-agent applicant |
| no context / unrecognised | Customer (default) |

**Critical:** derive this on **both** `OnGetAsync` and `OnPostAsync`. The POST handler must
re-derive from `ReturnUrl` and must **never** trust a posted variant field, hidden input, query
parameter, `Origin`, or `Referer`. `GetAuthorizationContextAsync` is trustworthy because Duende has
already validated `client_id` and `redirect_uri` against registered clients; a raw form field has
not been validated by anything.

The variant may be exposed as a read-only property for view rendering. It must not be
`[BindProperty]`.

---

## Task 2 — Page model

Create `RegisterModel : PageModel` in namespace `Talabat.Identity.Pages.Account`, decorated
`[AllowAnonymous]`. Mirror the structure and conventions of the existing `LoginModel` exactly.

Constructor-inject: `IUserCapabilityService`, `SignInManager<User>`,
`IIdentityServerInteractionService`.

`InputModel` fields:

| Field | Applies to | Validation |
|---|---|---|
| `Email` | both | `[Required]`, `[EmailAddress]` |
| `Password` | both | `[Required]`, `[DataType(DataType.Password)]` |
| `ConfirmPassword` | both | `[DataType(DataType.Password)]`, `[Compare(nameof(Password))]` |
| `FullName` | both | `[Required]` |
| `PhoneNumber` | both | optional |
| `Age` | customer only | nullable `int?`; required + range-checked in `OnPostAsync` |
| `VehicleType` | agent only | nullable `VehicleType?`; required + enum-validated in `OnPostAsync` |

`Age` and `VehicleType` are **conditionally required**, which DataAnnotations cannot express
cleanly. Declare both nullable and enforce them in `OnPostAsync` via `ModelState.AddModelError`
after the variant is derived. Do not introduce a validation library for this.

`ReturnUrl` follows the `LoginModel` pattern: `[BindProperty(SupportsGet = true)]`.

---

## Task 3 — Markup

`Register.cshtml` at route `@page "/auth/register"`, matching the `/auth/login` convention.

- Hidden `ReturnUrl` input, exactly as `Login.cshtml` does
- `asp-validation-summary="All"`
- Render `Age` only for the customer variant; render `VehicleType` (a select bound to the enum) only
  for the agent variant
- `autocomplete="username"` on email, `autocomplete="new-password"` on both password fields
- For the agent variant, include a short line stating the application requires approval before
  delivery features unlock
- Reuse `Pages/Shared/_Layout.cshtml`. Do not add a CSS framework, JS bundle, or client-side
  validation scripts.

Razor Pages antiforgery is on by default — confirm nothing in this page or `Program.cs` disables it.

---

## Task 4 — Success path

On valid input, call the matching capability method and inspect the returned `UseCaseResult<int>`.

On success:
1. Sign the new user in with `SignInManager.PasswordSignInAsync` using the submitted credentials.
   Prefer this over `SignInAsync` so the account passes the same sign-in validation as a normal
   login (including the `!IsActive || IsDeleted` rejection rule).
2. Redirect using the **identical** logic as `LoginModel.OnPostAsync`: if
   `GetAuthorizationContextAsync(ReturnUrl)` is non-null → `Redirect(ReturnUrl)`; else if
   `Url.IsLocalUrl(ReturnUrl)` → `Redirect(ReturnUrl)`; else → `Redirect("~/")`.

Do not duplicate or reimplement that redirect logic differently — an open-redirect introduced here
would be reachable pre-authentication.

---

## Task 5 — Failure path

Map `UseCaseResult` failures onto `ModelState` errors and re-render the page. Reuse
`AccountController.MapError`'s error-code vocabulary for consistency; do not invent new codes.

Never surface: password, `PasswordHash`, security stamp, `User.Id` of an existing account, stack
traces, or raw exception text. A duplicate-email message may state that the email is already
registered — that is standard for a registration form.

---

## Task 6 — Cross-links

- `Login.cshtml`: add a "Create an account" link to `/auth/register`, **forwarding the current
  `ReturnUrl`**.
- `Register.cshtml`: add a "Sign in" link back to `/auth/login`, forwarding `ReturnUrl`.

Losing `ReturnUrl` on either link silently breaks the pending authorize request and strands the
user. Cover both links with tests.

---

## Task 7 — Tests (`tests/Talabat.Identity.Tests`)

Follow the existing test-project conventions. Required cases:

**Rendering**
1. `GET /auth/register` returns 200.
2. With a `returnUrl` carrying a valid `talabat-customer-spa` authorize context: `Age` renders,
   `VehicleType` does not.
3. With a `talabat-delivery-spa` context: `VehicleType` renders, `Age` does not.
4. With no authorize context: the customer variant renders.

**Customer registration**
5. Valid POST creates one `User` with the Customer flag and the corresponding Identity role.
6. An authentication cookie is issued.
7. Duplicate email → validation error, no second `User` row.
8. Password/confirm mismatch → validation error, no `User` created.

**Delivery-agent application**
9. Valid POST creates a `User` with `AgentApprovalStatus == PendingApproval`, the supplied
   `VehicleType`, **no** `DeliveryAgent` flag, **no** delivery role, and
   `DeliveryAgentStatus == null`.

**Anti-tamper (highest value in this task)**
10. POST carrying a `talabat-customer-spa` `returnUrl` **plus** a forged agent-variant form field
    runs the *customer* workflow. Assert no `AgentApprovalStatus` and no `VehicleType` were set.
11. POST with no authorize context plus a forged agent-variant field runs the customer workflow.

**Redirects**
12. Valid authorize-context `returnUrl` → redirect to that `returnUrl`.
13. External absolute `returnUrl` with no authorize context → redirect to `~/`, not the external
    host.

**Leakage**
14. No response body or rendered page contains a password, hash, or security stamp.

---

## Task 8 — Verification

```
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx
```

The whole solution must build and every test project must pass.

Then smoke-test manually: from `http://localhost:4200`, start a login, follow the redirect to
`/auth/login`, click through to `/auth/register`, register, and confirm the browser lands back on
`http://localhost:4200/signin-callback` with a usable token.

---

## Acceptance criteria

- [ ] `Register.cshtml` / `Register.cshtml.cs` exist at `/auth/register` under `Pages/Account/`.
- [ ] Registration goes exclusively through `IUserCapabilityService`.
- [ ] No role name, `UserType` value, or capability is read from client-supplied input.
- [ ] The variant is re-derived server-side on POST via `GetAuthorizationContextAsync`.
- [ ] Agent registration produces a pending applicant only — no flag, no role, no agent status.
- [ ] Successful registration signs the user in and resumes the pending authorize request.
- [ ] Login ↔ Register links preserve `ReturnUrl`.
- [ ] Redirect logic matches `LoginModel` and blocks open redirects.
- [ ] Antiforgery is active on the page.
- [ ] All 14 test cases above exist and pass; full solution build and test run are green.
- [ ] No changes to `IdentityServerConfig`, migrations, the `User` aggregate, or `AccountController`.

---

## Do not

- Do not create a `User` outside `IUserCapabilityService`.
- Do not read `Origin`, `Referer`, a query string, or a form field to decide the variant.
- Do not add a second account for a person who already has one.
- Do not grant `DeliveryAgent` at registration under any condition.
- Do not add password reset, email confirmation, 2FA, or external login.
- Do not modify client registrations, scopes, redirect URIs, or CORS.
- Do not start any Angular work.
- Do not commit to `main`.
