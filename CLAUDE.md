# Talabat backend — project context

Food-delivery platform (Talabat-style) built as a DDD / Clean Architecture modular monolith.
Roles: Customer, Delivery Agent, Admin, Restaurant Owner (not built yet).

- Backend (this repo): `D:\link-dev\talabat` → `github.com/engakhattab/Talabat_API`, default branch `main`.
- Frontend: `D:\link-dev\talabat-frontend` → `github.com/engakhattab/talabat-frontend`, default branch `master`
  (Angular 22 workspace in `talabat-web/`; only the `customer` app exists).
- **Master plan: `ROADMAP.md`** — milestones, phases, gates, and the current position. Read it first; it wins over
  older plans. Audit evidence and history: `PROJECT_STATUS_REPORT.md`.

## Solution layout (`src/Talabat/Talabat.slnx`, .NET 10, EF Core 10.0.9, Duende IdentityServer 8.0.2)

| Project | Notes |
|---|---|
| `Talabat.Domain` | Aggregates `User` (unified identity + customer/agent/admin capabilities), `Restaurant`/`Product`, `Cart`, `Order` (immutable, **no status**), `Delivery` (8-state lifecycle, RowVersion). Repository interfaces live here. |
| `Talabat.Application` | Plain handler classes (no MediatR) registered in `DependencyInjection.cs`; return `UseCaseResult<T>`. |
| `Talabat.Infrastructure` | `TalabatDbContext` (Identity DbContext over `User`), repositories, `UnitOfWork`, `UserCapabilityService`, migrations, E2E provisioner. |
| `Talabat.API` | **Folder name ≠ assembly**: it builds `Talabat.Customer.API`. Routes `/api/me/*`, `/api/catalog/*`. |
| `Talabat.Delivery.API` | Routes `/api/agent/*`. |
| `Talabat.Admin.API` | Routes `/api/delivery-agent-applications/*` (approve/reject/suspend/reactivate agents only). |
| `Talabat.Identity` | OIDC provider + Razor login/register (`/auth/*`); `--e2e-provision` CLI for Customer test fixtures. |
| `tests/*` | Domain, Application, Architecture, Infrastructure, Identity, Customer.API, Delivery.API, Admin.API. |

## Architecture rules (enforced by `tests/Talabat.ArchitectureTests`)

- Dependency direction: hosts → Application → Domain; Infrastructure implements Domain/Application abstractions.
- Domain may reference only `Microsoft.Extensions.Identity.Stores` (so `User : IdentityUser<int>`) — a sanctioned
  exception in `.specify/memory/constitution.md`. No EF, ASP.NET Core, Duende, or JWT types in Domain/Application.
- Business keys stay `CustomerId` / `Delivery.AssignedAgentId`; both reference `AspNetUsers.Id`.
- Cross-aggregate work goes through domain services (e.g. `DeliveryAssignmentDomainService`) or an Application use
  case — never through controllers. No domain events/outbox exist yet.

## Conventions

- **Errors**: domain exceptions → `DomainExceptionMapper` → `ApplicationError` category → HTTP status via
  `UseCaseResultExtensions.ToProblemDetails()` (Validation 400, NotFound 404, Conflict 409, Unavailable 422,
  OwnershipMismatch 403). ProblemDetails carry a root `errorCode`. The global `DomainExceptionHandler` uses the same
  mapping, so exceptions escaping a handler keep their status.
- **Identity of the caller** always comes from the token `sub` via `ICurrentUser`; `me` routes never accept user IDs.
  Policies = scope + role + persisted-capability check (`CurrentUser` re-reads `UserType` from the DB).
- **Atomic multi-aggregate writes**: `IUnitOfWork.ExecuteInTransactionAsync`. Checkout saves order + cart + delivery in
  one transaction (BR-DEL-016 in `docs/delivery/delivery-business-rules.md`).
- **Paging**: `PageRequest` clamps page ≥ 1 and pageSize 1..50; repositories page in SQL and return real totals.
- **OpenAPI contracts are committed** (`*/openapi/*-api.json`) and parity-tested against the runtime document. If a
  contract changes intentionally, regenerate it, then byte-copy it to `talabat-frontend/talabat-web/openapi/`
  (`npm run api:copy` in the frontend does exactly that) and regenerate the frontend client.
- Checkout's 422 returns the typed `CheckoutUnavailableResponse` (the SPA depends on `unavailableItems`); it is the
  one sanctioned non-ProblemDetails error response.
- **Test-first (mandatory):** name the test cases, write them and see them fail before writing production code for any
  feature, bug fix, or domain transition; production code only makes those tests pass. Never weaken, skip, or delete
  tests to pass a gate.
- **Phase discipline:** work only on the current `ROADMAP.md` phase; do not start the next one until its gate passes.
  Every progress update states the current milestone/phase, completed phases, and the next phase.
- Commits: Conventional Commits whose body explains the bug, root cause, and fix.
- **Git attribution (strict, permanent):** Never add Claude, Anthropic, or AI attribution as git commit author or via
  `Co-authored-by` trailer. All commits must strictly use the local git config user and email with zero AI
  co-authorship metadata.

## Commands (run from repo root)

```bash
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx
dotnet test tests/Talabat.Delivery.API.Tests --filter "FullyQualifiedName~AssignmentConcurrency"
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
dotnet ef migrations add <Name> --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API
dotnet ef database update --project src/Talabat/Talabat.Infrastructure --startup-project src/Talabat/Talabat.API
```

- CI (`.github/workflows/ci.yml`) = Release build + full `dotnet test` (SQL Server container) + the vulnerability gate.
  The full suite takes ~6 min locally; Infrastructure/Identity tests fall back from Testcontainers to LocalDB (no Docker).
- Hosts: `.claude/launch.json` (Identity https://localhost:7237, Customer :7056, Delivery :7225, Admin :7310).
  SPA clients: `talabat-customer-spa` (4200), `talabat-delivery-spa` (4300), `talabat-admin-spa` (4400).
- GitHub: use `gh` (logged in as `engakhattab`); if Bash cannot find it, use `"/c/Program Files/GitHub CLI/gh.exe"`.

## Local environment gotchas

- DB: `Server=.\SQLEXPRESS;Database=talabat` (Windows auth). Integration tests create and drop their own
  `TalabatTest_*` databases; they never touch `talabat`.
- The repos were copied from another machine, so git reports "dubious ownership". Prefix git with
  `git -c safe.directory=*` (or the owner can add a global `safe.directory`).
- Node 24 (`C:\Program Files\nodejs`) and OpenJDK 21 are installed; shells started before the install may need them
  prepended to PATH. The `customer-web` launch entry serves the Angular app on http://localhost:4200.
- Bash on Windows: escaping `\` in JSON/connection strings is error-prone; prefer the Edit tool or PowerShell literals.

## Domain & test gotchas

- Seeded catalog: restaurants 1 "Cairo Grill" and 2 "Nile Pizza"; product IDs start at **101** (101, 102, …).
- `RestaurantSummaryDto.IsActive` actually carries "is open now" (misleading name kept for contract stability).
- Approving an agent sets status `Offline`; it must `GoOnline` (Available) before claiming deliveries or updating location.
- Agent "cancel" terminates the delivery (no re-dispatch); a Busy agent cannot be suspended.
- Customer API test factory: the default test user (Id 1) **has** a profile — use `CreateUserWithoutProfile()` for
  "profile not created" scenarios.
- SQL Server `datetime2` loses `DateTimeKind`; `Delivery` restores UTC before ordering checks.

## Current status (2026-10-14)

- Position, completed phases and next phase: see "Current position" in `ROADMAP.md` (kept up to date there only).
- `main` and frontend `master` are green in CI (backend 640/640 + vulnerability scan; frontend all CI stages).
- In flight: Phase 1 order-lifecycle design (`specs/011-order-lifecycle/design.md`, ADRs in `docs/adr/`). Until
  Phase 2 lands, checkout still creates the delivery and `Order` has no lifecycle.
