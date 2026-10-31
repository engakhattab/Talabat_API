# T3 technical walkthrough: preparing, ready, and the pickup gate through to delivered

> Ticket: Phase 2 · T3 ([engakhattab/Talabat_API#12](https://github.com/engakhattab/Talabat_API/issues/12)) ·
> Design: [`specs/011-order-lifecycle/design.md`](../../specs/011-order-lifecycle/design.md) D2, D5, D9, D11, D12 ·
> ADRs: [ADR-0001](../adr/0001-order-fulfillment-aggregate.md) (three lifecycle owners),
> [ADR-0002](../adr/0002-create-delivery-on-restaurant-acceptance.md) (the delivery is created at acceptance) ·
> Branch: `feat/t3-preparing-ready-pickup-gate` → `feature/phase-2-order-lifecycle`

This document explains what T3 built, where each piece lives, and why it lives there. It assumes you have read
[`CONTEXT.md`](../../CONTEXT.md) and the [T2 walkthrough](T2-technical-walkthrough.md). The terms below are the
glossary's terms.

---

## 1. Domain and business intent

### The story

After T2, an Admin can **accept** an order: the **Order Fulfillment** moves to `Accepted` and the order's **Delivery**
is created in `PendingAssignment`. A Delivery Agent can claim it, arrive at the restaurant and pick it up, but the
restaurant side of the Order Fulfillment stopped at `Accepted`: nobody could say the food was being cooked or was
ready. Worse, an agent could "pick up" food that did not exist yet.

T3 completes the happy path:

> The restaurant (an Admin on its behalf, until the Restaurant Owner app exists) **starts preparing** an
> accepted order and **marks it ready for pickup** (skipping `Preparing` is allowed for instant items). Delivery agents
> see the order fulfillment status, `fulfillmentStatus`, on pending and active deliveries. **Pickup is gated**: it is refused with
> `409 FulfillmentNotReady` until the order is `ReadyForPickup`; a successful pickup also performs the **Hand-over**
> of the fulfillment in the same transaction. The customer sees `Preparing`, `ReadyForPickup`, `OnTheWay` and
> `Delivered`.

### Ubiquitous language used

| Term | Meaning in T3 |
|---|---|
| **Order Fulfillment** | The restaurant's handling of one order. T3 adds `Accepted → Preparing → ReadyForPickup → HandedOver` (and `Accepted → ReadyForPickup`). |
| **Hand-over** | The food leaves the restaurant with the Delivery Agent. It is *never* an actor's command: only the pickup workflow performs it (D2, D5). |
| **Pickup gate** | The rule that a Delivery cannot be `PickedUp` unless its fulfillment is `ReadyForPickup` (D5, BR-DEL-019). |
| **Customer Order Status** | Derived from fulfillment + delivery (D9). T3 makes `Preparing`, `ReadyForPickup`, `OnTheWay` and `Delivered` reachable. |
| **Acting on behalf (D11)** | The Admin API performs restaurant actions; the use cases take the actor explicitly so the Owner API (M3) reuses them. |

### Business rules fulfilled

| Rule | Source | Where it is enforced |
|---|---|---|
| Start preparing only from `Accepted` | D2, test 4 | `OrderFulfillment.StartPreparing` |
| Mark ready only from `Accepted` or `Preparing` | D2, test 5 | `OrderFulfillment.MarkReady` |
| Hand over only from `ReadyForPickup` | D2, D5, test 6 | `OrderFulfillment.HandOver` |
| Every transition is guarded by `RestaurantId` and keeps UTC timestamps monotonic | D2, tests 9, 10 | `EnsureRestaurant` / `EnsureTimeNotBefore` inside each transition |
| Pickup before ready → `409 FulfillmentNotReady` | D5, BR-DEL-019, test 18 | `PickUpOrderHandler` (Application) |
| Pickup when ready moves delivery **and** fulfillment atomically | D5, D12, test 19 | `PickUpOrderHandler` + `ExecuteInTransactionAsync` |
| Agents see `fulfillmentStatus` on pending and active deliveries | D5, BR-DEL-020, test 26 | `GetPendingDeliveriesHandler`, `GetActiveDeliveryHandler`, Delivery API responses |
| Customers see `Preparing` / `ReadyForPickup` / `OnTheWay` / `Delivered` | D9, test 25 (part) | `CustomerOrderStatusMapper` |
| Only an Admin with `admin.api` can start preparing or mark ready | D11, test 34 (part) | `AdminAccess` policy on `OrdersController` |

**Re-arranged tests (§4.1):** the design says "any test that picks up a delivery without a `ReadyForPickup`
fulfillment" is re-arranged with a ready fulfillment. `PickUpOrderHandlerTests` were re-arranged that way. Their
assertions did not change. The Delivery API test factory now seeds an accepted fulfillment behind every seeded
delivery, because D3 makes a delivery without one impossible.

---

## 2. Layer-by-layer flow

### Write side: the restaurant (Admin API)

```
POST /api/orders/{orderId}/start-preparing     POST /api/orders/{orderId}/mark-ready     (Talabat.Admin.API)
  │  [Authorize(AdminAccess)]: scope admin.api + Admin role + persisted UserType
  ▼
OrdersController.StartPreparing / MarkReady
  │  builds StartPreparingCommand / MarkReadyCommand(orderId, FulfillmentActor.Admin)
  ▼
StartPreparingHandler / MarkReadyHandler                                                (Talabat.Application)
  │  1. load OrderFulfillment by OrderId (tracked) → 404 OrderNotFound if absent
  │  2. actor.ActingRestaurantId(fulfillment)      ← Admin acts as the order's own restaurant
  │  3. fulfillment.StartPreparing(...) / MarkReady(...)   ← Domain rule (status + restaurant + time)
  │  4. SaveChanges (one aggregate → no explicit transaction needed)
  ▼
UnitOfWork: UPDATE OrderFulfillments SET Status, PreparingAt|ReadyAt WHERE Id AND RowVersion
```

### Write side: the pickup (Delivery API)

```
POST /api/agent/deliveries/{deliveryId}/picked-up                                       (Talabat.Delivery.API)
  │  [Authorize(OperationalDeliveryAgentAccess)]; agent id from the token sub
  ▼
PickUpOrderHandler.Handle                                                               (Talabat.Application)
  │  1. load the Delivery *for this agent* → 404 DeliveryNotFound otherwise
  │  2. load the order's OrderFulfillment (missing = broken D3 invariant → throw)
  │  3. gate: fulfillment.Status != ReadyForPickup → 409 FulfillmentNotReady (nothing changed)
  │  4. delivery.MarkPickedUp(agentId, now)
  │     fulfillment.HandOver(delivery.RestaurantId, now)     ← same clock reading
  │  5. ExecuteInTransactionAsync { SaveChanges }            ← both rows commit or neither
  ▼
UnitOfWork: UPDATE Deliveries ... ; UPDATE OrderFulfillments ...   (one SQL transaction, both RowVersion-checked)
```

### Read side

- **Delivery agents**: `GET /api/agent/deliveries/pending` reads one page of deliveries, then the fulfillments for the
  whole page with **one** `GetByOrderIdsAsync` call, and adds `fulfillmentStatus` to each item.
  `GET /api/agent/deliveries/active` reads the agent's delivery and its order's fulfillment.
- **Customers**: unchanged handlers (`GetOrderHistoryHandler`, `GetOrderDetailsHandler`) already pass fulfillment +
  delivery to `CustomerOrderStatusMapper`. T3 only teaches the mapper the new rows, so customers immediately see the new
  statuses.

---

## 3. File-by-file breakdown

### Domain

**`Talabat.Domain/Aggregates/RestaurantOperations/OrderFulfillment.cs`** (modified)
- New nullable timestamps `PreparingAt`, `ReadyAt`, `HandedOverAt`.
- New transitions `StartPreparing(restaurantId, now)`, `MarkReady(restaurantId, now)`, `HandOver(restaurantId, now)`.
- *Why here:* these are **aggregate invariants**: "only from state X", "only this restaurant", "time never goes
  backwards". They hold no matter who calls (Admin API today, Owner API in M3, the provisioner in T8), so they belong
  in the aggregate, not in a handler. Each method uses the existing private guards (`EnsureRestaurant`,
  `EnsureStatus`, `EnsureTimeNotBefore`), so the three new transitions read exactly like `Accept`.
- `MarkReady` checks time against `PreparingAt ?? AcceptedAt`: the latest fulfillment timestamp, whichever path was
  taken.
- `HandOver` takes a `restaurantId` even though an agent triggers it. D2 says *every* transition is guarded by
  `RestaurantId`. The pickup passes the **delivery's** restaurant, so a delivery paired with another restaurant's
  fulfillment (a data bug) is refused rather than silently handed over.

### Infrastructure

**`Persistence/Migrations/20261014015113_AddOrderFulfillment.cs` + `.Designer.cs`, `TalabatDbContextModelSnapshot.cs`** (modified)
- Three nullable `datetime2` columns added to the `OrderFulfillments` table.
- *Why fold them into the existing migration instead of adding a new one:* see §4. The column mapping itself needs no
  configuration: EF maps public properties with private setters by convention.

### Application

**`DeliveryAgents/ProgressPickup/PickUpOrderHandler.cs`** (modified)
- Now depends on `IOrderFulfillmentRepository`; implements the gate and the atomic hand-over.
- *Why the gate is in the Application layer and not in `Delivery.MarkPickedUp`:* the rule spans **two aggregates**
  (`Delivery` and `OrderFulfillment`). An aggregate must not load or reference another aggregate. CLAUDE.md says
  cross-aggregate work goes through a domain service or an Application use case. The use case already loads both and
  owns the transaction, so it is the natural home. The domain methods it calls still protect their own invariants.
- *Why the gate check comes before `MarkPickedUp`:* failing early means neither aggregate is mutated in memory on the
  409 path. The test asserts both statuses are unchanged and nothing was saved.
- *Why it catches `DomainException or ArgumentException`:* the same contract as the other fulfillment use cases.
  Argument guards (for example `Guard.Utc` on the clock reading) come back as a Validation result instead of escaping
  as an exception. The older filter caught only `DomainException`; the review flagged the inconsistency.

**`DeliveryAgents/DeliveryFulfillmentLookup.cs`** (new, internal)
- `GetRequiredForDeliveryAsync` (active delivery, pickup) and `RequiredStatusFor` (the pending queue's batched lookup)
  hold the D3 invariant "every delivery has a fulfillment" **once**, and throw the same `InvalidOperationException`
  naming the delivery and the order. Before the review, two handlers copied the null-check and the pending queue
  failed with a bare `KeyNotFoundException`.

**`Common/Results/ApplicationErrorCodes.cs`** (modified)
- New `FulfillmentNotReady` code. It is an application-level code, not an exception name, because the gate is an
  application rule and the design names the error code exactly.

**`DeliveryAgents/GetPendingDeliveries/{GetPendingDeliveriesHandler,PendingDeliveryDto}.cs`** and
**`DeliveryAgents/GetActiveDelivery/{GetActiveDeliveryHandler,ActiveDeliveryDto}.cs`** (modified)
- The DTOs gain `OrderFulfillmentStatus FulfillmentStatus`; the handlers read it.
- *Why one batched read for the pending queue:* reading fulfillments one by one would be an N+1 query per page.
  `GetByOrderIdsAsync` already existed (T1 added it for the order history), so the pending queue costs one extra query.

**`Fulfillment/StartPreparing/*`, `Fulfillment/MarkReady/*`** (new: command, result record, handler each)
- Thin use cases: load → transition as the acting restaurant → save once.
- *Why no explicit transaction:* only one aggregate changes, and a single `SaveChanges` is already atomic. Compare with
  pickup and acceptance, which change two aggregates and therefore use `ExecuteInTransactionAsync`.
- *Why they take a `FulfillmentActor`:* D11. The Owner API (M3) will call the same handlers with
  `FulfillmentActor.Restaurant(id)`, and the domain's restaurant guard then refuses other restaurants' orders.

**`Fulfillment/FulfillmentActor.cs`** (modified)
- New `ActingRestaurantId(fulfillment)`. The "an Admin acts as the order's own restaurant" rule used to be an inline
  `switch` in `AcceptOrderHandler`; with three use cases needing it, it now lives once on the actor itself.

**`Fulfillment/AcceptOrder/AcceptOrderHandler.cs`** (modified): uses `ActingRestaurantId`; behaviour unchanged.

**`Ordering/Status/CustomerOrderStatusMapper.cs`** (modified)
- New rows: `Preparing` and `ReadyForPickup` with a pre-pickup delivery (agent-assigned follows the delivery);
  `HandedOver` + `PickedUp`/`OutForDelivery` → `OnTheWay`; `HandedOver` + `Delivered` → `Delivered`.
- Small helpers `IsBeforePickup` / `IsAgentAssigned` keep the switch readable. Everything else still falls through to
  the "broken invariant" throw (D9: the mapper is the only place statuses are derived, and impossible pairs are loud).

**`DependencyInjection.cs`** (modified): registers the two new handlers.

### API

**`Talabat.Delivery.API/Contracts/Deliveries/{PendingDeliveryResponse,ActiveDeliveryResponse}.cs`** and
**`Controllers/DeliveriesController.cs`** (modified): the responses gain `fulfillmentStatus` (serialized as a string,
like `status`), and the mapping passes it through.

**`Talabat.Admin.API/Controllers/OrdersController.cs`** (modified) and
**`Contracts/Orders/{StartPreparingResponse,MarkReadyResponse}.cs`** (new)
- Two endpoints under the controller-wide `AdminAccess` policy, shaped like `accept`: `orderId`,
  `fulfillmentStatus`, and the transition timestamp (`preparingAt` / `readyAt`).

**`openapi/delivery-api.json`, `openapi/admin-api.json`** (regenerated, own commits): additions only.

### Tests

| File | What it proves |
|---|---|
| `Talabat.Domain.Tests/RestaurantOperations/OrderFulfillmentTests.cs` | Tests 4, 5, 6 and the restaurant guard on the new transitions (test 9). |
| `Talabat.Application.Tests/Ordering/Status/CustomerOrderStatusMapperTests.cs` | Test 25 rows for T3, plus impossible pairs that must keep throwing. |
| `Talabat.Application.Tests/Domain/DeliveryAgents/PickUpOrderHandlerTests.cs` | Tests 18 and 19; existing tests re-arranged with a ready fulfillment (§4.1); the check order (gate before delivery state); argument guards return a Validation result. |
| `Talabat.Application.Tests/Domain/DeliveryAgents/DeliveryFulfillmentStatusTests.cs` (new) | Test 26 across both query handlers; a delivery without a fulfillment is reported the same descriptive way by both. |
| `GetPendingDeliveriesHandlerTests.cs`, `GetActiveDeliveryHandlerTests.cs` | Arranged with an accepted fulfillment (D3); assertions unchanged. |
| `Talabat.Delivery.API.Tests/PickupGateTests.cs` (new) | Test 33 (part): arrive → 409 `FulfillmentNotReady` → ready → pickup → `PickedUp` + `HandedOver`. |
| `Talabat.Delivery.API.Tests/DeliveryOperationalContractTests.cs` | `fulfillmentStatus` in the pending/active JSON and both OpenAPI schemas. |
| `Talabat.Delivery.API.Tests/Infrastructure/CustomWebApplicationFactory.cs` | Seeds an accepted fulfillment per seeded order; exposes `OrderId`. |
| `Talabat.Admin.API.Tests/OrderPreparationTests.cs` (new) | Test 34 (part): 401/403/403, 200 with body, skip-preparing, 409 before acceptance, 404 unknown order. |

---

## 4. Tricky concepts and decisions

**Why the hand-over is not an endpoint.** D2 says `HandedOver` is entered *only* by the pickup workflow. If the
restaurant could hand over on its own, the fulfillment could say "with the agent" while the delivery says "at the
restaurant".
That is exactly the two-lifecycles-disagree problem ADR-0001 exists to prevent. Tying both changes to one use case and
one transaction makes that disagreement impossible.

**Atomicity and RowVersion on pickup.** Both `Delivery` and `OrderFulfillment` have a `RowVersion`. Inside
`ExecuteInTransactionAsync`, a single `SaveChanges` issues two RowVersion-checked `UPDATE`s. If, say, an Admin cancel
(T6) changes the fulfillment between our read and our save, the fulfillment update matches zero rows. EF raises
`DbUpdateConcurrencyException`, the unit of work turns it into `ConcurrencyConflictException`, the transaction rolls
back (so the delivery is *not* picked up either), and the global handler returns `409 ConcurrencyConflict`.

**Order of checks on pickup.** Agent ownership first (404 hides other agents' deliveries, no information leak), then
the gate (409 `FulfillmentNotReady`), then the delivery's own state machine (409
`InvalidDeliveryStatusTransitionException`, e.g. picking up before arriving). An agent who has not arrived *and*
whose food is not ready gets `FulfillmentNotReady`, the more useful message. A test now pins this order.

**Why the migration was folded into `AddOrderFulfillment`.** Test 29 asserts `AddOrderFulfillment` is the **last**
migration, encoding D12's "one migration for the phase on top of the four existing ones". A second migration would
mean editing that test, which the project rules allow only when an accepted design revises the rule. Folding is safe
here because Phase 2 has not reached `main`, and D12 already approves dropping and recreating the local `talabat`
database during Phase 2. **Action for you:** if your local `talabat` already has `AddOrderFulfillment` applied, drop
and recreate it (`dotnet ef database update` after dropping) so the three new columns exist.

**Missing fulfillment is a throw, not a 404.** A delivery is only ever created by acceptance (D3), in the same
transaction as the fulfillment's change. A delivery without a fulfillment is therefore a corrupted database, not a
user error, and it surfaces loudly (500 + logs) instead of being disguised as "not found". The same reasoning made the
mapper throw on impossible combinations in T1/T2. This is also why the Delivery API test fixture had to be fixed: it
seeded exactly such impossible rows.

**`fulfillmentStatus` after pickup.** D5 lists `Accepted | Preparing | ReadyForPickup` for the agent. Those are the
values on the pending queue and on an active delivery before pickup. After pickup, the active delivery truthfully
reports `HandedOver`. The OpenAPI enum lists all fulfillment states because it is the one domain enum. The review
flagged this as wider than D5's wording; the owner chose to keep the full enum rather than add an agent-only enum.

**Trade-offs not taken.**
- *A domain service for pickup* (like `DeliveryAssignmentDomainService`): there is no extra invariant beyond "gate,
  then two transitions", so the use case is enough. If T6/T7 add more cross-aggregate pickup rules, extracting one is
  a refactor, not a redesign.
- *A separate `AddFulfillmentProgressTimestamps` migration*: see above.
- *Handler-level tests for start-preparing / mark-ready*: agreed seams were the domain (rules) and HTTP (wiring). The
  handlers are thin and are fully exercised by `OrderPreparationTests`.
- *Merging `StartPreparingHandler` and `MarkReadyHandler` into one generic handler*: the review noted they share
  their shape. They stay separate: each is one use case the Owner API (M3) will wrap, and the reject/cancel use cases
  (T5, T6) take reason codes, so a shared abstraction now would be shaped by two cases and bent by the next ones.

---

## 5. Commit trail

| Commit | What it proves |
|---|---|
| `586c26b` test / `6322ea6` feat | Test 4: start preparing only from `Accepted`. |
| `48ff9db` test / `e5ad818` feat | Test 5 + guard: mark ready from `Accepted` or `Preparing` only. |
| `9b12e65` test / `8d1d324` feat | Test 6 + guard: hand over only from `ReadyForPickup`. |
| `ce638f4` feat | The new timestamps are persisted; test 29 and every SQL test green again. |
| `2c30d83` test / `68eac26` feat | Test 25 rows: customers see `Preparing`, `ReadyForPickup`, `OnTheWay`, `Delivered`. |
| `c7dd68a` test / `eb57794` feat | Tests 18, 19: the pickup gate and atomic hand-over; §4.1 re-arrangement. |
| `1f44255` test / `cc0de4c` feat | Test 26: agents see `fulfillmentStatus`. |
| `8c92b75` test | Delivery API fixture seeds the fulfillment every delivery must have (D3). |
| `78292d0` test / `dc2b72b` feat | Test 33 (part): gate and `fulfillmentStatus` over HTTP. |
| `a0bd1d8` chore | Delivery contract regenerated (additions only). |
| `cd023cf` test / `4d2d936` feat | Test 34 (part): Admin start-preparing and mark-ready endpoints. |
| `d569ceb` chore | Admin contract regenerated (additions only). |
| `611ca21` docs | This walkthrough. |
| `df8ca32` refactor | Review: Order Fulfillment vocabulary instead of "kitchen" (CONTEXT.md). |
| `20142c2` test / `34d404e` fix | Review: a delivery without a fulfillment is reported the same descriptive way everywhere. |
| `8506a20` test / `671ecf4` fix | Review: pickup returns argument guards as a Validation result. |
| `dc33406` test | Review: the pickup check order is pinned (passed on first run). |
| next `test` commit | Review: pickup of a delivery without a fulfillment is reported like the queries (passed on first run). |

Each red commit compiled (stubs throwing `NotImplementedException`, or a missing route), so it fails on behaviour, not
on the build.

---

## 6. Self-check questions

1. Why does `PickUpOrderHandler` check the fulfillment's status itself instead of `Delivery.MarkPickedUp` doing it?
   What would break in the aggregate design if it did?
2. A customer's order shows `ReadyForPickup` with `deliveryAgentAssigned: false`. Which fulfillment and delivery states
   produce that, and what does the agent's pending queue show for the same order?
3. Between an agent reading a ready order and saving the pickup, an Admin cancels the order (T6). Walk through what SQL
   Server, EF and the API each do, and what state both rows end in.
4. Why do `StartPreparingHandler` and `MarkReadyHandler` save without `ExecuteInTransactionAsync`, while pickup and
   acceptance use it?
5. Why was the migration change folded into `AddOrderFulfillment`, and when would that *not* have been acceptable?
