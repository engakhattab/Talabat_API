# T2 technical walkthrough: an Admin accepts an order

> Ticket: Phase 2 · T2 ([engakhattab/Talabat_API#11](https://github.com/engakhattab/Talabat_API/issues/11)) ·
> Design: [`specs/011-order-lifecycle/design.md`](../../specs/011-order-lifecycle/design.md) D3, D9, D11, D12 ·
> ADRs: [ADR-0001](../adr/0001-order-fulfillment-aggregate.md) (three lifecycle owners),
> [ADR-0002](../adr/0002-create-delivery-on-restaurant-acceptance.md) (the delivery is created at acceptance) ·
> Branch: `feat/t2-admin-accepts-order` → `feature/phase-2-order-lifecycle`

This document explains what T2 built, where each piece lives, and why it lives there. It assumes you have read
[`CONTEXT.md`](../../CONTEXT.md). The terms below are the glossary's terms.

---

## 1. Domain and business intent

### The story

After T1, **checkout** *places* an order with the restaurant: it saves the `Order`, an `OrderFulfillment` in
`AwaitingRestaurant`, and moves the cart to `CheckedOut`. **No `Delivery` exists yet.** Until T2 nothing could move
the order forward, so delivery agents had nothing to claim.

T2 adds **acceptance**:

> An **Admin** accepts an order on the restaurant's behalf. Acceptance moves the **Order Fulfillment** to `Accepted`
> and, in the same transaction, creates the order's **Delivery**, waiting for a **Delivery Agent**
> (`PendingAssignment`). From then on the order appears in the delivery agents' pending queue. The customer sees
> `Accepted`.

### Ubiquitous language used

| Term | Meaning in T2 |
|---|---|
| **Order** | Immutable record of what the customer bought (ADR-0001). It has **no status**, and T2 never modifies it. |
| **Order Fulfillment** | The restaurant's handling of one order. T2 drives `AwaitingRestaurant → Accepted`. |
| **Delivery** | The Delivery Agent's job for one order. T2 *creates* it in `PendingAssignment`. |
| **Customer Order Status** | What the customer sees. It is *derived* from fulfillment + delivery and never stored (D9). |
| **Acting on behalf (D11)** | Until the Restaurant Owner app exists (M3), an Admin performs restaurant actions. |

### Business rules fulfilled

| Rule | Source | Where it is enforced |
|---|---|---|
| Only an order `AwaitingRestaurant` can be accepted; accepting twice conflicts | D2, test 17 | `OrderFulfillment.Accept` (Domain, from T1) |
| A restaurant accepts only its own orders | D2, D11 | `OrderFulfillment.Accept` → `OrderFulfillmentRestaurantMismatchException` → 403 |
| Acceptance creates the delivery **atomically** | D3, D12; revises BR-DEL-001/016 | `AcceptOrderHandler` + `IUnitOfWork.ExecuteInTransactionAsync` |
| The pickup snapshot is taken **at acceptance time**; the drop-off address comes from the order | D3 | `AcceptOrderHandler` → `DeliveryFactory.ForOrder` |
| Delivery agents only ever see accepted orders | D3, test 33 | Follows from the above: only acceptance creates deliveries |
| An accepted order shows the customer `Accepted` | D9, test 25 | `CustomerOrderStatusMapper` |
| Only an Admin with the `admin.api` scope may accept | D11, test 34 | `AdminAccess` policy on `OrdersController` |
| A lost write race returns `409 ConcurrencyConflict`, never 500 | D12 | Admin `DomainExceptionHandler` |

**Revised rule:** BR-DEL-001 ("Delivery is created after successful checkout") is superseded by D3. The test that encoded
it (`CreateDeliveryForOrderHandlerTests.Handle_CreatesPendingAssignmentDelivery`) was retired in the same red commit that
added its replacement, test 16, as the design's §4.1 requires.

---

## 2. Layer-by-layer flow

```
POST /api/orders/{orderId}/accept            (Talabat.Admin.API)
  │  [Authorize(AdminAccess)]: scope admin.api + Admin role + persisted UserType re-read from the DB
  ▼
OrdersController.Accept
  │  builds AcceptOrderCommand(orderId, FulfillmentActor.Admin)
  ▼
AcceptOrderHandler.Handle                    (Talabat.Application)
  │  1. load OrderFulfillment (tracked)   ─┐
  │  2. load Order                         ├─ via Domain repository interfaces
  │  3. load Restaurant                   ─┘
  │  4. resolve the acting restaurant (Admin → the order's own; Restaurant → its id)
  │  5. fulfillment.Accept(actingRestaurantId, now)        ← Domain rule (status + ownership + timestamps)
  │  6. DeliveryFactory.ForOrder(order, restaurant, now)    ← pickup snapshot copied now
  │  7. ExecuteInTransactionAsync { add delivery; SaveChanges }   ← one commit
  ▼
UnitOfWork / TalabatDbContext                (Talabat.Infrastructure)
  │  SaveChanges: UPDATE OrderFulfillments ... WHERE Id=@id AND RowVersion=@old
  │               INSERT Deliveries ...           (UX_Deliveries_OrderId guards duplicates)
  │  DbUpdateConcurrencyException / unique violation → ConcurrencyConflictException
  ▼
Result back up:
  success → 200 AcceptOrderResponse
  expected failure (UseCaseResult) → ToActionResult → 404 / 403 / 409 ProblemDetails
  escaping exception (ConcurrencyConflictException) → DomainExceptionHandler → 409 ProblemDetails
```

Then, on the **read side**:

- **Delivery API:** `GET /api/agent/deliveries/pending` lists `PendingAssignment` deliveries. The accepted order is there.
  Placed-but-unaccepted orders cannot be there because they have no delivery.
- **Customer API:** order list and details call `CustomerOrderStatusMapper.Map(fulfillment, delivery)`. The result is
  `(Accepted, PendingAssignment)` → status `Accepted`, `deliveryAgentAssigned = false`.

### Why the layers split this way

| Layer | Owns | Must not know |
|---|---|---|
| **Domain** | The *rules*: which transitions are legal, who may perform them, timestamp ordering | Databases, HTTP, transactions |
| **Application** | The *workflow*: load these aggregates, call the rule, build the delivery, commit atomically | EF Core, ASP.NET, status codes |
| **Infrastructure** | *How* things persist: SQL, RowVersion checks, translating DB errors into domain exceptions | Business rules |
| **API** | *Who* may call (authorization), HTTP shape, mapping errors to status codes | Business rules, persistence |

`tests/Talabat.ArchitectureTests` enforces the dependency direction (hosts → Application → Domain).

---

## 3. Class-by-class breakdown

### Domain: no production changes

`OrderFulfillment.Accept(restaurantId, currentTime)` already existed from T1. Test 2 was pulled forward there because
tests 9, 10 and 28 needed a transition to exercise. That is a deliberate sign of a healthy model: the *rule* was
already in place, and T2 only had to *orchestrate* it.

> **Why the rule is in the aggregate, not the handler.** Every future caller of the rule must get the same answer.
> That includes the Owner API (M3) and the provisioner's `advance-order` (T8). If the handler checked
> `Status == AwaitingRestaurant` itself, each caller would have to remember to do the same. The aggregate guards its own
> invariants, which is DDD's core idea of an *aggregate as a consistency boundary*.

### Application

#### `Fulfillment/FulfillmentActor.cs` (new)

A closed set of "who is doing this", written as an abstract record with two nested sealed records:
`FulfillmentActor.Admin` and `FulfillmentActor.Restaurant(int RestaurantId)`. The private constructor stops anyone from
adding a third subtype outside the file.

*Why:* D11 says "the Application use cases take the actor explicitly, so the Owner API (M3) wraps the **same**
handlers". If the handler took a plain `int? restaurantId`, then `null` would mean "Admin". That is *Primitive
Obsession*: a magic value standing in for a domain concept. A named type makes each call site say what it means, and
pattern matching (`switch`) handles each case explicitly. T5/T6 will add the actor to cancellations, where
`CancelledBy = Admin` must be recorded (D7). This type is where that grows.

#### `Fulfillment/AcceptOrder/AcceptOrderCommand.cs` and `AcceptedOrder.cs` (new)

The use case's input (`OrderId`, `Actor`) and output (`OrderId`, `DeliveryId`, `AcceptedAt`) are plain records.

*Why:* Application handlers take a command and return `UseCaseResult<T>` with a small read model. They never return
the aggregate itself, so the HTTP layer cannot reach into domain objects. The API turns `AcceptedOrder` into its own
`AcceptOrderResponse`, and the two can change independently.

#### `Fulfillment/AcceptOrder/AcceptOrderHandler.cs` (new): the heart of T2

It runs the steps in section 2. The decisions worth studying:

1. **Missing fulfillment and missing order collapse into one `404 OrderNotFound`.** The caller asked about an order.
   Which internal row was missing is not the caller's concern.
2. **The ownership rule stays in the domain.** The handler only decides *which* restaurant is acting
   (`Admin → fulfillment.RestaurantId`). The comparison happens inside `fulfillment.Accept`. For an Admin the check
   always passes, which is the intended meaning of "acting on behalf of the order's own restaurant".
3. **Only expected domain failures are caught.** The `try/catch` wraps only `fulfillment.Accept`, and it catches only
   `DomainException` and `ArgumentException`. These are *business outcomes* (wrong state, wrong restaurant) that become
   a `UseCaseResult` failure. A failure during `SaveChanges` is an *infrastructure outcome* and is deliberately **not**
   caught. It propagates to the host's exception handler (see "Tricky concepts").
4. **The transaction is the D12 boundary.** `ExecuteInTransactionAsync` wraps the delivery insert and the save, so the
   fulfillment's status change and the new delivery commit together or not at all.
5. **There is no `_fulfillmentRepository.Update(...)` call.** `GetByOrderIdAsync` returns a *tracked* entity, so EF's
   change tracker sees the mutation that `Accept` made and writes it on `SaveChanges`. `GetByOrderIdsAsync`, used by
   list screens, is `AsNoTracking`, because reads that never write should not pay for tracking.

#### `Deliveries/DeliveryFactory.cs` (reused, unchanged)

It copies `restaurant.Name` and `restaurant.PickupAddress` into a new `DeliveryAddressSnapshot` value object.

*Why a copy:* a `Delivery` in flight must not change when a restaurant later edits its address. Value objects are
immutable and compared by value, so the delivery owns its own snapshot. The new test
`Accept_takes_the_pickup_snapshot_at_acceptance_time` pins both halves of D3:
- an edit **before** acceptance *is* captured;
- an edit **after** acceptance is *not*.

#### `Ordering/Status/CustomerOrderStatusMapper.cs` (modified)

T2 adds one arm:
`(Accepted, PendingAssignment | Assigned | ArrivedAtRestaurant) → Accepted`. Here `deliveryAgentAssigned` is
`delivery.AssignedAgentId is not null`.

*Why one mapper:* the customer's status is **derived, never stored** (D9). Storing it would create a third copy of the
truth, which could drift from the two real lifecycles. Every combination the design does not list throws
`InvalidOperationException`, including `Accepted` with no delivery and `Accepted` with a `PickedUp` delivery. An
impossible state is a *bug signal*, not something to paper over with a default.

#### `Common/Results/DomainExceptionMapper.cs` (modified, fix A)

New arm: `OrderFulfillmentRestaurantMismatchException → OwnershipMismatch` (HTTP 403).

*Why 403 and not 409 or 404:*
- **409 Conflict** means "the resource is in a state that forbids this", for example accepting twice. A wrong
  restaurant is not about state.
- **403 Forbidden** means "you may not act on this resource". It mirrors `DeliveryAgentMismatchException`, so the
  whole platform answers ownership violations the same way.
- **404** would hide the order's existence. The project uses that for *reads* of foreign resources (Phase 6 ownership
  tests). For a state-changing command with an explicit actor, the domain's mismatch signal is the honest answer.
  When the Owner API arrives in M3, its *ownership-scoped read* will already 404 foreign orders before the handler runs.

#### `DependencyInjection.cs` (modified, fix D)

`AcceptOrderHandler` is registered under a new `// Fulfillment` section. Handlers are plain classes registered as
scoped (no MediatR), so each HTTP request gets one handler, one `DbContext`, and one unit of work. That is what makes
"one transaction per request" natural.

### Infrastructure: no production changes

T2 relies on what T1 and earlier phases built:

- **`OrderFulfillmentConfiguration`:** `RowVersion` is `.IsRowVersion()` (SQL Server `rowversion`). EF adds
  `WHERE RowVersion = @original` to every UPDATE.
- **`DeliveryConfiguration`:** the unique index `UX_Deliveries_OrderId` means at most one delivery per order.
- **`UnitOfWork`:**
  - `SaveChangesAsync` turns `DbUpdateConcurrencyException`, and unique violations (SQL errors 2601/2627), into the
    domain's `ConcurrencyConflictException`.
  - `ExecuteInTransactionAsync` opens a transaction, or joins an ambient one, and commits only if the operation
    finishes.

*Why translate DB errors in Infrastructure:* Application and API must not reference `Microsoft.Data.SqlClient` or EF
types; the architecture tests enforce this. Infrastructure converts a *technical* failure into a *domain-meaningful*
one ("someone else changed this first").

### Admin API

#### `Controllers/OrdersController.cs` (new)

`POST /api/orders/{orderId}/accept`, under `[Authorize(Policy = AuthorizationPolicies.AdminAccess)]`.

- The route matches the approved convention: resource-oriented, with the action as a sub-resource.
- It is `POST` because the call changes state and is not idempotent: a second call returns 409.
- The controller is deliberately thin. It builds the command, calls the handler, and maps the result. No business
  logic.

*Why the controller passes `FulfillmentActor.Admin` and never a restaurant id:* the caller's identity comes from the
token (`ICurrentUser`/policy) and is never taken from the request. The Admin API has exactly one possible actor, so the
request cannot claim to be a restaurant.

#### `Contracts/Orders/AcceptOrderResponse.cs` (new)

The response is `{ orderId, fulfillmentStatus, deliveryId, acceptedAt }`. The status is sent as a string so the SPA
does not depend on enum numbers.

#### `Middleware/DomainExceptionHandler.cs` (new, fix C) and `Extensions/UseCaseResultExtensions.cs` (refactored)

An `IExceptionHandler` registered in `Program.cs`. When a `DomainException` or `ArgumentException` escapes a handler,
it maps through the *same* `DomainExceptionMapper` and the same `ToProblemDetails` as handler results. So a status code
never depends on *how* a failure surfaced.

*Why it was needed:* before fix C, the Admin host only had `UseExceptionHandler(_ => { })`, which turns every escaping
exception into **500**. Until T2 the Admin API only wrote user rows, so it never mattered. Accept is the first Admin
command that can lose a RowVersion race.

The refactor moved the ProblemDetails construction into `ApplicationError.ToProblemDetails()`. Both paths share it,
which avoids *Duplicated Code*, and `ToActionResult`'s output is byte-for-byte unchanged.

#### `openapi/admin-api.json` (regenerated, separate commit)

The committed contract is parity-tested against the runtime document. Contract changes get their own commit, so a
reviewer sees "the API surface changed" on its own. The Delivery contract did not change in T2:
`fulfillmentStatus` for agents arrives in T3.

### Tests

| File | Seam | What it proves |
|---|---|---|
| `Application.Tests/Fulfillment/AcceptOrder/AcceptOrderHandlerTests.cs` | Use case with fakes | 16 (atomic create + snapshot), 17 (accept twice → 409), 404, wrong restaurant → 403, own restaurant OK, snapshot timing |
| `Application.Tests/TestDoubles/FakeUnitOfWork.cs` | Test double | Counts saves made *inside* a transaction, so "one transaction" is observable without a database |
| `Application.Tests/Deliveries/CreateForOrder/...Tests.cs` | Retirement | `Handle_CreatesPendingAssignmentDelivery` removed (BR-DEL-001 revised by D3) |
| `Application.Tests/Ordering/Status/CustomerOrderStatusMapperTests.cs` | Pure function | 25 (part): reachable `Accepted` rows plus two impossible ones |
| `Infrastructure.Tests/Persistence/AcceptOrderPersistenceTests.cs` | Real SQL Server | 30: a failed accept leaves the fulfillment `AwaitingRestaurant` and creates no delivery |
| `Admin.API.Tests/OrderAcceptanceTests.cs` | HTTP | 34 (part): 401/403/200/409/404, plus a write conflict → 409 `ConcurrencyConflict` |
| `Delivery.API.Tests/PendingQueueTests.cs` | HTTP | 33 (part): a placed order is not in the queue, then is after acceptance |

---

## 4. Tricky concepts and decisions

### 4.1 Two different "409"s, and why both exist

| Situation | What happens | Error code |
|---|---|---|
| Admin accepts, then accepts **again later** | The second request loads `Accepted` and `Accept` throws `InvalidOrderFulfillmentStatusTransitionException` *before any write* | `InvalidOrderFulfillmentStatusTransitionException` |
| Two Admins accept **at the same moment** | Both load `AwaitingRestaurant` with the same RowVersion and both pass the domain check. The first commit changes the RowVersion; the second UPDATE matches 0 rows → `DbUpdateConcurrencyException` → `ConcurrencyConflictException` | `ConcurrencyConflict` |

The domain check alone cannot stop the race, because each request sees its own consistent snapshot. **Optimistic
concurrency** (RowVersion) is what serializes the writers. The loser is told to re-read (D7: "the loser receives
`409 ConcurrencyConflict` and re-reads"). The server does not retry: a retry would silently re-run a business
decision that is no longer valid.

The unique index `UX_Deliveries_OrderId` is a **second line of defence**. Even if a bug bypassed the RowVersion check,
the database refuses a second delivery for the same order.

### 4.2 Rollback mechanics: what test 30 proves

`Failed_accept_rolls_back_fulfillment_and_delivery` seeds a stray delivery that already holds the order. The
acceptance's INSERT then violates `UX_Deliveries_OrderId` while the fulfillment UPDATE is part of the same save.
Afterwards, a fresh `DbContext` sees the fulfillment still `AwaitingRestaurant` with `AcceptedAt = null`, and only the
stray delivery.

Two subtleties:

- **One `SaveChanges` is already atomic.** EF wraps a single `SaveChanges` in its own transaction. So why
  `ExecuteInTransactionAsync`? Because D12 makes the boundary *explicit and future-proof*. Checkout already needs two
  saves (the fulfillment needs the generated order id). If acceptance ever needs a second save (for example an audit
  row in M4), it stays atomic without anyone remembering to add a transaction.
- **The test passed on first run.** That is honest, not a failure of TDD. The green handler already produced the
  behaviour. To prove the test is *not vacuous*, it was mutation-checked: temporarily saving the fulfillment before
  the transaction made it fail. A test you have never seen fail is a hypothesis, not evidence.

### 4.3 The in-memory entity after a failed save

When the save fails, the tracked `OrderFulfillment` object in memory is already `Accepted`, but the database says
`AwaitingRestaurant`. That is safe here because the `DbContext` is scoped to the request: the exception ends the
request, and the dirty context is thrown away. **Never reuse a `DbContext` after a failed `SaveChanges`.** Its change
tracker no longer matches the database.

### 4.4 Results vs exceptions: two failure channels, one mapping

- **Expected business outcomes** (not found, wrong state, wrong restaurant) are returned as
  `UseCaseResult.Failure(ApplicationError)`. They are part of the use case's contract and the controller maps them
  explicitly.
- **Unexpected or infrastructure outcomes** (a lost race) are thrown and caught once, at the edge, by
  `DomainExceptionHandler`.

Both channels go through `DomainExceptionMapper` → `ApplicationError` → `ToProblemDetails`. The client sees one
consistent shape, `ProblemDetails` with a root `errorCode`, whichever channel produced the error.

### 4.5 Idempotency: accept is deliberately *not* idempotent

A retried accept (for example after a network timeout) returns 409, not 200. The design chose the *state machine* as
the truth: "accepted twice" is a conflict. A client that gets a 409 after a timeout should re-read the order; it will
see `Accepted`. Making accept idempotent (200 if already accepted) would hide real conflicts, such as accepting an
order that a customer cancelled in between (T5). If idempotency is ever needed, the proper tool is an
`Idempotency-Key` header (Phase 10), not loosening the transition.

### 4.6 Authorization is three checks, not one

`AdminAccess` requires:
1. the `admin.api` **scope**: the token was issued for this API;
2. the **Admin role** claim;
3. a **persisted capability** check: `PersistedAdminAccessHandler` re-reads the user from the database.

The third check means a demoted Admin loses access immediately, even with a still-valid token. The tests cover a
missing token (401), a non-admin (403) and an Admin token without the scope (403).

### 4.7 Why "agents see only accepted orders" needed no query change

It is tempting to add `JOIN OrderFulfillments WHERE Status >= Accepted` to the pending-queue query. Instead, the
invariant is **structural**: only acceptance creates deliveries, so an unaccepted order has nothing to list. The
`PendingQueueTests` guard pins this at the API. A filter would duplicate the rule in a second place, where it could
drift.

### 4.8 A Windows-only trap: OpenAPI parity and line endings

The committed contracts are stored with LF line endings. With `core.autocrlf=true`, a branch switch can rewrite them
on disk as CRLF. The parity tests compare raw JSON text, so they fail locally while CI (Linux) passes. The fix is to
restore the files with `git -c core.autocrlf=false checkout -- <files>`. The durable fix, a `.gitattributes` rule
pinning `*/openapi/*.json` to `eol=lf`, is a separate chore.

---

## 5. Commit trail

| Commit | Kind |
|---|---|
| `test(fulfillment): accepting an order creates its delivery (red)` | Tests 16, 17; retires the BR-DEL-001 test |
| `feat(fulfillment): accepting an order creates its delivery (green)` | `AcceptOrderHandler` |
| `test/feat(fulfillment): customers see an accepted order as Accepted` | Mapper, test 25 (part) |
| `test(fulfillment): a failed accept rolls back fulfillment and delivery` | Test 30 (mutation-checked) |
| `test/feat(admin): an Admin accepts an order on the restaurant's behalf` | Endpoint, test 34 (part) |
| `chore(openapi): regenerate the Admin contract with order accept` | Contract only |
| `test(delivery): the pending queue holds only accepted orders` | Test 33 (part) |
| `test/fix(fulfillment): another restaurant's accept is an ownership mismatch` | Review fix A (403) |
| `test(fulfillment): the pickup snapshot is taken at acceptance time` | Review fix B (D3 timing) |
| `test/fix(admin): a write conflict on accept returns 409, not 500` | Review fix C (D12) |
| `refactor(fulfillment): register AcceptOrderHandler under its own section` | Review fix D |

## 6. Questions to test your understanding

1. Why does a second, *sequential* accept fail with a different error code than a *concurrent* one?
2. What would break if `GetByOrderIdAsync` used `AsNoTracking()`?
3. Why is the ownership comparison in `OrderFulfillment.Accept`, when the handler already knows the actor?
4. Test 30 passed the first time it ran. What did we do to trust it anyway?
5. If the Admin API had no `DomainExceptionHandler`, which T2 scenario returns 500, and why don't the handler's
   `try/catch` blocks cover it?
