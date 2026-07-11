# Feature Specification: Application Use Cases

**Feature Branch**: `[not-created]`  
**Created**: 2026-07-11  
**Status**: Draft  
**Input**: User description: "Phase 3 from PROJECT_IMPLEMENTATION_ROADMAP.md: Application Layer Use Cases"

## Clarifications

### Session 2026-07-11

- Q: Should Delivery workflows be part of the Phase 3 spec, or deferred to a separate delivery-focused phase? -> A: Defer Delivery workflows from Phase 3; Phase 3 covers customer ordering only.

## User Scenarios & Testing *(mandatory)*

### Primary User Story

A customer can complete the core food ordering journey through the system: browse available restaurants, view a restaurant menu, manage a cart, maintain profile/address information, checkout with a selected delivery address, and review their order history. The system coordinates these workflows consistently so business rules are enforced in one place and later customer-facing experiences can rely on stable behavior.

### Acceptance Scenarios

1. **Given** active restaurants and available products exist, **When** a customer browses restaurants and opens a menu, **Then** the customer sees only orderable restaurant and product information needed to choose items.
2. **Given** a customer has no active cart, **When** the customer adds an available product to the cart, **Then** the system creates a cart containing that product and records the restaurant for the cart.
3. **Given** a customer has an active cart, **When** the customer adds another product from the same restaurant, updates quantity, removes an item, or clears the cart, **Then** the cart reflects the requested change while preserving cart rules.
4. **Given** a customer has profile and address information, **When** the customer updates profile details or manages saved addresses, **Then** the system keeps the profile valid and preserves the one-default-address rule.
5. **Given** a customer has an active non-expired cart with available products and a selected saved delivery address, **When** checkout is requested, **Then** the system validates restaurant availability, product availability, cart state, delivery address ownership, creates an order, and closes the cart as checked out.
6. **Given** one or more cart products are no longer orderable during checkout, **When** checkout is requested, **Then** the system returns a structured outcome listing the unavailable items without creating an order or closing the cart.
7. **Given** a customer has previous orders, **When** the customer views order history or a specific order, **Then** only orders for that customer are returned with historical item and delivery address details.
8. **Given** a customer has an active cart for one restaurant, **When** the customer tries to add a product from another restaurant, **Then** the system returns a conflict outcome and preserves the existing cart unchanged.
9. **Given** a future presentation channel invokes a use case, **When** the use case completes or fails with an expected business outcome, **Then** the result is transport-neutral and contains no HTTP-specific response type.
10. **Given** a customer-scoped workflow receives caller-supplied profile context during this auth-deferred phase, **When** customer data is requested or changed, **Then** the workflow scopes all access to that customer profile and remains ready for future authenticated profile resolution.

### Edge Cases

- Customer attempts to modify an expired, checked-out, or cleared cart.
- Customer attempts to add a product from a different restaurant to an active cart.
- Customer attempts checkout with an empty cart or without a valid delivery address.
- Restaurant is inactive or closed at checkout time.
- Product becomes unavailable or missing after being added to the cart.
- Requested customer address or order does not belong to the customer.
- Customer attempts checkout after another request already checked out, cleared, or expired the cart.
- Customer submits zero or negative quantity during add-item or update-quantity workflows.
- Customer views order history when no orders exist.
- Customer requests an order that is missing or belongs to another customer profile.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a customer restaurant browsing workflow that returns only restaurants considered available for customer selection.
- **FR-002**: The system MUST provide a restaurant menu workflow that returns product information needed for customer item selection and excludes or clearly distinguishes unavailable products.
- **FR-003**: The system MUST allow a customer to view the current active cart, including selected products, quantities, and a calculated current total.
- **FR-004**: The system MUST create a customer cart when the first valid product is added and MUST NOT create an empty active cart as a normal customer workflow.
- **FR-005**: The system MUST allow cart item quantity changes, item removal, and cart clearing while enforcing cart status, expiration, quantity, and one-restaurant rules.
- **FR-006**: The system MUST allow a customer profile to be retrieved and updated while preserving required profile data rules.
- **FR-007**: The system MUST allow a customer to add, remove, and choose a default saved delivery address while preventing duplicate addresses and multiple defaults.
- **FR-008**: The system MUST validate checkout against cart state, restaurant state, current product availability, current product pricing, and selected delivery address ownership.
- **FR-009**: The system MUST create an order only after checkout succeeds and MUST preserve historical product, price, total, and delivery address information for that order.
- **FR-010**: The system MUST mark the cart as checked out only when order creation succeeds as part of the same business workflow.
- **FR-011**: The system MUST return a structured unavailable-products checkout outcome when products cannot be ordered during checkout.
- **FR-012**: The system MUST provide customer-scoped order history and order detail workflows.
- **FR-013**: The system MUST return workflow outcomes in a form that can be mapped by future presentation channels without exposing transport-specific response details.
- **FR-014**: The system MUST keep authentication and authorization framework decisions outside this feature while still requiring customer-scoped behavior to be explicit.

### Terminology And Business Rule Definitions

- **Available restaurant for browsing** means a restaurant that is active. Browse results may also include current open/closed status when local time can be resolved.
- **Available restaurant for checkout** means a restaurant that is active and open at the restaurant-local time used by the checkout workflow.
- **Available product** means a product that exists under the selected restaurant and is marked available.
- **Orderable product** means an available product whose restaurant is valid for the current workflow.
- **Valid delivery address** means a saved address that belongs to the same customer profile requesting checkout.
- **Current active cart** means a cart owned by the customer profile, associated with one restaurant, with `Active` status, and not expired at the time of the workflow.
- **Structured outcome** means an Application-level result with a stable category, code, message, and any required business data. It is not an HTTP response.
- **Predictable business outcome** means an expected success or failure result that can be mapped by future presentation channels without inspecting exceptions or framework-specific state.
- **Future presentation channels** means later API endpoints, web apps, or background adapters that call Application use cases. Phase 3 results must be mappable by those channels without containing transport-specific response types.
- **Basket** names the bounded context area; **Cart** names the customer aggregate and customer workflow object. Phase 3 contracts should use `Cart` for request/response names and reserve `Basket` for folder/context grouping.

### Restaurant And Product Availability Rules

- Browse restaurants returns active restaurants only.
- Menu retrieval returns the restaurant menu and clearly marks unavailable products with `isAvailable = false`; unavailable products are not silently hidden in Phase 3.
- Add-to-cart requires the selected product to exist and be available.
- Checkout validates the current restaurant state, current product existence, and current product availability.
- If a product is missing or unavailable during checkout, checkout returns item-level unavailable-product details and does not create an order or close the cart.
- Checkout always uses current Catalog prices. There is no old-cart-price comparison and no price-change checkout outcome in Phase 3.

### Basket Total Rules

- **CalculatedCurrentTotal** means the sum of all cart item line totals using current Catalog prices.
- **LineTotal** equals `CurrentCatalogUnitPrice * Quantity`.
- For this phase, `CalculatedCurrentTotal` includes item prices only.
- `CalculatedCurrentTotal` intentionally excludes delivery fees, service fees, taxes, tips, coupons, discounts, and payment fees.
- If the cart is empty, `CalculatedCurrentTotal` is `0`.
- `Cart` and `CartItem` must not store product prices. Current Catalog prices are supplied by the Application layer when cart totals are returned.

### Cart Lifecycle And Conflict Rules

- A cart starts as `Active` when the first valid product is added.
- A cart becomes `CheckedOut` only after successful order creation in the same checkout workflow.
- A cart becomes `Cleared` when the customer clears it.
- Expiration is evaluated using the current UTC time at cart view, cart mutation, and checkout boundaries. Expired carts are not considered current active carts.
- Checked-out, cleared, and expired carts cannot be modified.
- After a cart is cleared, a later add-item workflow creates a new cart rather than reusing the cleared cart.
- Zero or negative quantities are invalid for add-item and update-quantity workflows.
- Cross-restaurant add attempts must return a conflict outcome and preserve the existing cart unchanged.

### Customer Profile Rules

- `FullName` is required.
- `FullName` must be trimmed before validation.
- `FullName` cannot be empty or whitespace.
- `Age` is required.
- `Age` must be greater than zero.
- `PhoneNumber` is optional.
- Updating a customer profile must preserve these validation rules.
- Public API and use-case contracts must not treat a user-submitted `CustomerId` as final authorization. During this auth-deferred phase, use cases may receive a placeholder customer profile identifier from the caller, but a future authenticated user context must resolve the customer profile.

### Customer Address Rules

- Saved address `Street`, `City`, and `BuildingNumber` are required.
- Saved address `Floor` is optional.
- Duplicate saved addresses are detected by normalized address value: street, city, building number, and floor compared case-insensitively after required text normalization.
- At most one saved address may be marked as default.
- Setting a default address clears the previous default before marking the selected address as default.
- Removing an address removes only an address owned by the same customer profile.
- Removing the current default address does not automatically choose a new default in Phase 3.

### Checkout Reliability And Recovery Rules

- Checkout is one business workflow: load cart, load customer/address, load restaurant/products, validate through Domain, create one order, mark one cart checked out, and commit once.
- If checkout validation fails before order creation, no order is created and the cart is not marked checked out.
- If unavailable products are returned, the cart remains active and editable when it has not otherwise expired or changed state.
- The unavailable-products outcome includes `productId`, `productName`, and `reason` for each unavailable item.
- Duplicate checkout submissions are handled through cart state: the first successful checkout marks the cart checked out; later submissions for the same cart return a cart-not-active style outcome.
- Concurrent cart or checkout changes in Phase 3 are handled by re-evaluating the current cart state at the workflow boundary. Storage-specific optimistic concurrency, locks, and retry policy are deferred to Phase 4 persistence planning.
- Missing customer profile, missing saved address, inactive restaurant, closed restaurant, missing product, unavailable product, empty cart, expired cart, and non-active cart are expected business outcomes.

### Order Read Rules

- Order history returns only orders owned by the requested customer profile.
- Empty order history returns an empty collection.
- Order history is ordered from newest to oldest by order creation time.
- Order details return historical item names, quantities, unit prices, line totals, total amount, and delivery address snapshot.
- Missing orders and orders owned by another customer profile both return the same not-found style outcome so cross-customer existence is not exposed.

### Non-Functional Requirements

- Phase 3 does not set production latency targets because persistence and API transport are deferred.
- Use-case orchestration must remain bounded to the repositories required by the workflow: Catalog reads use restaurant data, Basket workflows use cart plus Catalog price data when totals are returned, Customer workflows use customer data, Checkout uses cart/customer/restaurant/order data, and Order reads use order data.
- Checkout must prevent partial business state changes by committing only after order creation and cart checkout state are both prepared.
- Expected failure outcomes must expose stable Application result categories and codes suitable for future logging, metrics, and tracing without containing HTTP response types.
- Customer profile, address, and order responses must include only data required by the customer workflows in this feature.
- Identity/Auth, role/policy enforcement, audit logging, telemetry sinks, and production monitoring are deferred, but this phase must keep stable result codes and customer ownership boundaries so those concerns can be added later.
- Historical order item and delivery address snapshots are retained as part of the order record for business history. Formal data retention, deletion, and compliance policy decisions are deferred to a later privacy/compliance phase.

### Deferred Decisions

- Persistence schema, migrations, indexes, seed data, and storage-specific concurrency controls are deferred to Phase 4.
- API routes, HTTP status mapping, OpenAPI contracts, and website/frontend flows are deferred to later phases.
- Identity/Auth framework selection and current authenticated user resolution are deferred to the reserved Identity/Auth phase.
- Payment, coupons, reviews, notifications, restaurant-owner workflows, and Delivery workflows are out of Phase 3 scope.
- Production latency, throughput, uptime, telemetry backend, audit-log sink, and privacy retention policy targets are deferred until Infrastructure/API and compliance concerns are planned.

### Key Entities *(include if feature involves data)*

- **Restaurant**: A food provider that can be browsed and whose state determines whether checkout may proceed.
- **Product**: A menu item owned by a restaurant, with current availability and current price.
- **Cart**: A customer's active pre-checkout selection of products from one restaurant.
- **Cart Item**: A selected product and quantity inside a cart.
- **Customer**: A business profile that owns contact information and saved delivery addresses.
- **Customer Address**: A saved delivery address belonging to a customer profile.
- **Order**: The historical record created after successful checkout.
- **Order Item**: The historical product, quantity, and price snapshot inside an order.
- **Checkout Outcome**: The result of a checkout attempt, either successful order creation or a structured list of unavailable items.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A customer can complete browse-to-checkout for a valid cart and address through these documented workflows without manual data repair: browse active restaurant, view menu, add item, view cart total, select saved address, checkout, and view order details.
- **SC-002**: 100% of checkout attempts with unavailable products return item-level reasons and create no order.
- **SC-003**: 100% of successful checkout attempts create exactly one order and close exactly one active cart.
- **SC-004**: 100% of customer order history results are scoped to the requested customer profile.
- **SC-005**: 100% of invalid cart, profile, address, and checkout actions return a documented Application result category/code and do not create partial order/cart state changes.
- **SC-006**: Every planned Phase 3 workflow has traceability to at least one acceptance scenario, one functional requirement, and one task entry in `tasks.md`.

## Assumptions

- Customer-facing Catalog, Basket, Customer, and Ordering workflows are required before Delivery Website support.
- Delivery workflows are deferred to a separate delivery-focused phase after core customer checkout and order history workflows are stable.
- Authentication and authorization are reserved for a later Identity/Auth phase; this feature still keeps customer ownership boundaries explicit.
- Profile and ownership context can be supplied by callers during this phase without selecting an identity framework.
- Payment, coupons, reviews, notifications, and restaurant-owner management are not part of this feature.

## Out of Scope

- Selecting or implementing an identity framework.
- Login, registration, token issuing, roles, claims, or account management.
- Data storage implementation, storage design, or initial data loading.
- External delivery interfaces or website/frontend screens.
- Delivery task creation, assignment, lifecycle transitions, delivery-agent workflows, and delivery status workflows.
- Payment processing.
- Coupons, discounts, offers, reviews, and notifications.
- Real-time delivery tracking, maps integration, route optimization, or automatic nearest-agent assignment.
