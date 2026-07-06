# Delivery Aggregates And Invariants

The Delivery bounded context contains two aggregate roots. Neither aggregate contains the other as a child, and each protects its own state transitions.

## Delivery Aggregate

**Root:** `Delivery`  
**Child entities:** None for this extension phase.

### State

- `Id`: Delivery identity.
- `OrderId`: Identity of the Order that caused this task.
- `CustomerId`: Identity of the customer receiving the order.
- `RestaurantId`: Identity of the pickup restaurant.
- `AssignedAgentId`: Nullable identity of the assigned courier.
- `Status`: Current delivery lifecycle state.
- `DeliveryAddressSnapshot`: Immutable delivery address copied from Order.
- `CreatedAt`: Delivery creation time.
- `AssignedAt`: Nullable assignment time.
- `ArrivedAtRestaurantAt`: Nullable restaurant-arrival time.
- `PickedUpAt`: Nullable pickup time.
- `OutForDeliveryAt`: Nullable departure-for-delivery time.
- `DeliveredAt`: Nullable completion time.
- `CancelledAt`: Nullable cancellation time.
- `FailedAt`: Nullable failure time.
- `FailureReason`: Nullable failure explanation.

### Invariants

- Delivery belongs to exactly one Order.
- Delivery starts as `PendingAssignment`.
- Delivery can have zero or one assigned agent.
- Delivery cannot be reassigned after assignment in this phase.
- Delivery keeps an immutable delivery address snapshot.
- The normal lifecycle follows this exact order:

```text
PendingAssignment
    -> Assigned
    -> ArrivedAtRestaurant
    -> PickedUp
    -> OutForDelivery
    -> Delivered
```

- A transition is valid only from its immediately preceding status.
- `Delivered`, `Cancelled`, and `Failed` are terminal statuses.
- Terminal deliveries cannot be assigned, progressed, cancelled again, or failed again.
- Every agent-driven transition must receive the assigned agent ID.
- Delivery cannot be progressed or delivered by an agent different from `AssignedAgentId`.
- Cancellation is allowed only before pickup in this phase: `PendingAssignment`, `Assigned`, or `ArrivedAtRestaurant`.
- Failure requires a non-empty reason and may end any non-terminal delivery.
- Each lifecycle timestamp is set only when its matching transition succeeds.

### Methods

- `AssignAgent(agentId, currentTime)`
- `MarkArrivedAtRestaurant(agentId, currentTime)`
- `MarkPickedUp(agentId, currentTime)`
- `MarkOutForDelivery(agentId, currentTime)`
- `MarkDelivered(agentId, currentTime)`
- `Cancel(currentTime)`
- `Fail(reason, currentTime)`
- `IsActive()`
- `IsTerminal()`

`IsActive()` means the delivery is assigned and in one of these operational states: `Assigned`, `ArrivedAtRestaurant`, `PickedUp`, or `OutForDelivery`. `PendingAssignment` is open work but has no active courier assignment.

## DeliveryAgent Aggregate

**Root:** `DeliveryAgent`  
**Child entities:** None for this extension phase.

### State

- `Id`: Agent identity.
- `FullName`: Required courier display name.
- `PhoneNumber`: Optional contact number.
- `VehicleType`: Bike, Motorcycle, or Car.
- `Status`: Offline, Available, Busy, or Suspended.
- `CurrentLocation`: Optional `GeoLocation`.
- `CreatedAt`: Agent profile creation time.

### Invariants

- Agent full name is required and cannot be empty.
- Agent can be `Offline`, `Available`, `Busy`, or `Suspended`.
- Only an `Available` agent can be assigned.
- Assignment changes an available agent to `Busy`.
- A `Busy` agent cannot go offline.
- A `Suspended` agent cannot become available through ordinary `GoOnline` behavior; an explicit future reinstatement decision is required.
- Completion of the assigned delivery changes the busy agent back to `Available`.
- Current location is optional.
- When present, current location must contain valid latitude and longitude.

### Methods

- `GoOnline()`
- `GoOffline()`
- `Suspend()`
- `IsAvailable()`
- `MarkBusy()`
- `MarkAvailable()`
- `UpdateLocation(location)`

`MarkBusy()` and `MarkAvailable()` support cross-aggregate coordination and should be invoked by `DeliveryAssignmentDomainService`, not directly by an Application use case.

## Why They Are Separate Aggregates

- Delivery represents the lifecycle of one courier task tied to one Order.
- DeliveryAgent represents a courier profile that exists before and after many deliveries.
- They protect different invariants and have separate persistence lifecycles.
- Loading a Delivery should not require loading the complete agent aggregate automatically.
- Delivery references the assigned agent by ID only.
- DeliveryAgent does not store `CurrentDeliveryId`; active-assignment uniqueness is coordinated by the domain workflow and protected by persistence later.

Assignment and completion require both aggregates, so those operations belong in a stateless domain service.
