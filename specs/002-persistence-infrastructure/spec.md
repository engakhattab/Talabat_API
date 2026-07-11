# Feature Specification: Persistence And Infrastructure

**Feature Branch**: `main`  
**Created**: 2026-07-11  
**Status**: Draft  
**Input**: User description: "Phase 4: Persistence And Infrastructure as defined in PROJECT_IMPLEMENTATION_ROADMAP.md Section 5, Phase 4, governed by the Phase 4 constitution scope guard."

## Clarifications

### Session 2026-07-11

- Q: Should duplicate saved-address value be enforced by the database in Phase 4, or remain Domain-only while the database enforces one default address? -> A: Domain-only duplicate address check; database enforces one default address but not duplicate address value.

## User Scenarios & Testing *(mandatory)*

### Primary User Story

As a Talabat backend maintainer, I need the core business model to persist reliably behind the existing repository contracts so customer ordering and future delivery coordination can survive process restarts, enforce data integrity, and be verified against the same relational behavior expected in production.

### Implementation Sub-Stories And Acceptance Groups

#### US1 - Catalog Persistence

**Acceptance Scenario**: Given SQL Server-backed persistence and deterministic catalog seed data, when restaurants and products are saved, seeded, and queried through `IRestaurantRepository`, then active restaurants and their products can be read back with generated IDs, opening hours, prices, availability, and product-name uniqueness intact.

**Independent Test**: Restaurant integration tests can save and read restaurants/products, verify generated IDs, round-trip `TimeRange` and `Money`, reject duplicate product names per restaurant, and prove the browseable seeded catalog persists and round-trips.

#### US2 - Customer Persistence

**Acceptance Scenario**: Given a saved customer profile with addresses, when addresses are persisted and queried through `ICustomerRepository`, then customer and address IDs are generated, address value-object data round-trips, and a second default address for the same customer is rejected by the database.

**Independent Test**: Customer integration tests can save and read customers with addresses, verify generated customer/address IDs, round-trip `Address`, reject two defaults for one customer, and confirm no database duplicate-address-value constraint is introduced.

#### US3 - Basket Persistence

**Acceptance Scenario**: Given an active basket for a customer and restaurant, when cart items are persisted and queried through `ICartRepository`, then the cart receives a generated ID, items are identified by `(CartId, ProductId)`, quantities stay positive, and a second active cart for the same customer is rejected.

**Independent Test**: Cart integration tests can save and read active carts with items, verify generated cart IDs, verify composite cart-item keys, reject invalid quantities, and reject two active carts for one customer.

#### US4 - Ordering And Checkout Atomicity

**Acceptance Scenario**: Given a customer, active cart, restaurant products, and a delivery address, when checkout persistence commits through the existing repositories and UnitOfWork, then exactly one order snapshot is saved, the cart is checked out, generated IDs are available after save, and customer-scoped order reads return the saved order.

**Independent Test**: Order integration tests can save and read order history/details, round-trip `Money` and `DeliveryAddressSnapshot`, preserve historical product snapshots, and prove one order plus one checked-out cart are committed in one UnitOfWork save.

#### US5 - DeliveryAgent Persistence

**Acceptance Scenario**: Given delivery-agent profiles, when agents are persisted and queried through `IDeliveryAgentRepository`, then generated IDs, vehicle/status enum constraints, optional current location, coordinate validity, and available-agent reads behave like SQL Server production persistence.

**Independent Test**: Delivery-agent integration tests can save and read agents, query available agents, round-trip nullable `GeoLocation`, and reject invalid coordinate persistence.

#### US6 - Delivery Persistence

**Acceptance Scenario**: Given saved orders and delivery agents, when deliveries are persisted and queried through `IDeliveryRepository`, then each delivery receives a generated ID, each order has at most one delivery, delivery address snapshots round-trip, and one agent cannot have two active assigned deliveries.

**Independent Test**: Delivery integration tests can save and read deliveries through repository methods, reject duplicate deliveries for one order, and reject two active deliveries assigned to one agent.

### Acceptance Scenarios

1. **Given** a configured application database and existing repository contracts, **When** a customer creates a cart, checks out, and the change is committed, **Then** the saved cart, order, item snapshots, totals, and generated identifiers can be read back through the existing contracts.
2. **Given** a saved customer with addresses, **When** a second default address is attempted, **Then** the database rejects invalid persisted state in addition to the Domain rule.
3. **Given** a delivery already exists for an order, **When** another delivery for the same order is attempted, **Then** the database rejects the duplicate delivery relationship.
4. **Given** active data has been soft-deleted, **When** normal repository reads are executed, **Then** soft-deleted rows are excluded unless a test or maintenance flow explicitly opts into them.

### Edge Cases

- Generated IDs must be positive only after a successful save; new entities may have `Id == 0` before persistence.
- Cart and order line items do not have independent IDs and must remain unique within their parent aggregate by product.
- Filtered uniqueness must be verified with a database engine that supports the same filtered-index semantics expected in production.
- Seed data must not hide broken identity behavior; static seed IDs must be explicit and deterministic.
- Connection strings must support local development and automated tests without committing credentials.
- Duplicate saved-address value remains a Domain rule in Phase 4 and is not backed by a database duplicate-address-value constraint.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST persist all aggregate roots currently represented by repository contracts: restaurants, carts, customers, orders, deliveries, and delivery agents.
- **FR-002**: The system MUST persist aggregate child data only through the parent aggregate boundary; child entities MUST NOT receive standalone repositories.
- **FR-003**: The system MUST assign positive integer identifiers during save for entities with identity keys, while allowing unsaved entities to start with `Id == 0`.
- **FR-004**: The system MUST preserve cart and order child identity using parent-plus-product uniqueness rather than introducing standalone line-item identifiers.
- **FR-005**: The system MUST persist value objects as part of their owning entities and prove that saved value-object data can be read back unchanged.
- **FR-006**: The system MUST enforce database constraints that back existing Domain invariants, including positive quantities, non-negative money, one active cart per customer, one default address per customer, unique product names within a restaurant, one delivery per order, and one active delivery per assigned agent.
- **FR-006a**: The system MUST keep duplicate saved-address value detection in the Domain for Phase 4 and MUST NOT add persistence-only normalized address columns solely to enforce duplicate address values.
- **FR-007**: The system MUST provide repository implementations for the existing aggregate-root repository contracts without changing those contracts.
- **FR-008**: The system MUST provide a single commit boundary so coordinated workflows can save multiple aggregate changes atomically.
- **FR-009**: The system MUST stamp audit timestamps during save while keeping audit user fields empty until identity is introduced in a later phase.
- **FR-010**: The system MUST exclude soft-deleted aggregate rows from normal reads.
- **FR-011**: The system MUST provide deterministic local catalog seed data with explicit identifiers so customer-facing flows have data before management tools exist.
- **FR-012**: The system MUST provide integration tests that verify generated IDs, aggregate persistence, value-object round trips, uniqueness constraints, repository queries, and atomic checkout persistence.
- **FR-013**: The system MUST provide one reviewed migration after mapping and constraint tests are defined.
- **FR-014**: The system MUST keep Domain and Application independent from persistence packages, database APIs, and infrastructure implementation details.
- **FR-015**: The system MUST not add business API endpoints, Identity/Auth behavior, Delivery Application use cases, MediatR, frontend code, or business-rule changes in this phase.
- **FR-016**: The solution MUST complete with no known package vulnerability warnings after the approved package update batch.

### Key Entities *(include if feature involves data)*

- **Restaurant**: A customer-visible food provider with opening hours and product menu.
- **Product**: A restaurant-owned menu item with current price and availability.
- **Cart**: A customer's active, checked-out, or cleared basket for one restaurant.
- **CartItem**: A product and quantity inside a cart, identified inside the cart by product.
- **Customer**: A domain customer profile with saved delivery addresses.
- **CustomerAddress**: A customer-owned saved address with optional default status.
- **Order**: A checkout snapshot for one customer and restaurant.
- **OrderItem**: Historical product, price, quantity, and line total inside an order.
- **Delivery**: A delivery task linked one-to-one with an order and optionally assigned to an agent.
- **DeliveryAgent**: A courier profile with vehicle, availability status, and optional current location.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing aggregate-root repository contracts have infrastructure implementations verified by integration tests.
- **SC-002**: 100% of generated-key entities receive positive IDs after save in integration tests.
- **SC-003**: Checkout persistence creates exactly one order and closes exactly one cart in one committed workflow in integration tests.
- **SC-004**: At least one integration test proves each required uniqueness rule rejects an invalid duplicate.
- **SC-005**: At least one integration test proves each owned value-object category round-trips without data loss.
- **SC-006**: The solution build and full test suite pass with zero known package vulnerability warnings.
- **SC-007**: Domain and Application project files still contain zero package references after the phase.

## Assumptions

- The repository contracts in `src/Talabat/Talabat.Domain/Interfaces/` are stable and remain unchanged in Phase 4.
- SQL Server-compatible relational behavior is required because filtered unique indexes are load-bearing for this model.
- Catalog seed data is static in this phase because no catalog management API or internal tooling exists yet.
- Identity/Auth remains deferred; audit user values remain null until that boundary is designed.

## Out of Scope

- API endpoints, request/response contracts, controllers, or business middleware.
- Identity/Auth packages, identity tables, claims, roles, tokens, or auth migrations.
- Delivery Application use cases or delivery API workflows.
- MediatR or command-bus packages.
- Domain business-rule changes beyond private materialization mechanics required for persistence.
- Frontend, payment, coupons, reviews, notifications, or restaurant-owner workflows.
