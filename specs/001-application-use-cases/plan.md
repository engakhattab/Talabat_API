# Implementation Plan: Application Use Cases

**Branch**: `main` | **Date**: 2026-07-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-application-use-cases/spec.md`

## Summary

Phase 3 implements the Application layer use cases for the customer ordering path: Catalog browsing, Basket/cart management, Customer profile/address management, Checkout, and customer-scoped order reads.

The implementation should add orchestration only. Handlers load aggregate roots through existing repository contracts, call Domain behavior, convert expected business outcomes into transport-neutral Application results, and commit through `IUnitOfWork` only for state-changing workflows. No EF Core implementation, API endpoints, Identity/Auth framework, Delivery workflow, frontend, or production transport code belongs in this phase.

Delivery is explicitly deferred by the Phase 3 spec clarification. The older roadmap text that mentions optional Delivery work is superseded for this feature by `specs/001-application-use-cases/spec.md`.

## Technical Context

**Language/Version**: C# with nullable reference types enabled; projects currently target `net10.0`.
**Primary Dependencies**: Existing project references only: `Talabat.Application` depends on `Talabat.Domain`; `Talabat.Infrastructure` and `Talabat.API` depend on `Talabat.Application`. Do not add MediatR, EF Core, Identity, or web packages in Phase 3.
**Storage**: Repository contracts already exist in `src/Talabat/Talabat.Domain/Interfaces/`. Storage implementations are deferred to Phase 4.
**Testing**: No test project currently exists. Phase 3 implementation should add focused xUnit Application tests under `tests/Talabat.Application.Tests/`, with fake repositories and clocks rather than infrastructure.
**Target Platform**: Backend Application layer in the existing .NET solution at `src/Talabat/Talabat.slnx`.
**Project Type**: Clean Architecture backend with DDD aggregate roots and Application orchestration.
**Performance Goals**: Keep use-case orchestration bounded to the aggregate/read model data needed by each workflow. Avoid query shapes that require callers to load full graphs unless the use case needs them.
**Constraints**: Domain remains independent of Application, Infrastructure, API, HTTP, EF Core, Identity/Auth, and database details. Application returns framework-neutral results and must not reference ASP.NET Core or EF Core.
**Scale/Scope**: Phase 3 covers customer ordering use cases only: Catalog, Basket, Customer, and Ordering. Delivery, payment, coupons, reviews, notifications, restaurant-owner workflows, websites, and Identity/Auth are excluded.

## Constitution Check

- Domain independence: PASS. Plan keeps use-case orchestration in `Talabat.Application` and does not modify Domain to reference framework concerns.
- Application orchestration: PASS. Handlers coordinate repositories, aggregates, domain services, result mapping, and unit-of-work commits.
- Aggregate invariants: PASS. Cart, Customer, Restaurant, and Order invariants remain enforced by aggregate methods and domain services.
- Aggregate-root repositories only: PASS. Existing repositories target aggregate roots. No repositories should be added for `Product`, `CartItem`, `OrderItem`, or `CustomerAddress`.
- API boundary: PASS. No controllers, endpoints, HTTP response types, or request binding types are planned.
- Identity/Auth deferral: PASS. Use cases receive explicit `customerId` in request models; no current-user abstraction or framework-specific identity type is added.
- Business logic placement: PASS. Business rules stay in Domain; handlers perform orchestration and result conversion only.
- Phase 3 exclusions: PASS. EF Core, migrations, Identity packages, frontend code, and Delivery workflows remain out of scope.

No constitution violations require justification.

## Project Structure

### Documentation

```text
specs/001-application-use-cases/
|-- spec.md
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   `-- application-use-cases.md
`-- checklists/
    |-- requirements.md
    `-- use-cases.md
```

### Source Code

```text
src/Talabat/
|-- Talabat.Domain/
|   |-- Aggregates/
|   |-- DomainServices/
|   |-- Interfaces/
|   `-- ValueObjects/
|-- Talabat.Application/
|   |-- Abstractions/
|   |-- Common/
|   |-- Catalog/
|   |-- Basket/
|   |-- Customers/
|   `-- Ordering/
|-- Talabat.Infrastructure/
`-- Talabat.API/
```

Expected Phase 3 source changes are limited to `src/Talabat/Talabat.Application/` and tests. Domain changes are allowed only if an implementation blocker exposes a real missing domain capability; do not move business rules into Application to avoid a Domain edit.

## Phase 0: Research

Research decisions are recorded in [research.md](research.md).

Resolved decisions:

- Use CQRS-lite handlers without MediatR or new packages.
- Keep caller-supplied `customerId` in request models until Identity/Auth is selected.
- Add transport-neutral Application result types for expected outcomes.
- Add Application abstractions for ID generation and restaurant local time.
- Return Application read models rather than Domain aggregates.
- Reject cross-restaurant cart additions and preserve the existing cart.
- Include unavailable menu products with availability flags.
- Use current product price at checkout and snapshot it into the order.
- Treat duplicate checkout submissions through cart state; do not add idempotency keys in Phase 3.
- Use xUnit for .NET unit tests in this phase.

### Testing Framework Decision

The project will use xUnit for .NET unit tests in Phase 3.

Initial test scope for this phase:

- Domain/Application-level behavior where applicable.
- Application use-case orchestration tests with fake repositories, fake clocks, fake ID generation, and fake local-time resolution.
- Infrastructure/API integration tests are deferred to later phases.

## Phase 1: Design And Contracts

Design artifacts are recorded in:

- [data-model.md](data-model.md)
- [contracts/application-use-cases.md](contracts/application-use-cases.md)
- [quickstart.md](quickstart.md)

Implementation shape:

- Add `Talabat.Application.Common` result contracts for transport-neutral success/failure.
- Add CQRS-lite request/response/handler files per use case.
- Use existing Domain repositories and `IUnitOfWork`.
- Use `IClock` for UTC time and a new Application abstraction for restaurant local time.
- Use a new Application abstraction for generating IDs required by aggregate factories.
- Add tests with fake repositories and no Infrastructure dependency.

## Phase 2: Planning Handoff

Implementation should proceed in this order:

1. Add Application common result/error contracts and ID/local-time abstractions.
2. Add Catalog query use cases: browse restaurants and get restaurant menu.
3. Add Basket use cases: get cart, add item, update quantity, remove item, clear cart.
4. Add Customer use cases: get profile, update profile, add address, remove address, set default address.
5. Add Ordering use cases: checkout, get order history, get order details.
6. Add Application tests in the same order, with special coverage for checkout and cart conflict outcomes.
7. Run `dotnet build src/Talabat/Talabat.slnx --no-restore` and the Application tests.

Do not start Phase 4 persistence, Phase 5 API endpoints, Phase 7 Delivery Website support, or Phase 8 Identity/Auth while implementing this plan.

## Complexity Tracking

No constitution gate failures or unusual complexity exceptions are required.

The only deliberate additions beyond direct handlers are:

- Application result contracts, needed to keep expected failures transport-neutral.
- Application ID generation abstraction, needed because existing aggregate factories require integer IDs while persistence remains deferred.
- Restaurant local-time abstraction, needed because `CheckoutDomainService` requires restaurant local time and the current Domain model does not yet carry a time-zone policy.

These additions preserve Clean Architecture boundaries and avoid infrastructure decisions in Phase 3.

## Post-Design Constitution Check

- Domain independence: PASS. Design artifacts do not introduce Domain references to Application, Infrastructure, API, EF Core, Identity/Auth, or HTTP.
- Application orchestration: PASS. All write use cases coordinate aggregate methods and `IUnitOfWork`; all read use cases return Application read models.
- Aggregate invariants: PASS. Cross-restaurant cart behavior, cart status, expiration, quantity validation, address defaults, checkout validation, and order snapshots remain Domain-owned.
- Repository boundary: PASS. No child-entity repository contracts are introduced.
- Identity/Auth deferral: PASS. `customerId` is request data for Phase 3; future API/Auth layers can map authenticated principals to that value later.
- Delivery deferral: PASS. Delivery entities and repositories may exist from previous phases, but no Delivery use cases are included in Phase 3 tasks.
- No unresolved clarifications: PASS. Checklist readiness is complete; implementation quality checks are tracked in `tasks.md`.
