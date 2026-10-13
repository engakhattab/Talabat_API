# Talabat Platform — Project Status Report

> **Superseded for sequencing by [`ROADMAP.md`](ROADMAP.md).** This report is audit evidence; its §17 roadmap is superseded.

| | |
|---|---|
| **Audit date** | 2026-10-13 |
| **Audit type** | Read-only, evidence-based (no source, migration, git, or config changes) |
| **Backend repo** | `D:\link-dev\talabat` → `github.com/engakhattab/Talabat_API`, branch `feature/user-aggregate-refactor` @ `896bd2f` (2026-08-17), clean worktree |
| **Frontend repo** | `D:\link-dev\talabat-frontend` → `github.com/engakhattab/talabat-frontend`, branch `master` @ `389f254` (2026-08-09), **27 modified + 24 untracked files (uncommitted)** |
| **Report location decision** | Placed in the backend root because the two repos are independent git repositories, the backend is the system of record for every contract (OpenAPI documents are generated here and copied to the frontend), and the governing `AGENTS.md` and constitution live here. The parent `D:\link-dev` is not a repository, so a report there would be unversioned. |

**Status labels used throughout:** `COMPLETE` · `PARTIAL` · `NOT IMPLEMENTED` · `BLOCKED` · `NEEDS VERIFICATION` · `DEFECT CONFIRMED`

**Evidence conventions:** **[V]** = verified fact (code read, command executed). **[I]** = inference from evidence. **[R]** = recommendation.

> **Update — same day, after remediation (2026-10-13).** The body below describes the repository *as audited*. Phase A was then carried out. Resolved since the audit:
>
> - Local environment works: all connection strings point to `.\SQLEXPRESS` / `talabat`, all 4 migrations are applied, and all 4 hosts plus a real OIDC sign-in → checkout journey were verified.
> - D1: checkout persists the order and its delivery in one transaction; BR-DEL-016 was rewritten.
> - D2: the Admin projects are in `Talabat.slnx`.
> - D3: Testcontainers 4.16.0 brings in the patched SSH.NET.
> - D4/D5: real `itemCount` and database paging, with page sizes clamped to 50.
> - D8: frontend `delivery-api.json` re-synced.
> - D9 and D14: every failing test was repaired.
> - D10: frontend CI triggers fixed.
> - D12: an invalid `sub` now returns 401.
> - D13: the checkout 422 is kept as the documented typed response, and the test was aligned to it.
> - A newly found defect is fixed: escaped domain exceptions, such as a losing concurrent delivery claim, returned 400 instead of their mapped status (409).
> - The frontend's uncommitted work is now committed and pushed (4 commits on `master`).
>
> Still open: D6/D7 (Identity production hardening), D11 (sync-over-async), frontend verification (Node.js not installed), and everything from Phase B onward.

---

## Table of contents

1. [Executive summary](#1-executive-summary)
2. [Audit scope, method, and limitations](#2-audit-scope-method-and-limitations)
3. [Repository inventory](#3-repository-inventory)
4. [Architecture overview](#4-architecture-overview)
5. [Current development position](#5-current-development-position)
6. [Implementation status by role and module](#6-implementation-status-by-role-and-module)
7. [Delivery backend deep-dive](#7-delivery-backend-deep-dive)
8. [Frontend / backend integration status](#8-frontend--backend-integration-status)
9. [DDD and Clean Architecture compliance](#9-ddd-and-clean-architecture-compliance)
10. [Database assessment](#10-database-assessment)
11. [API assessment](#11-api-assessment)
12. [Security assessment](#12-security-assessment)
13. [Testing assessment](#13-testing-assessment)
14. [CI, deployment, and observability assessment](#14-ci-deployment-and-observability-assessment)
15. [Findings register](#15-findings-register)
16. [Risks, blockers, assumptions, unresolved questions](#16-risks-blockers-assumptions-unresolved-questions)
17. [Completion roadmap](#17-completion-roadmap)
18. [Remaining-work checklist with acceptance criteria](#18-remaining-work-checklist-with-acceptance-criteria)
19. [The five best next actions](#19-the-five-best-next-actions)
20. [Verification log](#20-verification-log)
21. [Appendix: key file paths and documentation drift](#21-appendix-key-file-paths-and-documentation-drift)

---

## 1. Executive summary

**What exists.** A .NET 10 / EF Core 10 / Duende IdentityServer 8 backend with a clean four-layer structure (Domain → Application → Infrastructure → hosts) and **four hosts**: `Talabat.Customer.API`, `Talabat.Delivery.API`, `Talabat.Admin.API`, `Talabat.Identity`. An Angular 22 standalone/zoneless workspace contains **one application (`customer`)** plus six libraries. The Customer journey (browse → menu → cart → checkout → order history) is implemented end to end in both tiers. The Delivery Agent backend is functionally rich: applicant self-service, admin approval, availability, self-assignment from a pending queue, a full pickup-to-delivery lifecycle with optimistic concurrency, location updates, and history.

**Where development stopped [V].** The last backend commits (2026-08-17) executed Phases 1–4 of [`docs/delivery-backend-readiness-plan.md`](docs/delivery-backend-readiness-plan.md), plus an unplanned admin suspend/reactivate feature. **Phase 5 ("Test and Contract Readiness": delivery fixtures, contract publication) was never started.** That phase is the documented gate before any Delivery frontend work. The frontend stopped mid-"Phase 11 release hardening" (spec `005-release-hardening`) with that work **uncommitted**. The project has also **moved machines** since then: the connection strings, git ownership SIDs, and Playwright paths reference another PC (`DESKTOP-5IHGJ9F`, `C:\Users\ABDO`). Node.js is not installed on this one, and no `Talabat` dev database exists here.

**Overall assessment.** Roughly **30 % of a production-ready multi-role platform**. The Customer and Delivery-Agent backends are about **55–70 % of an MVP**. Admin is ≈17 % of backend scope, and Restaurant Owner is 0 % in both tiers. Delivery and Admin have no frontends. Method and limitations are in [§6.7](#67-completion-estimate-method-and-limitations).

**Most important findings**

| # | Finding | Label |
|---|---|---|
| 1 | `Order` has **no status/lifecycle**. Nothing models restaurant acceptance, preparation, or ready-for-pickup, so deliveries enter the courier queue immediately at checkout and a cancelled/failed delivery never affects the order. | `NOT IMPLEMENTED` (core-domain gap) |
| 2 | Customers **cannot see delivery status**: `OrderDetails`/`OrderSummary` carry none, and no tracking endpoint exists. | `NOT IMPLEMENTED` |
| 3 | Delivery creation is orchestrated **in `CheckoutController`, after the order commit, in a separate transaction**. `UseCaseResult` failures are silently dropped and exceptions are only logged, so an order can exist with no delivery and nothing reconciles it. | `DEFECT CONFIRMED` |
| 4 | `Talabat.Admin.API` and `Talabat.Admin.API.Tests` are **not in `Talabat.slnx`**, so CI neither builds nor tests the Admin host. | `DEFECT CONFIRMED` |
| 5 | CI's vulnerability gate **would fail today**: SSH.NET 2025.1.0 (two High advisories), transitive in `Talabat.Identity.Tests` and `Talabat.Infrastructure.Tests`. | `DEFECT CONFIRMED` |
| 6 | Order history shows **"0 items" for every order**: `ItemCount` is hardcoded to `0` in `OrderController`. The restaurant and order lists accept `page`/`pageSize` but ignore them. | `DEFECT CONFIRMED` |
| 7 | Identity host is not production-deployable: the non-Development signing credential is a `TODO`, all URIs/CORS origins are hardcoded `localhost`, and JSON `POST /account/login` runs with `lockoutOnFailure: false`. | `DEFECT CONFIRMED` / `PARTIAL` |
| 8 | Frontend `openapi/delivery-api.json` is **stale** (pre-Phase 1–4 hash `d0d6016e…` vs backend `63df9d82…`). `admin-api.json` was never copied. | `DEFECT CONFIRMED` (contract drift) |
| 9 | No Restaurant Owner capability anywhere. The detailed plan [`docs/restaurant-owner-and-admin-api-implementation-plan.md`](docs/restaurant-owner-and-admin-api-implementation-plan.md) is unimplemented. | `NOT IMPLEMENTED` |
| 10 | No payments, notifications, domain events/outbox, real-time tracking, rate limiting, structured logging/telemetry, containers, or deployment pipeline. | `NOT IMPLEMENTED` |
| 11 | **25 of 630 backend tests fail** (mostly stale fixtures/assertions). The API-level assignment-race test never exercises the race, and the BOLA location test is masked by a newer rule. | `DEFECT CONFIRMED` (test suite) |

**Verification headline [V].** The backend builds with **0 errors** (8 NU1903 vulnerability warnings). Tests: **605 / 630 pass (96 %)**. Domain 58/58, Application 260/260, Architecture 20/20, and Admin 13/13 are fully green. The **25 failures** are in Infrastructure (1), Identity (4), Customer.API (16), and Delivery.API (4). Root-cause tracing ([§13.1](#131-results-of-this-audit-run)) shows 17 are stale or contradictory tests, 2 a broken token helper, 2 environment-coupled tests, and 7 genuine but low-severity product contract defects. The branch is therefore **red** by its own constitution. Frontend build and tests **could not be executed** (no Node.js on this machine); historical logs from 2026-08-09 show 230/230 customer tests and 87 library tests passing.

**Recommended next action [R].** First restore a working, trustworthy baseline: commit or stash the frontend's uncommitted hardening work, add the Admin projects to the solution, fix the SSH.NET vulnerability and the 25 failing tests, and repoint configuration to this machine. Then fix the checkout→delivery atomicity defect **before** adding the `Order` lifecycle that the Restaurant Owner and tracking features depend on. See [§19](#19-the-five-best-next-actions).

---

## 2. Audit scope, method, and limitations

**Scope inspected**

- `D:\link-dev\talabat` (backend): all 628 tracked files were enumerated. Source was read for every aggregate, the key handlers, controllers, `Program.cs` hosts, persistence configuration, CI, and the governing docs/specs.
- `D:\link-dev\talabat-frontend` (frontend): workspace configuration, all non-spec TypeScript file names, routes, auth/interceptor code, the orders feature, CI workflow, specs, and docs.
- Siblings: `D:\link-dev\talabat-web` contains only an empty `projects` folder [V]. `D:\link-dev\DDD00` is a standalone DDD learning sample (Cart/Money). `D:\link-dev\DDD-artichle` is an article with images. None of these is part of the product.

**Method.** Git history and branch analysis; code tracing from controller → handler → domain → repository → EF configuration; spec/task checkbox reconciliation against code; build, test, and vulnerability commands (see [§20](#20-verification-log)).

**Limitations (explicit)**

- **Frontend not executed.** Node.js/npm are absent from PATH and from the common install locations [V]. Frontend status relies on code reading and on **historical** logs in `talabat-web/*.log` dated 2026-08-09, so it is labelled `NEEDS VERIFICATION` where it matters.
- **Integration tests were run against this machine's `.\SQLEXPRESS`** through the environment override `ConnectionStrings__TalabatDb`, because the committed connection string targets `DESKTOP-5IHGJ9F\SQLEXPRESS`. Infrastructure/Identity tests use Testcontainers and fall back to LocalDB (Docker is not installed). Every fixture creates GUID-named `TalabatTest_*` databases and drops them [V]. No user database was touched; there is no `Talabat` database on this instance.
- **Not executed:** the hosts (no manual API smoke tests), Playwright E2E, and migrations against a persistent database. No remote CI history was queried.
- `git` reported "dubious ownership" (repos copied from another machine). Read-only commands were run with `git -c safe.directory=*`; global git config was **not** modified.

---

## 3. Repository inventory

### 3.1 Backend — `D:\link-dev\talabat`

| Project | Path | Role | In `Talabat.slnx`? |
|---|---|---|---|
| `Talabat.Domain` | `src/Talabat/Talabat.Domain` | Aggregates, value objects, domain services, repository interfaces | ✅ |
| `Talabat.Application` | `src/Talabat/Talabat.Application` | Use-case handlers (CQRS-lite, no MediatR), `UseCaseResult`, abstractions | ✅ |
| `Talabat.Infrastructure` | `src/Talabat/Talabat.Infrastructure` | EF Core SQL Server, repositories, UoW, Identity integration, E2E provisioner | ✅ |
| `Talabat.Customer.API` | `src/Talabat/Talabat.API` (⚠ folder ≠ assembly name) | Customer REST API, audience `talabat.customer.api` | ✅ |
| `Talabat.Delivery.API` | `src/Talabat/Talabat.Delivery.API` | Delivery agent REST API, audience `talabat.delivery.api` | ✅ |
| `Talabat.Admin.API` | `src/Talabat/Talabat.Admin.API` | Admin REST API (delivery-agent applications only), audience `talabat.admin.api` | ❌ **missing** |
| `Talabat.Identity` | `src/Talabat/Talabat.Identity` | Duende IdentityServer 8.0.2 + ASP.NET Identity, Razor login/register, E2E CLI | ✅ |
| 8 test projects | `tests/*` | Domain, Application, Architecture, Infrastructure, Identity, Customer.API, Delivery.API, Admin.API | 7 of 8 (Admin.API.Tests ❌) |

**Stack [V]:** `net10.0`, SDK 10.0.401 installed, EF Core SqlServer 10.0.9, ASP.NET Identity 10.0.9, Duende IdentityServer 8.0.2, `Microsoft.AspNetCore.OpenApi` 10.0.9 + Swashbuckle SwaggerUI 10.2.3, xUnit, Testcontainers.MsSql.

**Branches [V]:** `feature/user-aggregate-refactor` (HEAD, pushed, **33 commits ahead of `main`**, never merged), `main` @ `8ad1aa6` (tag `before-user-aggregate-refactor`), `origin/copilot/fix-build-and-test-job` (2 unmerged commits, e.g. `469aabf fix(customer-api): return ProblemDetails for checkout unavailable errors`).

**Root-level artefacts:** many planning/prompt files (`PLAN.md`, `PROJECT_IMPLEMENTATION_ROADMAP.md`, `opencode-*.md`, `del-phase1.txt`, `procced.txt`), 4 ignored `test_results*.log` files from 2026-07-16, and ignored build-output folders `.codex-*`. **There is no `README.md`** [V].

**Documentation:** 46 files in `docs/` (design docs per layer, phase guides, delivery bounded-context set under `docs/delivery/`), 10 speckit feature folders `specs/001…010`, and the constitution in `.specify/memory/constitution.md` (v3.0.1).

### 3.2 Frontend — `D:\link-dev\talabat-frontend`

| Project (`talabat-web/angular.json`) | Type | Purpose |
|---|---|---|
| `customer` | application | Customer SPA (port 4200) |
| `api-customer` | library | Generated OpenAPI client (custom generator, `tools/api-generator.mjs`) |
| `core-auth` | library | `oidc-client-ts` 3.5.0 PKCE adapter, store, guard, interceptor |
| `core-config` | library | Runtime config loader (`/assets/config.json`) |
| `core-http` | library | Correlation-ID and ProblemDetails interceptors, `AppError` |
| `shared-ui` | library | Button, Card, Input, StatusChip, Spinner, EmptyState, ErrorState, DialogService |
| `shared-util` | library | Money pipe, date formatting |

**Stack [V]:** Angular 22.1, TypeScript ~6.0, Tailwind 4.3.3, Angular CDK, Vitest 4, Playwright 1.62.1 + axe-core.

**Missing [V]:** `delivery`, `admin`, and `restaurant` applications; `api-delivery` and `api-admin` libraries.

**Uncommitted work [V]:** 27 modified files (config generalization, ProblemDetails interceptor, auth/profile/orders pages, OpenAPI tooling) and 24 untracked files (`specs/005-release-hardening/*`, `e2e/*`, `playwright.config.ts`, `.github/workflows/customer-ci.yml`, `docs/talabat-delivery-frontend-roadmap.md`, `tools/verify-customer-bundle.mjs`, `bootstrap-failure.ts`). **This work exists only on this disk.**

**Stray file:** `projects/customer/src/app/features/catalog/components/test/test.ts`, a CLI-scaffolded `Test` component with no references.

---

## 4. Architecture overview

### 4.1 Dependency graph [V]

```text
Talabat.Customer.API ─┐
Talabat.Delivery.API ─┼─► Talabat.Application ─► Talabat.Domain ─► Microsoft.Extensions.Identity.Stores (sanctioned)
Talabat.Admin.API ────┤          ▲
Talabat.Identity ─────┘          │
        └──────────► Talabat.Infrastructure (EF Core, ASP.NET Identity stores, repos, UoW)
```

Every host references Application and Infrastructure. Architecture tests assert that Domain and Application carry no EF/ASP.NET Core/Duende references and that Delivery.API and Application do not reference `Talabat.Identity` ([`tests/Talabat.ArchitectureTests`](tests/Talabat.ArchitectureTests)).

### 4.2 Bounded contexts and aggregates [V]

| Context (folder) | Aggregate root | Children / VOs | Notes |
|---|---|---|---|
| Users (`Aggregates/Users`) | `User : IdentityUser<int>` | `UserAddress`; enums `UserType` [Flags: Customer, DeliveryAgent, Admin, RestaurantOwner], `AgentApprovalStatus`, `DeliveryAgentStatus`, `VehicleType`; `GeoLocation` | One unified identity + profile + capability aggregate ("unified User refactor", specs 004–006) |
| Catalog (`Aggregates/Catalog`) | `Restaurant` | `Product`, `TimeRange`, `Address` (pickup) | No ownership (`OwnerUserId` absent), no write API |
| Basket (`Aggregates/Basket`) | `Cart` | `CartItem`, `CartStatus` (Active/CheckedOut/Abandoned/Expired) | Single-restaurant cart with expiry |
| Ordering (`Aggregates/Ordering`) | `Order` | `OrderItem`, `DeliveryAddressSnapshot`, `Money` | **Immutable record with no status** |
| DeliveryManagement | `Delivery` | `DeliveryStatus` (8 states), pickup/drop-off snapshots, `RowVersion` | References Order/Customer/Restaurant/Agent by ID |

**Domain services [V]:** `CheckoutDomainService` (cart + restaurant validation → `CheckoutSucceeded | CheckoutProductsUnavailable`), and `DeliveryAssignmentDomainService` (assign / complete / cancel / fail, coordinating the `Delivery` and `User` busy/available state).

**Domain events [V]:** none. There is no `IDomainEvent`, dispatcher, outbox, or MediatR. This was a deliberate deferral (plan D10).

### 4.3 Application layer [V]

Handlers are plain classes registered in [`Talabat.Application/DependencyInjection.cs`](src/Talabat/Talabat.Application/DependencyInjection.cs) and return `UseCaseResult<T>` with `ApplicationError` (code + category → HTTP status mapped in each host's `UseCaseResultExtensions`). Feature folders: `Basket`, `Catalog`, `Customers`, `Ordering`, `Deliveries/CreateForOrder`, `DeliveryAgents/*` (16 use cases). Abstractions: `IClock`, `ICurrentUser`, `ICurrentUserCapabilityResolver`, `IRestaurantLocalTimeProvider`, `IUserCapabilityService`.

### 4.4 Infrastructure [V]

`TalabatDbContext` (an Identity DbContext over the unified `User`), configurations per aggregate, `AuditableEntitySaveChangesInterceptor`, 5 repositories, `UnitOfWork` (maps `DbUpdateConcurrencyException` and SQL 2601/2627 to `ConcurrencyConflictException`), `UserCapabilityService` (registration/approval/suspension, role sync, security-stamp refresh), `IdentityDataSeeder` (roles), `CatalogSeedData` (2 restaurants, 4 products), and the `Development/E2E/E2EProvisioner`.

**Migrations (4) [V]:** `20260719103927_InitialUnifiedUser`, `20260727122837_AddDeliveryRowVersion`, `20260807105019_AddCartExpiredStatus`, `20260817142818_AddDeliveryOperationalContract`.

### 4.5 Authentication and authorization [V]

- **IdP:** `Talabat.Identity`. Code+PKCE clients `talabat-customer-spa` (4200), `talabat-delivery-spa` (4300), `talabat-admin-spa` (4400). Scopes `customer.api`, `delivery.api`, `admin.api`; 900 s access tokens, sliding refresh tokens. `TalabatProfileService` emits `role` and capability IDs.
- **APIs:** JWT bearer with `MapInboundClaims = false`, explicit issuer/audience validation, `RoleClaimType = "role"`. Policies combine a scope requirement, a role, and a **persisted-capability check** (`CurrentUser` resolves `UserType` from the database; `OperationalDeliveryAgentHandler` rejects suspended agents; `PersistedAdminAccessHandler` re-checks Admin capability).
- **Ownership:** identity always derives from the `sub` claim. `me`-style routes never accept user IDs. A BOLA regression test exists ([`tests/Talabat.Delivery.API.Tests/BolaRegressionTests.cs`](tests/Talabat.Delivery.API.Tests/BolaRegressionTests.cs)).

---

## 5. Current development position

### 5.1 Timeline from git history [V]

| Dates | Milestones (commit) |
|---|---|
| 2026-07-04 → 07-11 | Basket, Customer/Ordering aggregates, Delivery logic, clean domain, application use cases, infrastructure (`f1a109d` → `e8f1ea7`) |
| 07-14 → 07-18 | Duende + ASP.NET Identity (`72458dc`, `815f4d4`), Customer API (`8ad1aa6` = `main`) |
| 07-18 → 07-21 | Unified `User` aggregate refactor Phases 1–3, "313 tests green" (`6caf9dd`…`858fd28`), Delivery Agent API (`ca6e095`) |
| 07-25 → 07-27 | Token/claims/scopes (`67bfa6e`), login UI, Phase 10 remediation (`d604f1e`, `75a7d00`, `6f5eaf6`) |
| 07-28 → 08-09 | Register page, OpenAPI contracts + generator (`3ebee68`…`18b105e`), expired-cart and default-address concurrency fixes (`357317c`, `5537331`) |
| **08-10** | Delivery readiness **Phase 1** — applicant self-service (`216dd9c`) |
| **08-17** | **Phase 2+3** — Admin API host + agent status read (`9b29fff`); **Phase 4** — pickup-ready operational contracts + migration (`333bb1d`); **extra** — admin suspend/reactivate (`896bd2f`, last commit) |
| 08-09 (frontend) | Last frontend commit `389f254`. Release-hardening work after this date is uncommitted |

### 5.2 Evidence of the last active work

- **Backend [V]:** HEAD `896bd2f` (2026-08-17 18:12). Phases 1–4 of [`docs/delivery-backend-readiness-plan.md`](docs/delivery-backend-readiness-plan.md) map exactly to the last four commit subjects; the plan even pre-wrote those commit messages. **Phase 5 is not implemented:** [`E2EProvisioningCommand.cs`](src/Talabat/Talabat.Identity/E2EProvisioningCommand.cs) only knows `prepare`, `make-negative-product-unavailable`, and `restore` (all Customer fixtures). There is no `prepare-delivery`/`restore-delivery`, and `admin-api.json` was not propagated to the frontend.
- **Frontend [V]:** `.specify/feature.json` → `specs/005-release-hardening`; 36 tasks checked and 3 open (T036 manual address acceptance, T038 runbook finalization, T039 the ordered Phase 11 gate). The last Playwright run (2026-08-10 16:17) failed 3/3 for **environmental** reasons: browser binaries were missing, and `https://localhost:7056/health` refused the connection.
- **Stale "active increment" pointer [V]:** `AGENTS.md` and `.specify/feature.json` still name *Phase 10 Remediation* (`specs/010-phase-10-remediation`), although work had moved on to the delivery readiness plan.
- **Machine migration [I, strongly supported]:** the committed connection strings target `DESKTOP-5IHGJ9F\SQLEXPRESS`; this host is `DESKTOP-017RFMR`. The repos' owner SID differs from the current user, the Playwright artefacts reference `C:\Users\ABDO`, and there is no `Talabat` database on the local instance and no Node.js.

### 5.3 Where to resume

The plan's own gate says: *"Do not start the Delivery Angular frontend until all five phase exit criteria pass, the Delivery/Admin OpenAPI documents are committed, and the generated frontend clients can be reproduced."* Phases 1–4 are done; **Phase 5 is the next planned increment.** This audit recommends a short stabilization pass first ([§17 Phase A](#phase-a--restore-baseline-and-fix-confirmed-defects-p0)), because several confirmed defects (Admin not in CI, vulnerable package, 25 failing tests, non-atomic delivery creation) would undermine Phase 5's own "all focused tests pass" exit criterion.

---

## 6. Implementation status by role and module

### 6.1 Customer

| Capability | Backend (route → handler) | Frontend | Status |
|---|---|---|---|
| Browse restaurants | `GET /api/catalog/restaurants` → `BrowseRestaurantsHandler` | `catalog/pages/restaurant-list.page.ts` | `PARTIAL`. Works, but `page`/`pageSize` are ignored and `totalCount = items.Count` ([`CatalogController.cs:27-45`](src/Talabat/Talabat.API/Controllers/CatalogController.cs)) — `DEFECT CONFIRMED` |
| Restaurant menu | `GET /api/catalog/restaurants/{id}/menu` → `GetRestaurantMenuHandler` | `restaurant-menu.page.ts` | `COMPLETE` |
| Search / filter / categories | — | — | `NOT IMPLEMENTED` |
| Register / sign-in | Identity Razor `/auth/register`, `/auth/login`; OIDC PKCE | `core-auth`, `signin-callback.page.ts` | `COMPLETE` |
| Profile create/get/update | `POST/GET/PUT /api/me/profile` (`CustomerScopeOnly`, `[RequireCustomerProfile]`) | `onboarding`, `profile.page.ts` | `COMPLETE` |
| Addresses | `POST /api/me/addresses`, `DELETE /{id}`, `PUT /{id}/default` | `profile.page.ts`, `address-form-modal.component.ts` | `COMPLETE`. List comes from the profile; there is no address **edit** endpoint |
| Cart | `GET/DELETE /api/me/cart`, `POST /items`, `PUT/DELETE /items/{productId}` | `cart.store.ts`, `cart.page.ts`, cross-restaurant dialog | `COMPLETE` (expiry handling fixed in `357317c`) |
| Checkout | `POST /api/me/checkout` → `CheckoutHandler` (+ delivery creation in controller) | `checkout.page.ts`, `checkout.store.ts` | `PARTIAL`. Order creation works; delivery creation is non-atomic (`DEFECT CONFIRMED`, [§7.4](#74-integration-with-orders-restaurants-and-customers)) |
| Unavailable-item recovery | 422 `CheckoutUnavailableResponse` | handled in checkout | `COMPLETE` |
| Order history | `GET /api/me/orders` | `orders.page.ts` | `PARTIAL`. `ItemCount` is hardcoded `0` and rendered as "0 items"; paging is ignored — `DEFECT CONFIRMED` |
| Order details / confirmation | `GET /api/me/orders/{id}` | `order-confirmation.page.ts` | `COMPLETE`. The UI hardcodes "Order confirmed" and "Cash on Delivery" labels with no backend basis |
| Order / delivery tracking | — (no delivery status in order DTOs) | — | `NOT IMPLEMENTED` |
| Cancel order | — | — | `NOT IMPLEMENTED` |
| Online payment | — (no payment model; COD implied only by UI copy) | — | `NOT IMPLEMENTED` |
| Delivery fee, tax, service fee, promo | — (`Order.TotalAmount` = sum of lines) | — | `NOT IMPLEMENTED` |
| Ratings / reviews | — | — | `NOT IMPLEMENTED` |
| Notifications (email/push/in-app) | — | — | `NOT IMPLEMENTED` |

### 6.2 Delivery Agent

See the deep-dive in [§7](#7-delivery-backend-deep-dive). Summary:

| Capability | Backend | Frontend | Status |
|---|---|---|---|
| Apply (register as applicant) | Identity Razor register (client-derived variant), `POST /account/register/delivery-agent` | — | Backend `COMPLETE` / FE `NOT IMPLEMENTED` |
| Applicant application/profile | `GET /api/agent/me/application`, `GET/PUT /api/agent/me/profile` | — | Backend `COMPLETE` / FE `NOT IMPLEMENTED` |
| Availability (status read, online/offline) | `GET /api/agent/status`, `PUT /online`, `PUT /offline` | — | Backend `COMPLETE` / FE `NOT IMPLEMENTED` |
| Pending queue + self-assignment | `GET /api/agent/deliveries/pending`, `POST /{id}/assign` | — | Backend `COMPLETE` / FE `NOT IMPLEMENTED` |
| Lifecycle | `arrived-at-restaurant`, `picked-up`, `out-for-delivery`, `delivered`, `cancel`, `fail` | — | Backend `COMPLETE` (cancel/fail downstream effects `NOT IMPLEMENTED`) / FE `NOT IMPLEMENTED` |
| Active delivery / history | `GET /active`, `GET /history` | — | Backend `COMPLETE` (unpaginated) / FE `NOT IMPLEMENTED` |
| Location | `PUT /api/agent/location` | — | Backend `COMPLETE` (write-only; no tracking policy) / FE `NOT IMPLEMENTED` |
| Earnings, ratings, navigation, proof of delivery | — | — | `NOT IMPLEMENTED` |

### 6.3 Admin

| Capability | Backend | Frontend | Status |
|---|---|---|---|
| List / inspect agent applications | `GET /api/delivery-agent-applications[?status=]`, `GET /{userId}` | — | Backend `COMPLETE`, **but not in solution/CI** (`DEFECT CONFIRMED`) |
| Approve / reject | `POST /{userId}/approve`, `/reject` | — | Backend `COMPLETE` (as above) |
| Suspend / reactivate agent | `POST /{userId}/suspend`, `/reactivate` | — | Backend `COMPLETE` (as above); busy agents cannot be suspended |
| Legacy approve/reject on Identity | `POST /account/delivery-agents/{id}/approve|reject` (Development-only, unauthenticated) | — | `PARTIAL`. Should be retired now that the Admin API exists (readiness plan Phase 2 said so) |
| First admin bootstrap | Manual SQL procedure ([`docs/admin-first-user-bootstrap.md`](docs/admin-first-user-bootstrap.md)) | — | `PARTIAL`. No tooling |
| Restaurant / catalog management | — | — | `NOT IMPLEMENTED` |
| User / customer management | — | — | `NOT IMPLEMENTED` |
| Order & delivery oversight (reassign, force-cancel, resolve failed) | — | — | `NOT IMPLEMENTED` |
| Reporting / dashboards | — | — | `NOT IMPLEMENTED` |
| Admin SPA (client `talabat-admin-spa`, port 4400 is registered) | — | — | `NOT IMPLEMENTED` |

### 6.4 Restaurant Owner

| Capability | Status |
|---|---|
| `RestaurantOwner` flag in `UserType` and seeded `RestaurantOwner` role (`IdentityDataSeeder`) | `PARTIAL` (enum value and role exist; no policy, client, scope, or use case) |
| Restaurant ownership (`Restaurant.OwnerUserId`) | `NOT IMPLEMENTED` (grep: zero matches) |
| Menu/product/opening-hours management | `NOT IMPLEMENTED`. Domain methods `AddProduct`, `UpdateProductPrice`, `Mark…Available` exist, but no repository write path or API |
| Incoming orders, accept/reject, preparing, ready-for-pickup | `NOT IMPLEMENTED` (no `Order` status; planned `OrderFulfillment` aggregate absent) |
| Restaurant API host + SPA | `NOT IMPLEMENTED` |

### 6.5 Cross-cutting

| Area | Status | Evidence |
|---|---|---|
| Authentication (OIDC/PKCE, JWT) | `COMPLETE` (dev) / `PARTIAL` (prod) | Production signing key is a `TODO` ([`Talabat.Identity/Program.cs`](src/Talabat/Talabat.Identity/Program.cs)) |
| Role + scope + persisted-capability authorization | `COMPLETE` | Policies in each host `Program.cs`; real-token pipeline tests |
| Error handling (ProblemDetails + `errorCode`) | `COMPLETE` | `DomainExceptionHandler`, `UseCaseResultExtensions` per host |
| Validation | `PARTIAL` | Domain guards and `[ApiController]` model validation; no request-validation layer |
| Logging | `PARTIAL` | `ILogger` used in 2 files total (`CheckoutController`, `TalabatSignInManager`); no structured logging |
| Observability (metrics/tracing) | `NOT IMPLEMENTED` | No OpenTelemetry |
| Health checks | `PARTIAL` | `/health` + DbContext check on Customer and Delivery APIs; none on Admin or Identity |
| Rate limiting | `NOT IMPLEMENTED` | No `AddRateLimiter` |
| Pagination | `DEFECT CONFIRMED` / `NOT IMPLEMENTED` | Fake on catalog/orders; absent on pending/history/admin lists |
| Concurrency control | `COMPLETE` (Delivery, User) / `PARTIAL` elsewhere | `RowVersion` on `Delivery` and `User`; filtered unique index on one active delivery per agent |
| Domain events / integration events / outbox | `NOT IMPLEMENTED` | — |
| Payments | `NOT IMPLEMENTED` | — |
| Notifications / real-time (SignalR) | `NOT IMPLEMENTED` | — |
| Caching / performance | `NOT IMPLEMENTED` | Sync-over-async capability lookup on every request (see [§12](#12-security-assessment)) |

### 6.6 Feature status at a glance

```text
                    Backend                 Frontend
Customer            ███████████░░░░ ~55%    ███████████░░░░ ~75% (uncommitted hardening)
Delivery Agent      ██████████░░░░░ ~69%    ░░░░░░░░░░░░░░░   0%
Admin               ███░░░░░░░░░░░░ ~17%    ░░░░░░░░░░░░░░░   0%
Restaurant Owner    ░░░░░░░░░░░░░░░   0%    ░░░░░░░░░░░░░░░   0%
```

### 6.7 Completion estimate: method and limitations

- **Method:** for each role/tier, I listed the capabilities a portfolio-grade, production-oriented food-delivery MVP needs (the rows in §6.1–6.4) and scored `COMPLETE` = 1, `PARTIAL` = 0.5, everything else = 0, unweighted. Customer backend: 6.5/12. Delivery backend: 11/16. Admin backend: 1/6. Owner: 0. Customer frontend: 7.5/10. The overall ≈30 % is the unweighted mean of the eight role×tier cells plus a cross-cutting/deployment cell scored at ≈40 %.
- **Limitations:** capabilities are not weighted by effort (a Restaurant Owner context is far larger than "address edit"), the MVP feature list is the auditor's judgement, and frontend scores rest on code reading plus historical logs, not a run executed during this audit. Treat these numbers as orientation, not commitments.

---

## 7. Delivery backend deep-dive

### 7.1 Domain model [V]

[`Delivery.cs`](src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/Delivery.cs) is a well-guarded state machine:

```text
PendingAssignment ──assign──► Assigned ──arrive──► ArrivedAtRestaurant ──pickup──► PickedUp ──out──► OutForDelivery ──deliver──► Delivered
        │                        │                         │                         │                      │
        └──cancel (no agent)     └────cancel (agent)───────┘                         └──────fail (any non-terminal)──► Failed
             ▼                                ▼
         Cancelled                        Cancelled          (cancel forbidden once PickedUp/OutForDelivery)
```

- The invariants it enforces: single assignment, assigned-agent ownership (`DeliveryAgentMismatchException`), strict step ordering, terminal-state immutability, monotonic UTC timestamps (with a documented `DateTimeKind.Unspecified` restoration for `datetime2`), and a required failure reason.
- Immutable snapshots: `RestaurantName`, `RestaurantPickupAddress`, `DeliveryAddress` (added in `333bb1d`).
- Agent state lives on `User`: `Offline → Available ⇄ Busy`, `Suspended` (admin), with `GoOnline`/`GoOffline`/`MarkBusy`/`MarkAvailable`/`Suspend`/`Reactivate`/`UpdateLocation` (location allowed only while Available or Busy).
- [`DeliveryAssignmentDomainService`](src/Talabat/Talabat.Domain/DomainServices/DeliveryManagement/DeliveryAssignmentDomainService.cs) coordinates `Delivery` and `User`: assign → agent Busy; deliver/cancel/fail → agent Available. Cancel/Fail on the aggregate throw `DeliveryAgentCoordinationRequiredException` when an agent is assigned, so the agent must be released through the service.

### 7.2 Application and API [V]

| Use case | Handler | Route (`Talabat.Delivery.API`) | Policy |
|---|---|---|---|
| Applicant application | `GetDeliveryApplicantProfileHandler` | `GET /api/agent/me/application` | `DeliveryApplicantAccess` (scope only) |
| Applicant profile get/update | `Get…`/`UpdateDeliveryApplicantProfileHandler` | `GET/PUT /api/agent/me/profile` | `DeliveryApplicantAccess` |
| Agent status | `GetCurrentDeliveryAgentStatusHandler` | `GET /api/agent/status` | `DeliveryAgentAccess` |
| Online / offline | `GoOnlineHandler` / `GoOfflineHandler` | `PUT /api/agent/status/online|offline` | `DeliveryAgentAccess` |
| Pending queue | `GetPendingDeliveriesHandler` | `GET /api/agent/deliveries/pending` | `OperationalDeliveryAgentAccess` |
| Claim | `AssignDeliveryAgentHandler` | `POST /api/agent/deliveries/{id}/assign` | Operational |
| Lifecycle (×4) | `ArrivedAtRestaurant…`, `PickUpOrder…`, `OutForDelivery…`, `DeliverOrder…` | `POST /{id}/arrived-at-restaurant|picked-up|out-for-delivery|delivered` | Operational |
| Cancel / fail | `CancelDeliveryHandler`, `FailDeliveryHandler` | `POST /{id}/cancel`, `POST /{id}/fail` | Operational |
| Active / history | `GetActiveDeliveryHandler`, `GetDeliveryHistoryHandler` | `GET /active`, `GET /history` | Operational |
| Location | `UpdateLocationHandler` | `PUT /api/agent/location` | Operational |

All agent IDs come from `ICurrentUser.AgentId` through `TryGetAgentIdHelper`. Handlers receive IDs in commands; none injects `ICurrentUser` [V] (Phase 10 tasks T047–T053 are done although unchecked).

**Data minimization [V]:** the pending queue exposes `Id, OrderId, RestaurantId, Status, RestaurantName, PickupStreet, PickupCity, CreatedAt`, with no customer data. The active delivery adds the drop-off address and `CustomerPhoneNumber`.

### 7.3 Persistence and concurrency [V]

- `Deliveries.RowVersion` (`IsRowVersion`) plus a **unique index on `OrderId`** and a **filtered unique index** on `AssignedAgentId WHERE Status IN (2,3,4,5) AND IsDeleted = 0`, which enforces "one active delivery per agent" (BR-DEL-004) at the database level.
- `UnitOfWork` turns concurrency exceptions and unique-key violations into `ConcurrencyConflictException` → HTTP 409.
- A status check constraint and the snapshot columns arrive in migration `AddDeliveryOperationalContract`.

### 7.4 Integration with orders, restaurants, and customers

| Integration | Finding | Label |
|---|---|---|
| Order → Delivery creation | [`CheckoutController.TryCreateDeliveryAsync`](src/Talabat/Talabat.API/Controllers/CheckoutController.cs) runs **after** `CheckoutHandler` commits the order and cart. A returned `UseCaseResult.Failure` is ignored (not even logged); exceptions are caught and logged. There is no retry, reconciliation job, or outbox, so an order without a delivery is never dispatched. Orchestrating in the controller also puts business workflow in the API layer. (Spec BR-DEL-016 deliberately says a delivery failure must not invalidate the order, but it does not license silent loss.) | `DEFECT CONFIRMED` |
| Restaurant readiness | Deliveries enter `PendingAssignment` immediately at checkout, so couriers can claim, "arrive", and "pick up" orders that no restaurant has accepted or prepared. There is no restaurant-side signal at all. | `NOT IMPLEMENTED` (blocked on Order lifecycle / Restaurant Owner) |
| Delivery → Order feedback | `Delivered`/`Cancelled`/`Failed` never update anything on the order (it has no status), and the customer is never told. | `NOT IMPLEMENTED` |
| Cancel semantics | An agent "cancel" before pickup **terminates the delivery** rather than releasing it back to the queue. Nothing re-dispatches the order, and no admin/ops flow handles cancelled or failed deliveries. | `PARTIAL` (business-rule gap) [I] |
| Customer visibility | `OrderDetails` and `OrderSummary` contain no delivery fields; `IDeliveryRepository` has no customer-facing query. | `NOT IMPLEMENTED` |
| Dispatch | Pull model only (agents self-assign, FIFO). No proximity, auto-assignment, or timeouts for unclaimed deliveries. Restaurant coordinates are not stored. | `PARTIAL` (acceptable for MVP) |
| Real-time | No SignalR/WebSocket/push. Agent location is write-only. | `NOT IMPLEMENTED` |

### 7.5 Delivery readiness plan status (`docs/delivery-backend-readiness-plan.md`)

| Plan phase | Commit | Status |
|---|---|---|
| 1 — Applicant contract | `216dd9c` | `COMPLETE` |
| 2 — Admin approval slice | `9b29fff` | `COMPLETE` in code. **Admin projects missing from `Talabat.slnx`**, and Identity dev-only approval routes not retired → `PARTIAL` overall |
| 3 — Agent runtime state | `9b29fff` | `COMPLETE` |
| 4 — Operational contract (pickup snapshot, PII trim, location tightening) | `333bb1d` | `COMPLETE` |
| (unplanned) Admin suspend/reactivate | `896bd2f` | `COMPLETE` |
| 5 — Test & contract readiness (delivery fixtures, published contracts) | — | `NOT IMPLEMENTED` |

### 7.6 Delivery business rules (`docs/delivery/delivery-business-rules.md`) vs code

| Rule | Enforced by | Status |
|---|---|---|
| BR-DEL-001 Delivery created after checkout | `CheckoutController` (non-atomic) | `PARTIAL` |
| BR-DEL-002 Starts `PendingAssignment` | `Delivery` ctor | `COMPLETE` |
| BR-DEL-003 Only available agents assigned | Domain service `IsAvailable()` | `COMPLETE` |
| BR-DEL-004 One active delivery per agent | Busy state + filtered unique index | `COMPLETE` |
| BR-DEL-005 Assigned once | `AssignAgent` + RowVersion | `COMPLETE` |
| BR-DEL-006…009 Ordering / terminal immutability | `EnsureStatus`, `EnsureNotTerminal` | `COMPLETE` |
| BR-DEL-010/011/015 Busy on assign, released on terminal | Domain service | `COMPLETE` |
| BR-DEL-012/013 Snapshots, ID references | Aggregate design | `COMPLETE` |
| BR-DEL-014 Location optional | `CurrentLocation` nullable + paired-null check | `COMPLETE` |
| BR-DEL-016 Delivery failure doesn't invalidate order | Controller try/catch | `COMPLETE` as written, but enables silent loss (see §7.4) |

---

## 8. Frontend / backend integration status

| Contract | Backend document | Frontend copy | Generated client | Status |
|---|---|---|---|---|
| Customer API | `src/Talabat/Talabat.API/openapi/customer-api.json` (sha256 `3b1d23a3…`) | `talabat-web/openapi/customer-api.json` (`3b1d23a3…`) | `projects/api-customer` | `COMPLETE` (byte-identical) |
| Delivery API | `…/Talabat.Delivery.API/openapi/delivery-api.json` (`63df9d82…`) | `talabat-web/openapi/delivery-api.json` (`d0d6016e…`, the 2026-08-10 snapshot) | none | `DEFECT CONFIRMED` (stale) |
| Admin API | `…/Talabat.Admin.API/openapi/admin-api.json` (`056dbbc4…`) | absent | none | `NOT IMPLEMENTED` |

**Customer SPA ↔ Customer API.** Every customer route in §6.1 has a matching feature. Auth is robust: PKCE, in-memory user store, the token attached only for same-origin `/api/me` paths (`core-auth/src/lib/auth.interceptor.ts`), and ProblemDetails normalization with a correlation ID. The runtime config (`projects/customer/public/assets/config.json`) points to `https://localhost:7237` / `:7056`.

**Known integration gaps.**

1. Order history shows "0 items" (backend `ItemCount = 0`).
2. Order confirmation hardcodes "Order confirmed" and "Cash on Delivery" without backend backing.
3. There is no delivery tracking to show.
4. Paging metadata from the API is fictional.

**Frontend readiness for more apps [V, per the frontend's own roadmap and code].** `core-config` requires a Customer-named `customerApiBaseUrl`, the auth interceptor protects only `/api/me`, and `core-http` embeds Customer error codes. The uncommitted hardening diff starts generalizing `provide-app-config.ts` (+87 lines) and the ProblemDetails interceptor. Most `.tal-*` primitive CSS lives in the Customer global stylesheet, and the Customer app imports Google Fonts remotely.

**Delivery frontend roadmap staleness.** [`talabat-frontend/docs/talabat-delivery-frontend-roadmap.md`](../talabat-frontend/docs/talabat-delivery-frontend-roadmap.md) (untracked, written against backend `5537331`) lists applicant status/profile, status read, and restaurant pickup data as *blockers*. All three were resolved by backend commits `216dd9c`, `9b29fff`, and `333bb1d`. Its "Phase G" gate can now proceed once the contract is re-copied.

---

## 9. DDD and Clean Architecture compliance

| Principle | Assessment | Evidence |
|---|---|---|
| Dependency rule | **Strong** | Architecture tests (20/20 pass) ban EF/ASP.NET Core/Duende from Domain/Application and `Talabat.Identity` from Application/Delivery.API |
| Rich aggregates with guarded invariants | **Strong** | `Delivery`, `Cart`, `User`, `Restaurant` use private setters, `Guard`, and domain exceptions; `internal` transitions (`MarkBusy`, `CancelAssigned`) force use of the domain service |
| Aggregate references by ID + snapshots | **Strong** | `Delivery`/`Order` hold IDs and immutable address snapshots |
| Repository per aggregate root; interfaces in Domain | **Good** | `Talabat.Domain/Interfaces/*` |
| Domain ↔ Identity coupling | **Sanctioned exception** | `User : IdentityUser<int>` and Domain references `Microsoft.Extensions.Identity.Stores`. Constitution v3.0.1 explicitly allows this; [`AGENTS.md`](AGENTS.md) says Domain must be "100 % free of … ASP.NET Core Identity", which **contradicts** it. Reconcile the wording [R] |
| Ubiquitous language / bounded contexts | **Partial** | Contexts exist as folders in a single model and DbContext (fine for a modular monolith). Ordering lacks its core concept (order status); "RestaurantOperations" is planned but absent |
| Application orchestration | **Weak in one place** | Checkout→Delivery workflow lives in the controller (§7.4) |
| Domain events | **Absent** | Cross-aggregate reactions (delivered → order completed, cancelled → re-dispatch/notify) have no mechanism; integration is ad hoc |
| CQRS-lite reads | **Partial** | Reads load aggregates via repositories; no projection/read-model layer (planned D9) |
| Host duplication | **Debt** | `ScopeHandler`, `CurrentUser`, `UseCaseResultExtensions`, `DomainExceptionHandler`, and `NumericSchemaTypeOpenApiVisitor` are copy-pasted across 3 hosts (plan D11 proposes `Talabat.Api.Shared`) |

**Verdict:** the tactical DDD and layering are above average for a portfolio project. The strategic gap is that the **Ordering context is anemic** (no lifecycle) and **cross-context workflows lack an event or orchestration mechanism**. Both must be addressed before adding Restaurant Owner and tracking features.

---

## 10. Database assessment

| Aspect | Finding | Label |
|---|---|---|
| Engine / provider | SQL Server via EF Core 10.0.9; `TalabatDbContextFactory` for design time | `COMPLETE` |
| Migrations | 4 linear migrations; model snapshot present | `COMPLETE` |
| Integrity | Check constraints (user enums, lat/long ranges, paired-null location, cart status, delivery status, order amounts), FKs to `AspNetUsers`, unique `Deliveries.OrderId`, filtered unique active-agent index | `COMPLETE` |
| Concurrency | `RowVersion` on `Delivery` and `User`; none on `Order`, `Cart`, or `Restaurant` | `PARTIAL` |
| Auditing / soft delete | `AuditableEntity` + `AuditableEntitySaveChangesInterceptor`; `ISoftDeletable` on User | `COMPLETE` |
| Seed data | 2 restaurants, 4 products with demo pickup addresses (`CatalogSeedData`) | `PARTIAL` (demo only) |
| Indexing for scale | `Orders(CustomerId, CreatedAt)`; pending queue filters on `Status` with no dedicated index [I] | `NEEDS VERIFICATION` |
| Environment config | Machine-specific server name `DESKTOP-5IHGJ9F\SQLEXPRESS` committed in every host's `appsettings.json`; no `Talabat` database on the current machine | `BLOCKED` for local runs until reconfigured |
| Stale test | `ConstraintPersistenceTests.One_migration_history_row_exists` asserts exactly one migration, but four exist | `DEFECT CONFIRMED` (test) |

---

## 11. API assessment

| Host | Endpoints | OpenAPI | Health | CORS | Notes |
|---|---|---|---|---|---|
| Customer API | 16 | Committed + OAuth2 scheme; runtime-vs-committed parity tests | `/health` | Hardcoded `http://localhost:4200` | Fake paging; `ItemCount = 0` |
| Delivery API | 17 | Committed (`delivery-api.json`) | `/health` | Hardcoded `http://localhost:4300` | OAuth URLs hardcoded to `https://localhost:7237` in the document transformer |
| Admin API | 6 | Committed (`admin-api.json`) | **none** | Hardcoded `http://localhost:4400` | Not in solution/CI |
| Identity | 7 JSON (`/account/*`) + Razor pages + OIDC endpoints | Dev-only Swagger | none | 7 hardcoded localhost origins | Ad hoc JSON register/login endpoints duplicate the Razor flows |

**Conventions [V]:** RFC 7807 ProblemDetails with a root `errorCode`; string enums; `int` IDs in routes; 201/204/409/422 declared explicitly. **Gaps:** no API versioning, no idempotency keys on checkout (a double submit can create two orders, mitigated only by cart status [I]), no rate limiting, no pagination.

---

## 12. Security assessment

| # | Finding | Severity [I] | Label |
|---|---|---|---|
| S1 | No production signing credential (`else { // TODO … }` in `Talabat.Identity/Program.cs`). Outside Development, IdentityServer has no key material configured | High (deploy blocker) | `DEFECT CONFIRMED` |
| S2 | `POST /account/login` (JSON) uses `lockoutOnFailure: false`, which bypasses the lockout the Razor login applies. The JSON `register/*`, `login`, and `logout` endpoints are reachable in every environment, with no rate limiting | High | `DEFECT CONFIRMED` |
| S3 | All redirect URIs, CORS origins, API URLs, and the authority are hardcoded to `localhost` (Swagger redirect URIs are marked "remove before production") | Medium (deploy blocker) | `PARTIAL` |
| S4 | Development-only, **unauthenticated** approve/reject routes remain in `AccountController` (gated by `IsDevelopment()`); the Admin API now supersedes them | Medium | `PARTIAL` |
| S5 | SSH.NET 2025.1.0 High advisories (GHSA-q939-rpr3-3284, GHSA-mggc-4xg6-vcxf) in two **test** projects. Production assemblies are clean | Low runtime / High CI | `DEFECT CONFIRMED` |
| S6 | `CurrentUser` resolves capabilities with `.GetAwaiter().GetResult()` (sync-over-async DB call on every request, in both Customer and Delivery `CurrentUser.cs:110`), a thread-pool starvation risk under load | Medium (availability) | `DEFECT CONFIRMED` (debt) |
| S7 | Duende IdentityServer requires a commercial license for production use beyond its community-edition terms; no license key is configured | — | `NEEDS VERIFICATION` (business decision) |
| S8 | Strengths: PKCE, short-lived access tokens, issuer/audience validation, `MapInboundClaims = false`, persisted-capability re-checks, security-stamp refresh on approval, BOLA regression test, in-memory SPA token storage | — | `COMPLETE` |
| S9 | No HSTS / security headers, no request-size limits, no audit logging of admin actions | Medium | `NOT IMPLEMENTED` |

---

## 13. Testing assessment

### 13.1 Results of this audit run

Command: `dotnet test tests/<Project> --no-build` (Debug), run per project with environment variable `ConnectionStrings__TalabatDb=Server=.\SQLEXPRESS;Database=Talabat;Trusted_Connection=True;TrustServerCertificate=True`. Infrastructure/Identity fixtures fell back from Testcontainers (no Docker) to LocalDB. TRX files were written to the session scratchpad, not the repo.

| Suite | Passed / Total | Failed | Duration | In CI? |
|---|---|---|---|---|
| `Talabat.Domain.Tests` | **58 / 58** | 0 | 0.4 s | ✅ |
| `Talabat.Application.Tests` | **260 / 260** | 0 | 0.4 s | ✅ |
| `Talabat.ArchitectureTests` | **20 / 20** | 0 | 1 s | ✅ |
| `Talabat.Infrastructure.Tests` | 68 / 69 | 1 | 4 m 34 s | ✅ |
| `Talabat.Identity.Tests` | 61 / 65 | 4 | 4 m 22 s | ✅ |
| `Talabat.Customer.API.Tests` | 67 / 83 | 16 | 20 s | ✅ |
| `Talabat.Delivery.API.Tests` | 58 / 62 | 4 | 16 s | ✅ |
| `Talabat.Admin.API.Tests` | **13 / 13** | 0 | 8 s | ❌ (not in solution) |
| **Total** | **605 / 630 (96.0 %)** | **25** | | |

**Consequence [I]:** by the constitution's own gate ("all test projects in the solution MUST pass"), the branch is **red**. `procced.txt` (2026-08-07) already called the Customer.API failures "known baseline", so the failures predate the last three weeks of work, and commits `9b29fff`/`333bb1d` added more.

**Classification of all 25 failures** (root cause traced in code; none indicates data corruption or an authorization bypass):

| Category | Count | Tests | Root cause | Label |
|---|---|---|---|---|
| Stale test (schema evolved) | 1 | Infra `ConstraintPersistenceTests.One_migration_history_row_exists` | Asserts exactly one migration; 4 exist | `DEFECT CONFIRMED` (test) |
| Stale test (third client added) | 2 | Identity `IdentityServerConfigTests.Both_clients_still_have_postman_v1_{callback,browser_callback}_redirect_uri` | They loop over **all** clients; `9b29fff` added `talabat-admin-spa` without Postman URIs | `DEFECT CONFIRMED` (test) |
| Environment-coupled test | 2 | Identity `AgentApprovalEndpointTests.{Approve,Reject}_outside_development_returns_404` | Boots the real host in `Staging` without replacing the DbContext; the startup role seeder needs an existing `Talabat` database ("Cannot open database \"Talabat\"") | `DEFECT CONFIRMED` (test isolation) |
| Fixture contradiction | 8 | Customer `ProfileNotCreatedShapeTests` ×3, `AuthEnforcementTests.GetCart_Authenticated_WithoutProfile…`, `ErrorMappingTests.CartOperations_WithoutProfile…`, `CustomerEndpointTests.{GetProfile_BeforeCreation…, CreateProfile_EmptyName…, CreateProfile_ValidRequest…}` | The shared `CustomWebApplicationFactory` seeds the default test user (`Id = 1`) **with** a customer profile; these tests assume that user has none (they get 200 / 409 ProfileAlreadyExists) | `DEFECT CONFIRMED` (test) |
| Behaviour drift vs contract | 6 | Customer `OwnershipTests.{Zero,Negative,Malformed}_subject…`, `AuthEnforcementTests.{Zero,Negative,Malformed}Subject…` | An invalid `sub` is treated as *authenticated with no user*; `RequireCustomerProfileAttribute` then returns 404/409 `ProfileNotCreated` instead of 401. No access is granted, but the documented contract is violated | `DEFECT CONFIRMED` (product, low severity) |
| Contract inconsistency | 1 | Customer `OpenApiDocumentQualityTests.ErrorResponses_ReferenceProblemDetails` | 422 returns `CheckoutUnavailableResponse`, not ProblemDetails. **A fix exists on the unmerged branch `origin/copilot/fix-build-and-test-job` (`469aabf`)** | `DEFECT CONFIRMED` (product) |
| Broken token-minting helper | 2 | Customer + Delivery `RealTokenPipelineTests.ExpiredToken_Returns401` | Test mints `Expires` one hour in the past while `NotBefore` defaults to now → `IDX12401` before any request is sent | `DEFECT CONFIRMED` (test) |
| Stale test (Phase 4 rule) | 1 | Delivery `BolaRegressionTests.UpdateLocation_with_extra_agentId…` | `333bb1d` made location updates require `Available`/`Busy`; the test agent is `Offline` → 409. The BOLA property itself is untested until the fixture is fixed | `DEFECT CONFIRMED` (test) |
| Fixture never exercises the race | 1 | Delivery `AssignmentConcurrencyTests.Concurrent_assign…` | Both seeded agents are approved (→ `Offline`) and never go online, so both requests fail `AgentNotAvailable` → `[409, 409]`. RowVersion protection **is** verified at the persistence level by the passing Infra test `Concurrent_agent_assignment_rejected_by_RowVersion` | `DEFECT CONFIRMED` (test); API-level race `NEEDS VERIFICATION` |
| Fixture drift | 1 | Delivery `RealTokenPipelineTests.ValidToken_Returns200` | Real-token auth succeeded (not 401/403), but `GET /api/agent/deliveries/active` returned 404 because the fixture agent has no active delivery | `DEFECT CONFIRMED` (test) |

**Post-run hygiene [V]:** the database list on `.\SQLEXPRESS` matched the pre-run snapshot (system databases only), and LocalDB holds 0 `TalabatTest%` databases.

**Suspected issue found while tracing (not covered by any test) — `NEEDS VERIFICATION`:** `CheckoutController` uses `CustomerAccess` (scope + role only, with no persisted-capability check) and lacks `[RequireCustomerProfile]`, yet dereferences `_currentUser.CustomerId!.Value`. A token carrying the `Customer` role for a user without persisted Customer capability (a stale role claim) would most likely produce a 500 rather than 409 `ProfileNotCreated`.

### 13.2 Coverage observations

- **Strong:** domain invariants (state machines, guards), handler behavior with fakes, the real JWT pipeline (`RealTokenPipelineTests` in Customer and Delivery), authorization matrices, BOLA, OpenAPI runtime-vs-committed parity, SQL Server constraints and migrations, E2E provisioner idempotency.
- **Gaps:** no test asserts that **every** successful checkout produces a delivery under failure; no customer-visible tracking tests (feature absent); Admin tests are excluded from CI; frontend E2E has never passed (environmental); no load or performance tests; test auth handlers still use obsolete `ISystemClock` (Phase 10 T065 open).
- **Frontend (historical, 2026-08-09 logs, not re-run):** customer 32 files / 230 tests passed; core-auth 63, core-http 17, shared-ui 5, shared-util 2 passed; OpenAPI client structural verification passed; production build completed. The last Playwright run (2026-08-10) failed 3/3 for environmental reasons → `NEEDS VERIFICATION`.

---

## 14. CI, deployment, and observability assessment

| Item | Finding | Label |
|---|---|---|
| Backend CI (`.github/workflows/ci.yml`) | restore → build Release → test (SQL Server 2022 service container) → vulnerability gate. Builds `Talabat.slnx` only, so **Admin is excluded**. On the current HEAD the **test step would fail** (25 failures locally; the Identity environment-coupled tests may pass in CI only by accident of the database name) and the vulnerability step **will fail** on SSH.NET. CI history on GitHub was not inspected | `PARTIAL` / `DEFECT CONFIRMED` |
| Frontend CI (`.github/workflows/customer-ci.yml`) | **Untracked** (never pushed). Staged unit tests and a production build. The `live-e2e` job is gated on `workflow_dispatch`/`schedule`, but neither trigger is declared under `on:`, so the job can never run. `pull_request` targets `main` while the repo uses `master` | `DEFECT CONFIRMED` |
| Containers | No `Dockerfile` or `docker-compose` anywhere | `NOT IMPLEMENTED` |
| Deployment / IaC / environments | None. No staging or production configuration; `appsettings.Production.json` absent | `NOT IMPLEMENTED` |
| Secrets management | No user-secrets/Key Vault usage; Windows-auth connection strings in committed JSON (no passwords committed [V]) | `PARTIAL` |
| Observability | No OpenTelemetry, metrics, dashboards, or structured log sinks; default console logging only | `NOT IMPLEMENTED` |
| Branch hygiene | 33-commit feature branch never merged to `main`; a Copilot branch is unmerged | `PARTIAL` |

---

## 15. Findings register

### 15.1 Completed work (highlights) — `COMPLETE`

- The unified `User` aggregate with Identity integration, role sync, and capability resolution (specs 004–006).
- Customer API and SPA: catalog, profile, addresses, cart (expiry), checkout with unavailable-item recovery, order history/details.
- OIDC/PKCE for three SPA clients; scope + role + persisted-capability authorization; real-token tests.
- Delivery Agent backend: applicant self-service, status, queue, claim, full lifecycle, location, history, concurrency, PII minimization.
- Admin API slice for delivery-agent applications, including suspend/reactivate.
- OpenAPI generation and committed contracts for three APIs; the Customer client is generated and verified.
- SQL Server schema with strong constraints; E2E provisioning CLI for Customer fixtures.

### 15.2 Incomplete — `PARTIAL`

Checkout→delivery integration; order history; catalog paging; Admin API CI integration; Identity production hardening; legacy Identity approval routes; frontend release hardening (uncommitted, 3 open tasks); health checks; logging; seed data; first-admin bootstrap tooling.

### 15.3 Missing — `NOT IMPLEMENTED`

Order lifecycle; customer tracking; Restaurant Owner (domain ownership, fulfillment, API, SPA); Admin restaurant/user/order/delivery operations and Admin SPA; Delivery SPA; delivery fixtures (readiness Phase 5); payments; fees/tax/promotions; notifications; real-time; domain events/outbox; search/filter; ratings; rate limiting; observability; containers/deployment; README.

### 15.4 Confirmed defects — `DEFECT CONFIRMED`

| ID | Defect | Location |
|---|---|---|
| D1 | Delivery creation non-atomic; failures silently lost | [`CheckoutController.cs:46-105`](src/Talabat/Talabat.API/Controllers/CheckoutController.cs) |
| D2 | Admin API and tests not in solution → not built or tested by CI | [`src/Talabat/Talabat.slnx`](src/Talabat/Talabat.slnx) |
| D3 | Vulnerable SSH.NET in test projects → CI vulnerability gate fails | `tests/Talabat.Identity.Tests`, `tests/Talabat.Infrastructure.Tests` (transitive) |
| D4 | `ItemCount` hardcoded `0`; UI shows "0 items" | [`OrderController.cs:58`](src/Talabat/Talabat.API/Controllers/OrderController.cs), `orders.page.ts:62` |
| D5 | Paging parameters ignored; `totalCount` wrong | `CatalogController.cs:27-45`, `OrderController.cs:36-61` |
| D6 | No production signing credential | `Talabat.Identity/Program.cs` |
| D7 | JSON login without lockout | `Talabat.Identity/Controllers/AccountController.cs:63-71` |
| D8 | Stale frontend Delivery contract; Admin contract not copied | `talabat-web/openapi/` |
| D9 | Stale test `One_migration_history_row_exists` | `tests/Talabat.Infrastructure.Tests/Persistence/ConstraintPersistenceTests.cs:122-136` |
| D10 | Frontend CI: unreachable `live-e2e` job; PR branch mismatch | `talabat-frontend/.github/workflows/customer-ci.yml` |
| D11 | Sync-over-async in `CurrentUser` | `Talabat.API/Auth/CurrentUser.cs:110`, `Talabat.Delivery.API/Auth/CurrentUser.cs:110` |
| D12 | Invalid `sub` (0, negative, non-numeric) yields 404/409 `ProfileNotCreated` instead of 401 | `Talabat.API/Auth/CurrentUser.cs:98-104`, `Middleware/RequireCustomerProfileAttribute.cs` |
| D13 | 422 checkout response is not ProblemDetails (contract rule violated); fix sits on unmerged branch `origin/copilot/fix-build-and-test-job` (`469aabf`) | `CheckoutController.cs`, `customer-api.json` |
| D14 | 25 failing backend tests: 17 stale/contradictory, 2 broken token helper, 2 environment-coupled, plus the product defects D12/D13. Two important safety nets (API assignment race, BOLA location) do not test what their names say | See §13.1 |

### 15.5 Technical debt

- Host plumbing duplicated across 3 APIs (`Talabat.Api.Shared` not extracted).
- `Talabat.API` folder vs `Talabat.Customer.API` assembly naming.
- Spec checkboxes are unreliable: `specs/005` shows 32 open tasks and `specs/010` shows 25 open, yet code shows most are done. Frontend `specs/004` has 31 unchecked despite commit `9c06f4a`.
- Root-level prompt/scratch files and ignored logs clutter both repos.
- An unused scaffolded `Test` component exists in the Customer SPA.
- `AGENTS.md` scope and the Identity-coupling wording are out of date.
- Test auth handlers use obsolete `ISystemClock` (`#pragma warning disable CS0618`).

---

## 16. Risks, blockers, assumptions, unresolved questions

### Blockers

| Blocker | Blocks | Resolution |
|---|---|---|
| Uncommitted frontend work exists only on this disk | Any frontend change; risk of loss | Commit or branch it immediately |
| No Node.js on this machine | All frontend verification and development | Install Node 24 (as CI specifies) and run `npm ci` |
| Connection strings target another machine | Running hosts locally | Use user-secrets or environment overrides pointing to `.\SQLEXPRESS`; create the dev database via migrations (a deliberate developer action, not done in this audit) |
| No `Order` lifecycle | Restaurant Owner, tracking, correct dispatch, admin oversight | Domain design decision (`OrderFulfillment` aggregate vs order status) |
| Readiness Phase 5 not done | Delivery SPA live E2E | Implement delivery fixtures |
| Identity production key + hosting decisions | Any deployment | Key management, domain names, Duende licensing |

### Risks

- **Silent order loss** (D1) in any real usage.
- **Scope sprawl:** many plans and prompts exist (`PLAN.md`, `PROJECT_IMPLEMENTATION_ROADMAP.md`, the readiness plan, the restaurant-owner plan, frontend roadmaps), and their status claims diverge from the code.
- A long-lived unmerged feature branch (33 commits) increases integration risk.
- Duende licensing cost/terms for a public deployment.

### Assumptions

- "Production-ready" means a deployable portfolio system with all four roles and real hosting, not necessarily live payments.
- Cash on Delivery is the intended payment model for the MVP (inferred only from UI copy; there is no backend artefact).

### Unresolved questions (need product/owner decisions)

1. Should an agent **cancel** release the delivery back to the queue (unassign) instead of terminating it? Who handles `Failed` deliveries?
2. Order lifecycle design: a single `Order.Status`, or the planned separate `OrderFulfillment` aggregate (plan D3)?
3. When should a delivery become claimable: at checkout, on restaurant acceptance, or at "ready for pickup"?
4. Payment scope for the portfolio release: COD only, or a sandbox gateway (e.g. Stripe test mode)?
5. Real-time strategy: polling vs SignalR for customer tracking and the agent queue?
6. Can rejected applicants re-apply? (Explicitly left open by readiness Phase 1.)
7. Duende license, or migrate to OpenIddict for a free production IdP?
8. Should `main` be fast-forwarded to `feature/user-aggregate-refactor` now?

---

## 17. Completion roadmap

Priorities: **P0** = blocking/correctness, **P1** = core MVP, **P2** = production readiness, **P3** = portfolio polish/advanced.

```text
A Baseline & defects ─► B Order lifecycle & integration ─┬─► C Delivery readiness Ph.5 ─► D Delivery SPA
                                                          ├─► E Restaurant Owner (BE ─► SPA)
                                                          └─► F Admin ops (BE ─► SPA)
                 (A also unblocks G) G Production hardening ───────────────► H Deployment & observability ─► I Advanced features
```

### Phase A — Restore baseline and fix confirmed defects (P0)

| Task | Status | Depends on | Modules | Acceptance criteria / Definition of done |
|---|---|---|---|---|
| A1 Commit the frontend hardening work to a branch | PARTIAL | — | talabat-frontend | `git status` clean; branch pushed; `.github/workflows/customer-ci.yml` tracked |
| A2 Local environment: install Node 24, set user-secrets/env for `TalabatDb`, create the dev database via `dotnet ef database update` | BLOCKED | — | all hosts | All 4 hosts start; `/health` returns 200 on Customer and Delivery |
| A3 Add Admin API + tests to `Talabat.slnx` | DEFECT CONFIRMED | — | solution, CI | `dotnet test src/Talabat/Talabat.slnx` lists `Talabat.Admin.API.Tests` |
| A4 Remove the SSH.NET vulnerability (pin a patched version or upgrade Testcontainers) | DEFECT CONFIRMED | — | test csproj | `dotnet list … --vulnerable --include-transitive` reports zero |
| A5 Repair the 25 failing tests (§13.1). Fix the fixtures rather than weakening assertions: a profile-less test user, agents brought online for the race and BOLA tests, a valid `NotBefore` in token helpers, Identity tests isolated from the real DB, migration assertion on the expected set. Fix D12 (invalid `sub` → 401) and merge `469aabf` (D13) | DEFECT CONFIRMED | A3 | all test projects, Customer.API | `dotnet test src/Talabat/Talabat.slnx` = 0 failures; the race test observes exactly one 200 and one 409; the BOLA test reaches the handler with an `Available` agent |
| A6 Fix `ItemCount` and implement real paging (catalog, orders) | DEFECT CONFIRMED | — | Application, Customer.API, OpenAPI, FE client | Correct item counts; `totalCount` = total rows; page/pageSize honoured; regenerated client |
| A7 Make checkout + delivery creation atomic, inside Application (one UoW transaction, or an outbox with a retrying dispatcher) | DEFECT CONFIRMED | — | Application, Customer.API | No path yields an order without a delivery or a durable pending-creation record; a failure-injection test proves it; the controller no longer orchestrates |
| A8 Re-copy `delivery-api.json`, copy `admin-api.json` | DEFECT CONFIRMED | — | talabat-web | Hashes match the backend; `api:verify` passes |
| A9 Fix frontend CI triggers (`master`, `workflow_dispatch`) | DEFECT CONFIRMED | A1 | FE CI | A manual dispatch runs `live-e2e` |
| A10 Update `AGENTS.md` / `feature.json` / spec checkboxes; add a backend README | PARTIAL | — | docs | README describes setup, hosts, ports, and test commands; AGENTS reflects the current increment |
| A11 Merge the feature branch to `main` via PR with green CI | PARTIAL | A3–A5 | git | `main` contains all work; CI green on `main` |

### Phase B — Order lifecycle and cross-context integration (P1)

| Task | Status | Depends on | Acceptance criteria / DoD |
|---|---|---|---|
| B1 Decide and model the order lifecycle (`Placed → Accepted → Preparing → ReadyForPickup → PickedUp → Delivered / Cancelled / Rejected`) as `Order.Status` or an `OrderFulfillment` aggregate | NOT IMPLEMENTED | A7 | Domain tests for every transition; migration with check constraint; ADR in `docs/` |
| B2 Introduce domain events (in-process dispatcher after commit) or an outbox, for Delivery → Order synchronization | NOT IMPLEMENTED | B1 | Delivered/Cancelled/Failed update order status transactionally or via a reliable outbox; tests |
| B3 Define cancel/fail semantics (unassign vs terminate; re-dispatch; ops queue) | NOT IMPLEMENTED | B1, product decision | Business rules documented (BR-DEL-017+); domain + API tests |
| B4 Customer tracking: delivery status/timestamps in order details plus `GET /api/me/orders/{id}/tracking` | NOT IMPLEMENTED | B1 | Contract in `customer-api.json`; SPA shows a live status timeline (polling first) |
| B5 Gate delivery claimability on restaurant readiness | NOT IMPLEMENTED | B1, E2 | A pending queue contains only ready-for-pickup orders (or the agreed state) |

### Phase C — Delivery readiness Phase 5 (P1)

| Task | Status | Depends on | Acceptance criteria / DoD |
|---|---|---|---|
| C1 `--e2e-provision prepare-delivery` / `restore-delivery` (pending applicant, approved agent, pending delivery) | NOT IMPLEMENTED | A2, A3 | Idempotent prepare/restore on SQL Server; no credentials or PII in JSON output; refuses unsafe environments (readiness plan §Phase 5) |
| C2 Real-token smoke tests across applicant / agent / admin boundaries | PARTIAL | C1 | Tests prove 403/200 boundaries with real tokens for all three audiences |
| C3 Published, reproducible Delivery + Admin contracts | PARTIAL | A8 | Regeneration yields byte-identical documents in CI |

### Phase D — Delivery Agent SPA (P1)

Follow [`talabat-frontend/docs/talabat-delivery-frontend-roadmap.md`](../talabat-frontend/docs/talabat-delivery-frontend-roadmap.md) Phases 1–9, after updating its blocker table (§8).

| Task | Depends on | Acceptance criteria / DoD |
|---|---|---|
| D1 App-neutral `core-config` / `core-auth` / `core-http`; move domain error codes out of `core-http` | A1 | Customer tests still pass; no Customer identifiers in `core-*` |
| D2 `api-delivery` generated client | A8, D1 | `api:verify` covers both clients |
| D3 `projects/delivery` shell on port 4300 with OIDC | D2 | Sign-in/out with `talabat-delivery-spa` works live |
| D4 Applicant status/profile screens | D3 | Pending/Approved/Rejected states render from `GET /api/agent/me/application` |
| D5 Availability, queue, claim with conflict recovery | D3, C1 | 409 on a race shows a recoverable message; state refetched |
| D6 Active-delivery lifecycle with a single primary action, plus cancel/fail confirmations | D5 | E2E covers the happy path through Delivered |
| D7 Foreground location reporting (after the policy decision) and history | D3 | Permission-denied path handled; history renders |
| D8 Release gate (unit, build budget, E2E, axe) | D4–D7 | CI green including live E2E |

### Phase E — Restaurant Owner (P1)

Implement [`docs/restaurant-owner-and-admin-api-implementation-plan.md`](docs/restaurant-owner-and-admin-api-implementation-plan.md) Phases 1, 2, 3 (owner capability), 4, 5, 6.

| Task | Depends on | Acceptance criteria / DoD |
|---|---|---|
| E1 Extract `Talabat.Api.Shared` | A11 | Zero behaviour change; all host tests green |
| E2 `Restaurant.OwnerUserId`, RowVersion, write behaviour + migration | E1 | Owner can only see and edit owned restaurants (ownership tests) |
| E3 RestaurantOwner capability, role, client, scope (`restaurant.api`) | E2 | Real-token tests |
| E4 `Talabat.RestaurantOwner.API`: menu CRUD, availability, opening hours, incoming orders, accept/reject/ready | B1, E3 | OpenAPI committed; authorization and ownership matrix tests |
| E5 Restaurant SPA (`projects/restaurant`) | E4 | Owner journey E2E: receive → accept → ready |

### Phase F — Admin operations (P2)

| Task | Depends on | Acceptance criteria / DoD |
|---|---|---|
| F1 Retire Identity dev-only approve/reject routes | A3 | Routes removed; tests ported to the Admin API |
| F2 Admin read models (paged lists of users, restaurants, orders, deliveries) | B1, E2 | Paged, filtered endpoints with tests |
| F3 Admin actions: restaurant activate/deactivate/assign owner; force-cancel/reassign delivery; resolve failed | B3, F2 | Audit-logged; idempotent; tests |
| F4 First-admin bootstrap command (CLI, environment-gated) | — | Documented, tested, no public endpoint |
| F5 Admin SPA (`projects/admin`, port 4400) | F2, F3 | Approve-agent and restaurant-management journeys E2E |

### Phase G — Production hardening (P2)

| Task | Acceptance criteria / DoD |
|---|---|
| G1 Identity: production signing key (Key Vault/cert), config-driven clients/URIs/CORS, retire or lock down JSON `/account/*`, enable lockout, decide Duende licensing | Host starts in `Production` with no `localhost` values; security tests |
| G2 Rate limiting (login, register, checkout, assign); security headers/HSTS | 429 behaviour tested |
| G3 Async capability resolution (remove sync-over-async); cache per request | No `.GetAwaiter().GetResult()` in hosts |
| G4 Idempotency key on checkout | A duplicate submit returns the same order |
| G5 Structured logging (Serilog or OTel logs) with correlation ID; audit logging for admin actions | Logs include `traceId`/user; admin actions persisted |
| G6 Health checks on Admin and Identity; readiness vs liveness | `/health/ready` and `/health/live` on all hosts |

### Phase H — Deployment and observability (P2)

| Task | Acceptance criteria / DoD |
|---|---|
| H1 Dockerfiles for 4 hosts + 3 SPAs; `docker-compose` with SQL Server for local/E2E | `docker compose up` yields a working system |
| H2 CI: build/test all projects, frontend CI pushed and green, container image build, migration bundle | All pipelines green on `main` |
| H3 Hosting (e.g. Azure App Service/Container Apps + Azure SQL), environment configs, secrets | Staging URL serves all apps over HTTPS |
| H4 OpenTelemetry traces/metrics + dashboard | Request traces span SPA → API → DB |

### Phase I — Advanced / portfolio features (P3)

Payments (sandbox), fees/promotions, notifications (email + SignalR), real-time tracking/maps, search/filter, ratings/reviews, nearest-agent dispatch, analytics. Each needs its own design doc first, per `PROJECT_IMPLEMENTATION_ROADMAP.md` Phase 11.

---

## 18. Remaining-work checklist with acceptance criteria

**P0 — Baseline**
- [ ] Frontend uncommitted work committed to a branch — *`git status` clean, pushed*
- [ ] Node 24 installed; `npm ci` succeeds in `talabat-web` — *`npx ng test customer --watch=false` passes*
- [ ] Local `TalabatDb` configured without editing committed files — *hosts start; `/health` = 200*
- [ ] Admin projects in `Talabat.slnx` — *CI runs Admin tests*
- [ ] Zero vulnerable packages — *CI vulnerability step passes*
- [ ] All 25 failing tests repaired (fixtures fixed, D12/D13 fixed, `469aabf` merged) — *all 8 suites green; race and BOLA tests exercise their real scenarios*
- [ ] `ItemCount` correct; real paging — *API tests assert counts and totals*
- [ ] Checkout→delivery atomic or outbox-backed — *failure-injection test: no orphan orders*
- [ ] Frontend contracts re-synced — *hashes match; `api:verify` passes*
- [ ] Frontend CI triggers fixed — *manual dispatch runs live E2E*
- [ ] README + AGENTS/spec status refreshed — *a new developer can run everything from the README*
- [ ] Feature branch merged to `main` — *CI green on `main`*

**P1 — Core MVP**
- [ ] Order lifecycle modelled — *transition tests + migration + ADR*
- [ ] Delivery ↔ Order synchronization — *Delivered/Cancelled/Failed reflected on the order*
- [ ] Cancel/fail/re-dispatch rules defined and implemented — *documented BRs + tests*
- [ ] Customer order tracking (API + SPA) — *live status timeline*
- [ ] Delivery claimable only when ready — *queue test*
- [ ] Delivery fixtures (`prepare-delivery`/`restore-delivery`) — *idempotency tests on SQL Server*
- [ ] Delivery SPA released — *E2E happy path to Delivered passes in CI*
- [ ] Restaurant ownership + Owner API — *ownership/authorization matrix tests*
- [ ] Restaurant SPA — *accept → ready journey E2E*

**P2 — Production readiness**
- [ ] Admin operations API + Admin SPA — *approve agent, manage restaurants, resolve deliveries E2E*
- [ ] Identity production hardening — *`Production` start without localhost/TODOs*
- [ ] Rate limiting + security headers — *429/headers tests*
- [ ] Async capability resolution — *no sync-over-async*
- [ ] Checkout idempotency — *duplicate-submit test*
- [ ] Structured logging + audit + OpenTelemetry — *trace visible end to end*
- [ ] Containers + compose + deployed staging — *public HTTPS staging*

**P3 — Advanced**
- [ ] Payments (sandbox) · [ ] Notifications/real-time · [ ] Search/filter · [ ] Ratings · [ ] Smart dispatch — *each with its own design doc and tests*

---

## 19. The five best next actions

1. **Secure and re-establish the working baseline (½ day).** Commit the 51 uncommitted frontend files to a branch and push it. Install Node 24. Configure `ConnectionStrings__TalabatDb` through user-secrets or environment variables for `.\SQLEXPRESS`, then create the dev database with `dotnet ef database update` (a deliberate developer step). *Evidence:* §3.2 uncommitted list; §5.2 machine migration. *Done when:* both repos are clean, all four hosts start, and the Customer SPA signs in locally.
2. **Make the test suite and CI truthful (1–1½ days).** Add `Talabat.Admin.API` and `Talabat.Admin.API.Tests` to `src/Talabat/Talabat.slnx` and fix the SSH.NET vulnerability. Repair the 25 failing tests by fixing fixtures, not assertions: a profile-less user for the "without profile" tests, online agents for the race and BOLA tests, a valid `NotBefore` in token helpers, isolated Identity Staging tests, and the migration-set assertion. Fix invalid-`sub` → 401 (D12) and merge the unmerged ProblemDetails fix `469aabf` (D13). Track the frontend CI with corrected triggers. *Evidence:* §13.1, D2, D3, D9, D10, D12–D14. *Done when:* `dotnet test src/Talabat/Talabat.slnx` reports 0 failures and the vulnerability check passes locally and in GitHub Actions; then merge to `main`.
3. **Fix checkout→delivery atomicity (1 day).** Move delivery creation into the Application layer within the checkout unit of work (or an outbox with a retrying dispatcher), and add a failure-injection test. *Evidence:* D1, `CheckoutController.cs:46-105`. *Done when:* no code path yields an order without a delivery or a durable creation record, and the controller only maps results.
4. **Fix the visible customer defects and re-sync contracts (½–1 day).** Correct `ItemCount`, implement real paging for catalog and orders, regenerate the Customer client, and re-copy `delivery-api.json` and `admin-api.json` to the frontend. *Evidence:* D4, D5, D8. *Done when:* the Orders page shows correct counts, paging metadata is accurate, and `npm run api:verify` passes.
5. **Design the Order lifecycle before any new role work (1–2 days of design + implementation start).** Write an ADR choosing between `Order.Status` and the planned `OrderFulfillment` aggregate. Decide cancel/fail/re-dispatch semantics and when deliveries become claimable, then implement the domain model with tests. *Evidence:* §7.4 integration gaps; plan D3/D4. *Done when:* the ADR is approved, domain transitions are tested, and the migration exists. This unblocks customer tracking (B4), the Restaurant Owner API (E4), and readiness Phase 5 fixtures that reflect the real lifecycle.

---

## 20. Verification log

All commands were run from the stated directory on 2026-10-13. No command modified tracked files. Build outputs went to the existing git-ignored `bin/` and `obj/` folders.

| # | Command | Result |
|---|---|---|
| 1 | `git -c safe.directory=* branch -a -vv; log --all --graph` (both repos) | See §3, §5.1. Backend HEAD `896bd2f`; frontend HEAD `389f254` |
| 2 | `git -c safe.directory=* status --short` (frontend) | 27 modified, 24 untracked files |
| 3 | `dotnet --list-sdks` | `10.0.401` |
| 4 | `dotnet build src/Talabat/Talabat.slnx -c Debug` | **Build succeeded, 0 errors, 8 warnings** (NU1903 SSH.NET ×8), 34 s |
| 5 | `dotnet build src/Talabat/Talabat.Admin.API/Talabat.Admin.API.csproj` and `dotnet build tests/Talabat.Admin.API.Tests/Talabat.Admin.API.Tests.csproj` | Both **succeeded**, 0 warnings |
| 6 | `dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive` | `Talabat.Identity.Tests`, `Talabat.Infrastructure.Tests`: **SSH.NET 2025.1.0 High** (GHSA-q939-rpr3-3284, GHSA-mggc-4xg6-vcxf). All other projects clean |
| 7 | `dotnet test tests/<project> --no-build --logger trx` for all 8 test projects, with `ConnectionStrings__TalabatDb=Server=.\SQLEXPRESS;Database=Talabat;Trusted_Connection=True;TrustServerCertificate=True` (env var only) | **605/630 passed, 25 failed.** Per-suite table and root causes in §13.1 |
| 8 | `sha256sum` of OpenAPI documents in both repos | customer match; delivery mismatch; admin missing (§8) |
| 9 | `sqlcmd -S .\SQLEXPRESS -E -Q "SELECT name FROM sys.databases"` before/after; LocalDB `TalabatTest%` count after | Before and after: `master, model, msdb, tempdb` only; LocalDB 0 leftover test databases |
| 10 | Node / npm discovery (`which node`, common install paths) | **Not installed.** Frontend build/test not executed |
| 11 | Historical frontend logs `talabat-web/*.log` (2026-08-09) | customer 230/230, core-auth 63, core-http 17, shared-ui 5, shared-util 2 passed; production build complete |
| 12 | `talabat-web/test-results/.last-run.json` + `error-context.md` (2026-08-10) | Playwright **failed 3/3**: browser executable missing; `ECONNREFUSED :7056` |

---

## 21. Appendix: key file paths and documentation drift

**Key backend files**

- Domain: [`Delivery.cs`](src/Talabat/Talabat.Domain/Aggregates/DeliveryManagement/Delivery.cs), [`Order.cs`](src/Talabat/Talabat.Domain/Aggregates/Ordering/Order.cs), [`User.cs`](src/Talabat/Talabat.Domain/Aggregates/Users/User.cs), [`Restaurant.cs`](src/Talabat/Talabat.Domain/Aggregates/Catalog/Restaurant.cs), [`Cart.cs`](src/Talabat/Talabat.Domain/Aggregates/Basket/Cart.cs), [`DeliveryAssignmentDomainService.cs`](src/Talabat/Talabat.Domain/DomainServices/DeliveryManagement/DeliveryAssignmentDomainService.cs)
- Application: [`CheckoutHandler.cs`](src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutHandler.cs), [`CreateDeliveryForOrderHandler.cs`](src/Talabat/Talabat.Application/Deliveries/CreateForOrder/CreateDeliveryForOrderHandler.cs), [`DependencyInjection.cs`](src/Talabat/Talabat.Application/DependencyInjection.cs)
- Infrastructure: [`UnitOfWork.cs`](src/Talabat/Talabat.Infrastructure/Persistence/UnitOfWork.cs), [`DeliveryConfiguration.cs`](src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryConfiguration.cs), [`UserCapabilityService.cs`](src/Talabat/Talabat.Infrastructure/Identity/UserCapabilityService.cs), [`E2EProvisioner.cs`](src/Talabat/Talabat.Infrastructure/Development/E2E/E2EProvisioner.cs)
- Hosts: [`Talabat.API/Program.cs`](src/Talabat/Talabat.API/Program.cs), [`Talabat.Delivery.API/Program.cs`](src/Talabat/Talabat.Delivery.API/Program.cs), [`Talabat.Admin.API/Program.cs`](src/Talabat/Talabat.Admin.API/Program.cs), [`Talabat.Identity/Program.cs`](src/Talabat/Talabat.Identity/Program.cs), [`IdentityServerConfig.cs`](src/Talabat/Talabat.Identity/IdentityServerConfig.cs)
- CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml)

**Key plans (most current first):** [`docs/delivery-backend-readiness-plan.md`](docs/delivery-backend-readiness-plan.md) (Phases 1–4 done, 5 open) · [`docs/restaurant-owner-and-admin-api-implementation-plan.md`](docs/restaurant-owner-and-admin-api-implementation-plan.md) (unimplemented) · [`PROJECT_IMPLEMENTATION_ROADMAP.md`](PROJECT_IMPLEMENTATION_ROADMAP.md) (status snapshot dated 2026-07-15, outdated) · frontend `docs/talabat-angular-roadmap-phases-5-to-completion.md`, `docs/talabat-delivery-frontend-roadmap.md` (untracked; blocker table outdated).

**Documentation vs code discrepancies (resolved in favour of code)**

| Document claim | Code reality |
|---|---|
| `AGENTS.md`: current increment is Phase 10 Remediation | Work progressed through readiness Phases 1–4 (Aug 10–17) |
| `AGENTS.md`: Domain 100 % free of ASP.NET Core Identity | Domain references `Identity.Stores`; sanctioned by constitution v3.0.1 |
| `specs/010…/tasks.md`: T046–T069 unchecked | Most implemented (`TryGetAgentIdHelper`, commands carry `AgentId`, `RequireCustomerProfileAttribute`, OAuth2 schemes, health checks, BOLA test). Still open: T062 (config-driven CORS), T065 (`ISystemClock`), T070–T075 gates |
| `specs/005…/tasks.md`: 32 unchecked | Unified-user cutover is implemented (`UserConfiguration`, `UserRepository`, etc.) |
| Readiness plan §Scope: "No Admin API host" | Admin host now exists (but outside the solution) |
| Delivery frontend roadmap: applicant status, status read, pickup data are blockers | Resolved by `216dd9c`, `9b29fff`, `333bb1d` |
| `PROJECT_IMPLEMENTATION_ROADMAP.md` status snapshot (2026-07-15) | Superseded by ~30 subsequent commits |
