# Feature Specification: Authorization Strategy And Quality Gates

**Feature Branch**: `feature/phase-10-authorization-quality-gates`
**Created**: 2026-07-26
**Status**: Draft
**Input**: User description: "Phase 10 — Authorization Strategy And Quality Gates: enforce scope and role policies on all protected endpoints, harden ownership so no endpoint derives caller identity from request fields, and establish quality gates (domain tests, delivery API tests, architecture tests, authorization integration tests, CI pipeline)."

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As a **delivery agent**, I want assurance that only I can progress my assigned deliveries and that no other agent can hijack my work, while the system enforces that every API call carries the correct authentication token, audience, scope, and role — so that authorization is consistently layered and ownership is never caller-supplied.

As a **customer**, I want assurance that no other customer can read or modify my orders, addresses, or cart, and that the system rejects cross-audience tokens (e.g., a delivery token hitting the customer API) — so that my data remains private and access is strictly controlled.

As a **platform operator**, I want automated quality gates (architecture tests, authorization integration tests, CI pipeline) that prevent regression — so that authorization rules are verified on every commit and no structural violation ships unnoticed.

### Acceptance Scenarios

1. **Given** a request with no authentication token, **When** it reaches any protected endpoint, **Then** the system returns `401 Unauthorized`.
2. **Given** a valid token with audience `talabat.delivery.api`, **When** it reaches a Customer API endpoint, **Then** the system returns `401 Unauthorized` (wrong audience).
3. **Given** a valid token with audience `talabat.customer.api` but missing scope `customer.api`, **When** it reaches a Customer API endpoint, **Then** the system returns `403 Forbidden` (authenticated, missing scope).
4. **Given** a valid token with audience `talabat.customer.api` and scope `customer.api` but role `DeliveryAgent` (not `Customer`), **When** it reaches a Customer API endpoint, **Then** the system returns `403 Forbidden` (authenticated, wrong role).
5. **Given** a valid token with audience `talabat.customer.api`, scope `customer.api`, and role `Customer`, **When** it reaches a Customer API endpoint, **Then** the request succeeds (200/201/204 as appropriate).
6. **Given** a valid delivery token with scope `delivery.api` and role `DeliveryAgent`, **When** it reaches `POST /api/me/profile`, **Then** the request succeeds (profile creation must not require the `Customer` role — otherwise the user can never gain it).
7. **Given** a customer (Customer A), **When** Customer A requests Customer B's order, address, or cart item, **Then** the system returns `404 Not Found` (never `403` — a `403` confirms the resource exists).
8. **Given** a delivery agent (Agent A), **When** Agent A calls a lifecycle endpoint on a delivery assigned to Agent B, **Then** the system returns `404 Not Found`.
9. **Given** a delivery agent (Agent A), **When** Agent A attempts to assign a delivery to a different agent id via the request body, **Then** the system rejects the request (the body field is absent or ignored; agent id comes from the token).
10. **Given** an anonymous request, **When** it reaches `/api/catalog/*` or `/health`, **Then** the request succeeds (200).
11. **Given** an authenticated user with no customer profile, **When** they call `POST /api/me/profile`, **Then** the request succeeds (profile creation endpoint).
12. **Given** an authenticated user with no customer profile, **When** they call `GET /api/me/cart`, **Then** the system returns `409 Conflict` with `ProfileNotCreated` error code.
13. **Given** a valid token, **When** the `Domain` project is inspected via architecture tests, **Then** it references no Entity Framework Core, no ASP.NET Core, and no Duende packages (except the documented `Microsoft.Extensions.Identity.Stores` exception).
14. **Given** a valid token, **When** the `Application` project is inspected via architecture tests, **Then** it references no EF Core, no `HttpContext`, no `ClaimsPrincipal`, and no Duende.
15. **Given** the CI pipeline runs on a push or pull request, **When** any test fails or a vulnerable transitive package is detected, **Then** the pipeline fails.

### Edge Cases

- A user who gains the `Customer` capability mid-session still holds a token without the `Customer` role until it expires (≤1 hour) or is refreshed — the system does not weaken the policy to accommodate this; the client must refresh the token.
- `POST /api/me/profile` is the endpoint that creates the customer capability; the caller does not yet have the `Customer` role. The system uses a scope-only policy (no role requirement) on this endpoint to avoid a deadlock.
- The `CatalogController` and `/health` endpoints remain fully anonymous — no authentication or authorization is applied.
- Delivery agent lifecycle commands (`OutForDelivery`, `ArrivedAtRestaurant`, `PickUpOrder`, `DeliverOrder`, `CancelDelivery`, `FailDelivery`) carry only `DeliveryId` in the request; agent identity is resolved from the token by the controller and injected into the command. Handlers load the delivery scoped to that agent and return not-found when it does not match.
- The `UpdateLocation`, `GoOnline`, and `GoOffline` commands act on the caller's own agent record — agent identity is resolved from `ICurrentUser` in the controller or handler, never from request fields.
- Architecture tests encode the `Microsoft.Extensions.Identity.Stores` reference in `Talabat.Domain` as an explicit allow — any new framework leak still fails the test.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every protected endpoint in both the Customer API and Delivery API enforces four independent authorization gates: (1) authentication (valid token, correct issuer, not expired), (2) audience (token audience matches the target API), (3) policy (required scope AND required role), and (4) ownership (resource belongs to the caller). Each gate is checked independently; no single gate replaces another.
- **FR-002**: A scope enforcement mechanism is implemented in both API hosts so that a token missing the required scope is rejected with `403 Forbidden`, regardless of role.
- **FR-003**: Authorization policies are defined as named constants — `CustomerAccess` (scope `customer.api` + role `Customer`), `CustomerScopeOnly` (scope `customer.api`, no role — used only for `POST /api/me/profile`), and `DeliveryAgentAccess` (scope `delivery.api` + role `DeliveryAgent`) — and applied to controllers via policy attributes.
- **FR-004**: All Customer API controllers except `CatalogController` and `POST /api/me/profile` are annotated with `[Authorize(Policy = "CustomerAccess")]`. `POST /api/me/profile` uses `[Authorize(Policy = "CustomerScopeOnly")]`.
- **FR-005**: All Delivery API controllers are annotated with `[Authorize(Policy = "DeliveryAgentAccess")]`.
- **FR-006**: No endpoint derives caller identity (customer id or agent id) from route parameters, query strings, or request bodies. All identity values are resolved from the authenticated token via `ICurrentUser`.
- **FR-007**: Delivery lifecycle commands (`OutForDelivery`, `PickUpOrder`, `DeliverOrder`, `ArrivedAtRestaurant`, `CancelDelivery`, `FailDelivery`) include the agent id, populated by the controller from `ICurrentUser.AgentId`. Handlers load the delivery scoped to that agent and reject the operation if the delivery is not assigned to the caller.
- **FR-008**: `AssignDelivery` is a self-assignment operation: the agent claims a pending delivery for themselves. The `AgentId` is taken from `ICurrentUser.AgentId`, not from the request body. The body-supplied `AgentId` field is removed.
- **FR-009**: Customer-side handlers are audited to confirm every customer id comes from `ICurrentUser.CustomerId` and repository calls use owner-scoped methods. Any handler accepting a caller-supplied customer id is fixed.
- **FR-010**: Ownership failures return `404 Not Found` (not `403 Forbidden`) to avoid leaking resource existence.
- **FR-011**: A `Talabat.Domain.Tests` project is created with pure unit tests for `User` capability transitions, address rules, `Cart`/`Order`/`Delivery` state machines, and value object validation. Tests run without EF Core or Identity infrastructure beyond the documented exception.
- **FR-012**: A `Talabat.Delivery.API.Tests` project is created, mirroring the `Talabat.Customer.API.Tests` host/fixture pattern, covering the Delivery API authorization matrix.
- **FR-013**: A `Talabat.ArchitectureTests` project is created, asserting that `Talabat.Domain` and `Talabat.Application` maintain clean boundaries (no prohibited package references, no aggregate types referencing `ICurrentUser` or claim/role types, no API project referencing `Talabat.Identity`).
- **FR-014**: Authorization integration tests in both API test projects verify the full matrix: no token → 401, wrong audience → 401, missing scope → 403, wrong role → 403, full valid token → success, anonymous → catalog/health succeeds, cross-customer ownership → 404, cross-agent ownership → 404, agent-to-agent assignment rejection → rejected/absent, profile creation with no role requirement → allowed, profile enforcement → 409.
- **FR-015**: A GitHub Actions CI workflow (`.github/workflows/ci.yml`) runs on push and pull request, executing: restore, build, test with code coverage, and vulnerable transitive package scan (fail on any finding).
- **FR-016**: Documentation deliverables are produced: `docs/authorization-strategy.md` (the four-gate model, status-code policy, naming reconciliation, role inventory, claim-freshness caveat, self-assignment decision, domain-cleanliness statement) and `docs/authorization-endpoint-matrix.md` (one table per host with policy, role, scope, ownership rule, and failure columns). The existing `docs/authorization-matrix.md` is marked superseded.

### Key Entities

- **Authorization Policy**: A named rule combining scope and role requirements applied to a controller or endpoint. Policies are defined as constants and registered in the host's authorization builder.
- **Scope Requirement**: An `IAuthorizationRequirement` that checks for the presence of a specific scope claim in the token. Handles both space-delimited and multi-entry scope claim formats.
- **Ownership Scope**: The principle that a resource load is scoped to the caller's identity (customer id or agent id from the token), so that unauthorized access returns `404` rather than `403`.
- **Delivery Lifecycle Command**: A request to progress a delivery through its state machine (out-for-delivery, arrived, picked-up, delivered, cancelled, failed). After Phase 10, each command carries both `DeliveryId` and `AgentId`.
- **Architecture Rule**: A test assertion verifying that project dependencies respect the layered architecture (Domain depends on no infrastructure; Application depends on no web or persistence packages; API projects depend on no Identity project).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every protected endpoint in both APIs returns `403` when the token is missing the required scope, and `403` when the token has the correct scope but the wrong role — verified by integration tests.
- **SC-002**: No endpoint accepts a customer id or agent id from route, query, or body — verified by architecture or integration tests that mock `ICurrentUser` and assert the correct value is used.
- **SC-003**: A delivery agent cannot read or progress another agent's delivery — verified by integration tests that mint tokens for Agent A and attempt operations on Agent B's delivery.
- **SC-004**: A customer cannot read another customer's order, address, or cart item — verified by integration tests that mint tokens for Customer A and request Customer B's resources.
- **SC-005**: Cross-audience tokens (customer token → Delivery API, delivery token → Customer API) are rejected with `401` — verified by integration tests.
- **SC-006**: The `Domain` project references zero prohibited packages (EF Core, ASP.NET Core, Duende) — verified by architecture tests.
- **SC-007**: The `Application` project references zero prohibited packages (EF Core, HttpContext, ClaimsPrincipal, Duende) — verified by architecture tests.
- **SC-008**: All test projects (`Talabat.Application.Tests`, `Talabat.Customer.API.Tests`, `Talabat.Identity.Tests`, `Talabat.Infrastructure.Tests`, `Talabat.Domain.Tests`, `Talabat.Delivery.API.Tests`, `Talabat.ArchitectureTests`) pass — verified by CI pipeline.
- **SC-009**: The CI pipeline runs on every push and pull request, building, testing, and scanning for vulnerable transitive packages — verified by the workflow file and a green run.
- **SC-010**: Every implemented endpoint appears in `docs/authorization-endpoint-matrix.md` with policy, role, scope, ownership rule, and failure codes — verified by documentation review.

## Assumptions

- The audience names (`talabat.customer.api`, `talabat.delivery.api`) and scope names (`customer.api`, `delivery.api`) shipped in Phase 9 are the source of truth and will not be renamed.
- The `Microsoft.Extensions.Identity.Stores` reference in `Talabat.Domain` is a deliberate, documented exception to the clean-domain rule and will be encoded as an explicit allow in architecture tests.
- The `ProfileEnforcementFilter` in the Customer API remains in place after Phase 10; it is not replaced by the new policies but works alongside them.
- Delivery agent lifecycle commands currently resolve agent identity via `ICurrentUser` inside handlers; Phase 10 changes the pattern so the controller resolves and injects the agent id into the command record, keeping the Application layer free of `ICurrentUser` dependency.
- The self-assignment model for `AssignDelivery` is the correct MVP approach; the `DeliveryOperations` role (operations-assigned model) has no approved use case and is out of scope.
- Test JWTs are minted by test fixtures and trusted only in the Test environment, following the existing pattern in `Talabat.Customer.API.Tests`.
- The CI pipeline uses .NET 10 (matching the project SDK), `XPlat Code Coverage` for coverage collection, and `dotnet list package --vulnerable --include-transitive` for vulnerability scanning.

## Out of Scope

- `Admin`, `DeliveryOperations`, `RestaurantOwner` policies — candidate names only, no approved use cases.
- Payment, notifications, coupons, reviews.
- Angular SPA client code.
- API versioning.
- Rate limiting.
- Production key hardening (signing credential rotation, key vault integration).
- Refresh-token tuning.
- External login, 2FA, password reset.
- Replacing `ProfileEnforcementFilter` with a policy-based mechanism.
- Optimizing `CurrentUser` to read claims from the JWT instead of querying the database (a separate performance concern).
