# Delivery Extension

> Phase 0 scope update: Delivery domain code now exists in `Talabat.Domain`. Repository, Application, Infrastructure, API, authentication, and Delivery Website work remain deferred to later phases.

The Delivery Extension is an MVP v2 feature added after the original Talabat MVP v1 domain. MVP v1 focuses on Catalog, Basket, Customer, and Ordering. This extension adds courier assignment and the delivery lifecycle that begins after successful checkout.

Delivery is a separate bounded context. It must not be mixed directly into the Order aggregate:

- Order remains the immutable historical purchase record.
- Delivery represents the operational courier task for that order.
- DeliveryAgent represents a courier profile and availability lifecycle.
- Delivery and DeliveryAgent are separate aggregate roots.
- DeliveryAssignmentDomainService coordinates assignment and completion across those aggregates.

## High-Level Flow

```text
Customer checkout succeeds
    -> Order is created
    -> Delivery task is created as PendingAssignment
    -> Available DeliveryAgent is assigned
    -> Agent arrives at restaurant
    -> Agent picks up order
    -> Agent goes out for delivery
    -> Delivery is delivered
```

The Application layer will eventually orchestrate this flow. Ordering does not directly create or mutate Delivery, and Delivery does not mutate Order.

## Extension Scope

This phase designs:

- Delivery task creation after checkout.
- Courier availability and assignment.
- Ordered delivery lifecycle transitions.
- Delivery progress timestamps.
- Optional agent location represented as a validated value.

This phase does not include:

- Real-time GPS tracking.
- Maps integration.
- Payment.
- Notifications.
- Authentication or courier login.
- Nearest-agent selection or route optimization.

Delivery agents may be seeded for testing in a later Infrastructure phase. Delivery domain implementation now exists, but persistence mapping, API, authentication, Delivery Website support, and Application use cases are still deferred.
