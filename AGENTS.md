# Agent Context

<!-- SPECKIT START -->
Current roadmap increment:

- Phase: **Phase 10 Remediation** (8 critical blockers from audit)
- Branch: `feature/user-aggregate-refactor`
- Technical plan: `specs/010-phase-10-remediation/plan.md`
- Governing scope: `.specify/memory/constitution.md` v3.0.1 -> "Current Phase Scope: User Aggregate Refactor"

Scope guard for the current increment: execute the remediation plan's phases in order (Phase 0 → Phase 8).
Key deliverables:
1. FR-R001: `MapInboundClaims = false` fix for real JWT pipeline.
2. FR-R002: Pending-delivery capability check + PII reduction.
3. FR-R003: Delivery creation after checkout (BR-DEL-016).
4. FR-R004: Agent approval endpoints (Development-gated).
5. FR-R005: Delivery concurrency token (`RowVersion`).
6. FR-R006: Identity host hardening.
7. FR-R007: CI quality gates (SQL Server container, vulnerability check).
8. FR-R008: Ownership convergence (controller-injection, `[RequireCustomerProfile]`, `ICurrentUserCapabilityResolver`, architecture tests).

Keep business names `CustomerId` and `Delivery.AssignedAgentId` (FKs reference `AspNetUsers.Id`). Domain and Application must remain 100% free of Duende, IdentityServer, JWT, or ASP.NET Core Identity dependencies.
<!-- SPECKIT END -->
