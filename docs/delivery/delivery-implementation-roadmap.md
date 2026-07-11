# Delivery Extension Implementation Roadmap

> Phase 0 scope update: This roadmap tracks Delivery domain progress only. Delivery repositories, Application use cases, Infrastructure, API endpoints, authentication, and the Delivery Website remain deferred. Identity/Auth is reserved/TBD and should not be implemented from this document.

This roadmap tracks the Delivery Extension. Domain implementation through Step 6 is complete. Repository, Application, Infrastructure, and API work remains deferred.

## Step 1 - Create Delivery Design Documentation

**Status:** Completed.

Deliverables:

- Extension overview.
- Business rules.
- Bounded context and context map.
- Aggregate boundaries and invariants.
- Entity, enum, and value-object design.
- Domain-service design.
- Database and ERD design.
- Implementation sequence.

The design documents remain the source for completed Domain work and deferred outer-layer work.

## Step 2 - Implement Delivery Value Objects And Enums

**Status:** Completed.

Implemented deliverables:

- `GeoLocation`
- `DeliveryStatus`
- `DeliveryAgentStatus`
- `VehicleType`

Verification goals:

- GeoLocation rejects invalid latitude or longitude.
- Enums use the documented numeric values.
- Domain project remains framework-independent.

## Step 3 - Implement Delivery Domain Exceptions

**Status:** Completed.

Implemented exceptions include:

- `AgentNotAvailableException`
- `DeliveryAlreadyAssignedException`
- `DeliveryNotAssignedException`
- `InvalidDeliveryStatusTransitionException`
- `DeliveryTerminalStateException`
- `DeliveryAgentMismatchException`

Each exception should inherit from DomainException and use business language without HTTP metadata.

## Step 4 - Implement DeliveryAgent Aggregate

**Status:** Completed.

Implemented work:

- Create DeliveryAgent aggregate root.
- Validate required full name and optional phone number.
- Implement vehicle and status state.
- Implement online, offline, suspension, busy, and available transitions.
- Support optional validated current location.
- Keep persistence and login concepts out of the aggregate.

## Step 5 - Implement Delivery Aggregate

**Status:** Completed.

Implemented work:

- Create Delivery aggregate root.
- Create Delivery as PendingAssignment from Order identity data and address snapshot.
- Implement assignment and ordered lifecycle transitions.
- Validate assigned-agent identity on courier actions.
- Record transition timestamps.
- Implement cancellation, failure, active-state, and terminal-state behavior.
- Keep Order, Customer, Restaurant, and DeliveryAgent as ID references only.

## Step 6 - Implement DeliveryAssignmentDomainService

**Status:** Completed.

Implemented work:

- Implement stateless assignment coordination.
- Assign only Available agents.
- Mark the agent Busy after successful assignment.
- Complete only an OutForDelivery delivery assigned to the supplied agent.
- Mark the agent Available after successful completion.
- Release the assigned agent after coordinated cancellation or failure.

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

No Infrastructure work should begin until the Domain model is stable and the relevant test scope is approved.

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

Domain implementation is complete through Step 6. Stop before Step 7; repository interfaces and all outer-layer work remain deferred.
