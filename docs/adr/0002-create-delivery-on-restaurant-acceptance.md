---
status: proposed
---

# A `Delivery` is created when the restaurant accepts, not at checkout

Checkout creates `Order` + `OrderFulfillment` only; accepting the order atomically creates the `Delivery`. This keeps
unaccepted orders out of the courier queue and makes "restaurant rejects an order a courier already holds" impossible,
at the cost of revising BR-DEL-001/016, which since 2026-10-13 created the delivery inside the checkout transaction.
We rejected keeping creation at checkout with a "not yet claimable" flag because it needs reject→cancel-delivery
coordination and a second, easily-forgotten gate on every courier query.
