# Phase 1 — Order Lifecycle Design Decision Document

| | |
|---|---|
| **Status** | **Proposed** — awaiting owner review (decisions marked ❓ need an answer) |
| **Milestone / Phase** | M1 Order Lifecycle Spine → Phase 1 (design). Implementation is Phase 2. |
| **Roadmap** | [`ROADMAP.md`](../../ROADMAP.md) |
| **ADRs** | [ADR-0001](../../docs/adr/0001-order-fulfillment-aggregate.md), [ADR-0002](../../docs/adr/0002-create-delivery-on-restaurant-acceptance.md) (both *proposed*) |
| **Builds on** | [`restaurant-owner-and-admin-api-implementation-plan.md`](../../docs/restaurant-owner-and-admin-api-implementation-plan.md) §4 D3/D4 and §6.4 |

## 1. Problem

Verified on `main` (`93741b3`):

- `Order` is an immutable purchase record with **no status** (by design: `docs/bounded-contexts.md`, BR-ORD-006).
- `Delivery` is created inside the checkout transaction and starts in `PendingAssignment`, so couriers can claim, arrive
  and pick up orders **no restaurant has accepted**.
- A courier "cancel" before pickup **terminates** the delivery; `Deliveries.OrderId` is unique, so the order can never be
  re-dispatched.
- Customers see no progress: order list/details expose no status.
- There is no restaurant-side state, no Restaurant Owner capability, and no customer cancellation.

This phase fixes the **state model and the rules between aggregates** so Customer, Courier, Restaurant Owner and Admin
all observe one consistent lifecycle. Restaurant Owner/Admin **apps** come later; their **use cases** are designed here.

## 2. Decisions

### D1 — Three lifecycle owners; `Order` stays immutable  *(ADR-0001)*

| Aggregate | Context | Owns |
|---|---|---|
| `Order` (unchanged) | Ordering | Purchase record: items, prices, totals, address snapshot |
| `OrderFulfillment` (new, one per order) | Restaurant Operations | Kitchen lifecycle: accept/reject, preparing, ready, hand-over |
| `Delivery` (extended) | Delivery | Courier lifecycle: assignment, pickup, drop-off |

Rejected: adding `Status` to `Order` (contradicts documented immutability, makes one row contended by restaurant and
courier writes, mixes purchase and kitchen invariants).

### D2 — `OrderFulfillment` state machine

```text
AwaitingRestaurant ──Accept──► Accepted ──StartPreparing──► Preparing ──MarkReady──► ReadyForPickup ──HandOver──► HandedOver
        │                         │                             │                       │
        └──Reject──► Rejected     └──────────── Cancel ─────────┴───────────────────────┴──► Cancelled
        └──Cancel──► Cancelled
```

- `Accepted → ReadyForPickup` directly is allowed (skip `Preparing` for instant items).
- Terminal: `HandedOver`, `Rejected`, `Cancelled`. `HandedOver` is entered only by the pickup workflow (D5), never by an
  actor directly.
- Every transition records a UTC timestamp, is monotonic in time, and is guarded by `RestaurantId` (same defence-in-depth
  shape as `Delivery`'s agent guard).
- `Reject` requires a reason. `Cancel` records `CancelledBy` (`Customer` | `Admin` | `System`) and a reason.
- `RowVersion` for optimistic concurrency.
- Deviation from the Owner/Admin plan §6.4: `CancelledByAdmin` becomes `Cancelled` + `CancelledBy`, because the customer
  (D7) and later the acceptance timeout (D10) can also cancel; `HandedOver` is new.

### D3 — A `Delivery` is created when the restaurant accepts  *(ADR-0002)*

- **Checkout** atomically saves `Order` + `OrderFulfillment(AwaitingRestaurant)` + cart → `CheckedOut`. No delivery.
- **AcceptOrder** atomically moves fulfillment to `Accepted` **and** creates `Delivery(PendingAssignment)`.
- Consequences: couriers only ever see accepted orders; "restaurant rejects an order that already has a courier" cannot
  happen; the Owner/Admin plan's reject→cancel-delivery coordination becomes unnecessary.
- This **revises BR-DEL-001 and BR-DEL-016** (introduced 2026-10-13): the delivery is still created atomically, but in the
  acceptance transaction instead of checkout.

### D4 — Courier dispatch during preparation  ❓

Recommendation: a delivery is claimable as soon as it exists (fulfillment `Accepted`, `Preparing` or `ReadyForPickup`),
so a courier can travel while the kitchen cooks. Alternative: claimable only at `ReadyForPickup` (longer customer wait,
simpler). **Question Q1.**

### D5 — Pickup gate and hand-over

- `ArrivedAtRestaurant` is allowed in any fulfillment state from `Accepted` onward.
- **`PickedUp` requires fulfillment `ReadyForPickup`**; otherwise `409 FulfillmentNotReady`.
- The pickup use case atomically sets `Delivery → PickedUp` **and** `OrderFulfillment → HandedOver`.

### D6 — Courier release replaces courier cancel

- New `Delivery.Release(agentId)`: allowed in `Assigned` / `ArrivedAtRestaurant`; clears assignment fields, returns to
  `PendingAssignment`, agent becomes `Available`. The same delivery row is reused, so the unique `OrderId` index stays.
- Delivery API: `POST /api/agent/deliveries/{id}/cancel` is **replaced** by `POST /api/agent/deliveries/{id}/release`
  (no Delivery frontend consumes it yet).
- `Delivery.Cancel` remains, used only by the cancellation workflow (D7).
- Reassignment history is not stored in this phase. **Question Q4.**

### D7 — Cancellation matrix

| Actor | Fulfillment state | Allowed | Effect (one transaction) |
|---|---|---|---|
| Customer | `AwaitingRestaurant` | ✅ | Fulfillment `Cancelled(Customer)` |
| Customer | any later state | ❌ `409 OrderNoLongerCancellable` | — |
| Restaurant (via Owner API later, Admin on behalf now) | `AwaitingRestaurant` | Reject ✅ | Fulfillment `Rejected` |
| Admin | `AwaitingRestaurant` … `ReadyForPickup` | ✅ | Fulfillment `Cancelled(Admin)`; delivery (if any) `Cancelled`; assigned courier released to `Available` |
| Admin | `HandedOver` (food with courier) | ❌ `409` — use delivery failure (D8) | — |
| Courier | — | Release only (D6) | — |

Customer cancellation window: until the restaurant accepts. **Question Q2.**

### D8 — Delivery failure

- `Fail(reason)` becomes allowed **only after pickup** (`PickedUp`, `OutForDelivery`); before pickup the courier uses
  Release. Today it is allowed in any active state.
- Courier returns to `Available`; fulfillment stays `HandedOver`; customer status becomes `DeliveryFailed`.
- Resolution (refund, re-delivery) is Admin work in Milestone M4; out of scope here.

### D9 — One customer-facing status, derived (never stored)

An Application function `CustomerOrderStatusMapper.Map(fulfillment, delivery?)` is the **only** place that turns the
two lifecycles into what customers (and later owner/admin lists) see:

| Fulfillment | Delivery | `CustomerOrderStatus` | `courierAssigned` |
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

Any other combination is a broken invariant: the mapper throws, and a test enumerates every combination.
Customer API: `status` + `courierAssigned` added to order list and order details (contract change → frontend client
regeneration in Phase 3). Delivery is polled; push/SignalR is Milestone M5.

### D10 — Acceptance deadline  ❓

Model it now: `OrderFulfillment.AcceptanceDeadline = PlacedAt + N minutes` (configuration). Automatic enforcement
(a background job cancelling with `CancelledBy = System`) is **deferred to M5**; until then Admin cancels manually.
**Question Q3** (value of N; recommended 10).

### D11 — Interim acceptor until the Restaurant Owner app exists

- Admin API gains "act on behalf of the restaurant" endpoints (accept, reject, start preparing, mark ready, cancel),
  under `AdminAccess`. These stay permanently as an operations capability.
- The Application use cases take the actor explicitly, so the Owner API (M3) wraps the **same** handlers with an
  ownership-scoped read; no use case is written twice.
- The E2E provisioner (Phase 4) can drive fixtures to `ReadyForPickup`.
- No hidden auto-accept. **Question Q5.**

### D12 — Transactions, concurrency, migration

- Multi-aggregate workflows (checkout, accept, pickup, admin cancel) use `IUnitOfWork.ExecuteInTransactionAsync`;
  RowVersion conflicts map to `409 ConcurrencyConflict` (global handler already does this).
- Migration `AddOrderFulfillment`: new table (unique `OrderId`, status check constraint, RowVersion). Backfill existing
  orders from their delivery: delivery `Delivered` → fulfillment `HandedOver`; any other delivery state → `Accepted`;
  no delivery → `AwaitingRestaurant`.

## 3. Out of scope (recorded so they are not silently dropped)

Payment method modelling and fees (Cash on Delivery stays implicit), notifications, real-time tracking, automatic
acceptance timeout, Admin failure resolution, Restaurant ownership/Owner API, reassignment history, partial cancellation.

## 4. Test catalogue — written FIRST in Phase 2 (red → green)

Names are binding for Phase 2; each group is committed failing before its production code.

**Domain — `OrderFulfillmentTests` (state machine)**
1. `New_fulfillment_starts_awaiting_restaurant`
2. `Accept_from_awaiting_restaurant_sets_accepted_and_timestamp`
3. `Reject_requires_reason` / `Reject_only_from_awaiting_restaurant`
4. `Start_preparing_only_from_accepted`
5. `Mark_ready_from_accepted_or_preparing` / `Mark_ready_from_other_states_throws`
6. `Hand_over_only_from_ready_for_pickup`
7. `Cancel_from_each_non_handed_over_state_records_actor_and_reason`
8. `Every_transition_from_a_terminal_state_throws` (theory over all terminal × all transitions)
9. `Wrong_restaurant_id_throws_mismatch`
10. `Transition_timestamps_must_be_utc_and_monotonic`

**Domain — `DeliveryReleaseTests`, `DeliveryFailTests`**
11. `Release_from_assigned_or_arrived_returns_to_pending_and_clears_assignment`
12. `Release_after_pickup_throws` / `Release_by_other_agent_throws_mismatch`
13. `Fail_before_pickup_throws` / `Fail_after_pickup_records_reason`

**Application — workflow handlers** (fakes; asserts on transaction use and saves)
14. `Checkout_creates_order_and_fulfillment_but_no_delivery`
15. `Accept_creates_pending_delivery_with_pickup_snapshot_in_one_transaction`
16. `Accept_twice_conflicts` / `Reject_after_accept_conflicts`
17. `Pickup_before_ready_returns_FulfillmentNotReady`
18. `Pickup_when_ready_hands_over_fulfillment_atomically`
19. `Release_returns_agent_to_available`
20. `Customer_cancel_only_while_awaiting_restaurant`
21. `Admin_cancel_cancels_delivery_and_releases_courier`
22. `Admin_cancel_after_handover_conflicts`
23. `Status_mapper_maps_every_reachable_combination` / `Status_mapper_rejects_impossible_combinations`

**Infrastructure (SQL Server)**
24. `Fulfillment_round_trips_and_enforces_unique_order_and_status_constraint`
25. `Fulfillment_rowversion_rejects_stale_update`
26. `Backfill_derives_fulfillment_state_from_existing_delivery`
27. `Failed_accept_rolls_back_fulfillment_and_delivery`

**API** (Customer, Delivery, Admin)
28. Order list/details expose `status` + `courierAssigned`; committed OpenAPI parity updated.
29. Delivery: pending queue excludes unaccepted orders; `/release` works; `/cancel` removed; pickup before ready → 409.
30. Admin act-on-behalf endpoints: 200 for Admin, 403 non-admin/missing scope, 401 anonymous; error codes per D7.

**End-to-end (backend, real SQL Server)**
31. `Customer_order_accepted_prepared_picked_up_and_delivered_is_visible_to_customer_at_every_step`
32. `Courier_release_returns_order_to_queue_and_second_courier_completes_it`

## 5. Business rules to add or revise

- New `BR-FUL-001…008`: fulfillment creation at checkout, accept/reject window, preparation states, ready, hand-over on
  pickup, cancellation matrix, terminal immutability, acceptance deadline.
- Revise `BR-DEL-001` (delivery created on acceptance) and `BR-DEL-016` (atomic with acceptance).
- New `BR-DEL-017` (release instead of cancel), `BR-DEL-018` (fail only after pickup), `BR-DEL-019` (pickup gate).

## 6. Questions for the owner

| # | Question | Recommended answer |
|---|---|---|
| Q1 | Can couriers claim an order while it is still being prepared? (D4) | **Yes** — claimable once accepted; pickup still waits for Ready |
| Q2 | Until when can a customer cancel? (D7) | **Until the restaurant accepts** |
| Q3 | Acceptance deadline N minutes (D10)? Enforced automatically later. | **10 minutes** |
| Q4 | Keep a history of courier releases/reassignments now? (D6) | **No** — add with the M4 audit log |
| Q5 | Allow a configurable auto-accept for seeded demo restaurants? (D11) | **No** — Admin acts on behalf instead |
| Q6 | Is Cash on Delivery the only payment method for the first release (stays out of this phase)? | **Yes** |

## 7. Phase 1 completion gate

- [ ] Owner answers Q1–Q6 (or accepts the recommendations).
- [ ] This document and ADR-0001/0002 move from *Proposed* to *Accepted*.
- [ ] Resolved terms are added to a root `CONTEXT.md` glossary (Order, Order Fulfillment, Delivery, Hand-over, Release,
      Customer Order Status).
- [ ] PR merged into `main`; `ROADMAP.md` marks Phase 1 complete and Phase 2 in progress.
