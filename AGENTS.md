# Agent Context

<!-- SPECKIT START -->
Active spec-kit implementation plan:

- Feature: Persistence And Infrastructure
- Plan: `specs/002-persistence-infrastructure/plan.md`
- Spec: `specs/002-persistence-infrastructure/spec.md`
- Research: `specs/002-persistence-infrastructure/research.md`
- Data model: `specs/002-persistence-infrastructure/data-model.md`
- Contracts: `specs/002-persistence-infrastructure/contracts/persistence-boundary.md`
- Quickstart: `specs/002-persistence-infrastructure/quickstart.md`

Scope guard: Phase 4 covers persistence and infrastructure only. Do not add API endpoints, Identity/Auth implementation, Delivery Application use cases, MediatR, frontend code, repository interface changes, or business-rule changes while implementing this plan. EF Core SQL Server work belongs in Infrastructure, with API changes limited to composition-root wiring and the approved OpenAPI vulnerability fix.
<!-- SPECKIT END -->
