<!--
Sync Impact Report
- Version change: unversioned legacy constitution -> 2.0.0
- Modified principles:
  - API composition root -> Separate HTTP composition roots
  - Identity/Auth fully deferred -> Minimal Identity/Auth before business APIs
- Added sections:
  - Phase 6 scope and quality gates
  - Governance and version metadata
- Removed sections:
  - Phase 4 persistence-only scope (that phase is complete)
- Templates reviewed:
  - ✅ .specify/templates/plan-template.md (no change required)
  - ✅ .specify/templates/spec-template.md (no change required)
  - ✅ .specify/templates/tasks-template.md (no change required)
  - ✅ .specify/templates/commands/ (no command templates present)
- Runtime guidance:
  - ✅ AGENTS.md updated for the current incremental Phase 6 scope
  - ✅ PROJECT_IMPLEMENTATION_ROADMAP.md already contains the approved sequence
- Deferred follow-up:
  - Create the complete Phase 6 spec-kit feature artifacts before broad Phase 6 implementation.
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
   and `DeliveryAgent` MUST remain pure domain profiles and MUST NOT inherit from Identity types.
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
- Account/profile separation: Phase 6 registration creates an account only. Account-to-`Customer` or
  account-to-`DeliveryAgent` linkage and any current-user abstraction remain deferred.
- Results: transport-neutral `UseCaseResult`/`ApplicationError`; handlers return Application read
  models, never Domain aggregates; generated IDs are read only after `SaveChangesAsync`.
- Tests: xUnit; every phase ships its own tests as part of its acceptance criteria.

## Current Phase Scope: Phase 6 — Minimal Identity/Auth Before Business APIs

Allowed in this phase:

- A separate `Talabat.Identity` ASP.NET Core Web API host with a one-way reference to Infrastructure.
- ASP.NET Core Identity EF support in Infrastructure, an empty `ApplicationUser : IdentityUser`, and
  extending the existing `TalabatDbContext` with the standard Identity model.
- Duende IdentityServer integration in the Identity host, minimal development configuration, and
  discovery endpoint verification.
- Minimal JSON register, login, and logout endpoints for account-only cookie-session learning.
- One reviewed Infrastructure migration that adds the standard Identity schema to `TalabatDb`.
- Focused Identity tests, existing regression tests, and package vulnerability checks.

Prohibited in this phase:

- `Talabat.Customer.API`, `Talabat.DeliveryAgent.API`, Angular/frontend work, or business endpoints.
- Identity, Duende, HTTP, JWT, `ClaimsPrincipal`, `ApplicationUser`, or `IdentityUser` types in Domain
  or Application.
- Making `Customer` or `DeliveryAgent` inherit from an Identity type or adding account/profile linkage.
- A custom password-to-token endpoint, hand-built JWTs, or Resource Owner Password Credentials.
- A second application `DbContext` or Duende EF configuration/operational stores during the approved
  minimal single-context setup.
- Refresh-token tuning, external login, password reset, email confirmation, 2FA, admin UI, advanced
  consent/custom grants, or production signing/secrets hardening.

## Quality Gates

- The whole solution MUST build and all existing tests MUST remain green.
- `Talabat.Domain` and `Talabat.Application` project files MUST contain no Identity/Auth packages.
- Dependency direction MUST remain `Talabat.Identity -> Talabat.Infrastructure ->
  Talabat.Application -> Talabat.Domain`, with no reverse reference.
- Registration, login-cookie creation, and logout MUST be tested incrementally; no response or log may
  expose a password, password hash, security stamp, or full Identity entity.
- Login MUST NOT mint or return a custom JWT. End-to-end Angular OIDC MUST remain deferred until an
  interaction UI/client exists.
- Any Identity migration MUST be reviewed before database update and MUST NOT unexpectedly alter
  existing business tables or add profile-link columns.
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

**Version**: 2.0.0

**Ratified**: 2026-07-03

**Last Amended**: 2026-07-14
