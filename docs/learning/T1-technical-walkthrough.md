# T1 technical walkthrough: checkout places the order with the restaurant

> Ticket: Phase 2 · T1 ([engakhattab/Talabat_API#10](https://github.com/engakhattab/Talabat_API/issues/10)) ·
> PR [engakhattab/Talabat_API#21](https://github.com/engakhattab/Talabat_API/pull/21) ·
> Design: [`specs/011-order-lifecycle/design.md`](../../specs/011-order-lifecycle/design.md) D1, D2, D3, D9, D10, D12 ·
> ADRs: [ADR-0001](../adr/0001-order-fulfillment-aggregate.md),
> [ADR-0002](../adr/0002-create-delivery-on-restaurant-acceptance.md)
>
> Written after the fact (T1 was merged before the walkthrough rule existed). Read it before
> [`T2-technical-walkthrough.md`](T2-technical-walkthrough.md): T2 builds directly on what T1 introduced.

---

## 1. Domain and business intent

### The problem T1 solved

Before T1, **checkout created a `Delivery` immediately**. This caused three problems:

- **Delivery Agents could claim an order no restaurant had agreed to cook.** If the restaurant later refused, an agent
  would already be driving to it.
- **The restaurant had no say.** There was no "the restaurant received this order and is deciding" state.
- **Customers saw no progress.** `Order` is immutable and has no status, so there was nothing to show between "paid"
  and "delivered".

### The design answer (D1–D3)

The order lifecycle is split between **three owners**, one aggregate each (ADR-0001):

| Aggregate | Owns | Lifecycle |
|---|---|---|
| `Order` | What was bought: items, prices, address snapshot | **None.** Immutable once placed. |
| `OrderFulfillment` (new in T1) | The restaurant's handling of the order | `AwaitingRestaurant → Accepted → Preparing → ReadyForPickup → HandedOver`, or `Rejected` / `Cancelled` |
| `Delivery` | The Delivery Agent's job | 8 states; **created only when the restaurant accepts** (D3, delivered in T2) |

T1 delivers the first step:

> **Checkout places the order with the restaurant**: it saves `Order` + `OrderFulfillment(AwaitingRestaurant)` + the
> cart's move to `CheckedOut`, atomically, **and creates no delivery**. Customers now see a status (`Placed`) on their
> order list and details.

### Ubiquitous language introduced

| Term | Meaning |
|---|---|
| **Order Fulfillment** | The restaurant side of an order's lifecycle (see the `CONTEXT.md` glossary). |
| **AwaitingRestaurant** | Placed; the restaurant has not decided yet. |
| **Acceptance window / deadline** | How long the restaurant has to decide: `AcceptanceDeadline = PlacedAt + AcceptanceWindow` (10 minutes by default, D10). Enforced automatically only in Phase 11. |
| **Customer Order Status** | One status the customer sees, *derived* from fulfillment + delivery (D9). T1 introduces the full enum and the first mapping row: `Placed`. |

### Business rules fulfilled or revised

| Rule | Change |
|---|---|
| **BR-DEL-001** "Delivery is created after successful checkout" | **Revised by D3.** Checkout no longer creates a delivery; acceptance does (T2). |
| **BR-DEL-016** "An order and its delivery are persisted atomically" | **Revised by D3.** Atomicity stays, but the pair is now order + *fulfillment* at checkout, and fulfillment + delivery at acceptance (T2). |
| D2: fulfillment transitions are guarded by status, restaurant id and monotonic UTC timestamps | `OrderFulfillment` (Domain) |
| D10: acceptance window comes from configuration and must be positive | `OrderFulfillmentOptions` + startup validation |
| D12: new table on top of existing migrations, unique `OrderId`, status check constraint, RowVersion, no backfill | `AddOrderFulfillment` migration |

---

## 2. Layer-by-layer flow

### Write side: `POST /api/me/checkout` (Customer API)

```
CheckoutController            (Talabat.API, builds Talabat.Customer.API)
  ▼
CheckoutHandler.Handle        (Application)
  │  load active cart, customer + addresses, restaurant + products
  │  CheckoutDomainService.ValidateCheckout(...)        ← Domain service: availability, open hours, prices
  │  Order.CreateFromCheckout(...)                       ← Domain factory: immutable order
  │  ExecuteInTransactionAsync {
  │     orders.Add(order); cart.MarkCheckedOut(now); carts.Update(cart)
  │     SaveChanges                                      ← generates order.Id
  │     new OrderFulfillment(order.Id, restaurantId, now, options.AcceptanceWindow)
  │     fulfillments.Add(fulfillment)
  │     SaveChanges
  │  }                                                   ← commit both, or neither
  ▼
UnitOfWork / TalabatDbContext (Infrastructure) → SQL Server
```

### Read side: `GET /api/me/orders` and `GET /api/me/orders/{id}`

```
GetOrderHistoryHandler / GetOrderDetailsHandler   (Application)
  │  load the order page (or one order)
  │  load their fulfillments   (batch: GetByOrderIdsAsync, AsNoTracking)
  │  load their deliveries     (batch: GetByOrderIdsAsync, AsNoTracking; none exist after T1)
  │  CustomerOrderStatusMapper.Map(fulfillment, delivery) → CustomerOrderProgress
  │  OrderMapper.ToSummary / ToDetails(order, progress)
  ▼
OrderController → OrderSummaryDto / OrderDetailResponse gain `status` and `deliveryAgentAssigned`
```

---

## 3. File-by-file breakdown

### Domain

#### `Aggregates/RestaurantOperations/OrderFulfillment.cs` (new)

The aggregate root for the restaurant's handling of one order.

- **Constructor:** validates `orderId`/`restaurantId` (positive), `placedAt` (UTC) and `acceptanceWindow` (> 0). It sets
  `Status = AwaitingRestaurant` and `AcceptanceDeadline = placedAt + window`.
- **`Accept(restaurantId, now)`:** the first transition. T1 added it early because the persistence and invariant
  tests (9, 10, 28) needed a transition to exercise. It runs three guards:
  - `EnsureRestaurant`: the acting restaurant must own the order, otherwise
    `OrderFulfillmentRestaurantMismatchException`.
  - `EnsureStatus(AwaitingRestaurant)`: otherwise `InvalidOrderFulfillmentStatusTransitionException`.
  - `EnsureTimeNotBefore(now, PlacedAt)`: timestamps must be UTC and move forward, otherwise
    `InvalidOrderFulfillmentTimestampException`.
- **`RowVersion`:** an optimistic-concurrency token (see 4.2).
- **Private setters and a private parameterless constructor:** only the aggregate's own methods change its state. EF
  uses the private constructor to materialize rows.

*Why a new aggregate instead of a `Status` on `Order` (ADR-0001):*
- `Order` is a **record of a purchase**. It must never change, because prices and items are a legal and financial
  fact.
- The restaurant's decision and the agent's progress are different **consistency boundaries**, changed by different
  actors at different times.
- Putting all of it on `Order` would make it a god-aggregate that every actor locks and every change risks.
- Separate aggregates have separate RowVersions, so a customer reading an order never conflicts with a kitchen
  update.

*Why `RestaurantOperations` and not `Ordering`:* the folder names the **bounded context that owns the behaviour**. The
fulfillment is the restaurant's work. M3's Restaurant Owner features grow here.

#### `OrderFulfillmentStatus.cs` (new)

The 7 states, with explicit numeric values (`AwaitingRestaurant = 1` … `Cancelled = 7`).

*Why explicit numbers:* they are stored as `int` and guarded by a SQL check constraint `[Status] IN (1..7)`. Explicit
values mean reordering the enum can never silently corrupt stored rows.

#### Three new exceptions

`InvalidOrderFulfillmentStatusTransitionException`, `InvalidOrderFulfillmentTimestampException` and
`OrderFulfillmentRestaurantMismatchException`, all derived from `DomainException`.

*Why one type per broken rule:* the type **is** the error contract. `DomainExceptionMapper` maps each to a category, and
the API exposes the type name as `errorCode`, so a client can tell "wrong state" apart from "wrong restaurant".
(T2 later fixed the mismatch mapping to 403.)

#### `Interfaces/IOrderFulfillmentRepository.cs` (new) and `IDeliveryRepository.GetByOrderIdsAsync` (added)

The repository interfaces live in **Domain** and are implemented in **Infrastructure**. This is the Dependency
Inversion Principle: the inner layer defines what it needs, and the outer layer supplies it.
- `GetByOrderIdAsync` returns a **tracked** entity, for writes.
- `GetByOrderIdsAsync` is a **batch** read for list screens.

### Application

#### `Fulfillment/OrderFulfillmentOptions.cs` (new)

A configuration object holding `AcceptanceWindow` (default 10 minutes), bound from the `OrderFulfillment` section of
`appsettings.json`.

*Why in Application:* the use case (checkout) consumes it. Application declares the shape; Infrastructure binds and
validates it. That keeps `IConfiguration` (an ASP.NET/hosting type) out of Application.

#### `Ordering/Checkout/CheckoutHandler.cs` (modified)

The delivery creation is replaced by `new OrderFulfillment(...)` inside the existing transaction. Its dependencies
change from `IDeliveryRepository` to `IOrderFulfillmentRepository` plus the options.

*Why two `SaveChanges` inside one transaction:* the fulfillment needs `order.Id`, which SQL Server generates (an
identity column) only on insert. The first save produces the id; the second inserts the fulfillment. The surrounding
`ExecuteInTransactionAsync` makes both commit together. A crash between the saves rolls back the order too, so a
**placed order can never exist without its fulfillment**.

#### `Ordering/Status/` (new): `CustomerOrderStatus`, `CustomerOrderProgress`, `CustomerOrderStatusMapper`

- **`CustomerOrderStatus`:** the 9 customer-facing states (`Placed` … `DeliveryFailed`), serialized as strings.
- **`CustomerOrderProgress`:** `(Status, DeliveryAgentAssigned)`, what the mapper returns.
- **`CustomerOrderStatusMapper.Map(fulfillment, delivery?)`:** T1 implements only
  `(AwaitingRestaurant, no delivery) → Placed`. Every other combination throws `InvalidOperationException`.

*Why derived, never stored (D9):* the truth lives in two aggregates. A stored third copy would need updating on every
transition by every actor, and would eventually drift. A **pure function** over the two real states can't drift and is
trivially testable. *Why throw on unknown combinations:* an impossible state means a bug somewhere. Failing loudly
surfaces it; a silent default would show customers a lie. Each later ticket adds exactly the rows it makes reachable
(T2 added `Accepted`).

#### `GetOrderHistoryHandler`, `GetOrderDetailsHandler`, `OrderMapper`, `OrderSummary`, `OrderDetails` (modified)

The handlers now load fulfillments and deliveries, compute the progress, and pass it to the mapper.

*Why batch-load in history:* a page of 20 orders loads all 20 fulfillments in **one** query
(`WHERE OrderId IN (...)`), then looks each up in a dictionary. Loading per order would be the classic **N+1 query**
problem: 1 query for the page plus 20 more.

*Why a missing fulfillment throws:* after T1 every order has one, created atomically at checkout. A missing one is a
broken invariant, not a normal case. (D12 means there is no backfill: the dev database was dropped and recreated.)

### Infrastructure

#### `Persistence/Configurations/OrderFulfillmentConfiguration.cs` (new)

The EF Core mapping:
- table `OrderFulfillments`;
- `Status` stored as `int`, with check constraint `CK_OrderFulfillments_Status`;
- `RowVersion` mapped with `.IsRowVersion()`;
- foreign keys to `Orders` and `Restaurants` with `Restrict` delete;
- a **unique index** `UX_OrderFulfillments_OrderId`.

*Why the database repeats the rules:* defence in depth. The aggregate already prevents bad states, but the database is
the last line against any other writer, such as a migration, a manual SQL fix or a future bug. The unique index
guarantees at most one fulfillment per order even under concurrent checkouts.

#### `Migrations/20261014015113_AddOrderFulfillment.cs` (+ Designer, snapshot)

The migration creates the table **on top of** the existing migrations, without rewriting history (D12). It does no
backfill, because the project is pre-production.

#### `Repositories/OrderFulfillmentRepository.cs` (new), `DeliveryRepository.GetByOrderIdsAsync` (added)

These implement the Domain interfaces. Batch reads use `AsNoTracking()`, because list screens never write, so EF
should not spend memory tracking them.

#### `DependencyInjection.cs` (modified)

Registers the repository and **binds and validates** `OrderFulfillmentOptions` at startup. A zero or negative window
throws `InvalidOperationException` naming the setting.

*Why validate at startup, not at checkout (`fail fast`):* this was the second red/green pair in T1. Before it, a bad
config value only surfaced when a customer checked out, as a confusing 400. A misconfiguration is an *operator* error:
the host should refuse to start, and the message should say exactly which setting is wrong.

#### `TalabatDbContext.cs`

Adds the `DbSet<OrderFulfillment> OrderFulfillments`.

### Customer API

- **`Contracts/Orders/OrderContracts.cs`, `Controllers/OrderController.cs`:** `OrderSummaryDto` and
  `OrderDetailResponse` gain `status` and `deliveryAgentAssigned`.
- **`appsettings.json`:** adds `"OrderFulfillment": { "AcceptanceWindow": "00:10:00" }`.
- **`openapi/customer-api.json`:** regenerated in its **own commit**. The frontend copies it and regenerates its client
  in Phase 3.

### Tests

| Area | File | What it proves (design §4) |
|---|---|---|
| Domain | `OrderFulfillmentTests.cs` | 1 (new = AwaitingRestaurant + deadline), 2 (accept), 9 (wrong restaurant), 10 (UTC + monotonic), window must be positive |
| Application | `CheckoutHandlerSuccessTests.cs` | 15 `Checkout_creates_order_and_fulfillment_but_no_delivery`; one transaction, two saves |
| Application | `CustomerOrderStatusMapperTests.cs` | 25 (part): `AwaitingRestaurant` + none → `Placed`; + a delivery → impossible |
| Application | history/details handler tests, test doubles (`FakeOrderFulfillmentRepository`, `TestData.CreateFulfillment`) | Lists and details carry the derived status |
| Infrastructure | `OrderFulfillmentPersistenceTests.cs` | 27 (round trip, unique `OrderId`, check constraint), 28 (stale RowVersion → `ConcurrencyConflictException`), 29 (migration applies on an empty DB) |
| Infrastructure | `OrderFulfillmentOptionsTests.cs` | A non-positive window fails at startup |
| Customer API | `CheckoutEndpointTests.cs`, `OrderEndpointTests.cs`, `OrderHistoryPagingTests.cs` | 32 (part): `status` + `deliveryAgentAssigned` exposed; checkout creates no delivery |

**Retired, per design §4.1 (same red commit as their replacements):**
- `CheckoutEndpointTests.Checkout_Authenticated_CreatesDeliveryWithRestaurantPickupSnapshot` (BR-DEL-001/016), replaced
  by `Checkout_Authenticated_PlacesOrderWithRestaurantAndCreatesNoDelivery`. The pickup snapshot moved to acceptance
  (test 16, T2).
- The delivery assertions in `CheckoutHandlerSuccessTests`, replaced by test 15.

---

## 4. Tricky concepts and decisions

### 4.1 "Two saves, one transaction": why not one save?

EF can insert related rows in one `SaveChanges` when they are linked through a **navigation property**: it inserts the
parent, reads back its id, and fixes up the child's foreign key. `OrderFulfillment` deliberately holds only `OrderId`
(an `int`), not an `Order` navigation. Aggregates reference each other **by id**, never by object, so loading one never
drags the other into memory or into its consistency boundary. The cost is that the id must exist before the child is
built, hence two saves. The transaction restores atomicity.

### 4.2 Optimistic concurrency with RowVersion

SQL Server's `rowversion` column changes automatically on every update. EF sends
`UPDATE ... WHERE Id = @id AND RowVersion = @originalVersion`. If another writer changed the row first, 0 rows match
and EF throws `DbUpdateConcurrencyException`, which `UnitOfWork` translates into the domain's
`ConcurrencyConflictException` (HTTP 409). Test 28 proves it with two scopes loading the same fulfillment.

*Why optimistic and not a lock (`SELECT ... WITH (UPDLOCK)`):* conflicts on one order are rare, and pessimistic locks
hold database resources across the request and risk deadlocks. Optimistic concurrency costs nothing until an actual
conflict, then makes the loser re-read (D7, D12). T2 relies on this for two Admins accepting at once.

### 4.3 `datetime2` loses `DateTimeKind`

SQL Server stores the time but not the `Kind`, so values read back come out as `Unspecified`. `EnsureTimeNotBefore`
re-tags a persisted `Unspecified` value as UTC before comparing, the same fix `Delivery` already used. Without it,
`Guard.Utc` would reject a perfectly valid stored timestamp.

### 4.4 Configuration validation belongs at the composition root

Options are bound where the host is composed (Infrastructure's `AddInfrastructure`), so a broken setting stops the
process **before it serves traffic**. The domain constructor still rejects a non-positive window (defence in depth),
but the startup check is what gives the operator a clear message.

### 4.5 Contract changes travel separately

The Customer contract gained two required fields. That is a **breaking change** for any client that deserializes
strictly. It was committed on its own (`chore(openapi): ...`) so the change is visible in review. The frontend picks
it up deliberately in Phase 3 (`npm run api:copy` and client regeneration), never by accident.

### 4.6 Dropping the dev database instead of backfilling

Existing orders had deliveries but no fulfillments, which is an impossible state under the new model. Writing a
backfill would mean inventing fulfillment states for history. The project is pre-production, so the owner approved
dropping and recreating the local `talabat` database (D12). In production this decision would need a real migration
plan.

---

## 5. Commit trail

| Commit | Kind |
|---|---|
| `cfc7e56 test(fulfillment): checkout places the order with the restaurant (red)` | Tests 1, 2, 9, 10, 15, 25 (part), 27–29, 32 (part); two §4.1 retirements; compiling stubs that throw `NotImplementedException` |
| `2ca6a8f feat(fulfillment): checkout places the order with the restaurant (green)` | Aggregate, persistence, migration, checkout, read side |
| `19cc295 chore(openapi): regenerate the Customer contract with order status` | Contract only |
| `1d0f891 test(fulfillment): reject a non-positive acceptance window at startup (red)` | Review follow-up |
| `f9ad611 fix(fulfillment): reject a non-positive acceptance window at startup (green)` | Startup validation |

## 6. Self-check questions

1. Why does `Order` stay immutable, and what would go wrong if the restaurant's status were a column on `Orders`?
2. `OrderFulfillment` stores `OrderId` instead of an `Order` navigation. What does that buy, and what does it cost in
   `CheckoutHandler`?
3. Why is the customer's status computed on every read instead of stored, and why does the mapper throw rather than
   return a default?
4. Two requests update the same fulfillment. Walk through what SQL Server, EF, `UnitOfWork` and the API each do.
5. Why validate `AcceptanceWindow` at startup when the `OrderFulfillment` constructor already rejects a bad window?
