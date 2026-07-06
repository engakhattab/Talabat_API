# Delivery Domain Services Design

## Why A Domain Service Is Needed

Assignment and completion change two aggregate roots in one business operation:

- Delivery owns assignment identity and delivery progress.
- DeliveryAgent owns courier availability.

Neither aggregate should contain or directly control the other. `DeliveryAssignmentDomainService` coordinates their domain methods using already-loaded aggregates.

## DeliveryAssignmentDomainService

The service is stateless. It has no repository, database, HTTP, mapping, or messaging dependency.

Every `currentTime` argument is a UTC `DateTime`. The Domain validates this policy before applying lifecycle transitions.

### Assign

Conceptual operation:

`Assign(delivery, agent, currentTime)`

Validation and behavior:

1. Delivery cannot be null.
2. DeliveryAgent cannot be null.
3. Delivery must be `PendingAssignment`.
4. Delivery must not already have an assigned agent.
5. Agent must report that it is `Available`.
6. Call `delivery.AssignAgent(agent.Id, currentTime)`.
7. Call `agent.MarkBusy()`.

The Delivery aggregate protects assignment status and identity. The DeliveryAgent aggregate protects its availability transition. The service expresses that both changes form one business operation.

An agent can have only one active delivery. In the Domain flow, an agent with an active assignment is Busy and therefore cannot be assigned again. Persistence later adds a filtered unique index to protect this rule against concurrent assignments.

### CompleteDelivery

Conceptual operation:

`CompleteDelivery(delivery, agent, currentTime)`

Validation and behavior:

1. Delivery cannot be null.
2. DeliveryAgent cannot be null.
3. Delivery must be assigned to this agent.
4. Delivery must be `OutForDelivery`.
5. Call `delivery.MarkDelivered(agent.Id, currentTime)`.
6. Call `agent.MarkAvailable()`.

The order matters: Delivery first validates and completes its lifecycle. Only after successful completion is the agent released back to Available.

### CancelDelivery

`CancelDelivery(delivery, agent, currentTime)` validates that the supplied agent is the assigned Busy agent, cancels the delivery before pickup, and then marks the agent Available.

### FailDelivery

`FailDelivery(delivery, agent, reason, currentTime)` validates that the supplied agent is the assigned Busy agent, fails the delivery with a required reason, and then marks the agent Available.

Unassigned PendingAssignment deliveries can be cancelled or failed directly on Delivery because no second aggregate must change.

## Failure Model

Expected domain exceptions for later implementation include:

- `AgentNotAvailableException`
- `DeliveryAlreadyAssignedException`
- `DeliveryNotAssignedException`
- `InvalidDeliveryStatusTransitionException`
- `DeliveryAlreadyCompletedException`
- `DeliveryAgentMismatchException`
- `DeliveryAgentCoordinationRequiredException`
- `InvalidDeliveryTimestampException`
- `InvalidDeliveryAgentStatusTransitionException`

These failures use delivery language and contain no HTTP status codes or API response details.

## What The Service Must Not Do

- Use repositories.
- Use DbContext or EF Core.
- Save changes.
- Select a nearest agent.
- Return HTTP responses.
- Return API DTOs.
- Send notifications.
- Process payment.
- Call a maps provider.
- Start background jobs.
- Mutate Order, Customer, or Restaurant.

## Application Responsibility Later

The Application layer will:

1. Load Delivery and DeliveryAgent aggregate roots.
2. Call the appropriate DeliveryAssignmentDomainService operation.
3. Save both aggregate changes through UnitOfWork in one transaction.

This keeps orchestration and persistence outside Domain while preserving business decisions inside aggregate methods and the domain service.
