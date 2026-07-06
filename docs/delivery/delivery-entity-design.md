# Delivery Entity Design

This document describes the state and behavior of the Delivery Extension domain objects. It is design only and does not define persistence or API models.

## Delivery

### Role

`Delivery` is an aggregate root representing the courier task created for one successful Order. It owns assignment identity, ordered progress, lifecycle timestamps, and terminal-state protection.

### Properties

| Property | Type category | Purpose |
|---|---|---|
| Id | Primitive identity | Identifies the delivery task. |
| OrderId | External aggregate ID | Links the task to exactly one historical Order. |
| CustomerId | External aggregate ID | Identifies the customer receiving the delivery. |
| RestaurantId | External aggregate ID | Identifies the pickup restaurant. |
| AssignedAgentId | Nullable aggregate ID | Identifies the assigned courier without holding a DeliveryAgent reference. |
| Status | Enum | Controls valid lifecycle transitions. |
| DeliveryAddress | Value Object | Preserves the address snapshot copied from Order. |
| CreatedAt | Time value | Records task creation. |
| AssignedAt | Nullable time value | Records successful assignment. |
| ArrivedAtRestaurantAt | Nullable time value | Records restaurant arrival. |
| PickedUpAt | Nullable time value | Records pickup. |
| OutForDeliveryAt | Nullable time value | Records departure toward the customer. |
| DeliveredAt | Nullable time value | Records successful completion. |
| CancelledAt | Nullable time value | Records cancellation. |
| FailedAt | Nullable time value | Records terminal failure. |
| FailureReason | Nullable primitive | Explains a failed delivery. |

### Behavior

| Behavior | Responsibility |
|---|---|
| Assign agent | Accept one available-agent identity while PendingAssignment and record assignment time. |
| Mark arrived at restaurant | Require Assigned status and the assigned agent identity. |
| Mark picked up | Require ArrivedAtRestaurant status and the assigned agent identity. |
| Mark out for delivery | Require PickedUp status and the assigned agent identity. |
| Mark delivered | Require OutForDelivery status and the assigned agent identity. |
| Cancel | End a delivery before pickup and record cancellation time. |
| Fail | End a non-terminal delivery with a required reason. |
| Check active state | Report whether an assigned delivery is operationally active. |
| Check terminal state | Report whether status is Delivered, Cancelled, or Failed. |

External code must not set status, assignment, timestamps, address snapshot, or failure details directly.

Assigned cancellation and failure are cross-aggregate operations and must be coordinated through DeliveryAssignmentDomainService so DeliveryAgent availability is updated. Transition timestamps must be monotonic.

## DeliveryAgent

### Role

`DeliveryAgent` is an aggregate root representing a courier profile and current availability. The agent can participate in many deliveries over time but is not a child of any Delivery.

### Properties

| Property | Type category | Purpose |
|---|---|---|
| Id | Primitive identity | Identifies the courier. |
| FullName | Primitive | Required human-readable courier name. |
| PhoneNumber | Nullable primitive | Optional contact detail for this phase. |
| VehicleType | Enum | Records the courier's delivery vehicle. |
| Status | Enum | Controls online, availability, busy, and suspension behavior. |
| CurrentLocation | Nullable Value Object | Stores the latest manually supplied valid location. |
| CreatedAt | Time value | Records profile creation. |

### Behavior

| Behavior | Responsibility |
|---|---|
| Go online | Move an eligible Offline agent to Available. |
| Go offline | Move an eligible Available agent to Offline; Busy agents are rejected. |
| Suspend | Move the agent to Suspended according to explicit domain behavior. |
| Check availability | Report whether status is Available. |
| Mark busy | Reserve an Available agent during assignment. |
| Mark available | Release a Busy agent after successful completion, cancellation, or failure coordination. |
| Update location | Replace optional location with a validated GeoLocation. |

Application code should not directly coordinate `MarkBusy` or `MarkAvailable`; the assignment domain service coordinates those changes with Delivery.

## Enums

### DeliveryStatus

| Name | Value |
|---|---:|
| PendingAssignment | 1 |
| Assigned | 2 |
| ArrivedAtRestaurant | 3 |
| PickedUp | 4 |
| OutForDelivery | 5 |
| Delivered | 6 |
| Cancelled | 7 |
| Failed | 8 |

### DeliveryAgentStatus

| Name | Value |
|---|---:|
| Offline | 1 |
| Available | 2 |
| Busy | 3 |
| Suspended | 4 |

### VehicleType

| Name | Value |
|---|---:|
| Bike | 1 |
| Motorcycle | 2 |
| Car | 3 |

## GeoLocation Value Object

### State

- `Latitude`
- `Longitude`

### Rules

- Latitude must be between -90 and 90, inclusive.
- Longitude must be between -180 and 180, inclusive.
- Both coordinates form one immutable value.
- Equality is based on latitude and longitude.
- DeliveryAgent may have no current location.

GeoLocation records a current value only. It is not a route, GPS history, tracking stream, or maps-provider integration.
