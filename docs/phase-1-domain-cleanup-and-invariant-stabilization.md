# Phase 1 Domain Cleanup And Invariant Stabilization

Date: 2026-07-11

This document records the Phase 1 decisions from `PROJECT_IMPLEMENTATION_ROADMAP.md`.

## Scope

Phase 1 is limited to Domain correctness, invariant stabilization, and documentation alignment.

This phase does not add:

- Repository interfaces.
- Repository implementations.
- Unit of Work.
- EF Core.
- Migrations.
- Application use cases.
- API endpoints.
- Identity/Auth packages.
- IdentityServer/Auth Portal code.
- Frontend or website code.

## Code Baseline Reviewed

The Domain layer was reviewed for:

- Framework independence.
- Aggregate method atomicity.
- Child entity access through aggregate roots.
- Checkout coordination.
- Delivery coordination.
- UTC timestamp policy.
- Identity/Auth leakage.
- Repository-readiness ambiguity.

## Phase 1 Code Changes

- Removed the stale empty `Interfaces\` folder include from `Talabat.Domain.csproj`.
  - Repository contracts are intentionally deferred to Phase 2.
  - The Domain project still has no package references and no project references.
- Added a formatting separation in `CheckoutDomainService` after restaurant open-state validation.
  - No checkout behavior changed.
- Renamed `DeliveryAlreadyCompletedException` to `DeliveryTerminalStateException`.
  - The delivery invariant applies to every terminal state: `Delivered`, `Cancelled`, and `Failed`.
  - The exception message remains framework-neutral and business-focused.

No aggregate behavior needed correction in this phase after review. The current code already includes the important fixes that the historical review had called out, including cart/restaurant checkout identity validation, customer profile atomic assignment, duplicate address ID rejection, delivery timestamp validation, vehicle enum validation, and assigned-delivery cancellation/failure coordination.

## Confirmed Domain Decisions

### Cart Creation

`Cart` is created through `Cart.Create(id, customerId, firstProduct, quantity, createdAt)`.

Decision:

- A new persisted cart must start with its first valid item.
- Empty active carts should not be created or persisted by Application/Infrastructure.
- The private constructor exists only to support the factory and later persistence materialization needs.
- `Cart.RestaurantId` is established from the first accepted product snapshot.

### Child Identity Strategy

Repository and persistence design must treat child entities as owned by their aggregate roots.

Confirmed child identity decisions:

- `Product` is a child of `Restaurant`.
  - Product has `Id` and `RestaurantId`.
  - Product is modified only through `Restaurant`.
- `CartItem` is a child of `Cart`.
  - Business identity inside a cart is `ProductId`.
  - No separate `CartItemId` exists in the Domain model.
  - Persistence may use `CartId + ProductId`.
- `OrderItem` is a child of `Order`.
  - Business identity inside an order is `ProductId` for this phase because cart duplicate products are merged before checkout.
  - No separate `OrderItemId` exists in the Domain model.
  - Persistence may use an owned collection key or `OrderId + ProductId`.
- `CustomerAddress` is a child of `Customer`.
  - It has a domain child ID because address management needs stable address selection.
  - Customer rejects duplicate child IDs and duplicate address values.
  - Persistence supplies the parent relationship; the child does not need a public `CustomerId` property.

Do not create repositories for child entities.

### UTC And Restaurant Local Time

Absolute timestamps use UTC `DateTime`.

Confirmed:

- Cart creation/expiration uses UTC.
- Order creation time uses UTC.
- Delivery creation and transition timestamps use UTC.
- Delivery transition timestamps must be monotonic.
- Restaurant opening hours use `TimeOnly` wall-clock values.
- Application must convert the current UTC instant to the restaurant-local `TimeOnly` before calling Domain.
- Time-zone lookup/conversion does not belong inside aggregates.

### Checkout Coordination

`CheckoutDomainService` remains a stateless domain service.

Confirmed:

- It receives already-loaded `Cart`, `Restaurant`, and `DeliveryAddressSnapshot`.
- It validates cart active/expired/empty state.
- It validates that `cart.RestaurantId == restaurant.Id`.
- It validates restaurant active/open state.
- It returns structured unavailable-product results.
- It does not create `Order`, mutate `Cart`, query repositories, save changes, or know HTTP.

Application later owns the full transaction:

1. Load aggregates.
2. Get selected delivery address from `Customer`.
3. Run checkout validation.
4. Create `Order`.
5. Mark `Cart` checked out.
6. Commit through Unit of Work.

### Delivery Coordination

`Delivery` and `DeliveryAgent` remain separate aggregate roots.

Confirmed:

- `Delivery` stores `AssignedAgentId` as an ID, not a navigation object.
- `DeliveryAgent` does not store a delivery collection.
- Assignment, completion, assigned cancellation, and assigned failure are coordinated by `DeliveryAssignmentDomainService`.
- Assigned cancellation/failure must go through the domain service so the Busy agent is released.
- Unassigned pending deliveries may be cancelled or failed directly on `Delivery`.
- Delivery terminal states are `Delivered`, `Cancelled`, and `Failed`.
- Terminal deliveries cannot be changed.

### Audit Boundary

`AuditableEntity` remains framework-neutral for now.

Confirmed:

- Audit user values are plain optional strings.
- Domain does not know `ClaimsPrincipal`, `HttpContext`, `IdentityUser`, JWT claims, or IdentityServer types.
- A future Application/API current-user abstraction may supply audit values later.
- No Identity/Auth linkage is added to Domain in Phase 1.

## Repository Readiness After Phase 1

Domain is now ready for Phase 2 repository and Application contracts from a boundary perspective.

Phase 2 should add contracts deliberately for aggregate roots only:

- `IRestaurantRepository`
- `ICartRepository`
- `ICustomerRepository`
- `IOrderRepository`
- `IDeliveryRepository`
- `IDeliveryAgentRepository`
- `IUnitOfWork`

Phase 2 should not add EF Core or repository implementations.

## Verification

Required Phase 1 verification:

- Build the solution.
- Confirm `Talabat.Domain` still has no package references.
- Confirm no Application, Infrastructure, API, Identity, migration, or frontend implementation was added.
