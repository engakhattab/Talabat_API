# Feature Specification: Unified User Identity and Persistence Cutover

**Feature Branch**: `feature/user-aggregate-refactor`  
**Created**: 2026-07-18  
**Status**: Draft  
**Input**: User description: "Phase 2 in user-aggregate-refactor-plan.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register One Customer Account (Priority: P1)

A new customer registers once and receives one active account that is also their customer profile.
The platform assigns the Customer capability and authorization role; the caller cannot choose a
role or cause a second profile/account record to be created.

**Why this priority**: This is the primary proof that account and business profile are now one
person record and that the removed linkage model is no longer required.

**Independent Test**: Register a customer with valid credentials and profile information, then
verify one generated positive integer identifier, the Customer capability and role, the persisted
profile, and successful login through that same account.

**Acceptance Scenarios**:

1. **Given** an email not already registered, **When** a customer submits valid registration data,
   **Then** exactly one active user is created with the Customer capability, Customer role, and the
   supplied customer profile, and the email is used as the account sign-in name.
2. **Given** an email already in use, **When** customer registration is attempted, **Then** a
   validation failure is returned and no account, profile, capability, or role change is persisted.
3. **Given** a user who already has a non-customer capability, **When** customer onboarding is
   completed, **Then** Customer is added to that same user without removing any existing
   capability or creating a second account.
4. **Given** a user who already has Customer capability, **When** customer onboarding is requested
   again, **Then** the existing `ProfileAlreadyExists` conflict is returned and the user is
   unchanged.

---

### User Story 2 - Apply and Be Approved as a Delivery Agent (Priority: P1)

A delivery-agent applicant creates an account and submits vehicle information without receiving
agent privileges. An admin-controlled service-level approval or rejection later decides the
application; applicants cannot approve themselves. Approval grants the DeliveryAgent capability
and role together and initializes the agent offline.

**Why this priority**: Separating application from authorization prevents unapproved users from
acting as agents while still keeping one account per person.

**Independent Test**: Register an applicant, verify the pending application has no agent capability,
role, or operational status, approve it, and verify the capability and role appear together with an
Offline operational state.

**Acceptance Scenarios**:

1. **Given** a new applicant with valid registration and vehicle data, **When** the application is
   submitted, **Then** one account is created with a pending application and no DeliveryAgent
   capability, role, or operational status.
2. **Given** a pending application, **When** it is approved, **Then** the same user receives the
   DeliveryAgent capability and role and starts in the Offline state.
3. **Given** a pending application, **When** it is rejected, **Then** it becomes rejected without
   receiving DeliveryAgent capability, role, or operational status.
4. **Given** an application that is not pending, **When** approval or rejection is attempted,
   **Then** a conflict is returned and all capability, role, and operational state remains
   unchanged.
5. **Given** any failure while synchronizing an approved capability with its role, **When** the
   workflow ends, **Then** every mutation is rolled back so the capability and role cannot drift.

---

### User Story 3 - Block Inactive and Deleted Accounts (Priority: P1)

An operator can deactivate an account without deleting its business history. Inactive or
soft-deleted users cannot sign in. Deactivation refreshes the account's session-validity state, and
the Identity host is configured to revalidate existing cookie sessions at least every five minutes;
the live-cookie timing journey remains Phase 3 acceptance coverage.

**Why this priority**: The cutover must not allow disabled users to bypass business account state
through the authentication system.

**Independent Test**: Attempt login with active, inactive, and soft-deleted users, then deactivate a
user and verify the session-validity state changes atomically, capabilities/history are preserved,
and the configured cookie validation interval is exactly five minutes.

**Acceptance Scenarios**:

1. **Given** an active, non-deleted user with valid credentials, **When** login is attempted,
   **Then** normal login behavior is preserved.
2. **Given** an inactive user, **When** login is attempted, **Then** access is denied even when the
   credentials are valid.
3. **Given** a soft-deleted user, **When** login is attempted, **Then** access is denied even when
   the credentials are valid.
4. **Given** an active user, **When** the account is deactivated, **Then** the session-validity state
   changes atomically and the host's five-minute validation configuration is retained so the Phase 3
   live-cookie journey can verify the end-to-end rejection timing.

---

### User Story 4 - Preserve Existing Customer and Delivery Behavior (Priority: P1)

Existing customer journeys continue to use the same business vocabulary and response contracts
while the platform internally resolves the unified user's integer identifier and Customer
capability. Delivery assignment continues to enforce one active delivery per available agent.

**Why this priority**: The persistence cutover is successful only if it removes duplicate user
models without changing the already-published customer behavior.

**Independent Test**: Run the existing customer and delivery behavior suites without changing their
401, 404, 409, `ProfileNotCreated`, ownership, cart, order, checkout, or agent-state expectations.

**Acceptance Scenarios**:

1. **Given** an authenticated user with Customer capability, **When** profile, address, cart,
   order, or checkout behavior is used, **Then** the operation targets that user's unified integer
   identifier and preserves the existing response contract.
2. **Given** an authenticated user without Customer capability, **When** a customer-only operation
   is attempted, **Then** the existing `ProfileNotCreated` 404/409 contract is returned unchanged.
3. **Given** a stale token whose role claims do not reflect current capability state, **When** a
   customer operation is attempted, **Then** current persisted capability state decides access.
4. **Given** a delivery assignment, **When** an approved available agent is reserved or released,
   **Then** the existing busy/available state rules and assigned-agent identifier behavior remain
   unchanged.
5. **Given** a user identifier that cannot be interpreted as a positive integer, **When** current
   identity is resolved, **Then** the request is treated as unauthenticated rather than resolving a
   different user.

---

### User Story 5 - Rebuild the Disposable Development Store Safely (Priority: P2)

A maintainer can replace the disposable development database with one clean unified-user schema.
The destructive rebuild proceeds only after the Phase 2 code and tests are green, the worktree is
checkpointed, and both configured connections match the explicitly authorized local development
database.

**Why this priority**: A clean rebuild removes obsolete tables and constraints, but its destructive
nature requires an explicit safety boundary.

**Independent Test**: Verify the two authorized local connection settings, rebuild the development
store, and inspect the resulting tables, relationships, constraints, indexes, roles, and schema
history.

**Acceptance Scenarios**:

1. **Given** either configured connection differs from the authorized local Talabat database,
   **When** the rebuild is requested, **Then** the destructive operation stops before any database
   is dropped or migration history is removed.
2. **Given** a clean checkpoint, green build and tests, and both authorized connections, **When**
   the rebuild completes, **Then** the database contains one unified user store, unified user
   addresses, retained cart/order/delivery relationships, and no legacy customer or delivery-agent
   profile tables.
3. **Given** the rebuilt database, **When** startup seeding runs more than once, **Then** exactly the
   four approved roles exist with no duplicate roles and no seeded users.

### Edge Cases

- A caller supplies or guesses a role name during registration or capability change; the value is
  ignored/rejected because roles are selected only by server-owned workflows.
- An applicant attempts to approve or reject their own delivery-agent application; no public
  self-approval operation exists, and only the admin-controlled service workflow may decide it.
- Customer onboarding is requested for a delivery agent; the Customer capability is added to the
  existing user and the DeliveryAgent capability, application, status, and history are preserved.
- An unapproved delivery applicant is queried as an available agent; the applicant is excluded
  because no agent operational state exists before approval.
- A capability mutation succeeds but its role mutation fails; the entire operation rolls back and
  leaves neither partial state nor a new account.
- A role changes while cookie or token sessions already exist; cookie sessions are invalidated by
  account-state refresh, while business capability checks use current stored state rather than
  cached token roles.
- A soft-deleted user is hidden from ordinary account lookup; login denial must still be an explicit
  account-state rule rather than relying only on that lookup behavior.
- Two writers update the same user concurrently; the later conflicting write is rejected rather
  than silently overwriting the first. Updates to different address records are allowed to proceed
  independently.
- A default address is soft-deleted while another default is selected; at most one non-deleted
  address remains the default.
- The clean rebuild is pointed at any database other than the explicitly authorized disposable
  local database; the operation must abort.

## Requirements *(mandatory)*

### Functional Requirements

#### Unified Account and Capability Workflows

- **FR-001**: The platform MUST persist one unified user record per person for account identity,
  customer profile, addresses, delivery-agent application, delivery-agent operational state,
  activation, audit, deletion, and concurrency information.
- **FR-002**: The platform MUST use database-generated positive integer user identifiers and MUST
  expose that same integer as the authenticated subject identifier.
- **FR-003**: Customer registration MUST accept email, password, full name, positive age, and an
  optional phone number; use the email as both email and account sign-in name; create one active
  user; initialize the supplied customer profile; and grant both the Customer capability and
  Customer authorization role without accepting a caller-supplied role name.
- **FR-004**: Delivery-agent applicant registration MUST accept email, password, full name, one of
  Bike, Motorcycle, or Car, and an optional phone number; use the email as both email and account
  sign-in name; create one active user and a pending vehicle application; and MUST NOT grant
  DeliveryAgent capability, role, or operational status before approval.
- **FR-005**: Only an admin-controlled service-level decision accepting the target user's integer
  identifier MAY approve a delivery-agent application. Approving a pending application MUST grant
  the DeliveryAgent capability and role together and initialize the agent in the Offline state.
- **FR-006**: Only an admin-controlled service-level decision accepting the target user's integer
  identifier MAY reject a delivery-agent application. Rejecting a pending application MUST leave
  the user without DeliveryAgent capability, role, or operational status.
- **FR-007**: Existing-account customer onboarding MUST grant Customer capability and role to the
  identified existing user while preserving all other capabilities and state.
- **FR-008**: Existing-account customer onboarding MUST return the existing
  `ProfileAlreadyExists` conflict when Customer capability is already present.
- **FR-009**: Registration with an already-used normalized email/sign-in name MUST return a
  validation failure and MUST persist no partial account, profile, capability, or role state.
- **FR-010**: Approval or rejection outside the PendingApproval state MUST return a conflict and
  MUST leave the user unchanged.
- **FR-011**: Every registration, capability grant, approval, rejection, and deactivation workflow
  MUST be atomic; any account, persistence, or role failure MUST roll back all changes from that
  workflow.
- **FR-012**: Only server-owned capability workflows MAY change capability flags or authorization
  roles; business handlers and transport endpoints MUST NOT perform those mutations independently.
- **FR-013**: The platform MUST NOT provide self-registration for Admin or RestaurantOwner during
  this phase.
- **FR-014**: Capability revocation MUST NOT be introduced; account deactivation and soft deletion
  remain the supported disablement mechanisms.

#### Authorization and Session Behavior

- **FR-015**: Login MUST reject any inactive or soft-deleted user even when credentials are valid.
- **FR-016**: Deactivation MUST preserve business history and capabilities, prevent new login,
  refresh the account's session-validity state atomically, and retain an exact five-minute cookie
  validation interval. End-to-end elapsed-time proof with an already-issued cookie is Phase 3.
- **FR-017**: Any role change MUST refresh the user's session-validity state so cached cookie role
  claims cannot remain valid indefinitely.
- **FR-018**: Customer authorization MUST use current persisted Customer capability state rather
  than trusting cached token roles.
- **FR-019**: Current-user resolution MUST interpret the authenticated subject as an integer user
  identifier and MUST treat a missing or malformed subject as unauthenticated.
- **FR-020**: The current-user contract MUST expose authentication state, the unified user ID,
  whether Customer capability is present, and a CustomerId equal to the user ID only when that
  capability is present.
- **FR-021**: Exactly four roles MUST be seeded idempotently: Customer, DeliveryAgent, Admin, and
  RestaurantOwner. No users or fixed passwords may be seeded.

#### Compatibility and Business Behavior

- **FR-022**: The prior generic registration operation MUST be replaced by distinct customer and
  delivery-agent applicant registration operations; login and logout request/response shapes MUST
  remain unchanged, and the current-user response MUST use the integer user ID.
- **FR-023**: Existing customer-facing 401, 404, 409, and `ProfileNotCreated` response bodies and
  meanings MUST remain byte-for-byte unchanged.
- **FR-024**: Customer profile, address, cart, order, checkout, and delivery operations MUST use the
  unified user while preserving the business names `CustomerId`, `AssignedAgentId`,
  `CustomerProfile`, and related customer-facing data contracts.
- **FR-025**: Customer-only behavior MUST require Customer capability on the current unified user.
- **FR-026**: Available-agent queries MUST return only approved users whose operational state is
  Available, in stable full-name order, and MUST exclude applicants and inactive operational
  states.
- **FR-027**: Delivery assignment, completion, and cancellation MUST preserve the existing agent
  availability, busy-state, identity, and one-active-delivery rules.
- **FR-028**: Ordinary user loads MUST exclude soft-deleted users, while audit stamping and retained
  soft-deletion behavior MUST apply to the unified user and its persisted changes.

#### Persistence Integrity and Concurrency

- **FR-029**: A persisted customer age MUST be absent or greater than zero.
- **FR-030**: Persisted vehicle type MUST be absent or Bike, Motorcycle, or Car; delivery-agent
  status MUST be absent or Offline, Available, Busy, or Suspended; approval status MUST be absent or
  PendingApproval, Approved, or Rejected; and capability flags MUST contain only Customer,
  DeliveryAgent, Admin, and RestaurantOwner bits.
- **FR-031**: Persisted current location latitude and longitude MUST either both be absent or both be
  present, with latitude from -90 through 90 and longitude from -180 through 180.
- **FR-032**: A unified user's full name MUST be present and MUST NOT exceed 200 characters.
- **FR-033**: Each user MUST have at most one non-deleted default address.
- **FR-034**: User changes from account workflows and business workflows MUST participate in the
  same audit, soft-deletion, and persistence rules.
- **FR-035**: Concurrent writes to the same user MUST detect stale state and return a conflict rather
  than lose an accepted update.
- **FR-036**: A concurrency conflict MUST map to the existing transport-neutral conflict category
  and the standard HTTP 409 problem response at web boundaries.
- **FR-037**: Updates that affect different address records without changing the user record MAY
  complete independently and are not required to conflict.
- **FR-038**: Cart, order, and delivery customer references and delivery assigned-agent references
  MUST point to the unified user without renaming their business-facing identifier properties.

#### Cutover and Development Rebuild

- **FR-039**: The separate legacy customer, customer-address, delivery-agent, account-linkage, and
  their dedicated repository concepts MUST cease to be active production models after cutover.
- **FR-040**: The rebuilt development schema MUST contain no legacy Customers or DeliveryAgents
  tables and MUST preserve cart, order, delivery, user-address, audit, and relationship integrity
  against the unified user store.
- **FR-041**: The rebuilt schema MUST enforce all eight documented unified-user checks and the
  unique non-deleted default-address rule.
- **FR-042**: Development rebuild history MUST be replaced by one clean initial unified-user schema
  history entry; preserving existing development data is not required.
- **FR-043**: The destructive rebuild MUST NOT start until Phase 1 is accepted and committed, the
  working tree is clean, the Phase 2 solution build and tests are green, and both configured
  connections match the explicitly authorized local Talabat database.
- **FR-044**: If any rebuild safety precondition fails, the operation MUST stop before dropping a
  database or removing migration history.
- **FR-045**: The Delivery API MUST remain a compiling scaffold; this phase MUST NOT introduce its
  full business API.

### Key Entities

- **Unified User**: The one person/account record, identified by a generated integer, that owns
  identity data, activation and deletion state, customer data, delivery-agent application and
  operational data, capability flags, audit metadata, and concurrency state.
- **User Address**: A child record owned by one unified user, with normalized address details,
  soft-deletion state, and a default marker constrained to at most one active default per user.
- **Capability**: A combinable business classification on the unified user: Customer,
  DeliveryAgent, Admin, or RestaurantOwner. It is the business source of truth.
- **Authorization Role**: The authorization projection of a capability. It must be synchronized by
  the server-owned workflow and is never selected directly by a caller.
- **Delivery-Agent Application**: The user's Bike, Motorcycle, or Car selection and approval state
  before agent capability is granted; pending and rejected applicants have no operational agent
  status and cannot decide their own applications.
- **Business Ownership Reference**: Existing cart, order, and delivery identifiers named
  `CustomerId` or `AssignedAgentId` that now reference the unified user while retaining their
  business meaning.
- **Account Session State**: The information used to reject inactive/deleted accounts and invalidate
  cookie sessions after account or role changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In every registration and onboarding acceptance test, one person is represented by
  exactly one positive integer user identifier, including when one user holds both Customer and
  DeliveryAgent capabilities.
- **SC-002**: 100% of successful customer registrations persist both the Customer capability and
  Customer role, while 100% of new delivery-agent applicants persist neither agent capability nor
  agent role before approval.
- **SC-003**: 100% of tested workflow failures leave zero partial account, profile, capability, role,
  or approval changes.
- **SC-004**: Inactive and soft-deleted users are rejected in 100% of Phase 2 login tests; 100% of
  deactivation tests preserve capabilities/history while changing session-validity state, and host
  configuration inspection finds an exact five-minute cookie validation interval.
- **SC-005**: 100% of existing customer-facing 401, 404, 409, and `ProfileNotCreated` contract
  assertions pass without response-body changes after the cutover.
- **SC-006**: In concurrent same-user update tests, the stale writer receives a conflict and the
  accepted writer's data is preserved in 100% of runs.
- **SC-007**: Post-rebuild inspection finds exactly four approved roles, zero seeded users, zero
  legacy customer/delivery-agent profile tables, all eight unified-user checks, and one active
  default-address uniqueness rule.
- **SC-008**: Repeating role seeding produces zero duplicate roles or role-count changes.
- **SC-009**: The complete solution build and all four test projects pass, including the migrated
  legacy assertions and the new customer-registration, applicant-approval, and login-rejection
  acceptance tests.
- **SC-010**: Production-code searches return zero obsolete account/profile linkage, legacy user
  aggregate, and retired repository symbols while retaining every approved business name.
- **SC-011**: All six simulated destructive-rebuild mismatches—wrong server, wrong catalog, and
  disabled integrated security in each of the two configured connection settings—abort before any
  database or schema-history deletion occurs.
- **SC-012**: Package vulnerability auditing reports zero known vulnerabilities before Phase 2 is
  accepted.

## Assumptions

- Phase 1 must be accepted, committed, and green before any Phase 2 implementation begins. At the
  time this specification is created, Phase 1 tasks T029 and T030 remain open due to the existing
  OpenAPI package vulnerability; specification work does not waive that prerequisite.
- The development database is disposable and contains no data that must be preserved.
- Destructive rebuild authorization applies only when both development connection settings resolve
  to `Server=DESKTOP-5IHGJ9F\SQLEXPRESS;Database=Talabat`; any deviation requires the operation to
  stop.
- Capability revocation is intentionally deferred. Deactivation and soft deletion preserve
  historical relationships rather than removing capabilities.
- Customer and delivery business identifiers remain integers and already enforce positive values.
- Role/capability rollback failure-injection proof and the full multi-role end-to-end journey receive
  additional coverage in Phase 3; Phase 2 still guarantees the atomic behavior they will test.
- The existing Customer API response contract is normative; production behavior must adapt to it,
  not vice versa.
- Existing account credential, password, normalization, and lockout policies remain authoritative;
  this phase changes the account entity and workflows, not those policies.
- Existing operational baselines for performance, scale, availability, observability, rate
  limiting, accessibility, localization, privacy, and compliance remain unchanged. This phase adds
  no user interface, external service integration, or data import/export contract.

## Out of Scope

- Full Delivery API implementation beyond compile integrity.
- Admin web pages or controllers; delivery-agent approval remains a service-level workflow.
- Self-service Admin or RestaurantOwner registration.
- Capability revocation for users with existing orders or deliveries.
- Interactive identity clients, redirect-based user interface, final token/scope design, production
  signing-key management, or JWT revocation.
- Customer API role-claim wiring, expanded ownership hardening, capability/role drift
  failure-injection tests, session-invalidation end-to-end tests, and final governance updates owned
  by Phase 3.
- Product discounts, employee offers, frontend work, deployment, or CI/CD changes.
- Data-preserving migration of any existing development database.
- Seeded users, fixed passwords, or production database rebuilds.
