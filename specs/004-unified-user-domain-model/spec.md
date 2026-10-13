# Feature Specification: Unified User Domain Model

**Feature Branch**: `feature/user-aggregate-refactor`  
**Created**: 2026-07-18  
**Status**: Draft  
**Input**: User description: "Create the Phase 1 specification for the unified User aggregate refactor, following user-aggregate-refactor-plan.md."

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As the platform team, we need one unified user concept that represents a person's account and all of
their business capabilities, so a person can later act as a customer, a delivery agent, or both
without duplicate accounts or linked profile records. Phase 1 establishes and proves this behavior
inside the business model while leaving every running application flow unchanged.

### Acceptance Scenarios

1. **Given** valid account details for a new person, **When** the person is registered in the business model, **Then** the user is active, has no business capability yet, and has no customer or delivery-agent state.
2. **Given** a registered user without customer capability, **When** a valid customer profile is initialized, **Then** the same user gains customer capability and retains the supplied name, positive age, and optional phone number.
3. **Given** a user without customer capability, **When** any customer-only profile, address, or delivery-address operation is attempted, **Then** the operation is rejected as an uninitialized customer profile.
4. **Given** a customer-capable user, **When** the customer profile is updated with valid values, **Then** the new values replace the prior profile values on that same user.
5. **Given** a customer-capable user with an address, **When** the same address is added again, **Then** the duplicate is rejected and the address collection remains unchanged.
6. **Given** a customer-capable user with multiple addresses, **When** one address is selected as default, **Then** it is the only default address; **When** a different address is selected, **Then** the prior default is cleared.
7. **Given** a customer-capable user and one of their addresses, **When** a delivery-address snapshot is requested, **Then** the snapshot contains the address values needed for delivery and is independent of later profile edits.
8. **Given** a registered user and a supported vehicle type, **When** a delivery-agent application is submitted, **Then** the application is pending while delivery-agent capability and operational status remain absent.
9. **Given** a pending delivery-agent application, **When** it is approved, **Then** the same user gains delivery-agent capability and begins in the Offline state.
10. **Given** a pending delivery-agent application, **When** it is rejected and later resubmitted with a supported vehicle type, **Then** the same user returns to pending review without gaining delivery-agent capability.
11. **Given** an application that is not pending, **When** approval or rejection is attempted, **Then** the decision is rejected without changing the user's capability or operational state.
12. **Given** an approved delivery agent, **When** valid online, offline, busy, release, suspension, and location operations occur, **Then** the state follows the established delivery-agent transition rules.
13. **Given** a user who has not been approved as a delivery agent, **When** an agent-only operational action is attempted, **Then** the action is rejected as an uninitialized delivery-agent capability.
14. **Given** an active user, **When** the user is deactivated and later reactivated, **Then** only the activation state changes and the user's capabilities and profiles are preserved.
15. **Given** the completed Phase 1 changes, **When** existing customer, delivery, and identity behavior is exercised, **Then** it behaves exactly as before because the unified model is not connected to running hosts or persistence in this phase.

### Edge Cases

- Blank required names and non-positive customer ages are rejected without partially changing the user.
- Unsupported vehicle values are rejected before an application becomes pending.
- Resubmission after rejection is allowed; submitting while an application is already pending refreshes the pending application with the newly supplied vehicle type and remains pending; submission after approval is rejected.
- Approval starts the agent at Offline, never Available, and no agent status exists before approval.
- A Busy agent cannot go offline or be suspended; a Suspended agent cannot change online status.
- Only an Available agent can become Busy, and only a Busy agent can be released to Available.
- Going online from Offline or Available results in Available; going offline from Offline or Available results in Offline; suspending Offline, Available, or Suspended results in Suspended.
- A non-positive address identifier is rejected as invalid input; an unknown positive identifier is rejected as an address-not-found operation.
- Removing the default address is allowed and may leave the user with no default address.
- Adding a non-default address does not implicitly change the current default.
- Customer and delivery-agent capabilities can coexist on one user; granting one must not remove another.
- A missing location value is rejected without changing the agent's last known location.
- Deactivation does not revoke capabilities or delete the user.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The business model MUST represent one person with one unified user record that can hold zero, one, or several capabilities simultaneously.
- **FR-002**: Supported capabilities MUST include Customer, DeliveryAgent, Admin, and RestaurantOwner, and combinations MUST preserve every independently granted capability. Phase 1 introduces Admin and RestaurantOwner only as combinable classifications; no registration, grant, approval, or lifecycle behavior exists for those two capabilities in this phase.
- **FR-003**: Registering a user MUST accept a username and email, MUST require a non-blank full name, MUST create an active user, and MUST start with no business capability. Username, email, and credential policy validation is deferred to the later account workflow.
- **FR-004**: The user MUST carry customer profile data, delivery-agent application and operational data, activation state, audit and soft-deletion state, an address collection, and an opaque concurrency marker that MUST be initialized to an empty non-null value and MUST have no observable Phase 1 behavior beyond existing safely.
- **FR-005**: Customer initialization MUST require a non-blank full name and positive age, MUST accept an optional phone number, and MUST grant Customer capability to that same user. The user has exactly one full name: registration sets it, and customer initialization or a customer profile update replaces that same single value — there is no separate customer-profile name field.
- **FR-006**: Customer profile updates and all customer address operations MUST require Customer capability and MUST preserve the validation behavior of the existing customer model.
- **FR-007**: Addresses MUST be controlled through the user, MUST reject duplicates by address value — duplicate detection compares the complete address value (street, city, building number, and floor) — MUST expose read-only collection access, and MUST allow at most one address to be marked as default.
- **FR-008**: Removing, selecting, or snapshotting an unknown address MUST fail explicitly rather than silently succeeding or selecting another address.
- **FR-009**: A delivery-agent application MUST require a supported vehicle type — the supported vehicle types are exactly the existing set: Bike, Motorcycle, and Car — and MUST place the application in Pending Approval without granting DeliveryAgent capability or creating operational status.
- **FR-010**: Only a pending application MAY be approved or rejected. Approval MUST grant DeliveryAgent capability and initialize status to Offline; rejection MUST leave the capability absent and operational status uninitialized.
- **FR-011**: A rejected application MAY be resubmitted. An approved user MUST NOT submit another delivery-agent application.
- **FR-012**: Agent operations MUST require both DeliveryAgent capability and an initialized agent status.
- **FR-013**: Agent availability and status changes MUST preserve the complete existing transition matrix: Available is the only state that may become Busy; Busy is the only state that may be released to Available; Busy cannot go Offline or Suspended; Suspended cannot change online status; and only approved agents may update location.
- **FR-014**: The operations that reserve and release an agent MUST remain unavailable to general callers so only controlled delivery workflows can invoke them.
- **FR-015**: Activation and deactivation MUST change only whether the account is business-active; they MUST NOT revoke capabilities, remove profile data, or hard-delete the user.
- **FR-016**: Customer-only and agent-only actions MUST produce distinct business failures when their required capability has not been initialized.
- **FR-017**: Repeated or out-of-order delivery-agent approval decisions MUST fail explicitly and MUST leave prior user state intact.
- **FR-018**: The phase MUST establish a business-data access contract for loading a user by identifier, loading a read-only user, loading a user with addresses, listing available agents, and marking an existing user for update. User creation MUST remain outside this contract.
- **FR-019**: The phase MUST establish a capability-workflow contract covering customer registration, delivery-agent applicant registration, customer-capability grants, agent approval, agent rejection, and user deactivation. Customer registration MUST accept email, password, full name, age, and an optional phone number; delivery-agent applicant registration MUST accept email, password, full name, vehicle type, and an optional phone number; the customer-capability grant MUST accept the user identifier, full name, age, and an optional phone number; approval, rejection, and deactivation decisions MUST accept only the user identifier; callers MUST never provide authorization role names.
- **FR-020**: Capability state in the business model MUST remain independent of authorization-role infrastructure; no role lookup or role mutation belongs in the Phase 1 business model.
- **FR-021**: Auditable and soft-deletable business objects MUST expose consistent contracts for creation, modification, deletion, and restoration metadata, and existing auditable objects MUST retain their current behavior.
- **FR-022**: The audit stamping behavior MUST recognize any object that fulfills the auditable contract, including the unified user when persistence is introduced later.
- **FR-023**: The unified model MUST remain independent of web, request, authorization-manager, and persistence concerns; only the minimum account-store type needed to unify account identity is permitted in the business layer.
- **FR-024**: Phase 1 MUST be additive: all existing customer, delivery-agent, account, and ordering behavior MUST remain operational and unchanged except for generalized recognition of auditable objects.
- **FR-025**: Phase 1 MUST NOT connect the unified user to a running application, change stored data structures, replace an existing operational model, grant an authorization role, or alter any externally observable contract.
- **FR-026**: Automated behavior tests MUST cover user registration defaults, activation changes, customer initialization and guards, customer profile updates, every address invariant, agent application decisions, resubmission, every legal and illegal agent-state transition, and uninitialized-capability guards.
- **FR-027**: All pre-existing automated tests MUST remain unchanged and pass together with the new behavior tests.
- **FR-028**: The phase MUST define a distinct business failure for future concurrent-update conflicts without detecting, storing, or translating such conflicts in Phase 1.

### Key Entities *(include if feature involves data)*

- **User**: The single business representation of a person's account, capabilities, customer profile, delivery-agent state, activation state, audit history, soft-deletion state, addresses, and future concurrency state.
- **User Capability**: A combinable classification describing the business roles a person may perform: Customer, DeliveryAgent, Admin, or RestaurantOwner.
- **User Address**: A customer address owned and modified only through its user, with value-based duplicate detection and an optional default designation.
- **Agent Approval Status**: The application lifecycle Pending Approval, Approved, or Rejected; it is separate from operational delivery-agent status.
- **Delivery-Agent Status**: The approved agent's operational state: Offline, Available, Busy, or Suspended.
- **Audit and Soft-Deletion State**: Creation, modification, deletion, and restoration metadata shared consistently by auditable business objects.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: One user can demonstrably hold both Customer and DeliveryAgent capabilities at the same time, with one account identity and no linked duplicate profile record.
- **SC-002**: 100% of the customer profile, address, agent application, agent transition, activation, and capability-guard scenarios defined in this specification pass automated behavior tests (this criterion is the measurable outcome of FR-026 and traces directly to it).
- **SC-003**: 100% of pre-existing automated tests pass without assertion or scenario changes, demonstrating no runtime behavior regression.
- **SC-004**: Every defined legal delivery-agent transition succeeds, and every defined illegal transition is rejected without unintended state changes.
- **SC-005**: Across all address behavior tests, duplicate addresses are rejected and the number of default addresses never exceeds one.
- **SC-006**: Inspection of the Phase 1 change set confirms zero deleted or replaced legacy customer or delivery-agent behavior, zero externally observable contract changes, and zero stored-data structure changes.
- **SC-007**: The unified business model can be exercised in isolation without a web request, database connection, or authorization service; account identity is its only approved external foundation.
- **SC-008**: The complete solution builds successfully and all existing plus new test suites pass at the Phase 1 acceptance gate.

## Assumptions

- The approved unified-user refactor plan and project constitution are normative; this specification narrows them to Phase 1 only. If wording conflicts, the constitution prevails, then the refactor plan, then this specification.
- Existing customer address behavior and delivery-agent state transitions are preserved exactly unless this specification explicitly introduces approval or capability guards.
- Phase 1 assigns username and email to the unified account but does not duplicate the later Identity workflow's username, email, or credential validation policy in the business aggregate.
- Repeating customer initialization is not required to be idempotent at the business-object level; if it is repeated directly on the business object, it reapplies the supplied profile values and leaves the capability set unchanged. The later capability workflow will prevent duplicate customer onboarding before invoking it.
- Performance, scale, availability, and observability requirements are intentionally not applicable to this persistence-free, non-runtime phase; their omission is deliberate, not an oversight.
- Capability revocation is not part of this refactor. Deactivation and soft deletion are the available disablement mechanisms.
- New business behavior tests reside with the existing application-level test suite because no separate business-model test suite exists.
- The later persistence phase owns storage mapping, concurrency enforcement, role synchronization, and database constraints; Phase 1 only prepares the required business state and contracts.

## Out of Scope

- Replacing or deleting the existing Customer, CustomerAddress, or DeliveryAgent operational models and their supporting components.
- Connecting the unified user to account registration, customer operations, delivery operations, or any other running application.
- Persisting unified users, addresses, capability flags, approval state, or concurrency state.
- Synchronizing capabilities with authorization roles or implementing capability-workflow transactions.
- Delivery-agent approval endpoints, admin controllers or user interfaces, and role seeding.
- Customer-operation ownership hardening, authorization-claim wiring, login rejection, session invalidation, or concurrency-conflict responses.
- Database constraints, migrations, development-database rebuilds, or data preservation.
- Capability revocation, discounts, employee offers, frontend work, or full Delivery API implementation.
