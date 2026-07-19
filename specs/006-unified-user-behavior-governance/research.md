# Research: Unified User Behavior and Governance

**Date**: 2026-07-19  
**Feature**: [spec.md](spec.md)  
**Baseline**: Phase 2 final evidence commit `cce10d0`

## R1 — Phase sequencing and checkpoint

**Decision**: Treat `cce10d0` as the completed Phase 2 runtime/evidence checkpoint. Phase 3
implementation begins only after the complete Phase 3 specification/design artifact set is accepted,
committed, and the worktree is clean.

**Rationale**: The current source, one `InitialUnifiedUser` migration, four test projects, and git
history show the Phase 2 cutover is complete. The repository guidance still requires a recoverable
checkpoint before the next phase.

**Alternatives considered**:

- Repeat Phase 2 implementation because `AGENTS.md` is stale: rejected; it contradicts source and
  commit evidence.
- Start Phase 3 from the uncommitted planning tree: rejected; it violates the explicit clean
  checkpoint rule.

## R2 — Role claim interpretation

**Decision**: Set Customer API JWT `TokenValidationParameters.RoleClaimType` to `"role"`. Role
claims materialize in `ClaimsPrincipal`, but the existing single scalar `UserType` query remains the
operative Customer business gate.

**Rationale**: This meets the authorization-host contract while defending against stale JWT roles.
It does not add a second database lookup or move business authorization into token state.

**Alternatives considered**:

- Trust `principal.IsInRole("Customer")` for Customer access: rejected because tokens can be stale.
- Map roles into Domain/Application types: rejected by dependency boundaries.

## R3 — Owner-scoped cart semantics

**Decision**: Foreign address/order identifiers return 404. `/api/me/cart` continues to select only
the authenticated customer's cart, or its established empty-cart result, because the route accepts
no cart ID or customer ID.

**Rationale**: A blanket “cross-user cart access returns 404” assertion is not representable by the
current public contract. Changing an absent current cart from its established empty success to 404
would create contract drift. Isolation is proven by seeding another user's cart and showing it is
never returned.

**Alternatives considered**:

- Add a cart ID route to manufacture a 404 scenario: rejected as a new API and an ownership risk.
- Return 404 whenever the current user has no cart: rejected because it breaks existing behavior.

## R4 — Delivery-agent initialization guard

**Decision**: `User.IsAvailable()` calls the existing private `RequireAgent()` before comparing the
status. `DeliveryAssignmentDomainService.Assign` continues to use `IsAvailable()`.

**Rationale**: It centralizes capability validation on the aggregate. A Customer-only user gets the
required `DeliveryAgentNotInitializedException`, while an approved Offline/Suspended/Busy agent is
initialized but not available and still gets `AgentNotAvailableException` from assignment.

**Alternatives considered**:

- Duplicate flag/status checks in the Domain service: rejected because the aggregate already owns
  the invariant.
- Add a public role or capability setter/checker for the service: rejected because it weakens
  encapsulation.

## R5 — Capability/role drift proof

**Decision**: For each public capability workflow, reload the user and Identity roles from a fresh
scope and compare roles to the exact projection of `UserType`. Delete the unused DeliveryAgent role
definition immediately before approval to inject failure, then verify rollback from another fresh
scope.

**Rationale**: Fresh tracking state proves committed database outcomes rather than the service's
in-memory entity. Deleting the unused role triggers the real Identity store failure without adding a
production fault-injection hook.

**Alternatives considered**:

- Mock `UserManager`: rejected because it cannot prove shared-context transaction rollback.
- Add a production repair/reconciliation job: rejected; Phase 3 proves prevention and does not add a
  new workflow.

## R6 — Multi-role journey boundary

**Decision**: Run the required journey in Identity tests at endpoint/service level: applicant
registration endpoint, service approval, login endpoint, service customer grant on the same ID, and
fresh role/principal verification.

**Rationale**: The project intentionally has no interactive Identity client, token client, or final
scope flow. A live cross-host browser journey would require out-of-scope identity work. Customer API
profile creation remains independently covered at its endpoint boundary.

**Alternatives considered**:

- Add an interactive client or token endpoint test: rejected by explicit scope.
- Add a project reference from Identity tests to the Customer API host: rejected because it couples
  host tests and complicates shared database/auth setup without improving workflow proof.

## R7 — Concurrency evidence split

**Decision**: Use a real SQL two-context test through `IUnitOfWork` to prove stale rowversion
translation to `ConcurrencyConflictException`, and a Customer API test-only failing save boundary to
prove standard HTTP 409 ProblemDetails.

**Rationale**: Each layer is tested at its responsibility. Creating a deterministic HTTP race would
be slow and flaky; a mapper-only unit test would not prove the controller/handler path.

**Alternatives considered**:

- Race two HTTP requests: rejected due nondeterminism.
- Keep only the existing direct result-mapping test: rejected because it bypasses the endpoint and
  handler path.

## R8 — Existing-session invalidation

**Decision**: Configure a zero security-stamp validation interval only in the Identity test host,
retain the issued cookie, mutate account state, and call an authorized endpoint with that same
cookie. Production remains exactly five minutes.

**Rationale**: This proves “next validation check” without a five-minute test delay. Deactivation
must use `IUserCapabilityService` so the security stamp changes transactionally. Soft deletion uses
the existing persistence path and is rejected when the validator cannot resolve an active user.

**Alternatives considered**:

- Sleep five minutes: rejected as slow and brittle.
- Assert only that the stamp string changed: rejected because Phase 3 requires live-cookie behavior.

## R9 — Governance update scope

**Decision**: Add explicit superseded notes to `specs/003-customer-api/{spec,plan,data-model}.md` and
`phase-7-architecture-guide.md`; rewrite `docs/authorization-matrix.md`; verify the constitution and
amend it only on real divergence.

**Rationale**: These are the governing plan's named contradictory/current artifacts. Historical
body text may remain for traceability only when a prominent note makes it inactive guidance.

**Alternatives considered**:

- Rewrite every historic learning document: rejected as scope expansion.
- Delete historical documents: rejected because they retain roadmap/audit value.

## R10 — Incremental validation

**Decision**: Group by affected test project. After each implementation group, run a solution build
and only the focused affected-project tests. Skip tests for documentation-only work. Run the full
solution tests once at final acceptance.

**Rationale**: This matches the user's explicit productivity policy while preserving confidence at
each layer and the constitution's final full-suite gate.

**Alternatives considered**:

- Full solution tests after every task: rejected as unnecessarily slow.
- No incremental tests until the end: rejected because failures would be expensive to localize.

## R11 — Schema neutrality

**Decision**: Generate no migration. Final checks require the existing
`20260719103927_InitialUnifiedUser` migration and zero pending model changes.

**Rationale**: Phase 3 changes claim interpretation, Domain behavior, tests, and documentation only.
Any model diff is unintended scope drift.

**Alternatives considered**:

- Add authorization data or constraints to the schema: rejected; role-conditional joins and new
  Phase 3 schema are prohibited.
