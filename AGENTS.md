# Agent Context

<!-- SPECKIT START -->
Current roadmap increment:

- Phase: **Authorization Strategy And Quality Gates** (Phase 10)
- Branch: `feature/user-aggregate-refactor`
- Technical plan: `specs/009-authorization-quality-gates/plan.md`
- Governing scope: `.specify/memory/constitution.md` v3.0.1 -> "Current Phase Scope: User Aggregate Refactor"

Scope guard for the current increment: execute the plan's phases in order for Phase 10.
Key deliverables:
1. Scope enforcement via `ScopeRequirement` + `ScopeHandler` in both API hosts.
2. Named authorization policies (`CustomerAccess`, `CustomerScopeOnly`, `DeliveryAgentAccess`) applied to controllers.
3. Delivery ownership hardening: lifecycle commands carry `AgentId` from token; `AssignDelivery` is self-assignment.
4. New test projects: `Talabat.Domain.Tests`, `Talabat.Delivery.API.Tests`, `Talabat.ArchitectureTests`.
5. Authorization integration tests in both API test projects.
6. GitHub Actions CI workflow (`.github/workflows/ci.yml`).
7. Documentation: `docs/authorization-strategy.md` and `docs/authorization-endpoint-matrix.md`.

Keep business names `CustomerId` and `Delivery.AssignedAgentId` (FKs reference `AspNetUsers.Id`). Domain and Application must remain 100% free of Duende, IdentityServer, JWT, or ASP.NET Core Identity dependencies.
<!-- SPECKIT END -->
