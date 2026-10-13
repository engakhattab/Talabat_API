# Feature Specification: Phase 10 Remediation

**Feature Branch**: `feature/user-aggregate-refactor`
**Created**: 2026-07-27
**Status**: Draft
**Input**: User description: "Remediation of 8 critical blockers and consistency issues found during architectural and security audit of Phase 10 (Authorization Strategy And Quality Gates). Separate spec-kit feature, not an amendment to Phase 10."

## Clarifications

_(No clarifications yet — awaiting `/speckit.clarify` pass)_

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As a **platform operator**, I want the 8 critical blockers identified in the Phase 10 audit remediated — JWT claim mapping fixed, delivery lifecycle completed, agent approval reachable, concurrency hardened, identity host secured, and CI gates functional — so that authorization is enforceable, the delivery workflow is end-to-end operational, and quality gates actually prevent regressions.

As a **delivery agent**, I want my pending-deliveries list to require real agent capability (not just authentication), to see minimal PII before assignment, and to be protected from race conditions when claiming deliveries — so that my identity and work are secure.

As a **customer**, I want delivery creation after checkout to succeed reliably (or fail gracefully without invalidating my order), so that the end-to-end purchase flow is complete.

### Acceptance Scenarios

1. **Given** a real JWT with role `Customer`, scope `customer.api`, and audience `talabat.customer.api`, **When** it reaches a Customer API protected endpoint, **Then** the request succeeds (200+) — the `MapInboundClaims = false` fix ensures `RequireRole("Customer")` works.
2. **Given** a real JWT with role `DeliveryAgent` and correct audience, **When** it reaches a Delivery API protected endpoint, **Then** the request succeeds.
3. **Given** a real JWT with role `Customer` but audience `talabat.delivery.api`, **When** it reaches a Customer API endpoint, **Then** the system returns `401 Unauthorized`.
4. **Given** a real JWT with correct audience but missing scope, **When** it reaches a protected endpoint, **Then** the system returns `403 Forbidden`.
5. **Given** a real JWT with correct audience and scope but wrong role, **When** it reaches a protected endpoint, **Then** the system returns `403 Forbidden`.
6. **Given** an expired real JWT, **When** it reaches a protected endpoint, **Then** the system returns `401 Unauthorized`.
7. **Given** no `Authorization` header, **When** it reaches a protected endpoint, **Then** the system returns `401 Unauthorized`.
8. **Given** a token with `DeliveryAgent` role but the user's `UserType` lacks the `DeliveryAgent` flag, **When** `GET /api/agent/deliveries/pending` is called, **Then** the system returns `403 Forbidden` (capability check, not just authentication).
9. **Given** an authenticated delivery agent, **When** the pending deliveries list is returned, **Then** the DTO contains no `Street`, `BuildingNumber`, `Floor`, or `CustomerId` fields.
10. **Given** a successful checkout, **When** the checkout completes, **Then** a `Delivery` row exists with status `PendingAssignment` — even if delivery creation fails, checkout still returns `200` (BR-DEL-016).
11. **Given** a delivery agent applicant registered as `PendingApproval`, **When** an admin approves them (Development only), **Then** `UserType.DeliveryAgent` is set, role `DeliveryAgent` assigned, and `DeliveryAgentStatus == Offline`.
12. **Given** two agents concurrently claiming the same `PendingAssignment` delivery, **When** both `SaveChanges` execute, **Then** exactly one succeeds (200) and one gets `409 Conflict`.
13. **Given** the CI pipeline runs, **When** any test project fails or a vulnerable package is found, **Then** the pipeline fails.
14. **Given** the `Domain` project is inspected via architecture tests, **When** prohibited packages are checked, **Then** zero prohibited references exist (EF Core, ASP.NET Core, Duende).
15. **Given** the `Application` project is inspected via architecture tests, **When** prohibited packages are checked, **Then** zero prohibited references exist (EF Core, HttpContext, ClaimsPrincipal, Duende).

### Edge Cases

- Delivery creation failure after committed checkout: order remains valid, checkout returns success, failure is logged at Error with `OrderId`. Delivery is created later by retry/reconciliation.
- Duplicate delivery creation for same order: returns `Conflict` / `DeliveryAlreadyExists` via pre-check and unique index.
- Approve an already-approved user: returns `409`.
- Approve a non-existent user: returns `404`.
- Agent approval endpoints return `404` outside Development environment (no Admin policy yet).
- `UpdateLocationCommand` must not accept `agentId` from request body — BOLA trap. Agent id comes from token only.
- Two agents reading the same `PendingAssignment` row concurrently: second `SaveChanges` throws concurrency exception → `409`.
- A user who gains capability mid-session still holds old token — must refresh; system does not weaken policy.
- Profile creation endpoint (`POST /api/me/profile`) uses scope-only policy (no role requirement) to avoid deadlock for new users.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-R001**: Both API hosts set `MapInboundClaims = false` on `JwtBearerOptions` so inbound `role` claims are not rewritten to `ClaimTypes.Role`, ensuring `RequireRole("Customer")` and `RequireRole("DeliveryAgent")` work with real tokens.
- **FR-R002**: `GetPendingDeliveriesHandler` checks `HasDeliveryAgentCapability` (not just `IsAuthenticated`). The returned DTO omits `Street`, `BuildingNumber`, `Floor`, and `CustomerId` to reduce pre-assignment PII exposure.
- **FR-R003**: A `CreateDeliveryForOrder` use case creates a `Delivery` in `PendingAssignment` status after successful checkout. Checkout is fire-and-forget: delivery creation failure does not invalidate the committed order (BR-DEL-016).
- **FR-R004**: Agent approval endpoints (`POST /account/delivery-agents/{userId}/approve` and `/reject`) are exposed, gated behind `IHostEnvironment.IsDevelopment()` (returns 404 outside Development). TODO comment references future `AdminAccess` policy.
- **FR-R005**: `Delivery` gains a `byte[] RowVersion` property with `IsRowVersion()` mapping, mirroring `User.RowVersion`. Concurrent assignment attempts yield exactly one success and one `409 Conflict`.
- **FR-R006**: Identity host hardening: developer signing credential gated behind `IsDevelopment()` (throw in non-Development), Postman redirect URIs added, token lifetimes corrected (15 min access, sliding refresh), profile service uses `AddRequestedClaims` not `IssuedClaims.AddRange`, duplicate `role` claim entries removed.
- **FR-R007**: CI quality gates become functional: SQL Server service container, connection string via environment variable, vulnerability check fails on findings, fixtures use `Database.Migrate()` instead of `EnsureCreated()`.
- **FR-R008**: Ownership convergence: six self-resolving handlers converted to controller-injection pattern. `ProfileEnforcementFilter` replaced by `[RequireCustomerProfile]` attribute. `ICurrentUserCapabilityResolver` added. Delivery API gets `DomainExceptionHandler` + `/health`. CORS driven from configuration. OpenAPI OAuth2 security scheme added. Architecture tests strengthened (`.csproj` PackageReference checks). `User.SetPhoneNumber` encapsulation fix. `ISystemClock` replaced with `TimeProvider`.

### Key Entities

- **Delivery**: Aggregate root gains `RowVersion` (`byte[]`, private setter) for optimistic concurrency. No new entities introduced.
- **CreateDeliveryForOrderCommand**: New use case record carrying `OrderId`, `CustomerId`, `RestaurantId`, `DeliveryAddress` — invoked by checkout controller after `CheckoutHandler` succeeds.
- **PendingDeliveryDto**: Reduced shape — omits PII fields (`Street`, `BuildingNumber`, `Floor`, `CustomerId`) pre-assignment.
- **ICurrentUserCapabilityResolver**: New abstraction in Application, implemented in Infrastructure, reducing `CurrentUser` to claims + one resolver call.
- **[RequireCustomerProfile]**: Attribute replacing `ProfileEnforcementFilter` path matching.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-R001**: `RealTokenPipelineTests` exist in both API test projects, pass with real `AddJwtBearer` pipeline, and **fail before** the `MapInboundClaims = false` fix — verified by before/after test runs.
- **SC-R002**: `GetPendingDeliveriesHandlerTests` verify: authenticated user without agent capability → `AgentRequired` failure; agent → success; DTO has no PII fields. API test: token with `DeliveryAgent` role but revoked capability → `403`.
- **SC-R003**: `CreateDeliveryForOrderHandlerTests` verify: creates `PendingAssignment` delivery with order data; duplicate call → `Conflict`. `CheckoutEndpointTests` verify: delivery exists after checkout; checkout returns `200` when delivery creation throws.
- **SC-R004**: `AgentApprovalEndpointTests` verify: register → approve → role assigned, status `Offline`, security stamp changed; already-approved → 409; non-existent → 404; outside Development → 404.
- **SC-R005**: `DeliveryPersistenceTests` verify concurrent assignment yields one success and one concurrency exception. `AssignmentConcurrencyTests` verify `POST {id}/assign` from two agents yields one `200` and one `409`.
- **SC-R006**: Token claim tests verify: dual-capability user gets scope-appropriate claims; Postman URIs present; `UpdateAccessTokenClaimsOnRefresh == true`.
- **SC-R007**: CI pipeline green on clean run with all seven test projects reported as executed.
- **SC-R008**: All test projects pass, architecture tests pass with no new exemptions, zero vulnerable packages, zero new warnings.

## Assumptions

- All 8 blockers trace to existing documentation (`delivery-business-rules.md`, `PROJECT_IMPLEMENTATION_ROADMAP.md`, Phase 9 and 10 plans). No new requirements are invented.
- Layer discipline is non-negotiable: Domain gains no framework dependencies; Application references no EF Core, ASP.NET Core, or Identity; ownership/authorization decisions live in Application use cases and API policies.
- One commit per phase using Conventional Commits. No commit with a failing gate.
- Phase 10's existing `spec.md`, `plan.md`, and `tasks.md` are not modified except for the D1 cross-reference edit in `tasks.md`. All 58 completed tasks remain marked complete — the gap is verification strength, not execution.
- The `Microsoft.Extensions.Identity.Stores` reference in `Talabat.Domain` remains a documented exception.
- No `Admin`, `DeliveryOperations`, or `RestaurantOwner` role, policy, scope, or claim is added.
- Production signing-key management, Key Vault, and certificate rotation are deferred.
- Transactional outbox and reconciliation job are recorded as deferred in Phase 0e.

## Out of Scope

- Decoupling `User` from `IdentityUser<int>` (full refactor needs its own spec).
- Transactional outbox / domain-event dispatch (deferred).
- Reconciliation job for orders missing a delivery (deferred).
- Production signing-key management, Key Vault, certificate rotation.
- Email confirmation, password reset, lockout policy, 2FA, external login.
- Any `Admin`, `DeliveryOperations`, or `RestaurantOwner` role, policy, scope, or claim.
- Changing `ApplicationErrorCategory.OwnershipMismatch → 403` mapping (documentation question only).
- Payment, notifications, coupons, reviews, admin dashboards, GPS/maps, nearest-agent optimization.
- Angular SPA client code.
