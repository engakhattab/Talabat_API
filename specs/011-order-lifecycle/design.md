# Phase 1 — Order Lifecycle Design Decision Document

| | |
|---|---|
| **Status** | **Accepted** (2026-10-14) — owner answered Q1–Q21 after two grilling rounds |
| **Milestone / Phase** | M1 Order Lifecycle Spine → Phase 1 (design). Implementation is Phase 2. |
| **Roadmap** | [`ROADMAP.md`](../../ROADMAP.md) |
| **ADRs** | [ADR-0001](../../docs/adr/0001-order-fulfillment-aggregate.md), [ADR-0002](../../docs/adr/0002-create-delivery-on-restaurant-acceptance.md) (both *accepted*) |
| **Glossary** | [`CONTEXT.md`](../../CONTEXT.md) — Delivery Agent and Restaurant Owner are the canonical role names |
| **Builds on** | [`restaurant-owner-and-admin-api-implementation-plan.md`](../../docs/restaurant-owner-and-admin-api-implementation-plan.md) §4 D3/D4 and §6.4 (lifecycle parts superseded by this document) |

## 1. Problem

Verified on `main` (`93741b3`):

- `Order` is an immutable purchase record with **no status** (by design: `docs/bounded-contexts.md`, BR-ORD-006).
- `Delivery` is created inside the checkout transaction and starts in `PendingAssignment`, so delivery agents can claim,
  arrive and pick up orders **no restaurant has accepted**.
- A delivery agent "cancel" before pickup **terminates** the delivery; `Deliveries.OrderId` is unique, so the order can
  never be re-dispatched. `Fail` is allowed in any active state, even before pickup.
- Customers see no progress: order list/details expose no status.
- There is no restaurant-side state, no Restaurant Owner capability, and no customer cancellation.

This phase fixes the **state model and the rules between aggregates** so Customer, Delivery Agent, Restaurant Owner and
Admin all observe one consistent lifecycle. Restaurant Owner/Admin **apps** come later; their **use cases** are designed
here.

## 2. Decisions

### D1 — Three lifecycle owners; `Order` stays immutable  *(ADR-0001)*

| Aggregate | Context | Owns |
|---|---|---|
| `Order` (unchanged) | Ordering | Purchase record: items, prices, totals, address snapshot |
| `OrderFulfillment` (new, one per order) | Restaurant Operations | Kitchen lifecycle: accept/reject, preparing, ready, hand-over, cancellation |
| `Delivery` (extended) | Delivery | Delivery agent lifecycle: assignment, release, pickup, drop-off, failure |

Rejected: adding `Status` to `Order` (contradicts documented immutability, makes one row contended by restaurant and
delivery agent writes, mixes purchase and kitchen invariants).

### D2 — `OrderFulfillment` state machine

```text
AwaitingRestaurant ──Accept──► Accepted ──StartPreparing──► Preparing ──MarkReady──► ReadyForPickup ──HandOver──► HandedOver
        │                         │                             │                       │
        ├──Reject──► Rejected     └──────────── Cancel (Restaurant | Admin) ────────────┴──► Cancelled
        └──Cancel (Customer | Admin | System)──► Cancelled
```

- `Accepted → ReadyForPickup` directly is allowed (skip `Preparing` for instant items).
- Terminal: `HandedOver`, `Rejected`, `Cancelled`. `HandedOver` is entered only by the pickup workflow (D5), never by an
  actor directly.
- Every transition records a UTC timestamp, is monotonic in time, and is guarded by `RestaurantId` (same defence-in-depth
  shape as `Delivery`'s agent guard).
- `Reject` and `Cancel` record a **reason code** and an optional note (D13). `Cancel` also records `CancelledBy`:
  `Customer` | `Restaurant` | `Admin` | `System`. Who may cancel in which state is D7.
- Accept, reject, start-preparing and mark-ready do not record who performed them; that history arrives with the M4
  audit log.
- `RowVersion` for optimistic concurrency.
- Deviation from the Owner/Admin plan §6.4: `CancelledByAdmin` becomes `Cancelled` + `CancelledBy`; `HandedOver` is new.

### D3 — A `Delivery` is created when the restaurant accepts  *(ADR-0002)*

- **Checkout** atomically saves `Order` + `OrderFulfillment(AwaitingRestaurant)` + cart → `CheckedOut`. No delivery.
- **AcceptOrder** atomically moves fulfillment to `Accepted` **and** creates `Delivery(PendingAssignment)` (pickup
  snapshot taken from the restaurant at acceptance time, delivery address from the order).
- Consequences: delivery agents only ever see accepted orders; "restaurant rejects an order an agent already holds"
  cannot happen.
- This **revises BR-DEL-001 and BR-DEL-016** (introduced 2026-10-13): the delivery is still created atomically, but in the
  acceptance transaction instead of checkout.

### D4 — Dispatch during preparation

A delivery is claimable as soon as it exists (fulfillment `Accepted`, `Preparing` or `ReadyForPickup`), so the agent can
travel while the kitchen cooks (owner answer Q1).

### D5 — Pickup gate, hand-over, and the kitchen state visible to agents

- `ArrivedAtRestaurant` is allowed in any fulfillment state from `Accepted` onward.
- **`PickedUp` requires fulfillment `ReadyForPickup`**; otherwise `409 FulfillmentNotReady`.
- The pickup use case atomically sets `Delivery → PickedUp` **and** `OrderFulfillment → HandedOver`.
- The Delivery API's pending and active delivery responses carry `fulfillmentStatus`
  (`Accepted` | `Preparing` | `ReadyForPickup`), so the agent knows whether the food is ready before trying to pick up
  (Q5).

### D6 — Release replaces delivery agent cancel

- New `Delivery.Release(agentId)`: allowed in `Assigned` / `ArrivedAtRestaurant`; clears assignment fields, returns to
  `PendingAssignment`, agent becomes `Available`. The same delivery row is reused, so the unique `OrderId` index stays.
- Delivery API: `POST /api/agent/deliveries/{id}/cancel` is **replaced** by `POST /api/agent/deliveries/{id}/release`
  (no Delivery frontend consumes it yet).
- **Admin release on behalf of an unresponsive agent** (Q3): an Admin API endpoint runs the same release use case with
  the Admin as actor. The agent becomes `Available`; Admin may then suspend them with the existing endpoint.
- Release history is not stored in this phase (Q4); it arrives with the M4 audit log.

### D7 — Cancellation matrix

| Actor | Fulfillment state | Allowed | Effect (one transaction) |
|---|---|---|---|
| Customer | `AwaitingRestaurant` | ✅ (reason optional) | Fulfillment `Cancelled(Customer)` |
| Customer | any later state | ❌ `409 OrderNoLongerCancellable` | — |
| Restaurant (Owner API, M3) | `AwaitingRestaurant` | Reject ✅ | Fulfillment `Rejected` |
| Restaurant (Owner API, M3) | `Accepted` … `ReadyForPickup` | ✅ (Q2) | Fulfillment `Cancelled(Restaurant)`; delivery `Cancelled`; assigned agent → `Available` |
| Admin | `AwaitingRestaurant` … `ReadyForPickup` | ✅ | Fulfillment `Cancelled(Admin)`; delivery (if any) `Cancelled`; assigned agent → `Available` |
| System (Phase 11 job) | `AwaitingRestaurant` after the acceptance deadline | ✅ | Fulfillment `Cancelled(System, AcceptanceTimeout)` |
| Anyone | `HandedOver` (food with the agent) | ❌ `409 OrderNoLongerCancellable` — use delivery failure (D8) | — |
| Delivery agent | — | Release only (D6) | — |

- When an Admin acts on behalf of a restaurant, the recorded actor is **always `Admin`** (Q18); a restaurant's request
  is expressed with the `RestaurantRequested` reason code.
- **Agent whose delivery is cancelled under them** (Q20): becomes `Available`; `GET /api/agent/deliveries/active`
  returns nothing; history shows the delivery `Cancelled` with its reason code; any further action on it returns the
  existing terminal-state `409`. No push — the Delivery app learns it on its next poll.
- A race between two writers (e.g. customer cancel vs restaurant accept) is settled by RowVersion: the loser receives
  `409 ConcurrencyConflict` and re-reads (Q8). No server-side retry.

### D8 — Delivery failure

- `Fail(reasonCode, note?)` becomes allowed **only after pickup** (`PickedUp`, `OutForDelivery`); before pickup the agent
  uses Release. Today it is allowed in any active state.
- Agent returns to `Available`; fulfillment stays `HandedOver`; customer status becomes `DeliveryFailed`.
- Resolution (refund, re-delivery) is Admin work in Milestone M4; out of scope here.

### D9 — One customer-facing status, derived (never stored)

An Application function `CustomerOrderStatusMapper.Map(fulfillment, delivery?)` is the **only** place that turns the
two lifecycles into what customers (and later owner/admin lists) see:

| Fulfillment | Delivery | `CustomerOrderStatus` | `deliveryAgentAssigned` |
|---|---|---|---|
| `AwaitingRestaurant` | none | `Placed` | false |
| `Rejected` | none | `Rejected` | false |
| `Cancelled` | none / `Cancelled` | `Cancelled` | false |
| `Accepted` | `PendingAssignment` / `Assigned` / `ArrivedAtRestaurant` | `Accepted` | by delivery |
| `Preparing` | same | `Preparing` | by delivery |
| `ReadyForPickup` | same | `ReadyForPickup` | by delivery |
| `HandedOver` | `PickedUp` / `OutForDelivery` | `OnTheWay` | true |
| `HandedOver` | `Delivered` | `Delivered` | true |
| `HandedOver` | `Failed` | `DeliveryFailed` | true |

- Any other combination is a broken invariant: the mapper throws, and a test enumerates every combination.
- Customer API: order list and order details gain `status` and `deliveryAgentAssigned`; order details also gains
  `cancelledBy` and `reasonCode` when the order is `Rejected`, `Cancelled` or `DeliveryFailed`. The free-text note is
  never exposed to customers. Contract change → frontend client regeneration in Phase 3.
- Progress is polled; push/SignalR is Milestone M5.

### D10 — Acceptance deadline

`OrderFulfillment.AcceptanceDeadline = PlacedAt + 10 minutes` (configuration; Q3). Automatic enforcement (a background job
cancelling with `CancelledBy = System`, reason `AcceptanceTimeout`) is **Phase 11**; until then Admin cancels manually.

### D11 — Interim acceptor until the Restaurant Owner app exists

- Admin API gains act-on-behalf endpoints — accept, reject, start preparing, mark ready, cancel, and release a delivery
  agent (D6) — under `AdminAccess`. They stay permanently as an operations capability.
- The Application use cases take the actor explicitly, so the Owner API (M3) wraps the **same** handlers with an
  ownership-scoped read; no use case is written twice.
- No hidden auto-accept (Q5).

### D12 — Transactions, concurrency, migration

- Multi-aggregate workflows (checkout, accept, pickup, restaurant/admin cancel) use
  `IUnitOfWork.ExecuteInTransactionAsync`; RowVersion conflicts map to `409 ConcurrencyConflict` (global handler already
  does this).
- Migration `AddOrderFulfillment` is added **on top of the four existing migrations**: new table (unique `OrderId`,
  status/actor/reason check constraints, RowVersion). **No backfill** (Q1/Q16): the project is pre-production, so the
  local dev database `talabat` is **dropped and recreated** during Phase 2 (owner approved). Integration tests already
  create their own databases.

### D13 — Reason codes (Q6, Q17, Q18)

Reject, cancel and fail record a **reason code** plus an optional free-text note (max 500 characters). The note is
**required when the code is `Other`**. Customers see the code (as friendly text in the app), never the note.

| Action | Codes |
|---|---|
| Restaurant rejects | `ItemsUnavailable`, `RestaurantBusy`, `RestaurantClosing`, `Other` |
| Restaurant cancels after accepting | `ItemsUnavailable`, `KitchenIssue`, `Other` |
| Customer cancels | `ChangedMind`, `OrderedByMistake`, `Other` — giving a reason is optional |
| Admin cancels | `CustomerRequested`, `RestaurantRequested`, `RestaurantUnresponsive`, `NoDeliveryAgentAvailable`, `SuspectedFraud`, `Other` |
| System cancels (Phase 11) | `AcceptanceTimeout` |
| Delivery agent fails the delivery | `CustomerUnreachable`, `WrongAddress`, `CustomerRefused`, `Other` |

A code is valid only for its own action (e.g. `KitchenIssue` cannot reject). Phase 7 adds the reason picker to the
Owner app and reviews/extends these lists (additive contract change).

### D14 — Driving the lifecycle before the role apps exist (Q12, Q19)

The dev-only E2E provisioner (`--e2e-provision`, refused in `Production`) gains, in Phase 2:

- a test **Admin** account and an approved, online **Delivery Agent** test account (credentials written only to the
  provisioner's output file, never committed);
- `advance-order --order <id> --to <Accepted|Preparing|ReadyForPickup|PickedUp|OutForDelivery|Delivered>`, which runs the
  **same Phase 2 Application use cases** in-process as the test Admin / test agent, so no tokens are needed and no rule
  is bypassed.

Phase 3's live customer journey uses it between the customer's own steps.

## 3. Out of scope (recorded so they are not silently dropped)

| Item | Where it goes |
|---|---|
| Payment method modelling and fees (Cash on Delivery stays implicit; Q6 of round 1) | M7 |
| Notifications, real-time tracking (polling only) | Phase 11 / M7 |
| Automatic acceptance timeout **and dispatch timeout** for accepted orders no agent claims (Admin cancels meanwhile) | Phase 11 jobs |
| Preparation-time estimate at acceptance / ETA | M7 |
| Release/reassignment history, who-performed-what history | M4 audit log |
| Admin failure resolution (refund, re-delivery) | M4 |
| Restaurant ownership, Owner API, restaurant staff accounts (none in v1) | M3 (ownership); staff accounts not planned |
| Partial cancellation | not planned |

Opening hours and `IsActive` gate **checkout only**: accept, prepare, ready and hand-over are never blocked by them; an
Admin cancels in-flight orders if a deactivation requires it (Q7).

## 4. Test catalogue — written FIRST in Phase 2 (red → green)

Names are binding for Phase 2; each group is committed failing before its production code.

**Domain — `OrderFulfillmentTests` (state machine)**
1. `New_fulfillment_starts_awaiting_restaurant_with_acceptance_deadline`
2. `Accept_from_awaiting_restaurant_sets_accepted_and_timestamp`
3. `Reject_requires_a_reject_reason_code` / `Reject_only_from_awaiting_restaurant`
4. `Start_preparing_only_from_accepted`
5. `Mark_ready_from_accepted_or_preparing` / `Mark_ready_from_other_states_throws`
6. `Hand_over_only_from_ready_for_pickup`
7. `Cancel_matrix_allows_only_permitted_actor_and_state_pairs` (theory over actor × state, per D7)
8. `Every_transition_from_a_terminal_state_throws` (theory over all terminal × all transitions)
9. `Wrong_restaurant_id_throws_mismatch`
10. `Transition_timestamps_must_be_utc_and_monotonic`
11. `Reason_code_must_belong_to_its_action` / `Other_reason_requires_note` / `Note_longer_than_500_throws`

**Domain — `DeliveryReleaseTests`, `DeliveryFailTests`**
12. `Release_from_assigned_or_arrived_returns_to_pending_and_clears_assignment`
13. `Release_after_pickup_throws` / `Release_by_other_agent_throws_mismatch`
14. `Fail_before_pickup_throws` / `Fail_after_pickup_records_reason_code`

**Application — workflow handlers** (fakes; asserts on transaction use and saves)
15. `Checkout_creates_order_and_fulfillment_but_no_delivery`
16. `Accept_creates_pending_delivery_with_pickup_snapshot_in_one_transaction`
17. `Accept_twice_conflicts` / `Reject_after_accept_conflicts`
18. `Pickup_before_ready_returns_FulfillmentNotReady`
19. `Pickup_when_ready_hands_over_fulfillment_atomically`
20. `Release_returns_agent_to_available` / `Admin_release_returns_agent_to_available`
21. `Customer_cancel_only_while_awaiting_restaurant`
22. `Restaurant_cancel_after_accept_cancels_delivery_and_releases_agent`
23. `Admin_cancel_records_admin_actor_cancels_delivery_and_releases_agent`
24. `Cancel_after_handover_conflicts`
25. `Status_mapper_maps_every_reachable_combination` / `Status_mapper_rejects_impossible_combinations`
26. `Pending_and_active_deliveries_include_fulfillment_status`

**Infrastructure (SQL Server)**
27. `Fulfillment_round_trips_and_enforces_unique_order_and_check_constraints`
28. `Fulfillment_rowversion_rejects_stale_update`
29. `Add_order_fulfillment_migration_applies_on_an_empty_database`
30. `Failed_accept_rolls_back_fulfillment_and_delivery`
31. `Advance_order_command_drives_an_order_to_delivered_and_is_refused_in_production`

**API** (Customer, Delivery, Admin)
32. Customer: order list/details expose `status`, `deliveryAgentAssigned`, and on details `cancelledBy` + `reasonCode`;
    `POST /api/me/orders/{id}/cancel`; committed OpenAPI parity updated.
33. Delivery: pending queue excludes unaccepted orders; `fulfillmentStatus` present; `/release` works; `/cancel` removed;
    pickup before ready → 409; fail before pickup → 409.
34. Admin act-on-behalf endpoints (accept, reject, start-preparing, mark-ready, cancel, release-agent): 200 for Admin,
    403 non-admin/missing scope, 401 anonymous; error codes per D7.

**End-to-end (backend, real SQL Server)**
35. `Customer_order_accepted_prepared_picked_up_and_delivered_is_visible_to_customer_at_every_step`
36. `Agent_release_returns_order_to_queue_and_second_agent_completes_it`

### 4.1 Existing tests retired or rewritten by this design (Q11)

A test may be rewritten or removed **only** because an accepted design revises the rule it encodes; the commit names the
rule ID and the replacement test lands in the same red commit.

| Existing test | Encodes | Replaced by |
|---|---|---|
| `CheckoutEndpointTests.Checkout_Authenticated_CreatesDeliveryWithRestaurantPickupSnapshot` | BR-DEL-001/016 (old) | 15, 16, 32 |
| `CheckoutHandlerSuccessTests.Handle_CreatesOneOrderChecksOutOneCartUsesCurrentCatalogPriceAndCommitsOnce` (delivery assertions only) | BR-DEL-016 (old) | 15 |
| `CreateDeliveryForOrderHandlerTests.Handle_CreatesPendingAssignmentDelivery` | BR-DEL-001 (old) | 16 |
| `CancelDeliveryHandlerTests.*` | agent cancel | 12, 13, 20 |
| `DeliveryAssignmentDomainServiceTests.CancelDelivery_Cancels_AgentAvailable`, `AgentAssignmentAuthorizationTests.CancelDelivery_BusyAgent_ReleasesToAvailable` | agent cancel | 12, 20 |
| `DeliveryOwnershipTests.AgentB_Cancel_OnAgentADelivery_Returns404` | agent cancel endpoint | 33 (`AgentB_Release_OnAgentADelivery_Returns404`) |
| `FailDeliveryHandlerTests.Handle_FailDelivery_FromAssigned_ShouldSucceed` | fail before pickup | 14 |
| `DeliveryAssignmentDomainServiceTests.FailDelivery_Fails_AgentAvailable` and other fail tests that start before pickup | fail before pickup | 14 (re-based to start after pickup) |
| Any test that picks up a delivery without a `ReadyForPickup` fulfillment | no pickup gate | same test, arranged with a ready fulfillment |

## 5. Business rules to add or revise

- New `BR-FUL-001…009`: fulfillment created at checkout; accept/reject window; preparation states; ready; hand-over on
  pickup; cancellation matrix (incl. restaurant cancel after acceptance); terminal immutability; acceptance deadline;
  reason codes per action.
- Revise `BR-DEL-001` (delivery created on acceptance) and `BR-DEL-016` (atomic with acceptance).
- New `BR-DEL-017` (release instead of cancel; Admin release on behalf), `BR-DEL-018` (fail only after pickup),
  `BR-DEL-019` (pickup gate), `BR-DEL-020` (agent sees fulfillment status).

## 6. Owner decisions

**Round 0 (initial review)**

| # | Question | Answer |
|---|---|---|
| Q1 | Can delivery agents claim while the order is being prepared? | **Yes** — claimable once accepted; pickup waits for Ready |
| Q2 | Until when can a customer cancel? | **Until the restaurant accepts** |
| Q3 | Acceptance deadline? | **10 minutes**, enforced automatically in Phase 11 |
| Q4 | Release history now? | **No** — M4 audit log |
| Q5 | Auto-accept for seeded restaurants? | **No** — Admin acts on behalf |
| Q6 | Cash on Delivery only for the first release? | **Yes** |

**Grilling round 1**

| # | Question | Answer |
|---|---|---|
| G1 | Backfill existing data? | **No** — drop and recreate the dev database (pre-production) |
| G2 | Restaurant cancel after accepting? | **Yes** — `CancelledBy = Restaurant`, `Accepted`…`ReadyForPickup` |
| G3 | Stuck agent? | **Admin release on behalf** |
| G4 | No dispatch timeout, no prep-time estimate now? | **Confirmed** — Phase 11 / M7 |
| G5 | Agents see kitchen state? | **Yes** — `fulfillmentStatus` |
| G6 | Reasons? | **Reason codes + optional note**, codes customer-visible; picker in Phase 7 |
| G7 | Closed/deactivated restaurant with in-flight orders? | **Checkout-only gate** |
| G8 | Race loser? | **Generic `409 ConcurrencyConflict`** |
| G9 | Role names? | **Delivery Agent**, **Restaurant Owner** |
| G10 | Phase 2 landing? | **Integration branch + 5 task branches**, CI on PRs into `feature/**` |
| G11 | Tests encoding revised rules? | **Retire/rewrite only per accepted design**, §4.1 |
| G12 | Driving restaurant/agent steps before their apps? | **Dev-only provisioner** (D14) |
| G13 | First-admin bootstrap? | **Operator CLI `--bootstrap-admin <email>`** in Phase 8 |
| G14 | Agent app before Owner app? | **Yes** |
| G15 | Parallel tracks? | **None — strictly sequential** |

**Grilling round 2**

| # | Question | Answer |
|---|---|---|
| Q16 | Migration strategy | **Keep existing migrations, add `AddOrderFulfillment`, drop/recreate local `talabat`** (approved) |
| Q17 | Reason code lists | **As D13**; picker deferred to Phase 7 |
| Q18 | Actor when Admin acts on behalf | **Always `Admin`**; `RestaurantRequested` code added |
| Q19 | Lifecycle driver | **`--e2e-provision advance-order`** (in-process, dev-only) |
| Q20 | Agent whose delivery is cancelled | **As D7**, learned by polling |
| Q21 | Sequential flow | **Confirmed**; parallel tracks removed from the roadmap |

## 7. Phase 1 completion gate

- [x] Owner answers all questions (Q1–Q6, grilling rounds 1–2).
- [x] This document and ADR-0001/0002 move from *Proposed* to *Accepted*.
- [x] Resolved terms are added to the root [`CONTEXT.md`](../../CONTEXT.md) glossary.
- [ ] PR merged into `main`; `ROADMAP.md` marks Phase 1 complete and Phase 2 next.
