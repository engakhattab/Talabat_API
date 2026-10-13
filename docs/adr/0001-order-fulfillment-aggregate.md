---
status: proposed
---

# Restaurant-side order lifecycle lives in a separate `OrderFulfillment` aggregate

`Order` stays the immutable purchase record (BR-ORD-006), and the kitchen lifecycle (accept/reject, preparing, ready,
hand-over) gets its own `OrderFulfillment` aggregate, one per order, in a Restaurant Operations context — mirroring the
existing `Order`/`Delivery` split. We rejected putting a `Status` on `Order` because it would contradict the documented
immutability, make a single row contended by restaurant and courier writes, and mix purchase and kitchen invariants.

## Consequences

Customer-facing order status is never stored: it is derived from fulfillment + delivery by a single mapper
(`specs/011-order-lifecycle/design.md` D9), and every cross-aggregate rule (pickup gate, cancellation) is coordinated by
Application use cases inside one transaction.
