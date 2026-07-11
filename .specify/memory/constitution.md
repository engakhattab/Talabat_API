# Talabat Project Constitution

## Core Architecture Principles

1. Domain layer stays independent from ASP.NET Core, EF Core, Identity/Auth frameworks, HTTP, controllers, database concerns, and external service implementations.
2. Application layer orchestrates use cases, coordinates aggregate roots through abstractions, and returns transport-neutral outcomes.
3. Domain entities and aggregate roots protect invariants; child entities are modified only through aggregate roots.
4. Repository contracts are defined for aggregate roots only. Infrastructure implements persistence and external integrations later.
5. API layer is responsible for request/response mapping, endpoint wiring, authentication middleware, and authorization policies when that phase is reached.
6. Identity/Auth remains a deferred cross-cutting boundary. Do not choose a framework or introduce framework-specific identity types in Domain or Application during Phase 3.
7. Business logic must not be placed in controllers, transport adapters, persistence mappings, or UI layers.
8. Phase 3 must not add EF Core persistence, migrations, Identity packages, frontend code, or production API endpoints.

## Quality Gates

- Every use case must map to a documented business workflow or requirement.
- Application contracts must stay independent of HTTP response types and persistence implementation details.
- Checkout must preserve atomic business intent: create one order and close one cart only when checkout succeeds.
- Customer ownership boundaries must be explicit without requiring a selected authentication framework.
- Delivery workflows are deferred out of Phase 3 unless the roadmap is explicitly revised.
