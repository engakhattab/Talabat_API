# Talabat

A food-delivery platform where customers order from restaurants and delivery agents bring the food. This glossary fixes
the language shared by the backend, the frontends and the design documents.

## Roles

**Customer**:
A person who browses restaurants, places orders and tracks them.
_Avoid_: Client, buyer, account

**Delivery Agent**:
An approved person who claims deliveries and carries orders from the restaurant to the customer.
_Avoid_: Courier, driver, rider

**Restaurant Owner**:
The person who operates a restaurant's incoming orders and catalog; the only restaurant-side role (no staff accounts).
_Avoid_: Merchant, vendor, restaurant staff

**Admin**:
Platform operations staff who approve delivery agents, onboard restaurants, and may act on behalf of a restaurant or a
delivery agent.
_Avoid_: Operator, support, superuser

## Ordering

**Order**:
The immutable record of a purchase: items, prices, total and delivery address at the moment of checkout.
_Avoid_: Purchase, transaction, basket

**Cart**:
The customer's in-progress selection from a single restaurant before checkout.
_Avoid_: Basket, bag

**Checkout**:
Turning the customer's cart into an order and placing it with the restaurant.
_Avoid_: Purchase, submit

## Restaurant Operations

**Order Fulfillment**:
The restaurant's handling of one order, from awaiting the restaurant's decision through acceptance, preparation and
readiness to hand-over, rejection or cancellation.
_Avoid_: Kitchen order, order status, restaurant order

**Acceptance**:
The restaurant agreeing to prepare an order; it is what makes the order available to delivery agents.
_Avoid_: Confirmation

**Rejection**:
The restaurant declining an order before accepting it.
_Avoid_: Decline, refusal

**Cancellation**:
Ending an order that was not rejected and has not been handed over, by the customer, the restaurant, an Admin or the
system.
_Avoid_: Void, abort

**Acceptance Deadline**:
The moment after which an order still awaiting the restaurant is cancelled by the system.
_Avoid_: Timeout window, SLA

**Hand-over**:
The moment the restaurant gives the ready food to the delivery agent; it happens together with the pickup.
_Avoid_: Dispatch, release (which means something else)

## Delivery

**Delivery**:
The delivery agent's handling of one accepted order, from waiting for an agent through pickup to drop-off or failure.
_Avoid_: Shipment, trip, job

**Claim**:
A delivery agent taking a waiting delivery for themselves.
_Avoid_: Assign (when the agent does it), accept

**Release**:
A delivery agent (or an Admin on their behalf) giving a claimed delivery back to the queue before pickup.
_Avoid_: Cancel, unassign, drop

**Pickup**:
The delivery agent collecting the ready food at the restaurant.
_Avoid_: Collection

**Delivery Failure**:
A delivery that cannot be completed after pickup.
_Avoid_: Failed order, cancellation

## Shared

**Customer Order Status**:
The single progress status customers see, computed from the order fulfillment and the delivery and never stored.
_Avoid_: Order state, tracking status

**Reason Code**:
The fixed code that explains a rejection, cancellation or delivery failure; each action has its own list.
_Avoid_: Reason text, cause
