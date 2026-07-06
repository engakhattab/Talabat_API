# Delivery Extension Implementation Roadmap

This roadmap sequences the Delivery Extension after its design is reviewed. Only Step 1 is performed now. Steps 2 through 10 are deferred.

## Step 1 - Create Delivery Design Documentation

**Status:** Completed by this documentation task.

Deliverables:

- Extension overview.
- Business rules.
- Bounded context and context map.
- Aggregate boundaries and invariants.
- Entity, enum, and value-object design.
- Domain-service design.
- Database and ERD design.
- Implementation sequence.

No C# implementation is part of this step.

## Step 2 - Implement Delivery Value Objects And Enums

Deferred deliverables:

- `GeoLocation`
- `DeliveryStatus`
- `DeliveryAgentStatus`
- `VehicleType`

Verification goals:

- GeoLocation rejects invalid latitude or longitude.
- Enums use the documented numeric values.
- Domain project remains framework-independent.

## Step 3 - Implement Delivery Domain Exceptions

Deferred exceptions:

- `AgentNotAvailableException`
- `DeliveryAlreadyAssignedException`
- `DeliveryNotAssignedException`
- `InvalidDeliveryStatusTransitionException`
- `DeliveryAlreadyCompletedException`
- `DeliveryAgentMismatchException`

Each exception should inherit from DomainException and use business language without HTTP metadata.

## Step 4 - Implement DeliveryAgent Aggregate

Deferred work:

- Create DeliveryAgent aggregate root.
- Validate required full name and optional phone number.
- Implement vehicle and status state.
- Implement online, offline, suspension, busy, and available transitions.
- Support optional validated current location.
- Keep persistence and login concepts out of the aggregate.

## Step 5 - Implement Delivery Aggregate

Deferred work:

- Create Delivery aggregate root.
- Create Delivery as PendingAssignment from Order identity data and address snapshot.
- Implement assignment and ordered lifecycle transitions.
- Validate assigned-agent identity on courier actions.
- Record transition timestamps.
- Implement cancellation, failure, active-state, and terminal-state behavior.
- Keep Order, Customer, Restaurant, and DeliveryAgent as ID references only.

## Step 6 - Implement DeliveryAssignmentDomainService

Deferred work:

- Implement stateless assignment coordination.
- Assign only Available agents.
- Mark the agent Busy after successful assignment.
- Complete only an OutForDelivery delivery assigned to the supplied agent.
- Mark the agent Available after successful completion.
- Add focused Domain tests for success and failure paths.

The service must not use repositories or save changes.

## Step 7 - Add Repository Interfaces Later

Deferred Domain contracts:

- `IDeliveryRepository`
- `IDeliveryAgentRepository`

Repositories should exist only for the two aggregate roots. They must not expose EF Core, DbContext, IQueryable, HTTP concepts, or API DTOs.

## Step 8 - Add Application Use Cases Later

Deferred use cases:

- `CreateDeliveryForOrder`
- `AssignDeliveryAgent`
- `MarkArrivedAtRestaurant`
- `MarkPickedUp`
- `MarkOutForDelivery`
- `MarkDelivered`
- `GetDeliveryStatus`

Application handlers will load aggregate roots, call Domain behavior, and coordinate UnitOfWork. They should not duplicate lifecycle rules.

## Step 9 - Add Infrastructure Later

Deferred work:

- EF Core entity configurations.
- `DbSet<Delivery>`.
- `DbSet<DeliveryAgent>`.
- Unique OrderId constraint.
- Filtered unique index for one active delivery per assigned agent.
- GeoLocation column mapping and checks.
- Delivery-agent seed data for testing.

No Infrastructure work should begin until the Domain model and tests are stable.

## Step 10 - Add API Later

Deferred API capabilities:

- Delivery status endpoints.
- Delivery assignment endpoints.
- Courier lifecycle simulation endpoints.

This phase still excludes:

- Authentication and courier login.
- Real-time GPS and maps.
- Payment.
- Notifications.
- Nearest-agent optimization.

Controllers should translate requests into Application use cases and must not own delivery transition rules.

## Current Stop Point

Stop after Step 1. Do not implement Steps 2 through 10 as part of the documentation task.
