# Talabat Platform — Master Roadmap

**Single source of truth for what happens next.** Strategy: **Order Lifecycle Spine First** (Strategy B) — the
cross-role order lifecycle is designed and built in the backend before any further role application, then each role's
backend and frontend are completed on top of that stable contract.

Covers both repositories: backend `engakhattab/Talabat_API` (this repo) and frontend
`engakhattab/talabat-frontend` (`D:\link-dev\talabat-frontend`). Detailed blueprints are linked per phase; where they
disagree with this file, this file wins.

## Current position

| | |
|---|---|
| **Current milestone** | M1 — Order Lifecycle Spine |
| **Current phase** | **Phase 1 — Order lifecycle design** (in review: [`specs/011-order-lifecycle/design.md`](specs/011-order-lifecycle/design.md)) |
| **Completed** | Phase 0 |
| **Next** | Phase 2 — Lifecycle backend (test-first) |

## Working rules (apply to every phase)

1. **Status in every update**: current milestone and phase, completed phases, next phase.
2. **Test-first**: every feature, bug fix or domain transition starts with its named test cases written and committed
   failing (unit, integration, state-machine, API, E2E as relevant). Production code is written only to make them
   pass. Tests are never weakened, skipped or deleted to pass a gate.
3. **Backend before frontend**: domain → use cases → API contract (committed OpenAPI, parity-tested) → only then copy the
   contract and regenerate the frontend client → screens. Contract changes are committed separately from feature code.
4. **Git**: one task branch per piece of work off `main`/`master`, PR via `gh`, merge only with CI green. Zero AI
   attribution in commits or PRs.
5. **Gate**: a phase is complete only when every acceptance criterion, test suite and contract check passes, and the
   owner has accepted any design decisions. No phase starts before the previous gate (except the explicitly parallel
   tracks below).

## Milestones and phases

| Phase | Milestone | Side | Objective | Prerequisites | Key deliverables | Completion gate | Status |
|---|---|---|---|---|---|---|---|
| **0** | M0 Foundation | BE + FE | Trustworthy baseline | — | Local DB + migrations, green CI both repos, tooling (Node 24, JDK 21, `gh`), audit (`PROJECT_STATUS_REPORT.md`) | Backend 640/640 + vuln scan green; frontend CI all stages green; full stack runs locally | ✅ Complete |
| **1** | M1 Lifecycle Spine | Design | Freeze the cross-role order state model | 0 | Design document, ADR-0001/0002, owner answers Q1–Q6, `CONTEXT.md` glossary | Document + ADRs *Accepted*; PR merged | 🔄 In review |
| **2** | M1 | BE | Implement the spine | 1 | `OrderFulfillment` + migration/backfill; checkout/accept/reject/prepare/ready/hand-over/cancel use cases; `Delivery.Release`; pickup gate; customer status mapper; Admin act-on-behalf endpoints; updated OpenAPI (Customer, Delivery, Admin) | Test catalogue §4 of the design written first and green; backend E2E scenarios 31–32 pass; contracts committed | ⏳ |
| **3** | M1 | FE | Customer order tracking | 2 | Copy contract + regenerate client (separate commit); status timeline on order details/list (polling) | Customer unit tests + `api:verify` + prod build green; live journey shows every status | ⏳ |
| **4** | M2 Courier | BE | Delivery test & contract readiness ([readiness plan](docs/delivery-backend-readiness-plan.md) Phase 5, revised) | 2 | `--e2e-provision prepare-delivery/restore-delivery` (applicant, approved agent, order at `ReadyForPickup`) | Idempotent on SQL Server; Delivery/Admin contracts reproducible | ⏳ |
| **5** | M2 | FE | Delivery (courier) app | 4 | [Delivery frontend roadmap](../talabat-frontend/docs/talabat-delivery-frontend-roadmap.md) Phases 1–9 on port 4300; frontend constitution amended | Courier E2E: online → claim → arrive → pickup blocked until ready → deliver | ⏳ |
| **6** | M3 Restaurant Owner | BE | Owner capability and API | 2 | [Owner/Admin plan](docs/restaurant-owner-and-admin-api-implementation-plan.md) Phases 1–4, 6: `Talabat.Api.Shared`, `Restaurant.OwnerUserId`, RestaurantOwner capability/scope/client, Owner API wrapping the Phase 2 fulfillment use cases, catalog editing, open/closed | Ownership tests (foreign resources → 404); owner real-token tests; contract committed | ⏳ |
| **7** | M3 | FE | Restaurant Owner app | 6 | New Angular app + `api-owner` client; frontend constitution amended | Owner E2E: receive → accept → preparing → ready | ⏳ |
| **8** | M4 Admin | BE | Admin operations | 2, 6 | Owner/Admin plan Phase 7: read models, restaurant onboarding/ownership, order and delivery oversight, failed-delivery resolution, audit log, first-admin bootstrap command; retire Identity dev-only approval routes | Admin authz matrix + workflow tests; contract committed | ⏳ |
| **9** | M4 | FE | Admin app | 8 | New Angular app (port 4400) + `api-admin` client | Admin E2E: approve agent, onboard restaurant, resolve failure | ⏳ |
| **10** | M5 Production | BE + FE | Hardening | 2 (Identity part may start any time) | Identity production signing key, config-driven URLs/CORS, JSON login lockout; rate limiting; checkout idempotency; async capability lookup; structured logging + OpenTelemetry; health checks on all hosts | Security tests; `Production` start with no localhost config | ⏳ |
| **11** | M5 | BE + FE | Events and notifications | 2 | Domain events + transactional outbox; acceptance-deadline job; in-app notifications (SignalR optional) | Outbox delivery tests; timeout cancels with `System` actor | ⏳ |
| **12** | M5 | Ops | Deployment | 10 | Dockerfiles + compose for 4 hosts and SPAs; staging environment; CI image builds | Staging over HTTPS serves all apps | ⏳ |
| **13** | M6 Release | BE + FE | Integrated release | 3, 5, 7, 9, 11, 12 | Cross-role E2E in CI; release runbook | Customer → Owner → Courier → Customer journey green on staging | ⏳ |
| **14+** | M7 Enhancements | — | Differentiators | 13 | Payments sandbox + fees, live map/ETA, ratings, search, smart dispatch — each with its own design document | Per feature | ⏳ |

### Allowed parallel tracks

- **During M1:** Delivery frontend roadmap Phases 1–3 (domain-neutral `core-*`, generated Delivery client tooling,
  app shell). They do not depend on lifecycle semantics.
- **Any time after Phase 0:** the Identity part of Phase 10.

## Dependency flow

```text
Phase 1 design ─► Phase 2 backend spine ─┬─► Phase 3 Customer tracking FE
                                          ├─► Phase 4 Delivery fixtures ─► Phase 5 Courier app
                                          ├─► Phase 6 Owner BE ─► Phase 7 Owner app
                                          │        └──────────► Phase 8 Admin BE ─► Phase 9 Admin app
                                          └─► Phase 11 events/notifications
Phase 10 hardening ─► Phase 12 deployment ─────────────────────────────► Phase 13 release
```

Within every phase that touches both repos: backend domain + use cases + contract **verified first**, then contract
copy + client regeneration (own commit), then frontend screens.

## Phase log

| Date | Event |
|---|---|
| 2026-10-13 | Audit + baseline remediation merged (backend PR #2–#4, frontend PR #1–#2) |
| 2026-10-14 | Frontend CI fixed (frontend PR #3) — **Phase 0 complete**; Phase 1 design opened for review |

## Superseded / reference documents

| Document | Role now |
|---|---|
| `PROJECT_STATUS_REPORT.md` | Audit evidence and history; its §17 roadmap is superseded by this file |
| `PROJECT_IMPLEMENTATION_ROADMAP.md` | Historical (2026-07); superseded |
| `docs/delivery-backend-readiness-plan.md` | Blueprint for Phase 4 (its Phases 1–4 are done) |
| `docs/restaurant-owner-and-admin-api-implementation-plan.md` | Blueprint for Phases 6 and 8 (lifecycle parts superseded by `specs/011-order-lifecycle/design.md`) |
| Frontend `docs/talabat-delivery-frontend-roadmap.md` | Blueprint for Phase 5 |
