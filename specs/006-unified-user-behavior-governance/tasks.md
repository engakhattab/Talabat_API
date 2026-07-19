# Tasks: Unified User Behavior and Governance

**Input**: Design documents from `specs/006-unified-user-behavior-governance/`  
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)  
**Tests**: Required by the feature specification and the governing Phase 3 acceptance criteria  
**Implementation status**: **ACTIVE** — T001-T004 complete, Phase 3 planning checkpoint committed

## Execution Rules

- Execute tasks in ID order unless a task has `[P]` and every stated prerequisite is complete.
- T001-T004 are a hard stop. Do not edit runtime or test code before the planning checkpoint is
  committed and `git status --short` is empty.
- Keep `UserType` and Identity role mutations inside the existing `IUserCapabilityService` workflow;
  Domain aggregate methods remain the only internal flag mutators.
- Preserve the exact Phase 2 `ProfileNotCreated` response bytes and the business names `CustomerId`,
  `AssignedAgentId`, `CustomerProfile`, and `DeliveryAgentStatus`.
- Follow the incremental validation policy: one solution build after each grouped change, then only
  the affected test project/filter. Do not run the full solution suite before T040-T041.
- Documentation-only tasks T035-T039 require searches and `git diff --check`, not builds or tests.
- Generate no migration and do not modify the configured development database.
- Do not add a full Delivery API, admin endpoint/UI, interactive Identity client, discount behavior,
  role-selection input, capability revocation, or data-preserving migration.

## Phase 1: Setup and Phase 3 Planning Checkpoint

**Purpose**: Prove Phase 2 is the accepted baseline and create the required clean Phase 3 checkpoint
before implementation.

- [X] T001 Verify `feature/user-aggregate-refactor`, confirm `cce10d0` is an ancestor of `HEAD`, inspect `git status --short`, and stop on any mismatch before changing `specs/006-unified-user-behavior-governance/tasks.md`
  - Branch: `feature/user-aggregate-refactor` ✓
  - Ancestor: `cce10d0` is ancestor of HEAD ✓
  - Status: modified `.specify/feature.json`, `AGENTS.md`, `ProfileEnforcementFilter.cs`, `Program.cs`, `Delivery.API/Program.cs`, `UserCapabilityService.cs`; untracked `specs/006/`
- [X] T002 Review `specs/006-unified-user-behavior-governance/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/behavior-proof.md`, `contracts/customer-api-authorization.md`, and `quickstart.md`; record any contract conflict in `specs/006-unified-user-behavior-governance/tasks.md` and stop rather than inventing behavior
  - All documents reviewed. No contract conflicts found. Research decisions R1-R11 consistent with implementation plan.
- [X] T003 Run the preflight `dotnet build src/Talabat/Talabat.slnx` without running the full test suite, require success, and record the result in `specs/006-unified-user-behavior-governance/tasks.md`
  - Build succeeded: 0 Warning(s), 0 Error(s). All 10 projects compiled successfully.
- [X] T004 Mark T001-T004 evidence in `specs/006-unified-user-behavior-governance/tasks.md`, update the marker-bounded Phase 3 status in `AGENTS.md` from planning-blocked to implementation-active, obtain the authorized checkpoint commit containing `.specify/feature.json`, `AGENTS.md`, and `specs/006-unified-user-behavior-governance/`, record its hash in the execution transcript, and require empty `git status --short`
  - Evidence recorded above. Checkpoint commit: `4ba16aa`
  - Working tree status: clean

**Checkpoint**: Phase 3 runtime/test edits are authorized only after T004 succeeds.

---

## Phase 2: Foundational Baseline Gates

**Purpose**: Freeze the structural and schema-neutral baselines shared by every story.

- [ ] T005 Run the removed-symbol, role/capability-mutation-owner, and route/body `customerId` baseline searches from `specs/006-unified-user-behavior-governance/quickstart.md` against `src/Talabat/`; record reviewed allowed hits and require zero active violations in `specs/006-unified-user-behavior-governance/tasks.md`
- [ ] T006 Run the migration list and pending-model checks from `specs/006-unified-user-behavior-governance/quickstart.md` using `src/Talabat/Talabat.Infrastructure` and `src/Talabat/Talabat.API`; require exactly `20260719103927_InitialUnifiedUser`, zero pending model changes, and no database update command

**Checkpoint**: The implementation baseline has one unified user model, no ownership-input leak, and
no Phase 3 schema delta.

---

## Phase 3: User Story 1 — Use One Account as Delivery Agent and Customer (Priority: P1)

**Goal**: Prove an approved delivery agent adds Customer capability to the same integer-key account,
retains both capabilities/roles, and exposes all applicable roles in the Customer API principal.

**Independent Test**: Register an applicant through Identity, approve and log in, grant Customer to
the same ID, then verify one persisted user with both flags/roles and a principal containing both
roles; separately verify the Customer API uses raw `role` claims without replacing the stored
capability gate.

### Tests and Implementation

- [ ] T007 [P] [US1] Set `TokenValidationParameters.RoleClaimType = "role"` in `src/Talabat/Talabat.API/Program.cs` without changing issuer/lifetime/signing validation or the persisted capability lookup
- [ ] T008 [P] [US1] Extend `tests/Talabat.Customer.API.Tests/Infrastructure/TestAuthHandler.cs` with deterministic Customer/DeliveryAgent `role` claims and construct the test identity with `"role"` as its role claim type while retaining existing subject-header cases
- [ ] T009 [P] [US1] Add a reusable Phase 3 Identity test factory in `tests/Talabat.Identity.Tests/Infrastructure/IdentityWebApplicationFactory.cs` that targets an isolated fixture database, preserves the production Identity registrations, and permits test-only option overrides without changing existing production configuration
- [ ] T010 [US1] Create `tests/Talabat.Customer.API.Tests/OwnershipTests.cs` with production JWT-option and test-principal assertions proving role claim type `role`, both roles materialize, and role presence alone does not replace current stored Customer capability; add a same-user multi-role journey case that exercises the existing browse, cart, order, and checkout paths through one authenticated principal and proves no second account or linkage record is created
- [ ] T011 [US1] Create `tests/Talabat.Identity.Tests/MultiRoleJourneyTests.cs` using `tests/Talabat.Identity.Tests/Infrastructure/IdentityWebApplicationFactory.cs` to register one applicant endpoint, approve via `IUserCapabilityService`, log in, grant Customer to the same ID, reload a fresh user/principal, and assert one account with both flags/roles and preserved Approved/Offline agent state
- [ ] T012 [US1] Run `dotnet build src/Talabat/Talabat.slnx --no-restore` after T007-T011 and fix only compile errors in the Phase 3 files named by those tasks
- [ ] T013 [US1] Run `dotnet test tests/Talabat.Customer.API.Tests/Talabat.Customer.API.Tests.csproj --no-build --filter "FullyQualifiedName~OwnershipTests|FullyQualifiedName~AuthEnforcementTests"` and preserve all existing 401/404/409 assertions
- [ ] T014 [US1] Run `dotnet test tests/Talabat.Identity.Tests/Talabat.Identity.Tests.csproj --no-build --filter "FullyQualifiedName~MultiRoleJourneyTests"` and require the one-ID/two-capability/two-role journey to pass against real SQL

**Checkpoint**: Journey A is independently demonstrable without a second user, interactive client,
or new endpoint.

---

## Phase 4: User Story 2 — Keep Customer Operations Owner-Scoped (Priority: P1)

**Goal**: Prove every `/api/me/*` target comes from the authenticated current Customer, foreign
resource identifiers do not leak existence, and another user's cart is never selected.

**Independent Test**: Seed customer A and customer B with distinct addresses, carts, and orders;
authenticate as A; require foreign address/order access to return 404, the cart route to expose only
A's cart or the established empty result, malformed subjects to return 401, and no caller-provided
CustomerId authority.

### Tests and Implementation

- [ ] T015 [US2] Extend `tests/Talabat.Customer.API.Tests/Infrastructure/CustomWebApplicationFactory.cs` with isolated customer A/customer B setup, generated-ID accessors, distinct owned addresses/carts/orders, and a scoped service-override hook; do not hard-code Identity or aggregate IDs or weaken existing fixture isolation
- [ ] T016 [US2] Extend `tests/Talabat.Customer.API.Tests/OwnershipTests.cs` with stale-role/current-flag denial, malformed/non-positive-subject 401, foreign address and order ID 404, and current-cart isolation assertions matching `specs/006-unified-user-behavior-governance/contracts/customer-api-authorization.md`
- [ ] T017 [US2] Run `rg` ownership scans against `src/Talabat/Talabat.API/Controllers/` and `src/Talabat/Talabat.API/Contracts/`, require no `[FromRoute]`/`[FromBody]` CustomerId authority, and record response-only retained `CustomerId` hits in `specs/006-unified-user-behavior-governance/tasks.md`
- [ ] T018 [US2] Run `dotnet build src/Talabat/Talabat.slnx --no-restore` after T015-T017 and keep `src/Talabat/Talabat.API/Auth/CurrentUser.cs`, controllers, and `ProfileEnforcementFilter.cs` unchanged unless a new test proves a contract defect
- [ ] T019 [US2] Run `dotnet test tests/Talabat.Customer.API.Tests/Talabat.Customer.API.Tests.csproj --no-build --filter "FullyQualifiedName~OwnershipTests"` and require deterministic 401/404/cart-isolation results with no permissive multi-status assertions

**Checkpoint**: Owner-scoped Customer API behavior is independently proven without changing the
empty-cart or `ProfileNotCreated` contracts.

---

## Phase 5: User Story 3 — Authorize Delivery Assignment at Both Boundaries (Priority: P1)

**Goal**: Distinguish users without DeliveryAgent capability from initialized but unavailable agents,
while preserving assignment and release transitions.

**Independent Test**: Customer-only assignment throws `DeliveryAgentNotInitializedException`;
approved Offline/Suspended/Busy users cannot be assigned; Available assignment makes the same user
Busy; completion and cancellation return the matching agent to Available.

### Tests and Implementation

- [ ] T020 [US3] Add failing-first cases in `tests/Talabat.Application.Tests/DeliveryDomain/AgentAssignmentAuthorizationTests.cs` for Customer-only, Offline, Suspended, Busy, Available assignment, matching completion, matching cancellation, and unchanged `AssignedAgentId`; document the Application-boundary role requirement as a future-entry-point contract because Phase 3 introduces no Delivery API or Application assignment handler
- [ ] T021 [US3] Update `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs` so `IsAvailable()` calls the existing private `RequireAgent()` before comparing `DeliveryAgentStatus`; do not expose internal reserve/release methods or change any legal status transition
- [ ] T022 [US3] Run `dotnet build src/Talabat/Talabat.slnx --no-restore` after T020-T021 and require the Delivery API scaffold to compile without editing `src/Talabat/Talabat.Delivery.API/`
- [ ] T023 [US3] Run `dotnet test tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj --no-build --filter "FullyQualifiedName~AgentAssignmentAuthorizationTests|FullyQualifiedName~DeliveryAssignmentDomainServiceTests|FullyQualifiedName~UserAgentLifecycleTests"` and preserve double-approve, reject/reapply, busy/release, and status-machine behavior

**Checkpoint**: The Domain half of the two-level assignment rule is green; future Application call
sites must check the DeliveryAgent role, but no full Delivery API is introduced.

---

## Phase 6: User Story 4 — Fail Safely Under Drift, Concurrency, and Account Blocking (Priority: P1)

**Goal**: Prove atomic capability/role projection, deterministic stale-write conflicts, and rejection
of existing cookies after deactivation or soft deletion.

**Independent Test**: Compare every workflow's flags/roles from fresh SQL scopes, inject a missing
DeliveryAgent role and prove full rollback, race two contexts and preserve the first writer with a
Domain conflict/HTTP 409, then reuse pre-block cookies and receive 401 at the next validation.

### Tests and Implementation

- [ ] T024 [P] [US4] Create `tests/Talabat.Infrastructure.Tests/Identity/CapabilityRoleDriftTests.cs` covering RegisterCustomer, RegisterApplicant, GrantCustomer, ApproveAgent, RejectAgent, and DeactivateUser with fresh-scope exact role projection, plus missing-DeliveryAgent-role approval rollback of flags, roles, approval/status, and security stamp
- [ ] T025 [P] [US4] Create `tests/Talabat.Infrastructure.Tests/Persistence/ConcurrencyConflictTests.cs` with two independent contexts and `IUnitOfWork` instances proving writer A persists, writer B receives `ConcurrencyConflictException`, writer A's values survive, and rowversion advances; also update two different address rows concurrently and prove those independent address-row writes do not require a user-row conflict
- [ ] T026 [P] [US4] Add `tests/Talabat.Customer.API.Tests/Infrastructure/ThrowingUnitOfWork.cs` implementing test-only deterministic `ConcurrencyConflictException` injection without changing production `IUnitOfWork` or `UnitOfWork`
- [ ] T027 [US4] Create `tests/Talabat.Customer.API.Tests/ConcurrencyConflictEndpointTests.cs` using the factory service-override hook and `ThrowingUnitOfWork` to call authenticated profile update and assert HTTP 409 ProblemDetails, `ConcurrencyConflict`, the existing Domain message, and no persistence detail leakage
- [ ] T028 [P] [US4] Extend `tests/Talabat.Identity.Tests/Infrastructure/IdentityWebApplicationFactory.cs` with a test-only `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` mode while leaving `src/Talabat/Talabat.Identity/Program.cs` at exactly five minutes
- [ ] T029 [US4] Create `tests/Talabat.Identity.Tests/SessionInvalidationTests.cs` that logs in and retains a cookie, deactivates through `IUserCapabilityService`, reuses the cookie for `/account/me` and gets 401, then repeats existing-cookie rejection after soft deletion while proving business history remains stored
- [ ] T030 [US4] Review existing double-approve/reject-reapply coverage in `tests/Talabat.Application.Tests/Domain/Users/UserAgentLifecycleTests.cs` and existing workflow failure coverage in `tests/Talabat.Infrastructure.Tests/Identity/UserCapabilityServiceTests.cs`; add assertions only where the new fresh-scope projection contract is not already covered
- [ ] T031 [US4] Run `dotnet build src/Talabat/Talabat.slnx --no-restore` once after T024-T030 and fix only files directly involved in the three affected test projects or an exposed production defect
- [ ] T032 [US4] Run `dotnet test tests/Talabat.Infrastructure.Tests/Talabat.Infrastructure.Tests.csproj --no-build --filter "FullyQualifiedName~CapabilityRoleDriftTests|FullyQualifiedName~ConcurrencyConflictTests"` and require real SQL execution rather than accepting an unavailable-environment skip as acceptance evidence
- [ ] T033 [US4] Run `dotnet test tests/Talabat.Customer.API.Tests/Talabat.Customer.API.Tests.csproj --no-build --filter "FullyQualifiedName~ConcurrencyConflictEndpointTests"` and require the endpoint/handler/save path to produce the standard 409 contract
- [ ] T034 [US4] Run `dotnet test tests/Talabat.Identity.Tests/Talabat.Identity.Tests.csproj --no-build --filter "FullyQualifiedName~SessionInvalidationTests"` and require same-cookie rejection without waiting five production minutes

**Checkpoint**: Drift, rollback, concurrency, and live-session failure paths are independently green
in their owning test projects.

---

## Phase 7: User Story 5 — Close the Refactor with Consistent Governance (Priority: P2)

**Goal**: Make the active architecture and authorization guidance consistent with the completed
unified-user runtime while retaining superseded history explicitly as history.

**Independent Test**: Every named current document describes integer `User.Id`, current capability,
the four roles, approval, ownership, concurrency, and `ProfileNotCreated`; historical Phase 7 text is
prominently superseded; contradiction searches find no unmarked active rule.

### Documentation

- [ ] T035 [US5] Compare `.specify/memory/constitution.md` v3.0.1 with accepted Phase 1-3 behavior and record the review in `specs/006-unified-user-behavior-governance/tasks.md`; amend/version the constitution only if an actual accepted divergence is found
- [ ] T036 [P] [US5] Add prominent superseded-by notes to `specs/003-customer-api/spec.md`, `specs/003-customer-api/plan.md`, and `specs/003-customer-api/data-model.md` linking `user-aggregate-refactor-plan.md` and `specs/004-unified-user-domain-model/`, `specs/005-unified-user-identity-cutover/`, and `specs/006-unified-user-behavior-governance/`
- [ ] T037 [P] [US5] Rewrite `docs/authorization-matrix.md` for integer `sub = User.Id`, persisted Customer capability, four roles, delivery-agent approval, owner-derived `CustomerId`, foreign-resource 404, cart isolation, and exact missing-capability `ProfileNotCreated` behavior
- [ ] T038 [P] [US5] Add a prominent superseded/current-reference note to `phase-7-architecture-guide.md` while preserving its obsolete split-profile walkthrough only as historical context
- [ ] T039 [US5] Run the documentation validation and contradiction searches from `specs/006-unified-user-behavior-governance/quickstart.md`, verify every new relative link resolves, require no unmarked active contradiction, and run `git diff --check` without building or testing

**Checkpoint**: Current guidance is unified-user consistent; obsolete material is explicitly marked
historical rather than silently authoritative.

---

## Final Phase: Polish and Cross-Cutting Acceptance

**Purpose**: Run the one allowed full validation milestone and close the three-phase refactor with
structural, schema, dependency, documentation, and worktree evidence.

- [ ] T040 Run `dotnet restore src/Talabat/Talabat.slnx` then `dotnet build src/Talabat/Talabat.slnx --no-restore`; require all production and four test projects to compile, including untouched `src/Talabat/Talabat.Delivery.API/`
- [ ] T041 Run the single final `dotnet test src/Talabat/Talabat.slnx --no-build`; require all four test projects and all six Phase 3 behavior groups to pass, with no real-SQL acceptance skip
- [ ] T042 Run `dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive` and `dotnet list src/Talabat/Talabat.Domain package`; require zero known vulnerabilities and only `Microsoft.Extensions.Identity.Stores` 10.0.9 in the Domain project
- [ ] T043 Run `dotnet ef migrations list` and `dotnet ef migrations has-pending-model-changes` with `src/Talabat/Talabat.Infrastructure` and `src/Talabat/Talabat.API`; require exactly `20260719103927_InitialUnifiedUser`, no pending model change, and no Phase 3 migration file
- [ ] T044 Run the removed-symbol, role/capability-mutation-owner, and route/body CustomerId sweeps from `specs/006-unified-user-behavior-governance/quickstart.md`; require zero active violations while retaining allowed business names
- [ ] T045 Run the active-document contradiction sweep over `.specify/`, `docs/`, `AGENTS.md`, and `phase-7-architecture-guide.md`; review every hit and require obsolete rules to sit only beneath explicit superseded/repealed notices
- [ ] T046 Compare `src/Talabat/Talabat.Delivery.API/` against Phase 2 checkpoint `cce10d0`, require no Phase 3 source change, and rely on T040 for compile evidence rather than adding Delivery API tests or behavior
- [ ] T047 Review T040-T046 evidence against every acceptance scenario, FR-001-FR-036, and SC-001-SC-009 in `specs/006-unified-user-behavior-governance/spec.md`; record the final results in `specs/006-unified-user-behavior-governance/tasks.md` and update the marker-bounded increment status in `AGENTS.md` to Phase 3 final acceptance complete
- [ ] T048 Run `git diff --check` and review `git status --short`, obtain authorization for the final Phase 3 checkpoint, commit only the intended Phase 3 code/tests/docs/evidence, and require a clean worktree without recording the commit's own hash in a tracked file

---

## Dependencies

### Phase dependency graph

```text
Setup T001-T004
  -> Foundational T005-T006
      -> US1 T007-T014
          -> US2 T015-T019
      -> US3 T020-T023
          -> US4 T024-T034
US1 + US2 + US3 + US4
  -> US5 T035-T039
      -> Final T040-T048
```

### User story dependencies

| Story | Depends on | Independently testable after |
|---|---|---|
| US1 — Multi-role account | Foundational baseline | T014 |
| US2 — Customer ownership | US1 test role support and `OwnershipTests.cs` baseline | T019 |
| US3 — Assignment authorization | Foundational baseline only; may proceed independently of US1/US2 | T023 |
| US4 — Failure safety | US1 Identity factory, US2 Customer API fixture override, and US3 lifecycle baseline | T034 |
| US5 — Governance | All runtime/test stories accepted so docs describe final behavior | T039 |

US1 is the demonstration MVP. It is not the feature acceptance boundary: the Phase 3 feature remains
incomplete until US2-US5 and the final gate pass.

## Parallel Execution Examples

### User Story 1

After T005-T006, T007 (`Program.cs`), T008 (`TestAuthHandler.cs`), and T009
(`IdentityWebApplicationFactory.cs`) may run in parallel. After those land, T010 and T011 target
different test projects and may run in parallel. Converge for T012-T014.

### User Story 2 and User Story 3

US3 T020-T023 may run in parallel with US1/US2 after the foundational gate because it edits only
`User.cs` and Application tests. Within US2, T015 must finish before T016; T017 can run alongside the
test work because it is read-only, then converge for T018-T019.

### User Story 4

After US1-US3 prerequisites, T024, T025, T026, and T028 target separate files/projects and may run in
parallel. T027 follows T026 and the US2 fixture hook; T029 follows T028. T030 is a read/review task
that may run alongside those additions. Converge once for T031, then T032-T034 may run in parallel if
the SQL test environment supports concurrent isolated databases.

### User Story 5

After T035 confirms no constitution amendment is needed, T036, T037, and T038 edit disjoint Markdown
files and may run in parallel. Converge for the documentation-only gate T039.

## Implementation Strategy

### Demonstration MVP

Complete Setup, Foundational, and US1 (T001-T014). This demonstrates one applicant becoming an
approved multi-role customer on the same integer-key account and proves role materialization without
trusting role claims as business state.

### Incremental delivery order

1. **Checkpoint first**: T001-T004; no runtime/test edits on a dirty planning tree.
2. **Freeze invariants**: T005-T006; zero legacy/ownership/schema drift at baseline.
3. **Multi-role identity**: US1; smallest business demonstration.
4. **Owner and assignment hardening**: US2 and US3; independently testable after their prerequisites.
5. **Failure proof**: US4; real SQL rollback/concurrency plus HTTP/session boundaries.
6. **Governance**: US5; documentation-only and no redundant tests.
7. **Final milestone**: T040-T048; one restore/build/full-test/audit/sweep cycle and authorized clean
   checkpoint.

### Validation discipline

- Build once per grouped story phase, not after every file edit.
- Run only the affected project/filter named by that story.
- Expand a focused run only when a shared production/test-support change directly affects an existing
  regression class listed in the task.
- Run the entire solution test suite only at T041.
- Do not rerun tests after T047 documentation/evidence-only edits; use `git diff --check` and content
  review instead.

## Implementation Notes

- Phase 2 runtime/evidence baseline: `cce10d0`.
- Phase 3 planning checkpoint: record during T004 after authorization; do not write its hash here if
  doing so would dirty the just-created checkpoint.
- SQL-backed test groups require Docker/Testcontainers or LocalDB. An unavailable environment is a
  blocker for final acceptance, not permission to mark tests complete.
- Record execution blockers and focused validation evidence here as tasks are completed. Do not
  weaken exact response assertions or add permissive status alternatives to make a red test green.
