# T5 technical walkthrough: restaurant reject and customer cancel with reason codes

> Ticket: Phase 2 · T5 ([engakhattab/Talabat_API#14](https://github.com/engakhattab/Talabat_API/issues/14)) ·
> Design: [`specs/011-order-lifecycle/design.md`](../../specs/011-order-lifecycle/design.md) D2, D7, D9, D11, D12, D13
> (Q2, Q6, Q17, Q18) · Branch: `feat/t5-reject-and-customer-cancel` → `feature/phase-2-order-lifecycle`

This document explains what T5 built, where each piece lives, and why it lives there. It assumes you have read
[`CONTEXT.md`](../../CONTEXT.md) and the [T4 walkthrough](T4-technical-walkthrough.md).

---

## 1. Domain and business intent

### The story

Until T5, an order could only move forward: Placed → Accepted → … → Delivered. T5 adds the two ways an order can
**end before the restaurant takes it**:

> **Rejection**: the restaurant declines an order it has not accepted, and must say why with a reason code. Until the
> Restaurant Owner app exists, an Admin rejects on the restaurant's behalf.
>
> **Customer cancellation**: the customer cancels their own order while it is still `Placed`. Giving a reason is
> optional. Once the restaurant has accepted, cancelling returns `409 OrderNoLongerCancellable`.

Customers then see `Rejected` or `Cancelled`, who cancelled, and the reason code. They never see the free-text note,
which is meant for support staff.

### Ubiquitous language used

| Term | Meaning in T5 |
|---|---|
| **Rejection** | The restaurant declining an order before accepting it. Fulfillment `AwaitingRestaurant` → `Rejected`. |
| **Cancellation** | Ending an order that was not rejected and not handed over. T5 builds only the customer's row of the matrix. |
| **Reason Code** | The fixed code that explains a rejection or cancellation. Each action has its own list. |
| **Customer Order Status** | Now also `Rejected` and `Cancelled`, with `cancelledBy` and `reasonCode` on order details. |

### Business rules fulfilled

| Rule | Source | Where it is enforced |
|---|---|---|
| Reject only from `AwaitingRestaurant` | D2, D7, tests 3 and 17 | `OrderFulfillment.Reject` (aggregate invariant) |
| Reject only by the order's own restaurant | D11, test 9 | `EnsureRestaurant` inside `Reject`; the Admin acts as the order's restaurant |
| Customer cancel only while `AwaitingRestaurant`, else `OrderNoLongerCancellable` | D7, Q2, test 21 | `OrderFulfillment.CancelByCustomer` |
| A code is valid only for its own action | D13, test 11 | `FulfillmentReasons.Validate` with the action's list |
| `Other` needs a note; a note is at most 500 characters | D13, test 11 | `FulfillmentReasons.Validate`, plus `nvarchar(500)` in SQL |
| A customer reason is optional | D13 | `CancelByCustomer(FulfillmentReasonCode? …)`; the endpoint accepts an empty body |
| Customer sees `Rejected` / `Cancelled`, `cancelledBy`, `reasonCode`, never the note | D9, test 25 and 32 | `CustomerOrderStatusMapper`, `OrderDetailResponse` |
| Another customer's order is not found | `me` route convention | `CancelOrderHandler` reads through `GetByIdForCustomerAsync` |

Business-rule IDs `BR-FUL-002` (accept/reject window), `BR-FUL-006` (cancellation matrix) and `BR-FUL-009` (reason
codes per action) are written into the business-rule docs in T9, as the roadmap assigns it.

**Not in T5** (on purpose): restaurant and Admin cancellation (T6), the system cancellation after the acceptance
deadline (Phase 11), and delivery-failure reason codes (T7). No existing tests are retired.

---

## 2. Layer-by-layer flow

### Write side: an Admin rejects (Admin API)

```
POST /api/orders/{orderId}/reject  { "reasonCode": "ItemsUnavailable", "note": "Out of rice" }
  → OrdersController.Reject                         [AdminAccess policy: admin.api scope + Admin role]
  → RejectOrderHandler.Handle(RejectOrderCommand(orderId, FulfillmentActor.Admin, code, note))
      → IOrderFulfillmentRepository.GetByOrderIdAsync      (null → 404 OrderNotFound)
      → fulfillment.Reject(actor.ActingRestaurantId(fulfillment), code, note, clock.UtcNow)
            EnsureRestaurant → EnsureStatus(AwaitingRestaurant) → EnsureTimeNotBefore → FulfillmentReasons.Validate
      → IUnitOfWork.SaveChangesAsync                       (one aggregate, so no explicit transaction)
  ← 200 RejectOrderResponse { orderId, fulfillmentStatus: "Rejected", reasonCode, rejectedAt }
```

### Write side: the customer cancels (Customer API)

```
POST /api/me/orders/{orderId}/cancel   (body optional: { "reasonCode": "ChangedMind", "note": null })
  → OrderController.CancelOrder                     [CustomerAccess + RequireCustomerProfile]
  → CancelOrderHandler.Handle(CancelOrderCommand(currentUser.CustomerId, orderId, code?, note?))
      → IOrderRepository.GetByIdForCustomerAsync           (someone else's order → null → 404)
      → IOrderFulfillmentRepository.GetByOrderIdAsync
      → fulfillment.CancelByCustomer(code?, note?, clock.UtcNow)
      → IUnitOfWork.SaveChangesAsync
  ← 200 CancelOrderResponse { orderId, status: "Cancelled", reasonCode, cancelledAt }
```

### Read side

`GET /api/me/orders/{id}` was already wired through `CustomerOrderStatusMapper.Map(fulfillment, delivery)` (T1). T5
adds two rows and two fields:

| Fulfillment | Delivery | Status | `cancelledBy` | `reasonCode` |
|---|---|---|---|---|
| `Rejected` | none | `Rejected` | null | the reject code |
| `Cancelled` | none | `Cancelled` | `Customer` | the customer's code, or null |

`CustomerOrderProgress` carries the two new values. `OrderMapper.ToDetails` copies them into `OrderDetails`, and the
controller puts them on `OrderDetailResponse`. The order list (`OrderSummaryDto`) shows only the status, as D9 says.

---

## 3. File-by-file breakdown

### Domain (`Talabat.Domain`)

| File | Change | Why it lives here |
|---|---|---|
| `Aggregates/RestaurantOperations/OrderFulfillment.cs` | New `RejectedAt`, `CancelledAt`, `CancelledBy`, `ReasonCode`, `ReasonNote`; new `Reject(...)` and `CancelByCustomer(...)` | Which states allow which ending is an **aggregate invariant**: no caller should be able to reject an accepted order, whatever path it takes. |
| `Aggregates/RestaurantOperations/FulfillmentReasonCode.cs` (new) | Every D13 fulfillment code, stored as an int, serialized as a string | One enum for all fulfillment endings, so the database has one `ReasonCode` column with one check constraint. Delivery-failure codes are a separate concern (T7). |
| `Aggregates/RestaurantOperations/CancellationActor.cs` (new) | `Customer`, `Restaurant`, `Admin`, `System` | The value behind design D2's `CancelledBy`. All four values exist now so the check constraint does not change in T6; only `Customer` is used. |
| `Aggregates/RestaurantOperations/FulfillmentReasons.cs` (new, `internal`) | The reject and customer-cancel lists, and the note rules | Reason validation is domain knowledge shared by every ending. It is `internal` because only the aggregate should call it; tests go through `Reject` and `CancelByCustomer`, so the shape of this class can change freely. |
| `Exceptions/ReasonCodeNotAllowedException.cs`, `ReasonNoteRequiredException.cs`, `ReasonNoteTooLongException.cs` (new) | One exception per rule | The class name becomes the API `errorCode`, so a client can tell "wrong code" from "note missing" without parsing text. |
| `Exceptions/OrderNoLongerCancellableException.cs` (new) | Thrown by `CancelByCustomer` outside `AwaitingRestaurant` | D7 names this exact error. A generic invalid-transition error would not tell the customer app what to show. |

### Infrastructure (`Talabat.Infrastructure`)

| File | Change | Why |
|---|---|---|
| `Persistence/Configurations/OrderFulfillmentConfiguration.cs` | Maps the five new columns; enums as `int`; note `HasMaxLength(500)`; check constraints `CK_OrderFulfillments_CancelledBy` and `CK_OrderFulfillments_ReasonCode` | The database refuses values the domain could never produce, the same way `CK_OrderFulfillments_Status` already does (D12). |
| `Persistence/Migrations/20261014015113_AddOrderFulfillment.cs`, `.Designer.cs`, `TalabatDbContextModelSnapshot.cs` | The new columns and constraints are folded into the existing migration | Same reasoning T3 used: Phase 2 has not reached `main`, and the local `talabat` database is dropped and recreated during Phase 2, so the phase still ships one migration. Test 29 pins it as the latest one. |

### Application (`Talabat.Application`)

| File | Change | Why |
|---|---|---|
| `Fulfillment/RejectOrder/RejectOrderCommand.cs`, `RejectedOrder.cs`, `RejectOrderHandler.cs` (new) | Load, `Reject`, save | A use case orchestrates; it does not decide rules. It takes a `FulfillmentActor`, so the Owner API (M3) will wrap the same handler with `FulfillmentActor.Restaurant(id)` (D11). |
| `Ordering/CancelOrder/CancelOrderCommand.cs`, `CancelledOrder.cs`, `CancelOrderHandler.cs` (new) | Load **through the customer's scope**, `CancelByCustomer`, save | It sits under `Ordering`, next to order history and details, because it is a customer use case. Reading with `GetByIdForCustomerAsync` makes ownership a query condition rather than an `if` that could be forgotten. |
| `Common/Results/ApplicationErrorCodes.cs` | `OrderNoLongerCancellable` | The error code D7 names. |
| `Common/Results/DomainExceptionMapper.cs` | `OrderNoLongerCancellableException` → Conflict (409) with that code; the three reason exceptions → Validation (400) | A bad reason is the caller's input mistake (400). A cancel that is too late is a state conflict (409). |
| `Ordering/Status/CustomerOrderStatusMapper.cs` | `Rejected`/`Cancelled` with no delivery | The mapper is the only place that turns the two lifecycles into what customers see (D9). Either status *with* a delivery still throws: it would be a broken invariant. |
| `Ordering/Status/CustomerOrderProgress.cs` | Optional `CancelledBy` and `ReasonCode` | Keeps "what the customer sees" in one value. Optional parameters leave the existing rows unchanged. |
| `Ordering/Models/OrderDetails.cs`, `Ordering/Mapping/OrderMapper.cs` | Carry the two values to details only | D9 puts them on details, not on the list. |
| `DependencyInjection.cs` | Registers both handlers | Plain handler classes, no MediatR. |

### API

| File | Change | Why |
|---|---|---|
| `Talabat.Admin.API/Controllers/OrdersController.cs` | `POST /api/orders/{orderId}/reject` | Sits beside accept, start-preparing and mark-ready: the Admin's act-on-behalf endpoints (D11). |
| `Talabat.Admin.API/Contracts/Orders/RejectOrderRequest.cs`, `RejectOrderResponse.cs` (new) | `{ reasonCode, note? }` → `{ orderId, fulfillmentStatus, reasonCode, rejectedAt }` | Same response shape as accept. The code is the enum, so OpenAPI lists every value for the future reason picker. |
| `Talabat.API/Controllers/OrderController.cs` | `POST /api/me/orders/{orderId}/cancel` with `EmptyBodyBehavior.Allow`; details pass `cancelledBy`/`reasonCode` | The body is optional because a customer reason is optional. The customer id comes from the token, never the route. |
| `Talabat.API/Contracts/Orders/OrderContracts.cs` | `CancelOrderRequest`, `CancelOrderResponse`; two nullable fields on `OrderDetailResponse` | The note is deliberately absent from every customer contract. |
| `*/openapi/admin-api.json`, `*/openapi/customer-api.json` | Regenerated | The committed contracts are parity-tested against the running app. Each was committed on its own. |

### Tests

| File | What it proves |
|---|---|
| `Talabat.Domain.Tests/RestaurantOperations/OrderFulfillmentTests.cs` | Tests 3 and 11, plus the customer row of D7 at the aggregate. |
| `Talabat.Infrastructure.Tests/Persistence/OrderFulfillmentPersistenceTests.cs` | Test 27 extended: the new values survive SQL Server, the check constraints refuse unknown actors and codes, and the note column refuses 501 characters. |
| `Talabat.Application.Tests/Fulfillment/RejectOrder/RejectOrderHandlerTests.cs` (new) | Test 17 (`Reject_after_accept_conflicts`), plus validation, not found, another restaurant, and that nothing is saved on refusal. |
| `Talabat.Application.Tests/Ordering/CancelOrder/CancelOrderHandlerTests.cs` (new) | Test 21 (`Customer_cancel_only_while_awaiting_restaurant`), another customer's order, and a wrong reason. |
| `Talabat.Application.Tests/Ordering/Status/CustomerOrderStatusMapperTests.cs` | Test 25 rows for `Rejected` and `Cancelled`, valid and impossible. |
| `Talabat.Admin.API.Tests/OrderRejectionTests.cs` (new) | Test 34 (reject): 401/403/200/409/400/404. |
| `Talabat.Customer.API.Tests/OrderCancellationTests.cs` (new) | Test 32 (cancel and the details fields): 200 with and without a body, 409, 404, 400, 401, and that the note never leaks. |

---

## 4. Tricky concepts and decisions

**Why `CancelByCustomer` instead of `Cancel(actor, …)`.** Every actor needs something different. The customer's
reason is optional; the restaurant must pass its id so the ownership guard runs; an Admin and the system each have
their own code list. One method with an `actor` switch would grow branches nobody tests yet. T6 adds
`CancelByRestaurant` and `CancelByAdmin`, each with its own test, and the full matrix theory (test 7) runs over all of
them.

**The order of checks.** `Reject` checks restaurant, then state, then time, then the reason. So a reject on an accepted
order with a bad code returns 409, not 400. The state answer is more useful: fixing the code would not help anyway.

**A missing reject code binds to 0.** `RejectOrderRequest.ReasonCode` is a non-nullable enum. When the client leaves it
out, JSON binding gives `0`, which is in no list, so the domain throws `ReasonCodeNotAllowedException` (400). That
means one validation path instead of two (model validation plus domain).

**`OrderNoLongerCancellable` covers every later state.** Accepted, Preparing, ReadyForPickup, HandedOver, and also
`Rejected` and an already `Cancelled` order. From the customer's point of view the answer is the same: it can no
longer be cancelled.

**A rejected order has no `cancelledBy`.** A rejection is not a cancellation (see `CONTEXT.md`), so `cancelledBy` stays
null and the reason code alone explains it.

**Concurrency.** A customer cancel racing an Admin accept writes the same `OrderFulfillments` row. RowVersion makes the
second save fail with `ConcurrencyConflictException`, which the global handler turns into `409 ConcurrencyConflict`
(D7, Q8). That path is already covered by test 28 and the T2 API test, so T5 adds no new race test.

**No transaction.** Reject and customer cancel change one aggregate, so one `SaveChangesAsync` is atomic by itself.
Restaurant and Admin cancel after acceptance (T6) also cancel the delivery and free the agent, so they will need
`ExecuteInTransactionAsync`.

**Trade-off not taken: store codes as strings.** Strings read better in SQL, but `Status` is already an `int` with a
check constraint, and consistency wins. The API still shows names, through `JsonStringEnumConverter`.

---

## 5. Commit trail

| Commit | What it proves |
|---|---|
| `4471d8f` test(domain) … (red) | The domain rules, written before the code (did not compile). |
| `7f86d6d` feat(domain) … (green) | `Reject`, `CancelByCustomer`, the reason lists and the note rules. |
| `50dc68e` test(fulfillment) … round-trip on SQL Server (red) | Failed with `PendingModelChangesWarning`: no columns yet. |
| `98b2626` feat(fulfillment): persist … (green) | Columns, constraints, migration folded in; test 29 still green. |
| `9f4205b` test(fulfillment): … use cases … (red) | Handler and mapper tests (did not compile). |
| `2086155` feat(fulfillment): … use cases (green) | Handlers, error mapping, mapper rows. |
| `c92edff` test(api) … (red) | Endpoints returned 404: routes did not exist. |
| `62ea491` feat(api) … (green) | Both endpoints and the details fields; only the parity tests remained red. |
| `4607a75`, `90543a8` chore(openapi) | Admin and Customer contracts regenerated; parity green. |

---

## 6. Self-check questions

1. Why does the reason-code rule live in the domain and not in the request validation? What would break if only the
   Admin endpoint checked it?
2. A customer calls cancel on someone else's order. Which line makes it a 404 instead of a 403, and why is 404 the
   right answer for a `me` route?
3. Why is the free-text note stored at all if no customer ever sees it?
4. T6 will let the restaurant cancel after acceptance. Which files from this ticket will change, and why will that
   handler need a transaction when these two did not?
5. What makes `(Cancelled, Assigned delivery)` an impossible combination for the mapper, and which design rule
   guarantees it?
