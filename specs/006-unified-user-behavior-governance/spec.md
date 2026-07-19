# Feature Specification: Unified User Behavior and Governance

**Feature Branch**: `feature/user-aggregate-refactor`  
**Created**: 2026-07-19  
**Status**: Draft  
**Input**: User description: "Phase 3 from user-aggregate-refactor-plan.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Use One Account as Delivery Agent and Customer (Priority: P1)

An approved delivery agent can enter the customer journey with the same account, add the Customer
capability, and shop without losing delivery-agent identity, authorization, application state, or
operational history.

**Why this priority**: The refactor's central business promise is one person with multiple
capabilities, rather than separate accounts or profiles for each role.

**Independent Test**: Register a delivery-agent applicant, approve the application, authenticate on
the customer site, create the customer profile, and complete browse, cart, and checkout behavior.
Verify that one user identifier is retained throughout and that both Customer and DeliveryAgent
capabilities and authorization roles are present at the end.

**Acceptance Scenarios**:

1. **Given** a registered delivery-agent applicant, **When** the application is approved, **Then**
   the same user receives the DeliveryAgent capability and role and begins in the Offline state.
2. **Given** an approved delivery agent authenticated on the customer site, **When** the user
   creates a customer profile, **Then** Customer capability and role are added to that same user
   without creating another account or changing the user's identifier.
3. **Given** a user holding Customer and DeliveryAgent capabilities, **When** the user browses,
   manages a cart, and checks out, **Then** customer behavior succeeds while the DeliveryAgent
   capability, role, application state, and operational history remain intact.
4. **Given** a multi-role user with an authenticated principal, **When** authorization information
   is inspected, **Then** both applicable roles are represented; no employee discount or special
   offer is applied in this feature.

---

### User Story 2 - Keep Customer Operations Owner-Scoped (Priority: P1)

An authenticated customer can operate only on their own profile, addresses, cart, and orders. The
platform determines the owner from the authenticated user's current Customer capability and never
trusts a customer identifier supplied by a caller.

**Why this priority**: Ownership hardening prevents horizontal access while preserving the
published customer contract and avoiding disclosure that another user's resource exists.

**Independent Test**: Exercise every owner-scoped customer operation as user A while user B owns a
cart, address, and order. Verify that user A's own operations work, user B's cart is never selected,
resource-ID access to user B's address or order returns the established not-found response, and no
route or request body can override the authenticated owner.

**Acceptance Scenarios**:

1. **Given** an authenticated user with Customer capability, **When** the user performs any
   `/api/me/*` profile, address, cart, order, or checkout operation, **Then** the operation is
   resolved exclusively against that authenticated user's Customer identity.
2. **Given** user A while user B owns a cart, address, or order, **When** user A uses the current-cart
   route or supplies user B's address or order identifier, **Then** the cart route selects only user
   A's cart or the established empty-cart result, resource-ID access returns 404, and no information
   about user B's resource is revealed.
3. **Given** a request that supplies a route or body customer identifier different from the
   authenticated user, **When** an owner-scoped operation is processed, **Then** the supplied value
   cannot select or authorize the target customer.
4. **Given** an authenticated user without Customer capability, **When** a customer-only operation
   is attempted, **Then** the existing `ProfileNotCreated` 404/409 response contract is returned
   byte-for-byte unchanged.
5. **Given** an authenticated subject value that is missing, malformed, non-numeric, zero, or
   negative, **When** current-user identity is resolved, **Then** the request is treated as
   unauthenticated and returns 401.
6. **Given** role information that is stale or inconsistent with stored Customer capability,
   **When** a customer operation is attempted, **Then** current stored capability state remains the
   operative business gate.

---

### User Story 3 - Authorize Delivery Assignment at Both Boundaries (Priority: P1)

Only a user who is authorized as a delivery agent and is in the required operational state can be
assigned a delivery. A Customer-only user cannot be assigned merely because the same unified user
type can hold agent data, and an approved but Offline agent is still unavailable.

**Why this priority**: Assignment affects fulfillment and must require both authorization to act as
an agent and valid domain state.

**Independent Test**: Attempt assignment with a Customer-only user, an approved Offline agent, and
an approved Available agent. Verify the first two are rejected for the correct business reason and
the Available agent becomes Busy, then returns to Available after completion or cancellation.

**Acceptance Scenarios**:

1. **Given** a Customer-only user, **When** delivery assignment is attempted, **Then** authorization
   is denied and the domain reports that delivery-agent capability is not initialized.
2. **Given** an approved delivery agent whose status is Offline, **When** assignment is attempted,
   **Then** the assignment is rejected because the agent is not available.
3. **Given** an approved Available delivery agent, **When** a delivery is assigned, **Then** the
   delivery references that user's agent identifier and the agent becomes Busy.
4. **Given** a Busy agent with an active delivery, **When** the delivery is completed or cancelled,
   **Then** the agent returns to Available under the existing transition rules.
5. **Given** a user whose role claim suggests DeliveryAgent but whose current capability or status
   does not permit assignment, **When** assignment is attempted, **Then** the domain guard rejects
   the operation.

---

### User Story 4 - Fail Safely Under Drift, Concurrency, and Account Blocking (Priority: P1)

Capability changes, simultaneous edits, and account disablement fail closed. A role-processing
failure cannot leave capability and authorization out of sync, concurrent writers cannot silently
overwrite accepted user changes, and blocked accounts cannot keep using previously issued sessions.

**Why this priority**: These are the failure paths most likely to undermine authorization or corrupt
the unified user after the happy-path cutover succeeds.

**Independent Test**: Verify the role projection after every capability workflow; force approval to
fail because its required role is unavailable; perform conflicting writes to one user; deactivate
and soft-delete accounts with valid credentials or existing sessions; and observe only atomic,
conflict-safe, access-denied outcomes.

**Acceptance Scenarios**:

1. **Given** any successfully completed capability workflow, **When** the user's capability set and
   authorization roles are compared, **Then** the roles exactly project all and only the user's
   capabilities.
2. **Given** a pending agent application and an unavailable DeliveryAgent role, **When** approval is
   attempted, **Then** approval fails and all flag, role, approval, operational-state, and session
   changes from that attempt are rolled back.
3. **Given** an application that has already been approved, **When** approval is attempted again,
   **Then** the operation is rejected and the user remains unchanged.
4. **Given** a rejected application, **When** the user reapplies with valid vehicle information,
   **Then** the application returns to PendingApproval without receiving agent capability or role.
5. **Given** two actors who loaded the same user state, **When** both attempt conflicting user-row
   updates, **Then** the first accepted update remains and the stale update receives the standard
   409 problem response without overwriting data.
6. **Given** an active authenticated session, **When** the account is deactivated, **Then** new
   login is denied immediately and the existing session is rejected no later than the next
   configured session-validity check.
7. **Given** a soft-deleted user with valid credentials or an existing session, **When** access is
   attempted, **Then** authentication is rejected while retained business history remains intact.

---

### User Story 5 - Close the Refactor with Consistent Governance (Priority: P2)

A maintainer can verify the completed refactor from one consistent set of rules. Superseded
customer-architecture documents clearly point to the unified-user design, authorization guidance
matches runtime behavior, obsolete symbols are absent, and the final solution remains healthy.

**Why this priority**: Contradictory guidance can reintroduce the removed split-account model even
when the running implementation is correct.

**Independent Test**: Review the constitution and every named architecture document, run the final
behavior and structural checks, and verify there is one active unified-user account model, one
initial schema history entry, no prohibited package or legacy-symbol reintroduction, and no change
to the Delivery API scaffold beyond compile integrity.

**Acceptance Scenarios**:

1. **Given** the Phase 3 implementation, **When** the project constitution is compared with actual
   behavior, **Then** its unified-user, capability, role, concurrency, and retained-name rules are
   accurate; it is amended only if implementation accepted in Phases 1-2 legitimately diverged.
2. **Given** the older Customer API specification artifacts, **When** a maintainer reads them,
   **Then** each clearly identifies the unified-user work that supersedes its account/profile
   assumptions.
3. **Given** the authorization matrix and architecture guide, **When** a maintainer reads current
   identity and access rules, **Then** they find an integer user subject, current-capability
   enforcement, the four supported roles, the approval flow, and the preserved
   `ProfileNotCreated` meaning.
4. **Given** the completed codebase, **When** final structural and text searches run, **Then** no
   active legacy user aggregate, linkage key, repository, contradictory inheritance rule, or
   unauthorized capability mutation remains.
5. **Given** the final solution, **When** all acceptance checks run, **Then** every existing and new
   behavior check passes, the schema has exactly one initial unified-user history entry, and the
   Delivery API remains an otherwise untouched compiling scaffold.

### Edge Cases

- A user holds both Customer and DeliveryAgent capabilities before customer onboarding is retried;
  the existing customer-profile conflict is returned and neither capability is removed.
- A capability workflow succeeds in business state but cannot complete its role projection; no
  partial capability, role, approval status, operational status, or session change is retained.
- The DeliveryAgent role definition is missing during approval; the application remains pending and
  the user remains without the DeliveryAgent capability and role.
- A token contains Customer or DeliveryAgent role information that no longer matches persisted
  capability state; current business state denies operations that the stale role would allow.
- A caller presents a syntactically valid user identifier belonging to another person; it cannot
  override the authenticated owner for any `/api/me/*` operation.
- User A guesses user B's cart or order identifier; the result is indistinguishable from a resource
  that does not exist.
- A subject claim contains whitespace, a decimal, an integer overflow, zero, or a negative value;
  it does not resolve a user and is treated as unauthenticated.
- An approved agent is Offline, Suspended, or already Busy when assignment is attempted; the
  existing availability rules reject the assignment.
- Two writers change the same user while a separate address-only update occurs; user-row conflicts
  are rejected, while the Phase 2 accepted independence of different address records is unchanged.
- A deactivated account is reactivated after an old session was invalidated; only a newly valid
  authentication flow may establish access again.
- Documentation still contains historical text that conflicts with the unified-user model but is
  not explicitly marked as superseded; the governance gate fails.

## Requirements *(mandatory)*

### Functional Requirements

#### Multi-Role Journey and Capability Integrity

- **FR-001**: An approved delivery agent MUST be able to add Customer capability to the same user
  account and retain one unchanged positive integer user identifier throughout the journey.
- **FR-002**: Granting Customer capability to an approved delivery agent MUST add the Customer
  authorization role while preserving the DeliveryAgent capability, role, application state,
  operational state, audit history, and owned business data.
- **FR-003**: A multi-role Customer and DeliveryAgent user MUST be able to complete existing browse,
  cart, order, and checkout behavior without creating a second account or profile linkage record.
- **FR-004**: A caller MUST NOT select a role name during onboarding or capability changes, and only
  the approved server-owned capability workflows MAY change capabilities or their role projection.
- **FR-005**: After every successful capability workflow, authorization roles MUST exactly equal
  the defined role projection of all capability flags on the user.
- **FR-006**: Any failure during a capability workflow MUST roll back every change made by that
  workflow, including capability, role, approval, operational-state, and session-validity changes.
- **FR-007**: Approval attempted when its DeliveryAgent role definition is unavailable MUST fail
  without granting DeliveryAgent capability or role and MUST leave the application pending.
- **FR-008**: Repeated approval outside PendingApproval MUST be rejected without changing the user;
  reapplication after rejection MUST return the application to PendingApproval without granting
  agent capability, role, or operational status.

#### Customer Identity, Authorization, and Ownership

- **FR-009**: Customer-facing authentication MUST recognize the platform's role claim so all roles
  belonging to a multi-role user are represented in the authenticated principal.
- **FR-010**: Current persisted Customer capability MUST remain the operative business gate for
  customer behavior; a role claim alone MUST NOT grant customer access.
- **FR-011**: Every owner-scoped `/api/me/*` operation MUST derive its target Customer identity only
  from the authenticated current user and MUST NOT accept a route or request-body customer
  identifier as an ownership authority.
- **FR-012**: Profile, address, cart, order, and checkout operations MUST act only on resources owned
  by the authenticated Customer identity.
- **FR-013**: Cross-user address and order identifiers MUST return 404 rather than 403. The current
  cart route MUST expose only the authenticated customer's cart or its established empty-cart result
  because it accepts no cart/customer identifier. No owner-scoped response may disclose another
  user's resource.
- **FR-014**: A missing, non-integer, zero, negative, or out-of-range authenticated subject MUST be
  treated as unauthenticated and MUST return 401.
- **FR-015**: A user without Customer capability MUST continue to receive the existing
  `ProfileNotCreated` 404/409 status, payload, and meaning byte-for-byte unchanged for customer-only
  behavior.
- **FR-016**: Existing customer-facing authentication enforcement and profile, address, cart,
  order, and checkout response contracts MUST remain unchanged except for the explicitly defined
  ownership hardening.

#### Delivery Assignment Authorization

- **FR-017**: The in-scope delivery assignment domain service MUST require delivery-agent capability
  and operational-state validation. Any Application assignment entry point introduced in a future
  phase MUST additionally require DeliveryAgent authorization before invoking that service; Phase 3
  does not introduce a Delivery API or Application assignment handler and MUST record this boundary
  explicitly.
- **FR-018**: A Customer-only user MUST NOT be assignable to a delivery and MUST fail the existing
  delivery-agent-initialization guard.
- **FR-019**: An approved agent who is not Available MUST NOT be assignable and MUST fail the
  existing availability rule.
- **FR-020**: Assigning an Available approved agent MUST make that same user Busy and preserve the
  business-facing assigned-agent identifier.
- **FR-021**: Completing or cancelling a Busy agent's active delivery MUST return that agent to
  Available under the established state-transition rules.

#### Concurrency and Account Session Safety

- **FR-022**: Conflicting writes based on stale state for the same user MUST reject the stale write,
  preserve the first accepted update, and return the standard HTTP 409 problem response at customer
  web boundaries.
- **FR-023**: Concurrent changes to different address records MUST retain the independence accepted
  in Phase 2 and are not required to conflict when no user-row state is changed.
- **FR-024**: Inactive and soft-deleted users MUST be rejected at login even when their credentials
  are valid.
- **FR-025**: Deactivation, soft deletion, and relevant role changes MUST invalidate previously
  issued cookie sessions no later than the next configured session-validity check, whose maximum
  interval remains five minutes.
- **FR-026**: Account blocking and session invalidation MUST preserve business history and MUST NOT
  revoke or rewrite the user's capability history.

#### Governance and Final Acceptance

- **FR-027**: The project constitution MUST be verified against the accepted Phase 1-2
  implementation and MUST be amended only when an accepted implementation detail differs from its
  current unified-user, role, concurrency, or naming rules.
- **FR-028**: The prior Customer API specification, plan, and data model MUST each be clearly marked
  as superseded where their account/profile assumptions conflict with the unified-user design.
- **FR-029**: Current authorization guidance MUST document the integer unified-user subject,
  current-capability enforcement, CustomerId business naming, the Customer, DeliveryAgent, Admin,
  and RestaurantOwner role set, delivery-agent approval, and the missing-Customer-capability meaning
  of `ProfileNotCreated`.
- **FR-030**: Current architecture guidance MUST identify the unified-user work as superseding the
  former split account/profile model and MUST contain no active contradictory inheritance or linkage
  rule.
- **FR-031**: Final validation MUST prove the multi-role journey, exact capability/role projection,
  rollback on projection failure, assignment authorization, concurrency conflict, ownership
  isolation, malformed-subject rejection, login blocking, and existing-session invalidation.
- **FR-032**: Final validation MUST find no active production use of the removed separate account,
  Customer aggregate, DeliveryAgent aggregate, dedicated customer/agent repositories, or identity
  linkage key; retained business terms such as `CustomerId`, `AssignedAgentId`, and
  `CustomerProfile` MUST NOT be treated as violations.
- **FR-033**: The final persisted schema history MUST contain exactly one initial unified-user entry
  and MUST introduce no Phase 3 schema change.
- **FR-034**: The Domain layer MUST retain only its single approved identity-store dependency and
  MUST remain free of account-management, persistence, and web concerns.
- **FR-035**: The full solution and all four existing test suites, including the six Phase 3
  business-behavior groups, MUST pass before this feature is accepted.
- **FR-036**: The Delivery API MUST remain an otherwise untouched compiling scaffold; this feature
  MUST NOT implement its full API.

### Key Entities

- **Unified User**: The single person and account, identified by one integer, that may hold several
  capabilities while retaining activation, deletion, audit, application, and operational state.
- **Capability Set**: The combinable Customer, DeliveryAgent, Admin, and RestaurantOwner business
  classifications that define what the unified user currently is allowed to do.
- **Authorization Role Projection**: The role representation of the capability set used by host
  authorization. It must match the capability set after every successful workflow but is not a
  substitute for checking current business state.
- **Current Customer Identity**: The authenticated unified-user identifier exposed as CustomerId
  only while Customer capability is currently present. It is the sole ownership authority for
  `/api/me/*` behavior.
- **Delivery-Agent Operational State**: The approved agent's Offline, Available, Busy, or Suspended
  state that combines with authorization to determine assignment eligibility.
- **Authenticated Session**: Previously issued account access whose validity changes when an
  account is deactivated, deleted, or has authorization-affecting state refreshed.
- **Owner-Scoped Resource**: A customer profile, address, cart, order, or checkout operation whose
  visible owner is always the authenticated current customer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of the approved-agent-to-customer journey, one unchanged positive integer user
  identifier is observed from applicant registration through customer checkout, with both Customer
  and DeliveryAgent capabilities and roles present at completion.
- **SC-002**: Across 100% of capability workflow outcomes, successful workflows end with an exact
  capability-to-role projection and forced role-processing failures retain zero partial changes.
- **SC-003**: Every tested owner-scoped customer operation uses the authenticated Customer identity;
  100% of cross-user address and order identifier attempts return 404, current-cart requests expose
  0 data from another user's cart, and 100% of malformed-subject attempts return 401.
- **SC-004**: Customer-only and non-Available users receive zero successful delivery assignments;
  every tested Available agent becomes Busy on assignment and returns to Available on completion or
  cancellation.
- **SC-005**: In every tested same-user concurrent-write collision, no accepted update is lost and
  the stale customer-facing write receives the standard 409 problem response.
- **SC-006**: Inactive and soft-deleted accounts have a 0% successful new-login rate, and every
  deactivated or soft-deleted account's existing cookie session is rejected by the next validity
  check within five minutes.
- **SC-007**: All three superseded Customer API artifacts and both named current guidance documents
  accurately direct maintainers to the unified-user model, with zero active contradictory rules
  found by the final documentation review.
- **SC-008**: All four existing test suites pass with all six Phase 3 behavior groups included, the
  solution builds successfully, exactly one initial unified-user schema history entry exists, and
  zero prohibited legacy production symbols or extra Domain dependencies are found.
- **SC-009**: Existing `ProfileNotCreated`, authentication, profile, address, cart, order, checkout,
  login, and logout contract checks show 0 unintended response changes.

## Assumptions

- Phase 2 is accepted, committed, and has a clean working tree, green full solution, rebuilt
  disposable development database, synchronized capability workflow, login rejection, and exactly
  one initial unified-user schema history entry before Phase 3 implementation begins.
- Phase 1 and Phase 2 specifications and the governing root plan remain authoritative for aggregate
  invariants, capability workflow behavior, status transitions, response payloads, and retained
  business names.
- CustomerId continues to mean the authenticated user's integer identifier when Customer capability
  is present; it is business vocabulary, not a separate customer record or linkage key.
- Authorization roles are represented in the authenticated principal, but current stored capability
  remains authoritative for business access as a defense against stale claims.
- Existing cookie sessions are checked for refreshed validity at the Phase 2 accepted maximum
  interval of five minutes.
- Returning 404 for cross-owner resources is the established anti-disclosure behavior and is not
  changed to 403 in this feature.
- Phase 3 adds behavioral proof, ownership hardening, and governance updates only; it requires no new
  persisted schema.

## Out of Scope

- Full Delivery API implementation beyond preserving compile integrity and validating existing
  delivery-assignment behavior.
- Admin controllers, admin user interfaces, or a public delivery-agent approval endpoint; approval
  remains an admin-controlled service workflow.
- Capability revocation or data-reassignment policy for users with existing carts, orders, or
  deliveries.
- Employee discounts, delivery-agent offers, or any other behavior based on holding multiple roles.
- Interactive identity clients, redirect user interfaces, final token/scope design, production key
  management, or general token revocation.
- New roles, caller-selected roles, Customer or DeliveryAgent self-approval, or self-registration as
  Admin or RestaurantOwner.
- Reintroducing separate Customer or DeliveryAgent aggregates, a separate account entity, or an
  account/profile linkage identifier.
- New database constraints, data-preserving migrations, production database rebuilds, or additional
  schema history entries.
- Frontend work, deployment, infrastructure hosting, CI/CD changes, discounts, or unrelated roadmap
  features.
