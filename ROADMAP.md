# Talabat Platform — Master Roadmap

**Single source of truth for what happens next.** Strategy: **Order Lifecycle Spine First** (Strategy B) — the
cross-role order lifecycle is designed and built in the backend before any further role application, then each role's
backend and frontend are completed on top of that stable contract.

Covers both repositories: backend `engakhattab/Talabat_API` (this repo) and frontend
`engakhattab/talabat-frontend` (`D:\link-dev\talabat-frontend`). Detailed blueprints are linked per phase; where they
disagree with this file, this file wins. Domain language: [`CONTEXT.md`](CONTEXT.md).

## Current position

| | |
|---|---|
| **Current milestone** | M1 — Order Lifecycle Spine |
| **Current phase** | **Phase 1 — Order lifecycle design: accepted** ([`specs/011-order-lifecycle/design.md`](specs/011-order-lifecycle/design.md)); complete once its PR is merged |
| **Completed** | Phase 0 |
| **Next** | Phase 2 — Lifecycle backend (test-first) |

## Working rules (apply to every phase)

1. **Status in every update**: current milestone and phase, completed phases, next phase.
2. **Test-first**: every feature, bug fix or domain transition starts with its named test cases written and committed
   failing (unit, integration, state-machine, API, E2E as relevant). Production code is written only to make them
   pass. Tests are never weakened, skipped or deleted to pass a gate.
3. **Retiring a test**: a test may be rewritten or removed only because an accepted design revises the business rule it
   encodes. The commit names the rule ID, and the replacement test lands in the same red commit.
4. **Backend before frontend**: domain → use cases → API contract (committed OpenAPI, parity-tested) → only then copy the
   contract and regenerate the frontend client → screens. Contract changes are committed separately from feature code.
5. **Git**: zero AI attribution in commits or PRs; merge only with CI green.
   - Single-task phase: one task branch off `main`/`master`, PR via `gh`.
   - Multi-task phase: an integration branch `feature/phase-<n>-<slug>` off `main`; each task gets its own branch and PR
     into it; one final PR into `main` when the phase gate passes, so `main` never holds a half-built phase.
6. **Strictly sequential**: one phase in flight at a time. A phase starts only after the previous phase's gate passes:
   every acceptance criterion, test suite and contract check green, and the owner has accepted any design decisions.

## Milestones and phases

| Phase | Milestone | Side | Objective | Prerequisites | Key deliverables | Completion gate | Status |
|---|---|---|---|---|---|---|---|
| **0** | M0 Foundation | BE + FE | Trustworthy baseline | — | Local DB + migrations, green CI both repos, tooling (Node 24, JDK 21, `gh`), audit (`PROJECT_STATUS_REPORT.md`) | Backend 640/640 + vuln scan green; frontend CI all stages green; full stack runs locally | ✅ Complete |
| **1** | M1 Lifecycle Spine | Design | Freeze the cross-role order state model | 0 | Design document (Q1–Q21 answered), ADR-0001/0002, `CONTEXT.md` glossary | Document + ADRs *Accepted*; PR merged | ✅ Accepted — merge pending |
| **2** | M1 | BE | Implement the spine | 1 | See [Phase 2 plan](#phase-2-plan) | Design §4 test catalogue written first and green (incl. §4.1 retirements); backend E2E 35–36 pass; Customer, Delivery and Admin contracts committed and parity-tested | ⏳ Next |
| **3** | M1 | FE | Customer order tracking | 2 | Copy contract + regenerate client (separate commit); status timeline on order list/details (polling); customer cancel while `Placed`; reason-code display | Customer unit tests + `api:verify` + prod build green; live journey (driven with `advance-order`) shows every status | ⏳ |
| **4** | M2 Delivery Agent | BE | Delivery app test & contract readiness ([readiness plan](docs/delivery-backend-readiness-plan.md) Phase 5, revised) | 3 | Fixtures for the Delivery app: orders at `ReadyForPickup` (and before it), restore operation; Delivery/Admin contracts reproducible | Idempotent on SQL Server; contracts reproducible from a clean DB | ⏳ |
| **5** | M2 | FE | Delivery Agent app | 4 | [Delivery frontend roadmap](../talabat-frontend/docs/talabat-delivery-frontend-roadmap.md) Phases 1–9 on port 4300; frontend constitution Scope Guard amended at phase start | Agent E2E: online → claim → arrive → pickup blocked until ready (`fulfillmentStatus` shown) → deliver; release; fail after pickup | ⏳ |
| **6** | M3 Restaurant Owner | BE | Owner capability and API | 5 | [Owner/Admin plan](docs/restaurant-owner-and-admin-api-implementation-plan.md) Phases 1–4, 6: `Talabat.Api.Shared`, `Restaurant.OwnerUserId`, RestaurantOwner capability/scope/client, Owner API wrapping the Phase 2 fulfillment use cases (incl. restaurant cancel after acceptance), catalog editing, open/closed | Ownership tests (foreign resources → 404); owner real-token tests; contract committed | ⏳ |
| **7** | M3 | FE | Restaurant Owner app | 6 | New Angular app + `api-owner` client; **reason-code picker** for reject/cancel; review and extend the reason-code lists (additive); frontend constitution amended | Owner E2E: receive → accept → preparing → ready; reject and cancel with reason codes | ⏳ |
| **8** | M4 Admin | BE | Admin operations | 7 | Owner/Admin plan Phase 7: read models, restaurant onboarding/ownership, order and delivery oversight, failed-delivery resolution, audit log (incl. who-did-what and release history); **`--bootstrap-admin <email>` operator CLI** replacing the manual-SQL procedure in `docs/admin-first-user-bootstrap.md`; retire Identity dev-only approval routes | Admin authz matrix + workflow tests; contract committed | ⏳ |
| **9** | M4 | FE | Admin app | 8 | New Angular app (port 4400) + `api-admin` client | Admin E2E: approve agent, onboard restaurant, act on behalf, resolve failure | ⏳ |
| **10** | M5 Production | BE + FE | Hardening | 9 | Identity production signing key, config-driven URLs/CORS, JSON login lockout; rate limiting; checkout idempotency; async capability lookup; structured logging + OpenTelemetry; health checks on all hosts | Security tests; `Production` start with no localhost config | ⏳ |
| **11** | M5 | BE + FE | Events and notifications | 10 | Domain events + transactional outbox; **acceptance-deadline job** (`System`, `AcceptanceTimeout`) and **dispatch-timeout job** for accepted orders no agent claims; in-app notifications (SignalR optional) | Outbox delivery tests; both timeout jobs tested | ⏳ |
| **12** | M5 | Ops | Deployment | 11 | Dockerfiles + compose for 4 hosts and SPAs; staging environment; CI image builds | Staging over HTTPS serves all apps | ⏳ |
| **13** | M6 Release | BE + FE | Integrated release | 12 | Cross-role E2E in CI; release runbook | Customer → Owner → Delivery Agent → Customer journey green on staging | ⏳ |
| **14+** | M7 Enhancements | — | Differentiators | 13 | Payments sandbox + fees, preparation-time estimate / live ETA and map, ratings, search, smart dispatch — each with its own design document | Per feature | ⏳ |

## Phase 2 plan

Integration branch `feature/phase-2-order-lifecycle` off `main`; each task below is its own branch and PR into it, and
each starts with its red tests from design §4.

| Task | Content | Tests (design §4) |
|---|---|---|
| 2.0 | CI also runs on PRs into `feature/**` | CI run on the first task PR |
| 2.1 | Domain: `OrderFulfillment` (states, `CancelledBy`, reason codes, deadline), `Delivery.Release`, fail-after-pickup, pickup gate | 1–14 + §4.1 domain retirements |
| 2.2 | Persistence: mapping, `AddOrderFulfillment` migration (no backfill), repositories; drop and recreate local `talabat` | 27–29 |
| 2.3 | Application: checkout/accept/reject/prepare/ready/pickup/cancel/release workflows, `CustomerOrderStatusMapper`, provisioner admin + agent + `advance-order` | 15–26, 30, 31 + §4.1 application retirements |
| 2.4 | APIs and contracts: Customer (status, cancel), Delivery (`fulfillmentStatus`, `/release`, remove `/cancel`), Admin act-on-behalf; OpenAPI regenerated and committed | 32–34 + §4.1 API retirements |
| 2.5 | Backend E2E and docs: E2E 35–36, BR-FUL-001…009 and BR-DEL revisions in `docs/delivery/delivery-business-rules.md`, `CLAUDE.md` gotchas | 35–36 |

## Dependency flow (strictly sequential)

```text
0 baseline ─► 1 design ─► 2 spine BE ─► 3 customer tracking FE
  ─► 4 agent BE ─► 5 agent FE ─► 6 owner BE ─► 7 owner FE ─► 8 admin BE ─► 9 admin FE
  ─► 10 hardening ─► 11 events ─► 12 deployment ─► 13 release ─► 14+ enhancements
```

Within every phase that touches both repos: backend domain + use cases + contract **verified first**, then contract
copy + client regeneration (own commit), then frontend screens.

## Phase log

| Date | Event |
|---|---|
| 2026-10-13 | Audit + baseline remediation merged (backend PR #2–#4, frontend PR #1–#2) |
| 2026-10-14 | Frontend CI fixed (frontend PR #3) — **Phase 0 complete**; Phase 1 design opened for review |
| 2026-10-14 | Phase 1 design grilled (two rounds, Q1–Q21) and **accepted**; roadmap made strictly sequential |

## Superseded / reference documents

| Document | Role now |
|---|---|
| `PROJECT_STATUS_REPORT.md` | Audit evidence and history; its §17 roadmap is superseded by this file |
| `PROJECT_IMPLEMENTATION_ROADMAP.md` | Historical (2026-07); superseded |
| `docs/delivery-backend-readiness-plan.md` | Blueprint for Phase 4 (its Phases 1–4 are done) |
| `docs/restaurant-owner-and-admin-api-implementation-plan.md` | Blueprint for Phases 6 and 8 (lifecycle parts superseded by `specs/011-order-lifecycle/design.md`) |
| `docs/admin-first-user-bootstrap.md` | Current manual procedure; replaced by the Phase 8 CLI |
| Frontend `docs/talabat-delivery-frontend-roadmap.md` | Blueprint for Phase 5 |
