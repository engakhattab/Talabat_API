# Delivery Bounded Context

## Purpose

Delivery manages courier assignment and delivery lifecycle after an Order is created. It translates a completed checkout into an operational task without adding mutable courier state to the historical Order aggregate.

## What It Owns

- Delivery task lifecycle.
- Delivery assignment.
- Delivery agent availability.
- Delivery agent status.
- Delivery progress timestamps.
- Optional current agent location.

## Main Entities

- `Delivery`, an aggregate root representing one courier task for one order.
- `DeliveryAgent`, an aggregate root representing a courier profile and availability lifecycle.

## Main Value Objects

- `GeoLocation`, a validated optional latitude/longitude pair.
- `DeliveryAddressSnapshot`, reused as an immutable address value copied from the Order when Delivery is created.

`DeliveryAddressSnapshot` is shared immutable data, not a reference that lets Delivery mutate Ordering or Customer state.

## What It Does Not Own

- Cart items.
- Product prices.
- Restaurant menu.
- Customer address management.
- Order item creation.
- Payment.
- Notifications.
- Authentication or courier login.
- Real-time tracking.
- Nearest-agent optimization.

## Communication With Other Contexts

- Ordering creates an Order after checkout succeeds.
- The Application layer creates a Delivery for the created Order.
- Delivery stores `OrderId`, `CustomerId`, `RestaurantId`, and the copied delivery address snapshot.
- The Application layer loads Delivery and DeliveryAgent, then calls `DeliveryAssignmentDomainService`.
- Delivery does not directly mutate Order.
- Order does not directly own Delivery.
- Delivery references Catalog/Restaurant by `RestaurantId` only.
- Delivery references Customer by `CustomerId` only.

## Context Map

```text
Customer -> Ordering
Ordering -> Delivery
Delivery -> DeliveryAgent
Delivery --RestaurantId--> Catalog/Restaurant
Delivery --CustomerId--> Customer
```

The `Delivery -> DeliveryAgent` relationship represents coordination between two aggregate roots in the same bounded context. They reference each other by identity rather than object navigation.

## Important Boundary Decision

Order is historical purchase data. Delivery is mutable courier lifecycle data. They change for different reasons and protect different invariants, so they remain separate aggregates and separate lifecycle owners.

The Application layer is responsible for the workflow boundary between them. It reads the successful Order data needed to create Delivery, but neither aggregate calls the other.
