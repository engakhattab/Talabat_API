# Implementation Plan: Persistence And Infrastructure

**Branch**: `main` | **Date**: 2026-07-11 | **Spec**: `specs/002-persistence-infrastructure/spec.md`
**Input**: Feature specification from `specs/002-persistence-infrastructure/spec.md`

## Summary

Phase 4 adds SQL Server-backed persistence behind the existing aggregate-root repository contracts. The implementation uses EF Core only in `Talabat.Infrastructure`, wires Infrastructure from the API composition root, maps Domain aggregates without weakening encapsulation, enforces documented relational constraints, adds deterministic catalog seed data, and verifies behavior with SQL Server-backed integration tests.

The plan preserves the Phase 3.5 IDENTITY decision: entities are constructed with `Id == 0`, SQL Server assigns positive integer IDs during `SaveChangesAsync`, child line items use composite parent/product keys, and there is no application-side ID generation, no sequences, and no `ValueGeneratedNever` for runtime-generated IDs.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)  
**Primary Dependencies**: Add `Microsoft.EntityFrameworkCore.SqlServer` 10.0.9, `Microsoft.EntityFrameworkCore.Design` 10.0.9, and `Microsoft.EntityFrameworkCore.Tools` 10.0.9 to `Talabat.Infrastructure`; add `Testcontainers.MsSql` 4.13.0 to Infrastructure integration tests; update or override `Microsoft.AspNetCore.OpenApi` dependency only to clear NU1903/GHSA-v5pm-xwqc-g5wc.  
**Storage**: SQL Server relational database using IDENTITY integer keys, filtered indexes, check constraints, owned value-object columns, and one reviewed EF migration.  
**Testing**: xUnit unit tests plus SQL Server-backed integration tests using Testcontainers first, LocalDB fallback, and no SQLite.  
**Target Platform**: ASP.NET Core API host as composition root; Infrastructure class library contains persistence.  
**Project Type**: Clean Architecture backend persistence/infrastructure phase.  
**Performance Goals**: Repository reads should use bounded aggregate-loading queries and documented indexes for active carts, customer order history, restaurant/product lookup, delivery order lookup, and active agent assignment.  
**Constraints**: No API endpoints, no Identity/Auth, no Delivery Application use cases, no MediatR, no repository interface changes, no business-rule changes, and no Domain/Application package references.  
**Scale/Scope**: Persist all current aggregate roots (`Restaurant`, `Cart`, `Customer`, `Order`, `Delivery`, `DeliveryAgent`) and aggregate children reachable through those roots.

## Constitution Check

**Gate 1 - Domain/Application independence**: PASS. EF Core packages and implementation types are restricted to Infrastructure and Infrastructure tests. Domain and Application project files must remain package-free.

**Gate 2 - Repository boundaries**: PASS. Infrastructure implements existing contracts from `src/Talabat/Talabat.Domain/Interfaces/`; no contract changes, `IQueryable`, DbContext exposure, or child repositories are planned.

**Gate 3 - API composition root only**: PASS. API changes are limited to referencing Infrastructure, reading `ConnectionStrings:TalabatDb`, calling `AddInfrastructure`, and resolving the OpenAPI vulnerability. No endpoints or business middleware are included.

**Gate 4 - Identity/Auth reserved**: PASS. Audit user fields remain null and no identity packages, identity tables, auth middleware, claims, roles, tokens, or auth migrations are planned.

**Gate 5 - SQL Server IDENTITY strategy**: PASS. Runtime generated IDs use SQL Server IDENTITY. Explicit IDs are allowed only for deterministic `HasData` seed rows.

**Gate 6 - Encapsulation**: PASS. EF materialization mechanics are limited to private parameterless constructors and backing-field/private-member mapping. Public setters and mutable collections are not introduced.

**Gate 7 - Constraint traceability**: PASS. Constraints and indexes are documented in `research.md` and `data-model.md` and map to existing Domain or roadmap rules.

## Project Structure

### Documentation

```text
specs/002-persistence-infrastructure/
|-- spec.md
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   `-- persistence-boundary.md
`-- tasks.md
```

### Source Code

```text
src/
`-- Talabat/
    |-- Talabat.API/              # Composition root only
    |-- Talabat.Application/      # No persistence package references
    |-- Talabat.Domain/           # Repository contracts and aggregates
    `-- Talabat.Infrastructure/   # EF Core DbContext, mappings, repos, UnitOfWork, migrations

tests/
|-- Talabat.Application.Tests/
`-- Talabat.Infrastructure.Tests/ # SQL Server-backed integration tests added in Phase 4
```

## Phase 0: Research

Completed in `specs/002-persistence-infrastructure/research.md`.

Resolved decisions:

- EF Core SQL Server package set and versions are pinned to the .NET 10 stable patch line.
- Migrations run from Infrastructure with API as startup project:
  `dotnet ef migrations add InitialPersistence --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API --output-dir Persistence\Migrations`.
- Value objects are owned columns: `Money` as `decimal(18,2)`, `Address`, `DeliveryAddressSnapshot`, `TimeRange` as `TimeOnly`/SQL `time`, and `GeoLocation` as paired nullable `decimal(9,6)` coordinates.
- SQL Server IDENTITY is used for all entities with IDs; `CartItem` and `OrderItem` keep composite parent/product keys.
- Required indexes and constraints include filtered one-active-cart-per-customer, filtered one-default-address-per-customer, unique delivery per order, unique product name per restaurant, and active delivery per assigned agent.
- Duplicate saved-address value detection remains Domain-only in Phase 4.
- Audit stamping is handled by an Infrastructure SaveChanges interceptor and soft-delete filters are applied globally.
- Seed data uses `HasData` with explicit IDs for deterministic catalog rows.
- Integration tests use SQL Server Testcontainers first, LocalDB fallback, and forbid SQLite.
- Connection string key is `ConnectionStrings:TalabatDb` with no committed production secrets.

## Phase 1: Design And Contracts

Completed design artifacts:

- `data-model.md`: One mapping table per aggregate with table names, keys, owned types, indexes/constraints, and EF materialization notes.
- `contracts/persistence-boundary.md`: Contract boundary for DI registration, repository implementations, UnitOfWork behavior, migration workflow, and scope guard. It explicitly confirms that no HTTP/API endpoint contracts are introduced in Phase 4.
- `quickstart.md`: Review and implementation handoff commands for package installation, DbContext/migration flow, SQL Server-backed tests, vulnerability verification, and scope checks.

No external HTTP contracts are generated because API endpoints are out of scope.

## Phase 2: Planning Handoff

Implementation must follow `specs/002-persistence-infrastructure/tasks.md` in this order:

1. Packages and vulnerability update.
2. DbContext, `AddInfrastructure` shell, UnitOfWork, API composition-root reference, SQL Server test fixture, EF materialization guardrails, and audit interceptor registration.
3. Shared mapping conventions.
4. Per-aggregate mapping and repository work with integration tests for each aggregate.
5. Constraints, seed verification, and one reviewed migration plus idempotent SQL script.
6. Full build, full tests, vulnerability audit, and scope-guard checks.

Do not implement before review approval. During implementation, create tests against SQL Server behavior before generating the reviewed migration, and generate the migration only after mappings and constraints have been reviewed.

## Complexity Tracking

No constitution violations or complexity exceptions are required.

## Post-Design Constitution Check

PASS. The design artifacts keep EF Core isolated to Infrastructure, preserve existing Domain repository contracts, keep API work to composition-root wiring, defer Identity/Auth and Delivery Application use cases, retain SQL Server IDENTITY-compatible IDs, and require database constraints to trace back to documented Domain or roadmap rules.
