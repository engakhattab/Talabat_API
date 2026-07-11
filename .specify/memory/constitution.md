# Talabat Project Constitution

Authoritative sequencing lives in `PROJECT_IMPLEMENTATION_ROADMAP.md` (Status Snapshot + Section 5). This constitution holds the permanent architecture rules plus a scope guard for the phase currently being implemented. Update the "Current Phase Scope" section when a phase completes; the permanent principles do not change per phase.

## Core Architecture Principles (permanent)

1. Domain stays independent from ASP.NET Core, EF Core, Identity/Auth frameworks, HTTP, controllers, and external service implementations. `Talabat.Domain` and `Talabat.Application` must never reference Infrastructure or carry any persistence/web/identity package.
2. Application orchestrates use cases through abstractions and returns transport-neutral results; business rules live in Domain aggregates and domain services only.
3. Aggregate roots protect invariants; child entities (`Product`, `CartItem`, `OrderItem`, `CustomerAddress`) are modified only through their roots and never get repositories.
4. Repository contracts live in `Talabat.Domain/Interfaces/` for aggregate roots only; Infrastructure implements them. `IQueryable`, `DbContext`, and EF types never leak outside Infrastructure.
5. API is the composition root and a thin transport layer: request/response mapping, DI wiring, middleware, and (in later phases) authentication/authorization. No business logic in controllers.
6. Identity/Auth is a reserved boundary (roadmap Phase 8). Do not select a framework, install identity packages, or add identity types, claims, or `IdentityUserId` fields to Domain or Application before that phase. `Customer` and `DeliveryAgent` remain pure domain profiles.
7. Persisted entity IDs are database-generated SQL Server IDENTITY values (Phase 3.5 decision). Entities are constructed with `Id == 0`; persistence assigns positive IDs during `SaveChangesAsync`. Do not reintroduce application-side ID generation, database sequences, or `ValueGeneratedNever` keys.
8. EF mapping must never weaken encapsulation: use private parameterless constructors and backing-field mapping; do not add public setters, public mutable collections, or domain members that exist only to satisfy the ORM.
9. Database constraints back domain invariants; every constraint or index maps to a documented rule in the roadmap or design docs.

## Decided Standards

- Handlers: CQRS-lite — one explicit handler per use case; no MediatR or command-bus packages.
- Identity context: use cases receive explicit `customerId`/`agentId` request data until Phase 8; no current-user abstraction yet.
- Results: transport-neutral `UseCaseResult`/`ApplicationError`; handlers return Application read models, never Domain aggregates; generated IDs are read only after `SaveChangesAsync`.
- Tests: xUnit; every phase ships its own tests as part of its acceptance criteria.

## Current Phase Scope: Phase 4 — Persistence And Infrastructure

Allowed in this phase:

- EF Core SQL Server packages in `Talabat.Infrastructure` (plus design/tooling packages where required), `DbContext`, entity type configurations, repository and `IUnitOfWork` implementations, migrations, seed data, and integration tests under `tests/`.
- Composition-root wiring: `Talabat.API` project reference to `Talabat.Infrastructure` plus an `AddInfrastructure()` DI extension; connection-string configuration.
- Updating `Microsoft.AspNetCore.OpenApi` to clear the NU1903 vulnerability warning.

Prohibited in this phase:

- Business API endpoints, API request/response contracts, or middleware behavior beyond DI wiring.
- Identity/Auth packages, identity tables, or auth-related migrations.
- Delivery Application use cases (Phase 7), websites/frontend, MediatR, payment, notifications, coupons, or reviews.
- Domain changes except mechanics EF genuinely requires (private parameterless constructors, backing-field mapping); any other Domain edit needs explicit approval before it is made.

## Quality Gates

- Solution builds with all tests green; `Talabat.Domain` and `Talabat.Application` project files still contain no package references.
- Integration tests prove: IDs are populated after `SaveChangesAsync`; checkout persists one order and closes one cart atomically; the one-active-cart-per-customer and unique-delivery-per-order constraints reject violations; owned value objects round-trip correctly.
- Migrations are generated only after entity configurations are reviewed, and reflect aggregate boundaries (snapshots mapped as owned data, never as independent tables).
- `dotnet list package --vulnerable` reports no known vulnerabilities after the OpenApi update.
