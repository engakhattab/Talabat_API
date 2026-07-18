# Feature Specification: Talabat Customer API

**Created**: 2026-07-16  
**Status**: Draft  
**Input**: User description: "Phase 7 — expose customer-facing use cases through the dedicated `Talabat.Customer.API` HTTP host"

## Clarifications

### Session 2026-07-16

- Q: Endpoint style — controllers or minimal APIs? → A: Controllers (attribute-routed controller classes grouped by domain area).
- Q: Provisional account-to-Customer resolution strategy? → A: Explicit profile creation on first use — account registration stays account-only; the customer creates their `Customer` profile (full name, positive age, optional phone) via `POST /api/me/profile` on first use, which sets the `IdentityUserId` linkage from the token subject claim. No empty or placeholder profile is ever created; the `Customer` invariants (required name, positive age) are preserved.
- Q: Does Phase 7 ship its own API integration test project? → A: Yes — create `tests/Talabat.Customer.API.Tests` with integration tests for endpoint contracts, auth enforcement, and error mapping.
- Q: CORS policy for future Angular client? → A: Development CORS — add a permissive `localhost`-only CORS policy for local Angular development; production origins deferred.
- Q: Health check endpoint? → A: Basic health — add a `/health` endpoint that reports host status and database connectivity.

## User Scenarios & Testing *(mandatory)*

### Primary User Story

A customer interacts with the Talabat platform through a set of HTTP endpoints that let them browse
restaurants and menus, manage a shopping cart, maintain a personal profile with delivery addresses,
check out an order, and review past orders. The API host is a thin transport layer; every business
decision is delegated to the existing Application use-case handlers and Domain aggregates. The host
validates bearer tokens issued by the `Talabat.Identity` authority so that owner-scoped operations
(cart, profile, addresses, checkout, order history) are tied to the authenticated account.

### Acceptance Scenarios

1. **Given** no authentication is required for catalog browsing, **When** an anonymous caller
   requests the restaurant list or a restaurant menu, **Then** the API returns the catalog data with
   a `200 OK` response.

2. **Given** a customer is authenticated with a valid token, **When** they add an item to their
   cart, **Then** the API delegates to the Application handler and returns the updated cart state
   with the generated cart/item identifiers.

3. **Given** a customer is authenticated, **When** they update their profile or manage addresses,
   **Then** the API persists the change through the Application layer and returns the updated
   profile or address details.

4. **Given** a customer is authenticated and has a cart with available items, **When** they submit
   checkout, **Then** the API delegates to the checkout handler and returns a success result with
   the new order identifier, or a structured error describing unavailable items.

5. **Given** a customer is authenticated, **When** they request their order history or a specific
   order, **Then** the API returns only the orders belonging to that customer.

6. **Given** a protected endpoint receives a request without credentials or with an invalid/expired
   token, **Then** the API responds with `401 Unauthorized`.

7. **Given** a Domain exception is thrown during request processing (e.g., cart conflict, restaurant
   closed, duplicate address), **Then** the API maps it to a standard Problem Details response with
   an appropriate `4xx` status code.

8. **Given** the template `WeatherForecast` endpoint still exists in the renamed project, **When**
   the host starts, **Then** the template code has been removed and is no longer routable.

9. **Given** a customer attempts an ownership-sensitive operation (e.g., viewing another customer's
   orders), **When** the operation is processed, **Then** the API rejects the request or scopes
   the data so that only the authenticated customer's resources are returned.

10. **Given** an authenticated account has no `Customer` profile yet, **When** the account calls
    `POST /api/me/profile` with a valid full name and positive age, **Then** the API creates the
    `Customer` profile, links it to the account via the token subject claim, and returns the created
    profile. **When** the same account instead calls any other owner-scoped endpoint before creating a
    profile, **Then** the API responds with `409 Conflict` and an error code indicating the profile
    must be created first.

11. **Given** the host is running, **When** a caller requests the OpenAPI document, **Then** the API
    returns a complete, browsable specification that describes all available endpoints, parameters,
    request/response shapes, and error responses.

12. **Given** the host is running and the database is reachable, **When** a caller requests
    `/health`, **Then** the API returns a healthy status. **Given** the database is unreachable,
    **Then** the health endpoint reports an unhealthy or degraded status.

### Edge Cases

- A request body is syntactically valid but contains values that violate Domain rules (e.g., zero
  quantity, negative money amount). The API returns a `400 Bad Request` or the specific Domain
  exception mapped to a Problem Details response.
- The Identity authority is temporarily unreachable. Token validation fails, and the API returns
  `401 Unauthorized` rather than a `500 Internal Server Error`.
- A checkout request arrives for a cart whose items have become unavailable since the cart was
  created. The API returns a structured checkout-failure response listing the unavailable items.
- A request targets a resource that does not exist (e.g., unknown restaurant ID, unknown order ID).
  The API returns `404 Not Found`.
- Two concurrent requests attempt to modify the same cart. The Infrastructure concurrency mechanism
  determines the outcome; the API surfaces the resulting error as a Problem Details response.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The existing `Talabat.API` project must be renamed to `Talabat.Customer.API` across
  the project file, namespace root, solution file, assembly name, and all documentation references
  before any business endpoints are added.

- **FR-002**: The template `WeatherForecast` controller and model must be removed from the renamed
  project.

- **FR-003**: An Application-layer DI extension (`AddApplication()`) must be created to register all
  use-case handlers with the service container. The Infrastructure DI extension (`AddInfrastructure()`)
  already exists.

- **FR-004**: A global exception-handling mechanism must map Domain exceptions to RFC 9457 Problem
  Details responses. Each Domain exception type must map to a specific HTTP status code (e.g.,
  `EntityNotFoundException` → 404, `DomainValidationException` → 422 or 400, business-rule conflicts
  → 409).

- **FR-005**: Bearer-token authentication must be configured against the `Talabat.Identity` authority
  using the smallest viable audience and scope contract. The exact audience, scopes, and custom claims
  are intentionally minimal; they will be refined in Phase 9. Phase 7 must document every known
  token/claim limitation per endpoint for Phase 9 follow-up.

- **FR-006**: Catalog endpoints must be publicly accessible (anonymous):
  - Browse restaurants (with optional filtering/pagination).
  - Get a specific restaurant's menu (products).

- **FR-007**: Basket/cart endpoints must require authentication and be scoped to the authenticated
  customer:
  - Get the current active cart.
  - Add an item to the cart.
  - Update a cart item's quantity.
  - Remove a cart item.
  - Clear the cart.

- **FR-008**: Customer profile endpoints must require authentication and be scoped to the
  authenticated customer:
  - Create the current customer's profile on first use (full name, positive age, optional phone);
    sets the `IdentityUserId` linkage and returns `409 Conflict` if a profile already exists.
  - Get the current customer's profile (`404 Not Found` if not yet created).
  - Update the current customer's profile.

- **FR-009**: Address management endpoints must require authentication and be scoped to the
  authenticated customer:
  - Add an address.
  - Remove an address.
  - Set the default address.

- **FR-010**: Checkout endpoint must require authentication and be scoped to the authenticated
  customer:
  - Submit checkout for the current active cart using a delivery address the customer selects; the
    address id is supplied in the request body (it may be the customer's default address). The
    `customerId` is resolved server-side from the token, never supplied by the caller.
  - Return the order identifier on success.
  - Return structured unavailable-item details on partial or full unavailability.

- **FR-011**: Order history and detail endpoints must require authentication and be scoped to the
  authenticated customer:
  - List the authenticated customer's orders (with optional pagination).
  - Get a specific order's details (only if it belongs to the authenticated customer).

- **FR-012**: Owner-scoped routes must use a pattern like `/api/me/...` so that the caller never
  supplies a `customerId` in the route for authorization purposes. The server resolves the customer
  identity from the validated token.

- **FR-013**: The host must produce a complete OpenAPI document describing all endpoints, their
  authentication requirements, request/response schemas, and error responses.

- **FR-014**: Attribute-routed controllers grouped by domain area (e.g., `CatalogController`,
  `CartController`, `CustomerController`, `OrderController`) must remain thin transport adapters.
  They must not contain business logic, access the database directly, or reference EF Core types.
  All business operations must be delegated to Application use-case handlers.

- **FR-015**: The host must not contain login, register, logout, or any account-management
  endpoints. Those belong exclusively to `Talabat.Identity`.

- **FR-016**: The host must not contain any Delivery-Agent-facing endpoints. Those belong to the
  future `Talabat.DeliveryAgent.API` (Phase 8).

- **FR-017**: Request and response contracts must be host-specific DTOs. Domain entities and
  Application internal read models must not be directly exposed as HTTP response bodies.

- **FR-018**: Account registration (owned by `Talabat.Identity`) creates an account only. The
  `Customer` profile is created explicitly on first use: the authenticated account calls
  `POST /api/me/profile` supplying the domain-required fields (full name, positive age, optional
  phone), and the system creates the `Customer` with those values and sets its `IdentityUserId` from
  the token subject claim (`sub`). The system MUST NOT create an empty or placeholder `Customer`; the
  `Customer` aggregate's invariants (required full name, positive age) are always satisfied. If a
  profile already exists for the account, `POST /api/me/profile` returns `409 Conflict`. The
  `sub` -> `IdentityUserId` linkage rule is provisional and marked for Phase 9 refinement, but the
  profile data itself is real from creation.

- **FR-022**: Every owner-scoped endpoint other than profile creation MUST require an existing
  `Customer` profile. When the authenticated account has no profile yet, the API MUST respond with
  `409 Conflict` and error code `ProfileNotCreated`, directing the caller to `POST /api/me/profile`.
  The customer identity is resolved server-side from the validated token's `sub` claim mapped to
  `Customer.IdentityUserId`; the caller never supplies a `customerId`. Profile resolution is
  read-only — it MUST NOT create a `Customer` as a side effect.

- **FR-019**: A dedicated test project (`tests/Talabat.Customer.API.Tests`) must be created as part
  of this phase. It must include integration tests that verify endpoint routing, authentication
  enforcement (anonymous vs. authenticated), Domain exception-to-Problem Details mapping, and
  response contract shapes. The test project must be added to the solution and all tests must pass
  before the phase is accepted.

- **FR-020**: A development-only CORS policy must be configured to allow requests from `localhost`
  origins (any port). This enables future local Angular SPA development without cross-origin
  blocking. Production CORS origins are deferred until the Angular client and deployment topology
  are defined.

- **FR-021**: A health check endpoint (`/health`) must be added to report the host's operational
  status, including database connectivity. The endpoint must be publicly accessible (no
  authentication required) and return a standard healthy/unhealthy response.

### Key Entities *(include if feature involves data)*

- **Restaurant**: A food provider with a name, description, opening hours, active status, and a
  product catalog. Exposed as a read-only catalog resource. (There is no "category" field in the
  domain.)
- **Product**: A menu item belonging to a restaurant, with name, description, price, and
  availability status. Exposed as part of the restaurant menu.
- **Cart**: A customer's active shopping basket containing items from a single restaurant. Managed
  through add/update/remove/clear operations.
- **CartItem**: A line item in a cart, referencing a product and quantity.
- **Customer**: A customer profile with name, email, phone, and a collection of delivery addresses.
- **CustomerAddress**: A delivery address belonging to a customer, one of which may be marked as
  default.
- **Order**: An immutable purchase record created from checkout, containing price/address snapshots.
- **OrderItem**: A line item in an order with captured product snapshot and price.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All customer-facing use cases (catalog browse, menu view, cart management, profile
  management, address management, checkout, order history) are reachable through documented HTTP
  endpoints within 4 weeks of phase start.

- **SC-002**: Anonymous users can browse restaurants and view menus without credentials. The
  response time for catalog browsing is under 2 seconds for a catalog of up to 100 restaurants.

- **SC-003**: Authenticated customers can complete a full shopping workflow (browse → add to cart →
  checkout → view order) in under 5 minutes using only the published API.

- **SC-004**: 100% of owner-scoped endpoints reject unauthenticated requests and return appropriate
  error responses within 1 second.

- **SC-005**: Domain exceptions produce structured, human-readable error responses that include an
  error type identifier and a descriptive message, enabling client applications to present
  meaningful feedback.

- **SC-006**: The OpenAPI specification is complete and accurately describes all endpoints, enabling
  a client developer to integrate without additional documentation.

- **SC-007**: Zero business logic resides in controller or endpoint code. Every business operation
  is delegated to an Application use-case handler.

- **SC-008**: The project builds, all existing Application and Infrastructure tests remain green,
  and the new `tests/Talabat.Customer.API.Tests` integration tests pass — covering endpoint
  routing, auth enforcement, error mapping, and response shapes.

- **SC-009**: Every endpoint's current authentication/authorization behavior and its Phase 9
  refinement need is documented in a structured authorization matrix.

## Assumptions

- Phase 6 (Minimal Identity/Auth) is complete. The `Talabat.Identity` authority is running and can
  issue tokens that the Customer API validates.
- The Application layer use-case handlers (Phase 4) and Infrastructure persistence (Phase 5) are
  implemented and stable.
- The initial token validation uses a simple bearer scheme against the Identity authority's discovery
  endpoint. The exact audience and scope values are intentionally minimal and will be refined in
  Phase 9.
- Account-to-Customer profile linkage is provisional. Phase 7 creates the `Customer` profile
  explicitly on first use (`POST /api/me/profile`) and links it via the token subject claim. The
  linkage rule is reversible and will be refined in Phase 9; the profile data (name, age, phone,
  addresses) is real and supplied by the customer, never fabricated.
- API versioning strategy may be deferred to a later phase if a decision is not required for the
  initial endpoint set. If deferred, the API design must not preclude future versioning.
- Delivery status is not displayed in customer order details at this phase. It will be added after
  the `Talabat.DeliveryAgent.API` (Phase 8) is implemented.
- Pagination defaults and filtering capabilities follow standard web API conventions (page size,
  page number, sort order) unless specific requirements emerge during planning.

## Out of Scope

- Customer Website frontend (Angular or any other framework).
- Anonymous/guest carts, client-generated cart identifiers, or any unauthenticated cart surface.
  Phase 7 cart endpoints are authenticated and customer-scoped (`/api/me/cart`) and require an
  existing profile. A guest-cart / Basket redesign (nullable `Cart.CustomerId`, a server-generated
  public cart handle, and a checkout-claim step) is a separate future phase.
- Delivery-Agent-facing endpoints (Phase 8).
- Login, register, logout, or any account management endpoints (owned by `Talabat.Identity`).
- Final token audiences, scopes, custom claims, or per-endpoint authorization policies (Phase 9).
- Account-to-Customer profile linkage finalization (Phase 9).
- Refresh-token tuning, external login, password reset, email confirmation, 2FA, admin UI, or
  advanced Identity features.
- Payment, notifications, coupons, reviews, or restaurant-owner workflows (Phase 11).
- Real-time features (WebSocket, SignalR) for order status updates.
- API rate limiting or throttling (future operational concern).
- Deployment, CI/CD pipeline configuration, or production infrastructure.
