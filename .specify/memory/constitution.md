<!--
Sync Impact Report
- Version change: 2.0.0 -> 2.1.0 (MINOR: materially new governance — provisional account/profile
  linkage and a current-user abstraction, previously deferred, are now permitted in Phase 7)
- Modified principles:
  - Principle 6 clarified: `Customer`/`DeliveryAgent` MAY carry a nullable, framework-neutral scalar
    linkage key (e.g. `IdentityUserId` string); a scalar string is not an Identity framework type.
- Modified standards:
  - Account/profile separation: linkage + current-user abstraction move from "deferred" to
    "provisional, introduced in Phase 7"; finalization deferred to Phase 9.
- Replaced sections:
  - Current Phase Scope: Phase 6 (Minimal Identity/Auth) -> Phase 7 (`Talabat.Customer.API`)
  - Quality Gates: rewritten for Phase 7
- Templates reviewed:
  - ✅ .specify/templates/plan-template.md (no change required)
  - ✅ .specify/templates/spec-template.md (no change required)
  - ✅ .specify/templates/tasks-template.md (no change required)
- Runtime guidance:
  - ✅ AGENTS.md re-scoped to Phase 7
  - ✅ PROJECT_IMPLEMENTATION_ROADMAP.md already contains the approved sequence (Phase 7 = Customer.API)
  - Source spec: `specs/003-customer-api/spec.md`
- Open decision recorded in scope:
  - Token issuance: Phase 7 validates bearer tokens against the Identity authority; real user-token
    acquisition (interactive client) stays deferred to Phase 9; tests use test-minted JWTs.
- 2.1.1 (PATCH, clarification): account->profile strategy is explicit create-on-first-use (no
  auto-provisioning of empty profiles); Phase-7 Domain change permits the `IdentityUserId` scalar
  plus a `CreateForAccount` factory, with `Customer` name/age invariants unchanged.
-->

# Talabat Project Constitution

Authoritative sequencing lives in `PROJECT_IMPLEMENTATION_ROADMAP.md` (Status Snapshot and
Section 5). This constitution holds permanent architecture rules plus the scope guard for the phase
currently being implemented. Update "Current Phase Scope" when a phase completes; permanent
principles change only through an explicit constitution amendment.

## Core Architecture Principles (permanent)

1. Domain MUST stay independent from ASP.NET Core, EF Core, Identity/Auth frameworks, HTTP,
   controllers, and external service implementations. `Talabat.Domain` and `Talabat.Application`
   MUST NOT reference Infrastructure or carry persistence, web, or Identity packages.
2. Application MUST orchestrate use cases through abstractions and return transport-neutral results.
   Business rules MUST live in Domain aggregates and domain services only.
3. Aggregate roots MUST protect invariants. Child entities (`Product`, `CartItem`, `OrderItem`, and
   `CustomerAddress`) MUST be modified only through their roots and MUST NOT have repositories.
4. Repository contracts MUST live in `Talabat.Domain/Interfaces/` for aggregate roots only, and
   Infrastructure MUST implement them. `IQueryable`, `DbContext`, and EF types MUST NOT leak outside
   Infrastructure.
5. Each HTTP host MUST be a thin composition root responsible only for transport mapping, dependency
   wiring, middleware, and its host-specific authentication responsibilities. Business logic MUST NOT
   live in controllers or endpoints.
6. Identity/Auth MUST remain a separate boundary. Minimal Identity/Auth is introduced in roadmap
   Phase 6 in the `Talabat.Identity` host, using Duende IdentityServer with ASP.NET Core Identity.
   Identity EF persistence MAY live in Infrastructure because the approved design uses the existing
   `TalabatDbContext`, but Identity framework types MUST NOT enter Domain or Application. `Customer`
   and `DeliveryAgent` MUST remain domain profiles and MUST NOT inherit from Identity types. They MAY
   carry a nullable, framework-neutral scalar linkage key (e.g. `IdentityUserId` as a `string`) to
   associate an account with a profile; a scalar string is not an Identity framework type and does
   not violate Principle 1.
7. Persisted entity IDs MUST be database-generated SQL Server IDENTITY values. Entities are
   constructed with `Id == 0`; persistence assigns positive IDs during `SaveChangesAsync`.
   Application-side ID generation, database sequences, and `ValueGeneratedNever` keys MUST NOT be
   reintroduced.
8. EF mapping MUST NOT weaken encapsulation. Use private parameterless constructors and backing-field
   mapping; do not add public setters, public mutable collections, or Domain members solely for the
   ORM.
9. Database constraints MUST back Domain invariants. Every constraint or index MUST map to a
   documented rule in the roadmap or an approved design document.

## Decided Standards

- Handlers: CQRS-lite, with one explicit handler per use case and no MediatR or command-bus packages.
- Identity framework: Duende IdentityServer integrated with ASP.NET Core Identity in the separate
  `Talabat.Identity` ASP.NET Core Web API host.
- Identity persistence: one physical SQL Server database and the existing Infrastructure
  `TalabatDbContext`; the dependency direction is `Talabat.Identity -> Talabat.Infrastructure` and
  never the reverse.
- Account/profile separation: accounts (Identity) and profiles (`Customer`/`DeliveryAgent`) are
  distinct records joined only by a scalar linkage key. Phase 6 registration creates an account only.
  Phase 7 introduces a **provisional** account->`Customer` linkage keyed on the token `sub` claim; the
  profile is created explicitly on first use (`POST /api/me/profile`) through an Application use case,
  never auto-provisioned as an empty profile, plus a read-only framework-neutral `ICurrentUser`
  abstraction in `Talabat.Application`. Linkage finalization (uniqueness/reconciliation rules,
  `DeliveryAgent` linkage) and the token/claims/scopes contract are deferred to Phase 9.
- Results: transport-neutral `UseCaseResult`/`ApplicationError`; handlers return Application read
  models, never Domain aggregates; generated IDs are read only after `SaveChangesAsync`.
- Tests: xUnit; every phase ships its own tests as part of its acceptance criteria.

## Current Phase Scope: Phase 7 — `Talabat.Customer.API` (Customer-Facing Business API)

Source spec: `specs/003-customer-api/spec.md`.

Allowed in this phase:

- Rename the existing `Talabat.API` -> `Talabat.Customer.API` (project file, assembly name, namespace
  root, solution, docs) before adding endpoints, and remove the template `WeatherForecast` code.
- An `AddApplication()` DI extension registering the use-case handlers; the host composes
  `AddApplication()` + `AddInfrastructure()`.
- Thin attribute-routed controllers grouped by domain area (Catalog, Cart, Customer, Order) that
  delegate to Application handlers only, with host-specific request/response DTOs.
- Global `DomainException` -> RFC 9457 ProblemDetails mapping.
- JwtBearer **validation** against the `Talabat.Identity` authority (issuer/JWKS/discovery); anonymous
  catalog endpoints; `[Authorize]` owner-scoped endpoints on `/api/me/...` routes.
- Explicit `Customer` profile creation on first use via `POST /api/me/profile` (the token `sub` sets
  the linkage) through an Application use case — never an empty or placeholder profile. Plus a
  read-only framework-neutral `ICurrentUser` in `Talabat.Application`, the nullable `IdentityUserId`
  scalar on `Customer`, and a `Customer.CreateForAccount(...)` factory that keeps the existing
  name/age invariants. Owner-scoped endpoints return `409 Conflict` (`ProfileNotCreated`) until a
  profile exists.
- Complete OpenAPI, a development-only `localhost` CORS policy, and a public `/health` endpoint
  (host + database connectivity).
- `tests/Talabat.Customer.API.Tests` integration tests, and a per-endpoint authorization matrix
  documenting current behavior and its Phase 9 refinement need.

Prohibited in this phase:

- `Talabat.DeliveryAgent.API` or any Delivery-agent endpoints (Phase 8); login/register/logout or any
  account-management endpoints (owned by `Talabat.Identity`); `Talabat.Customer.API` MUST NOT
  reference `Talabat.Identity`.
- Adding an interactive/authorization-code, ROPC/password, or client-credentials token-**issuing**
  client to `Talabat.Identity`, or minting production user tokens. Real end-to-end user-token
  acquisition stays deferred until an interaction UI/client exists (Phase 9). Integration tests MUST
  use test-minted JWTs trusted only in the Test environment.
- Final token audiences, scopes, custom claims, per-endpoint authorization policies, or linkage
  finalization — all Phase 9.
- Business logic, direct database access, or EF Core types in controllers; exposing Domain entities or
  Application read models directly as HTTP response bodies.
- Anonymous/guest carts, client-generated cart identifiers, or making `Cart.CustomerId` nullable. Cart
  endpoints in Phase 7 are authenticated and customer-scoped; the guest-cart / Basket redesign is a
  separate future phase.
- Angular/frontend; payment, notifications, coupons, reviews, restaurant-owner workflows (Phase 11);
  API versioning, rate limiting, or deployment/CI concerns.

## Quality Gates

- The whole solution MUST build; existing Application/Infrastructure/Identity tests MUST stay green;
  the new `Talabat.Customer.API.Tests` MUST pass.
- `Talabat.Domain` and `Talabat.Application` project files MUST contain no web or Identity/Auth
  packages; controllers MUST contain no business logic and no EF Core types.
- Dependency direction MUST remain `Talabat.Customer.API -> Talabat.Application` (with
  `Talabat.Infrastructure` referenced only for composition-root wiring) `-> Talabat.Domain`, with no
  reverse reference and no reference to `Talabat.Identity`.
- Every owner-scoped endpoint MUST reject unauthenticated requests with `401`; catalog endpoints MUST
  be anonymous. Owner-scoped data MUST be resolved from the validated token, never a route/body
  `customerId`.
- `DomainException`s MUST map to ProblemDetails with correct `4xx` codes; responses MUST be
  host-specific DTOs only.
- The authorization matrix MUST be committed, recording each endpoint's current behavior and its
  Phase 9 refinement need.
- Package vulnerability auditing MUST report no known vulnerabilities before the phase is accepted.

## Governance

- This constitution governs all implementation work. The roadmap determines phase sequence, while
  `AGENTS.md` may narrow the current increment further but MUST NOT broaden this constitution.
- Amendments require an explicit approved direction, a documented Sync Impact Report, semantic
  versioning, and consistency review of the spec-kit templates and runtime guidance.
- MAJOR versions redefine or remove a principle, MINOR versions add materially new governance, and
  PATCH versions clarify without changing meaning.
- Plans and reviews MUST include a constitution check before implementation and again before phase
  acceptance. Any exception MUST be documented and approved before code is changed.

**Version**: 2.1.1

**Ratified**: 2026-07-03

**Last Amended**: 2026-07-16
