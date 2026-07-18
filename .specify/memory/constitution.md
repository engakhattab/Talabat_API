<!--
Sync Impact Report
- Version change: 2.1.1 -> 3.0.0 (MAJOR: Principles 1, 6, and 7 are redefined and the
  account/profile-separation standard is removed, per an explicit instructor-approved
  architecture override)
- Redefined principles:
  - Principle 1: Domain independence narrowed — `Talabat.Domain` MAY reference
    `Microsoft.Extensions.Identity.Stores` solely so the `User` aggregate can inherit
    `IdentityUser<int>`. All other framework independence rules stand.
  - Principle 6: the separate `Customer`/`DeliveryAgent` profile aggregates are REMOVED and
    replaced by one unified Domain `User : IdentityUser<int>` aggregate. The former
    "MUST NOT inherit from Identity types" rule is repealed for `User` specifically.
  - Principle 7: int IDENTITY keys now extend to the Identity tables
    (`IdentityDbContext<User, IdentityRole<int>, int>`).
- Removed standards:
  - "Account/profile separation" (accounts vs. profile records joined by `IdentityUserId`) —
    superseded by the unified `User` aggregate; the `sub` claim is `User.Id`.
- Added standards:
  - UserType/role synchronization, capability workflow, approval flow, auditing interfaces,
    rowversion concurrency, role seeding (see Decided Standards).
- Replaced sections:
  - Current Phase Scope: Phase 7 (`Talabat.Customer.API`) -> User Aggregate Refactor
    (governing plan: `user-aggregate-refactor-plan.md` at the repository root)
- 3.0.1 (PATCH, correction): the governing plan file lives at the repository root
  (`user-aggregate-refactor-plan.md`), not under `docs/`; both references corrected. No rule change.
  - Quality Gates: rewritten for the refactor
- Runtime guidance:
  - ✅ AGENTS.md re-scoped to the User Aggregate Refactor increment
  - ⚠ specs/003-customer-api/*, docs/authorization-matrix.md, phase-7-architecture-guide.md:
    superseded-notes added in refactor Phase 3 (see plan)
- Authority for this amendment: explicit instructor direction (2026-07-18), reviewed against the
  repository in the refactor audit; plan approved on branch `feature/user-aggregate-refactor`.
-->

# Talabat Project Constitution

Authoritative sequencing lives in `PROJECT_IMPLEMENTATION_ROADMAP.md` (Status Snapshot and
Section 5). This constitution holds permanent architecture rules plus the scope guard for the phase
currently being implemented. Update "Current Phase Scope" when a phase completes; permanent
principles change only through an explicit constitution amendment.

## Core Architecture Principles (permanent)

1. Domain MUST stay independent from ASP.NET Core, EF Core, HTTP, controllers, and external service
   implementations. `Talabat.Domain` and `Talabat.Application` MUST NOT reference Infrastructure or
   carry persistence or web packages. **Single approved exception**: `Talabat.Domain` MAY reference
   `Microsoft.Extensions.Identity.Stores` (version matching the solution's Identity package line)
   solely so the `User` aggregate can inherit `IdentityUser<int>`. No other Identity, EF, or web
   package may enter Domain or Application; `UserManager`, `RoleManager`, `SignInManager`,
   `ClaimsPrincipal`, and `HttpContext` MUST NOT appear in Domain.
2. Application MUST orchestrate use cases through abstractions and return transport-neutral results.
   Business rules MUST live in Domain aggregates and domain services only.
3. Aggregate roots MUST protect invariants. Child entities (`Product`, `CartItem`, `OrderItem`, and
   `UserAddress`) MUST be modified only through their roots and MUST NOT have repositories.
4. Repository contracts MUST live in `Talabat.Domain/Interfaces/` for aggregate roots only, and
   Infrastructure MUST implement them. `IQueryable`, `DbContext`, and EF types MUST NOT leak outside
   Infrastructure.
5. Each HTTP host MUST be a thin composition root responsible only for transport mapping, dependency
   wiring, middleware, and its host-specific authentication responsibilities. Business logic MUST NOT
   live in controllers or endpoints.
6. The platform has ONE unified user model. `Talabat.Domain.User` inherits `IdentityUser<int>` and is
   simultaneously the ASP.NET Core Identity account entity and the Domain aggregate for customer and
   delivery-agent capabilities (profile data, `UserAddress` collection, delivery-agent state machine,
   `UserType` flags, `IsActive`, auditing/soft delete). This inheritance is an intentional,
   instructor-approved design; do not reintroduce separate `Customer`/`DeliveryAgent` aggregates,
   a separate `ApplicationUser`, or an account/profile linkage key. Identity/Auth hosting remains a
   separate boundary: Duende IdentityServer + ASP.NET Core Identity live in the `Talabat.Identity`
   host; Identity EF persistence lives in Infrastructure's `TalabatDbContext`
   (`IdentityDbContext<User, IdentityRole<int>, int>`; one context, one physical database).
   `UserType` flags are the Domain source of truth for capabilities; Identity roles are an
   authorization projection kept in sync EXCLUSIVELY by the `IUserCapabilityService` workflow inside
   one transaction. No controller, handler, or service may mutate `UserType` or Identity roles
   outside that workflow.
7. Persisted entity IDs MUST be database-generated SQL Server IDENTITY `int` values, including the
   Identity tables (`User.Id`, `IdentityRole<int>`). Entities are constructed with `Id == 0`;
   persistence assigns positive IDs during `SaveChangesAsync`. Application-side ID generation,
   database sequences, string/GUID Identity keys, and `ValueGeneratedNever` keys MUST NOT be
   reintroduced.
8. EF mapping MUST NOT weaken encapsulation. Use private parameterless constructors and backing-field
   mapping; do not add public setters, public mutable collections, or Domain members solely for the
   ORM.
9. Database constraints MUST back Domain invariants. Every constraint or index MUST map to a
   documented rule in the roadmap or an approved design document. Capability-conditional rules
   (customer-only / agent-only fields) use NULL-tolerant CHECK constraints; constraints MUST NOT
   join the Identity role tables.

## Decided Standards

- Handlers: CQRS-lite, with one explicit handler per use case and no MediatR or command-bus packages.
- Identity framework: Duende IdentityServer integrated with ASP.NET Core Identity in the separate
  `Talabat.Identity` ASP.NET Core Web API host, over the unified `User` entity.
- Identity persistence: one physical SQL Server database and the existing Infrastructure
  `TalabatDbContext`; the dependency direction is `Talabat.Identity -> Talabat.Infrastructure` and
  never the reverse.
- Unified account: one `User` row per person for every capability (Customer, DeliveryAgent, Admin,
  RestaurantOwner — `[Flags] UserType`). A delivery agent using the customer site keeps the same
  account; a second account MUST NOT be created. The authenticated `sub` claim is `User.Id` (int).
- Capability workflow: `IUserCapabilityService` (implemented in Infrastructure with
  `UserManager<User>` + `TalabatDbContext`) is the only path for registration, capability grants,
  and delivery-agent approval. Customer capability is granted at registration/onboarding;
  DeliveryAgent capability requires server-side admin approval (`AgentApprovalStatus`); Admin and
  RestaurantOwner are never self-registered. Callers never supply role names.
- Repositories: `IUserRepository` serves business aggregate loading/behavior; `UserManager<User>` is
  reserved for account/password/security/role operations. The two are not mixed in one use case
  outside the capability workflow's explicit transaction.
- Auditing/soft delete: `IAuditable`/`ISoftDeletable` interfaces (implemented by `AuditableEntity`
  and by `User` directly); interceptor and query filters target the interfaces. `IsActive` governs
  business activation; Identity lockout governs security lockout; `IsDeleted` is retained soft
  deletion. Login MUST be rejected for `!IsActive || IsDeleted` via the custom
  `SignInManager<User>.CanSignInAsync` override (not by query filter accident).
- Concurrency: SQL `rowversion` (`User.RowVersion`) is the single EF concurrency token for business
  writes; conflicts map to `ConcurrencyConflictException` -> Conflict result -> HTTP 409.
- Business naming: `CustomerId` (Cart/Order/Delivery/checkout) and `Delivery.AssignedAgentId` are
  retained business names whose FKs reference `AspNetUsers.Id`. Do not rename them to `UserId`.
- Role seed data: exactly `Customer`, `DeliveryAgent`, `Admin`, `RestaurantOwner`, seeded
  idempotently at Identity-host startup. No seeded users or fixed passwords.
- Results: transport-neutral `UseCaseResult`/`ApplicationError`; handlers return Application read
  models, never Domain aggregates; generated IDs are read only after `SaveChangesAsync`.
- Tests: xUnit; every phase ships its own tests as part of its acceptance criteria.

## Current Phase Scope: User Aggregate Refactor (`feature/user-aggregate-refactor`)

Governing plan: `user-aggregate-refactor-plan.md` (repository root; three phases; the plan's ordered steps,
acceptance criteria, and quality gates are normative for this increment).

Allowed in this phase:

- Everything the plan specifies: the Domain `User : IdentityUser<int>` aggregate and `UserAddress`;
  deletion of `Customer`, `CustomerAddress`, `DeliveryAgent`, their repositories and EF
  configurations, and Infrastructure's `ApplicationUser`; `TalabatDbContext` ->
  `IdentityDbContext<User, IdentityRole<int>, int>`; `IUserRepository`/`UserRepository`;
  `IUserCapabilityService`/`UserCapabilityService`; `TalabatSignInManager`; `IdentityDataSeeder`;
  registration/approval endpoints in `Talabat.Identity`; `ICurrentUser` v2 (int `UserId`);
  Customer API capability enforcement; destructive development database rebuild with a single clean
  `InitialUnifiedUser` migration; full test-suite migration; documentation updates.
- The `Talabat.Domain` package reference `Microsoft.Extensions.Identity.Stores` (10.0.9 line).

Prohibited in this phase:

- Full `Talabat.Delivery.API` implementation (compile/wiring integrity only; the scaffold stays).
- Interactive Duende clients/redirect UI, token/claims/scopes finalization, production signing keys.
- Admin website/controllers (approval stays service-level, covered by tests).
- Product discount/employee-offer logic (only the multi-role identity information that enables it
  later).
- Data-preserving migrations, seeded users with fixed passwords, Angular/frontend, deployment/CI
  concerns.
- Any mutation of `UserType` or Identity roles outside `IUserCapabilityService`.

## Quality Gates

- The whole solution (`src/Talabat/Talabat.slnx`) MUST build at the end of every plan phase; at final
  acceptance all four test projects MUST pass.
- `Talabat.Domain` MUST contain no package reference other than
  `Microsoft.Extensions.Identity.Stores`; `Talabat.Application` MUST contain no web, EF, or Identity
  packages; controllers MUST contain no business logic and no EF Core types.
- Dependency direction MUST remain hosts -> Infrastructure -> Application -> Domain, with no reverse
  references; `Talabat.Customer.API` MUST NOT reference `Talabat.Identity`.
- Owner-scoped endpoints MUST resolve identity from the validated token (`sub` = `User.Id`), never a
  route/body `customerId`; unauthenticated requests get `401`; missing customer capability keeps the
  `ProfileNotCreated` 404/409 contract.
- Migration history MUST be exactly one `InitialUnifiedUser` migration; the schema MUST match the
  plan's "Final target database" section (verified by inspection commands in the plan).
- Role/`UserType` synchronization MUST be proven by tests (including a rollback/failure-injection
  test); multi-role, login-rejection (inactive/deleted), concurrency-409, address-invariant, and
  delivery-assignment tests MUST pass.
- The plan's removed-symbol sweeps MUST return zero production hits (`ApplicationUser`, `Customer`
  and `DeliveryAgent` aggregates, their repositories, `IdentityUserId`, string-key Identity
  registration).
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

**Version**: 3.0.1

**Ratified**: 2026-07-03

**Last Amended**: 2026-07-18
