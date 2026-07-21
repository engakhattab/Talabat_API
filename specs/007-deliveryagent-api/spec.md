# Feature Specification: DeliveryAgent API

**Feature Branch**: `feature/user-aggregate-refactor`  
**Created**: 2026-07-21  
**Status**: Draft  
**Input**: User description: "Phase 8 DeliveryAgent API from PROJECT_IMPLEMENTATION_ROADMAP.md"

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As a delivery agent, I need a dedicated API that lets me manage my availability status, update my real-time location, view my assigned deliveries, and progress through delivery lifecycle steps (arrive at restaurant → pick up → out for delivery → delivered) so that customers receive their orders reliably and the platform can coordinate delivery operations efficiently.

### Acceptance Scenarios

1. **Given** an authenticated delivery agent who is offline, **When** they call the go-online endpoint, **Then** their status changes to Available and they become eligible for delivery assignment.

2. **Given** an authenticated delivery agent who is available, **When** they call the go-offline endpoint, **Then** their status changes to Offline and they are no longer eligible for new assignments.

3. **Given** an authenticated delivery agent, **When** they submit a location update with valid latitude and longitude, **Then** their current location is persisted for operational visibility.

4. **Given** a delivery in PendingAssignment status and an available delivery agent, **When** the assignment endpoint is called for that agent, **Then** the delivery transitions to Assigned, the agent transitions to Busy, and the assignment timestamp is recorded.

5. **Given** a delivery assigned to a specific agent, **When** that agent calls the arrived-at-restaurant endpoint, **Then** the delivery transitions to ArrivedAtRestaurant with a timestamp.

6. **Given** a delivery where the agent has arrived at the restaurant, **When** that agent calls the picked-up endpoint, **Then** the delivery transitions to PickedUp with a timestamp.

7. **Given** a delivery that has been picked up, **When** that agent calls the out-for-delivery endpoint, **Then** the delivery transitions to OutForDelivery with a timestamp.

8. **Given** a delivery that is out for delivery, **When** that agent calls the delivered endpoint, **Then** the delivery transitions to Delivered with a timestamp, and the agent transitions back to Available.

9. **Given** a delivery assigned to an agent (in Assigned or ArrivedAtRestaurant status), **When** that agent calls the cancel endpoint, **Then** the delivery transitions to Cancelled, and the agent transitions back to Available.

10. **Given** a delivery assigned to an agent (in any non-terminal active status), **When** that agent calls the fail endpoint with a reason, **Then** the delivery transitions to Failed with the reason recorded, and the agent transitions back to Available.

11. **Given** a delivery assigned to Agent A, **When** Agent B attempts any lifecycle action on that delivery, **Then** the system rejects the request with a 403 Forbidden response.

12. **Given** an unauthenticated caller, **When** they attempt any delivery agent endpoint, **Then** the system returns 401 Unauthorized.

13. **Given** an authenticated user without the DeliveryAgent capability, **When** they attempt any delivery agent endpoint, **Then** the system returns 403 Forbidden.

14. **Given** an authenticated delivery agent, **When** they request their current delivery, **Then** they receive the details of their active delivery or a 404 if none is assigned.

15. **Given** an authenticated delivery agent, **When** they request their delivery history, **Then** they receive a list of all deliveries previously assigned to them, ordered by most recent first.

### Edge Cases

- An agent who is already busy (has an active delivery) cannot go offline or be assigned another delivery.
- A suspended agent cannot go online or be assigned deliveries.
- A delivery in a terminal state (Delivered, Cancelled, Failed) cannot have any further lifecycle actions performed on it.
- A delivery that has been picked up or is out for delivery cannot be cancelled directly; it can only be failed with a reason.
- An unassigned delivery cannot have lifecycle progression actions (arrived, picked up, etc.) performed on it.
- Location updates must have valid latitude (-90 to 90) and longitude (-180 to 180) values.
- Concurrent modification of a delivery by two requests should be detected and rejected (409 Conflict) using the concurrency token.
- A delivery agent profile that has not been approved cannot access agent-specific endpoints.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system shall provide an endpoint for an authenticated delivery agent to set their availability status to online (Available).
- **FR-002**: The system shall provide an endpoint for an authenticated delivery agent to set their availability status to offline (Offline).
- **FR-003**: The system shall provide an endpoint for an authenticated delivery agent to update their current geographic location.
- **FR-004**: The system shall provide an endpoint to assign a pending delivery to an available agent, transitioning the delivery to Assigned and the agent to Busy.
- **FR-005**: The system shall provide an endpoint for the assigned agent to mark arrival at the restaurant.
- **FR-006**: The system shall provide an endpoint for the assigned agent to mark the order as picked up.
- **FR-007**: The system shall provide an endpoint for the assigned agent to mark the delivery as out for delivery.
- **FR-008**: The system shall provide an endpoint for the assigned agent to mark the delivery as delivered, releasing the agent back to Available.
- **FR-009**: The system shall provide an endpoint for the assigned agent to cancel an active delivery (before pick-up stage), releasing the agent back to Available.
- **FR-010**: The system shall provide an endpoint for the assigned agent to fail an active delivery with a mandatory reason, releasing the agent back to Available.
- **FR-011**: The system shall provide an endpoint for an authenticated agent to retrieve their currently active delivery details.
- **FR-012**: The system shall provide an endpoint for an authenticated agent to retrieve their delivery history.
- **FR-013**: The system shall provide an endpoint to list deliveries pending assignment for operational visibility.
- **FR-014**: All delivery lifecycle endpoints shall enforce that only the assigned agent can perform actions on their assigned delivery.
- **FR-015**: All delivery agent endpoints shall require valid authentication and the DeliveryAgent role/capability.
- **FR-016**: Delivery lifecycle transitions shall follow the defined state machine: PendingAssignment → Assigned → ArrivedAtRestaurant → PickedUp → OutForDelivery → Delivered (or Cancelled/Failed from eligible states).
- **FR-017**: The system shall map domain exceptions to appropriate HTTP Problem Details responses with consistent error codes.
- **FR-018**: The system shall use the existing `DeliveryAssignmentDomainService` to coordinate agent-delivery assignment, completion, cancellation, and failure operations atomically.
- **FR-019**: The system shall use Application-layer handlers (CQRS-lite pattern) to orchestrate delivery use cases, consistent with the Customer API pattern.
- **FR-020**: The system shall validate the delivery agent's JWT access token against the `Talabat.Identity` authority, using the `talabat.deliveryagent-api` audience/scope.

### Key Entities *(include if feature involves data)*

- **User (DeliveryAgent role)**: The unified user aggregate representing a delivery agent with status tracking (Offline, Available, Busy, Suspended), current location, vehicle type, and approval status.
- **Delivery**: An operational task linked to an order, tracking assignment, lifecycle progression through defined states, timestamps, and delivery address.
- **DeliveryAssignmentDomainService**: Stateless domain service coordinating the atomic agent-delivery lifecycle (assign, complete, cancel, fail).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All delivery lifecycle transitions (assign, arrive, pick-up, out-for-delivery, deliver, cancel, fail) complete successfully for authorized agents within standard response times.
- **SC-002**: An agent's active delivery count never exceeds one at any time, enforced by both application logic and database constraint.
- **SC-003**: Unauthorized or unauthenticated requests to any delivery agent endpoint are rejected 100% of the time with appropriate error responses.
- **SC-004**: Ownership-violating requests (Agent B acting on Agent A's delivery) are rejected 100% of the time with appropriate error responses.
- **SC-005**: The delivery state machine rejects all invalid transitions, returning clear error details to the caller.
- **SC-006**: Agent status transitions (online, offline, busy, available) reflect correct availability, and an agent marked busy cannot be assigned additional deliveries.
- **SC-007**: Delivery lifecycle completion (delivered, cancelled, failed) always releases the assigned agent back to Available status atomically.
- **SC-008**: All existing Customer API and Identity tests remain green after DeliveryAgent API implementation.
- **SC-009**: The Delivery Website can be built against the stable backend contracts provided by this API.
- **SC-010**: Concurrency conflicts during delivery operations are detected and surfaced as 409 Conflict responses.

## Assumptions

- The assignment model for this phase is **manual operations assignment** — an authorized caller triggers delivery assignment to a specific agent. Automatic nearest-agent assignment and agent self-acceptance are deferred to Phase 11.
- The unified User aggregate already contains all delivery agent domain behavior (go online/offline, suspend, mark busy/available, update location, submit/approve/reject agent application). No new domain model changes are needed unless a gap is discovered during implementation.
- The existing `Delivery` aggregate, `DeliveryAssignmentDomainService`, `IDeliveryRepository`, and `DeliveryRepository` are already implemented and ready for use.
- The `IUserCapabilityService` already handles delivery agent registration and approval workflows. These are not re-implemented in Phase 8.
- JWT bearer authentication is configured against the `Talabat.Identity` authority, mirroring the Customer API pattern but using the `talabat.deliveryagent-api` scope.
- The `ICurrentUser` abstraction will be extended with `HasDeliveryAgentCapability` and `AgentId` properties for agent-scoped operations.
- The `DomainExceptionMapper` and `ApplicationErrorCodes` will be extended with delivery-specific exception mappings and error codes.
- Delivery creation from checkout is **not** included in this phase. Deliveries for testing will be created through infrastructure seeding or test setup. Automatic delivery creation on checkout is a future integration concern.
- The `WeatherForecast` placeholder files in `Talabat.Delivery.API` will be removed as part of this phase.
- Role-based authorization in the API uses the existing `DeliveryAgent` Identity role granted through the `UserCapabilityService` approval workflow.

## Out of Scope

- Delivery Website frontend.
- Real-time GPS tracking, maps integration, or push notifications.
- Automatic nearest-agent assignment or route optimization algorithms.
- Agent self-acceptance of delivery tasks (agent pull model).
- Payment processing or integration.
- Customer-facing delivery status tracking endpoints (these belong to the Customer API).
- Admin/operations management dashboard or admin-specific endpoints.
- Advanced Identity/Auth features (refresh-token tuning, 2FA, external login, email confirmation).
- Production signing keys, secrets hardening, or CI/CD pipeline configuration.
- Automatic delivery creation on checkout (a future cross-API integration concern).
- Restaurant-owner workflows or restaurant management endpoints.
- Data-preserving database migrations (destructive dev-DB rebuild is acceptable for this phase).
