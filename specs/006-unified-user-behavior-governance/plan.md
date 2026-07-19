# Implementation Plan: Unified User Behavior and Governance

**Branch**: `feature/user-aggregate-refactor` | **Date**: 2026-07-19 | **Spec**: [spec.md](spec.md)  
**Input**: Phase 3 of [user-aggregate-refactor-plan.md](../../user-aggregate-refactor-plan.md) and
the feature specification in [spec.md](spec.md)  
**Implementation status**: **BLOCKED** until this Phase 3 planning artifact set is accepted,
committed as the required checkpoint, and `git status --short` is clean. Phase 2 runtime work is
complete at commit `cce10d0`.

## Summary

Phase 3 proves the unified user cutover under real business journeys and failure conditions, adds
the single missing Customer API role-claim mapping, makes delivery assignment distinguish a user
without agent capability from an approved but unavailable agent, hardens owner-scoped test evidence,
and replaces contradictory current guidance with the unified-user rules.

The runtime delta is deliberately small: one authentication option in the Customer API and one
Domain guard call before availability is reported. Most work is organized into six named behavior
test groups across the four existing test projects. There is no schema or migration change, no new
endpoint, and no Delivery API implementation.

Validation follows the user-provided productivity policy. Related changes are grouped by affected
test project. Each implementation group ends with `dotnet build` plus only that project's focused
tests. Documentation-only work uses searches and link/content review without tests. The full
solution test suite runs once, at final acceptance.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`)  
**Primary Dependencies**: ASP.NET Core Identity/JWT Bearer 10.0.9, EF Core SQL Server 10.0.9,
Duende IdentityServer 8.0.2, existing `Microsoft.Extensions.Identity.Stores` 10.0.9 approved Domain dependency exception  
**Storage**: Existing SQL Server `TalabatDbContext`; `AspNetUsers` unified aggregate;
`RowVersion` SQL rowversion; existing `20260719103927_InitialUnifiedUser` migration only  
**Testing**: xUnit; Application Domain/unit tests; Infrastructure SQL Server integration tests with
Testcontainers/LocalDB fallback; Identity and Customer API `WebApplicationFactory` tests  
**Target Platform**: Existing ASP.NET Core backend hosts; Windows development SQL Server with
cross-platform runtime support retained  
**Project Type**: Layered backend solution with Customer API, Identity, and compile-only Delivery
API hosts  
**Performance Goals**: No new database lookup per Customer API request; role interpretation is
claim-local; existing single scalar persisted-capability lookup remains the business gate  
**Constraints**: One user row and integer subject; no caller-owned role or customer ID; no schema
change; flags remain business truth; role mutations remain exclusive to `IUserCapabilityService`;
preserve exact `ProfileNotCreated` bodies and retained business names; no web/EF types in Domain or
Application; no full Delivery API  
**Scale/Scope**: Two minimal production edits, six behavior-proof groups, test-support hardening,
five named documentation updates, structural sweeps, and one final full acceptance gate

### Validation Policy

| Change group | Default validation | Focused test validation |
|---|---|---|
| Customer API authentication/ownership/concurrency | `dotnet build src/Talabat/Talabat.slnx --no-restore` | `Talabat.Customer.API.Tests` filters only |
| Domain delivery assignment behavior | solution build | `Talabat.Application.Tests` assignment filters only |
| Capability drift and rowversion conflict | solution build | `Talabat.Infrastructure.Tests` new groups only |
| Multi-role and live-session behavior | solution build | `Talabat.Identity.Tests` new groups only |
| Documentation/governance | no build or tests | link, content, and symbol searches only |
| Final acceptance | restore + solution build | one full `dotnet test` over the solution |

Do not run the full solution test suite between work packages. If a focused test exposes a shared
regression, expand only to the directly related existing test class/project before considering the
final suite.

## Constitution Check

| Gate | Status | Design evidence |
|---|---|---|
| Domain dependency boundary | PASS | The only Domain edit is an aggregate guard call; no package or framework type is added. |
| Application dependency boundary | PASS | Assignment authorization is documented as an Application boundary; `ClaimsPrincipal` and Identity types do not enter Application. |
| Single unified user model | PASS | All journeys retain one integer `User.Id`; no alternate account/profile model is introduced. |
| Capability/role synchronization | PASS | Drift tests exercise the existing exclusive workflow; no new mutation path is added. |
| Thin hosts | PASS | Customer API changes only JWT claim interpretation and testable transport ownership; controllers continue delegating. |
| Owner-scoped identity | PASS | All `/api/me/*` commands use `ICurrentUser`; route/body `customerId` remains forbidden. |
| Concurrency | PASS | Real two-context proof covers rowversion and the existing conflict translation; HTTP proof covers standard 409 output. |
| Session invalidation | PASS | Existing security-stamp workflow and five-minute production interval remain unchanged; tests compress the interval only in the test host. |
| Persistence integrity | PASS | No model change is planned; final migration and pending-model checks prove schema neutrality. |
| Phase sequencing | PASS with hard preflight | Phase 2 is committed at `cce10d0`; Phase 3 code waits for an accepted, committed, clean planning checkpoint. |
| Validation proportionality | PASS | Focused project tests run incrementally; all four suites run once at final acceptance. |

No constitution exception is requested.

## Project Structure

### Documentation

```text
specs/006-unified-user-behavior-governance/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── behavior-proof.md
│   └── customer-api-authorization.md
└── checklists/
    └── requirements.md

specs/003-customer-api/
├── spec.md                         # superseded note
├── plan.md                         # superseded note
└── data-model.md                   # superseded note

docs/authorization-matrix.md        # rewritten current rules
phase-7-architecture-guide.md       # superseded note
AGENTS.md                           # Phase 3 active-plan pointer
```

`tasks.md` is not created by this workflow. Generate it with `speckit-tasks` after this plan is
accepted.

### Source Code

```text
src/Talabat/
├── Talabat.Domain/
│   └── Aggregates/Users/User.cs
├── Talabat.Application/
│   └── Common/Results/             # unchanged conflict mapping contract
├── Talabat.Infrastructure/
│   ├── Identity/UserCapabilityService.cs     # behavior under proof; no planned edit
│   └── Persistence/UnitOfWork.cs              # behavior under proof; no planned edit
├── Talabat.API/
│   ├── Program.cs
│   ├── Auth/CurrentUser.cs                    # behavior under proof; edit only if a test exposes a contract defect
│   ├── Middleware/ProfileEnforcementFilter.cs # exact compatibility oracle; no planned edit
│   └── Controllers/                           # ownership sweep; no caller customer ID
├── Talabat.Identity/
│   └── Program.cs                             # five-minute production interval remains unchanged
└── Talabat.Delivery.API/                      # compile-only, untouched

tests/
├── Talabat.Application.Tests/
│   └── DeliveryDomain/AgentAssignmentAuthorizationTests.cs
├── Talabat.Infrastructure.Tests/
│   ├── Identity/CapabilityRoleDriftTests.cs
│   └── Persistence/ConcurrencyConflictTests.cs
├── Talabat.Identity.Tests/
│   ├── MultiRoleJourneyTests.cs
│   └── SessionInvalidationTests.cs
└── Talabat.Customer.API.Tests/
    ├── OwnershipTests.cs
    ├── ConcurrencyConflictEndpointTests.cs
    └── Infrastructure/
        ├── TestAuthHandler.cs
        └── CustomWebApplicationFactory.cs
```

Test-support filenames may be split only when it reduces shared mutable fixture state; do not add a
fifth test project or a cross-host production dependency.

## Phase 0: Research

All planning unknowns are resolved in [research.md](research.md). Binding decisions are:

| ID | Decision |
|---|---|
| R1 | Treat `cce10d0` as the completed Phase 2 runtime/evidence checkpoint; require the Phase 3 planning commit and clean tree before code. |
| R2 | Set Customer API `TokenValidationParameters.RoleClaimType` to `"role"`; roles materialize in the principal, while persisted `UserType` remains the business gate. |
| R3 | Preserve cart's current-user/empty result because the route accepts no cart/customer ID; use 404 anti-disclosure for foreign address/order IDs. |
| R4 | Make `User.IsAvailable()` run `RequireAgent()` before comparing status so Customer-only assignment gets `DeliveryAgentNotInitializedException`; approved Offline still gets `AgentNotAvailableException`. |
| R5 | Prove role projection after every public capability workflow and missing-role rollback from a fresh scope; do not add a repair job or mutation API. |
| R6 | Exercise the multi-role journey at Identity endpoint/service level because interactive clients and cross-host token acquisition remain out of scope. |
| R7 | Split concurrency proof: real SQL/two-context conflict in Infrastructure and controlled HTTP conflict translation in Customer API. |
| R8 | Test cookie invalidation with a zero validation interval only in the test host; production remains exactly five minutes. |
| R9 | Add superseded notes to the three Phase 7 artifacts and architecture guide, rewrite the authorization matrix, and amend the constitution only for an actual accepted divergence. |
| R10 | Use build-first, affected-project tests for each group, no tests for docs, and a single final full solution test run. |
| R11 | Generate no migration; require exactly the existing `InitialUnifiedUser` and zero pending model changes. |

## Phase 1: Design And Contracts

### Data model

[data-model.md](data-model.md) records that Phase 3 has no persisted-model delta. It freezes the
existing User/capability/role projection, agent lifecycle, ownership relationships, rowversion
conflict, and session invalidation behavior needed by the six test groups.

### Customer API authorization contract

[contracts/customer-api-authorization.md](contracts/customer-api-authorization.md) freezes:

- raw `role` claim materialization without using it as Customer business truth;
- positive integer subject resolution and malformed-subject 401;
- current-user-only `/api/me/*` targeting;
- 404 for foreign address/order IDs and cart isolation through its current-user-only route;
- byte-identical `ProfileNotCreated` responses; and
- standard concurrency 409 ProblemDetails fields.

### Behavior proof contract

[contracts/behavior-proof.md](contracts/behavior-proof.md) maps each required test group to its
arrangement, action, and evidence, including exact flag/role projection, missing-role rollback, one
ID through the multi-role journey, assignment guards, two-context rowversion behavior, and live
cookie rejection.

### Developer validation guide

[quickstart.md](quickstart.md) provides the preflight, one affected-project command per work package,
documentation searches, final solution gate, migration/package checks, and retained-name-aware
symbol sweeps.

## Phase 2: Planning Handoff

### WP0 — Phase 2 evidence and Phase 3 checkpoint (hard stop)

Before any production/test edit:

1. Confirm branch `feature/user-aggregate-refactor` and Phase 2 final checkpoint `cce10d0` is an
   ancestor of `HEAD`.
2. Review the Phase 2 final evidence commit and the immutable Phase 3 spec/contracts.
3. Commit the complete Phase 3 planning artifact set, record that commit, and require empty
   `git status --short`.
4. Run `dotnet restore` only if dependencies are not already restored, then run the solution build.
   Do not rerun all tests here; `cce10d0` already records the Phase 2 full-suite milestone.

**Done gate**: clean tree, recorded planning checkpoint, solution builds, and no Phase 3 runtime
edit has started early.

### WP1 — Customer API claim wiring, ownership proof, and HTTP conflict proof

**Production**:

- In `src/Talabat/Talabat.API/Program.cs`, set
  `TokenValidationParameters.RoleClaimType = "role"` alongside the existing validation settings.
- Do not replace the persisted `UserType` lookup in `CurrentUser`, add a role-only business gate, or
  accept a customer ID from transport input.

**Test support and evidence**:

- Extend `TestAuthHandler` with deterministic `role` claims and a role claim type of `"role"`.
- Extend `CustomWebApplicationFactory` with isolated, discoverable customer A/customer B fixtures
  and owned addresses/carts/orders, without hard-coding database-generated IDs.
- Add `OwnershipTests`: production JWT option uses `"role"`; roles materialize in the test
  principal; a stale Customer role cannot bypass a missing stored capability; malformed subjects
  return empty 401; customer A cannot load customer B's address/order (404); `/api/me/cart` exposes
  only A's cart or established empty result; no request customer ID is authoritative.
- Add `ConcurrencyConflictEndpointTests`: replace `IUnitOfWork` only in the test host with a
  deterministic stale-save failure, call the profile update endpoint as a seeded Customer, and
  assert 409 ProblemDetails with code `ConcurrencyConflict` and the existing Domain message.

**Validation**: solution build, then only the two new Customer API test classes. Existing
`AuthEnforcementTests` is added only if a compatibility assertion is touched.

### WP2 — Agent assignment guard and Application-boundary evidence

**Production**:

- In `User.IsAvailable()`, call the existing private `RequireAgent()` before comparing
  `DeliveryAgentStatus` with Available.
- Do not expose `MarkBusy`/`MarkAvailable`, move authorization into the host, or change the legal
  status machine.

**Evidence**:

- Add `AgentAssignmentAuthorizationTests` in the existing Application test project.
- Prove Customer-only assignment throws `DeliveryAgentNotInitializedException`.
- Prove approved Offline/Suspended/Busy users are not assignable under existing rules.
- Prove Available assignment makes the agent Busy and preserves `AssignedAgentId`.
- Prove completion and cancellation release the same Busy agent to Available.
- Record the two-level rule: the Domain service always validates current capability/status, and any
  future Application assignment entry point must check the DeliveryAgent role before invoking it.
  Phase 3 has no Application assignment handler and does not implement the Delivery API; the
  Application-boundary contract is therefore recorded and covered by the authorization test plan.

**Validation**: solution build, then only `AgentAssignmentAuthorizationTests` plus the existing
`DeliveryAssignmentDomainServiceTests` regression class.

### WP3 — Capability drift and real concurrency proof

**CapabilityRoleDriftTests**:

- Use the existing real-SQL Infrastructure fixture and seeded four role definitions.
- After RegisterCustomer, RegisterApplicant, GrantCustomer, ApproveAgent, RejectAgent, and
  DeactivateUser, load a fresh user and roles and assert exact projection for all flags.
- Delete only the unused DeliveryAgent role definition before approving a pending applicant; assert
  `IdentityOperationFailed`, then verify from a fresh scope that flags, role membership, approval,
  operational state, and security stamp did not partially advance.
- Retain existing double-approve and reject/reapply tests; reference rather than duplicate them
  unless the new projection assertion materially adds evidence.

**ConcurrencyConflictTests**:

- Create one customer and load it through two independent contexts/providers.
- Save writer A through `IUnitOfWork`, then save stale writer B through its own `IUnitOfWork`.
- Assert writer B receives `ConcurrencyConflictException`, writer A's values remain stored, and the
  accepted rowversion changes.
- Update two different address rows through independent contexts and assert both address-row
  changes can persist without requiring a user-row conflict; do not broaden rowversion to
  address-only updates.

**Validation**: solution build, then only the two new Infrastructure test classes. Run existing
`UserCapabilityServiceTests` or `UserConcurrencyPersistenceTests` only if shared helpers or
production behavior changed.

### WP4 — Multi-role and live-session Identity proof

**MultiRoleJourneyTests**:

- Register a DeliveryAgent applicant through the Identity endpoint and capture its positive int ID.
- Approve through `IUserCapabilityService`, authenticate through the existing login endpoint, then
  grant Customer capability to that same ID at service level (the Customer controller path is
  separately covered by Customer API tests; no interactive cross-host client is introduced).
- Load a fresh user and claims principal; assert one ID throughout, both flags, both roles, approved
  agent state preserved, and no second user/account.

**SessionInvalidationTests**:

- Override `SecurityStampValidatorOptions.ValidationInterval` to zero only in the test factory.
- Log in, retain the issued cookie, deactivate through `IUserCapabilityService`, and prove the same
  cookie receives 401 on the next authorized request.
- Repeat with soft deletion through the existing persistence path; query filtering/validation must
  reject the existing cookie while preserving history.
- Keep the production five-minute option assertion in `LoginRejectionTests` unchanged.

**Validation**: solution build, then only `MultiRoleJourneyTests` and
`SessionInvalidationTests`. Add `LoginRejectionTests` only if shared Identity factory code changes.

### WP5 — Documentation and governance alignment

No production code, build, or tests in this package.

- Verify `.specify/memory/constitution.md` v3.0.1 against the accepted implementation. Do not amend
  or bump it unless a real Phase 1-2 divergence is found.
- Add a top-level superseded note to `specs/003-customer-api/spec.md`, `plan.md`, and
  `data-model.md`, linking the root refactor plan and `specs/004`, `005`, and `006` artifacts.
- Rewrite `docs/authorization-matrix.md` with int `sub`, current Customer capability, four roles,
  applicant approval, owner-derived CustomerId, 404 non-disclosure, and exact
  `ProfileNotCreated` meanings.
- Add a prominent superseded note to `phase-7-architecture-guide.md`; retain its body as historical
  context instead of spending time rewriting every obsolete code walkthrough.
- Update the Phase 3 status in `AGENTS.md` at implementation completion without broadening scope.
- Review contradiction-search hits: active guidance must have none; intentionally historical text
  is acceptable only beneath an explicit superseded marker.

**Validation**: link/path checks, targeted content searches, and `git diff --check` only.

### WP6 — Final acceptance gate

Run once after WP1-WP5 are individually green:

1. `dotnet restore src/Talabat/Talabat.slnx`
2. `dotnet build src/Talabat/Talabat.slnx --no-restore`
3. `dotnet test src/Talabat/Talabat.slnx --no-build`
4. migration list and pending-model checks: exactly the existing `InitialUnifiedUser`, no Phase 3
   migration, and no model drift;
5. solution vulnerability audit and Domain package list;
6. removed-symbol, mutation-owner, route/body customer ID, contradiction, and retained-name-aware
   searches from `quickstart.md`;
7. confirm Delivery API source is unchanged apart from any pre-existing state and still compiled by
   the solution build; and
8. `git diff --check`, evidence review against all spec scenarios/SC-001-SC-009, then the authorized
   final Phase 3 checkpoint.

Do not rerun the full suite after documentation-only evidence edits unless those edits alter project
or runtime configuration.

## Complexity Tracking

None. The design introduces no constitution violation, new project, new persistence model, new
public endpoint, or extra Domain dependency.

## Post-Design Constitution Check

All pre-design gates remain PASS:

- the only planned production edits stay inside Domain behavior and Customer API composition;
- capability and role mutations remain exclusive to `IUserCapabilityService`;
- test-only Identity configuration does not change the production five-minute interval;
- ownership remains derived from positive integer authenticated identity and current stored
  capability;
- no schema, migration, key, aggregate, repository, or package boundary changes are introduced;
- the Delivery API remains a compile-only scaffold; and
- incremental validation is proportional, with the constitution-required full suite retained at
  final acceptance.
