# Domain Services Design

> Phase 0 scope update: This document was written before the Delivery domain implementation was added. `CheckoutDomainService` and `DeliveryAssignmentDomainService` both exist in current Domain code. Identity/Auth remains outside Domain services.

This document defines the Domain Service design for Talabat MVP v1.

This is a design document only. It does not generate C# code and does not create entities, repositories, controllers, handlers, EF configurations, or migrations.

MVP v1 scope:

- No authentication, authorization, Identity, login/register, JWT, admins, or restaurant owners.
- No payment, delivery drivers, notifications, coupons, or reviews.
- Assume one normal customer profile.
- Restaurants and products are seeded for testing.
- Cart is not created until the first item is added.

## What Is A Domain Service?

Most business behavior should live inside entities and aggregate roots.

A Domain Service is used only when a business operation does not naturally belong to one aggregate. It is a way to express domain logic that needs data from multiple aggregates or bounded contexts without forcing that logic into the wrong entity.

Domain Services should contain domain logic, not infrastructure logic. They should be stateless and should not directly use EF Core, DbContext, HTTP, controllers, or API concepts.

For this MVP, Domain Services should not directly call repositories. The Application layer loads aggregates and passes them in. This keeps the Domain layer independent from persistence and makes the domain logic easier to test.

## When To Use A Domain Service In This Project

Use aggregate methods when the rule belongs to one aggregate:

- `Cart.AddItem`
- `Cart.UpdateQuantity`
- `Customer.SetDefaultAddress`
- `Restaurant.IsOpenAt`
- `Order.CreateFromCheckout`

Use a Domain Service when the rule needs multiple aggregates or cross-context data:

- Checkout validation.
- Checking product availability again during checkout.
- Checking restaurant active/open state during checkout.
- Preparing checkout item snapshots with current Catalog prices for Order creation.

The design rule is simple: keep behavior on the aggregate when one aggregate owns the rule; use a Domain Service only when the business decision spans multiple aggregates.

## Domain Services Needed For MVP v1

MVP v1 needs only one main domain service:

- `CheckoutDomainService`

Avoid creating unnecessary services until the domain needs them. A service per entity would weaken the model and move behavior away from the aggregate roots that own the invariants.

## CheckoutDomainService

### Purpose

`CheckoutDomainService` coordinates checkout domain validation across Basket, Catalog, Customer, and Ordering data.

It validates whether the current cart can become an order. It uses already-loaded domain data, checks cross-aggregate business rules, returns structured checkout outcomes when the customer needs details, and prepares checkout item snapshots that can be passed to `Order.CreateFromCheckout`.

It does not load or save data.

### Inputs

Conceptually, it should receive already-loaded data from the Application layer:

- Cart.
- Restaurant with products needed for validation.
- DeliveryAddressSnapshot or selected Address converted into a delivery snapshot.
- current UTC time for cart expiry and order history.
- restaurant-local time-of-day for opening-hours validation.

It may receive current catalog product data through the Restaurant aggregate or through a prepared list of current product snapshots. The important boundary is that the Application layer gathers the data; the Domain Service evaluates the business rules.

Customer owns saved addresses. The Application layer may ask Customer for the selected address and then create a DeliveryAddressSnapshot, or Customer may expose a method that creates the snapshot. In both cases, the selected address must belong to the customer before checkout can continue.

### Responsibilities

The domain service should:

- Ensure cart is active.
- Ensure cart is not expired.
- Ensure cart is not empty.
- Ensure restaurant is active.
- Ensure restaurant is open at the supplied restaurant-local time.
- Ensure a delivery address snapshot is present before Order creation.
- Validate every cart item against current catalog product data.
- Detect products that became unavailable.
- Return a structured checkout result for unavailable products.
- Produce checkout item snapshots using current Catalog names and prices when checkout is valid.

### Suggested Validation Order

1. Ensure cart is active.
2. Ensure cart is not expired.
3. Ensure cart is not empty.
4. Ensure the supplied restaurant matches `Cart.RestaurantId`.
5. Ensure restaurant is active.
6. Ensure restaurant is open at the supplied restaurant-local time.
7. Ensure a delivery address snapshot exists.
8. Validate product availability using current catalog data.
9. If valid, produce checkout item snapshots using current Catalog prices.

Earlier failures should stop later checks when later checks are unnecessary. For example, there is no reason to inspect product availability if the cart is expired, and there is no reason to create checkout item snapshots if the restaurant is closed or the delivery address is missing.

Absolute timestamps must be UTC. The Application layer is responsible for converting the current UTC instant to the restaurant's configured local time before calling this service; the Domain receives the resulting `TimeOnly` and does not perform time-zone lookup.

### Multiple Checkout Problems

MVP v1 returns `CheckoutProductsUnavailable` when one or more products became unavailable. If all products remain available, checkout validation succeeds and uses their current Catalog prices. There is no price-change comparison or price-change result because Cart stores no old price.

### What It Should NOT Do

It should not:

- Query the database.
- Use repositories directly.
- Call EF Core or DbContext.
- Know HTTP status codes.
- Return API response objects.
- Save the Order.
- Save Cart changes.
- Send notifications.
- Process payment.
- Assign delivery drivers.
- Mutate Catalog products.
- Mutate Customer addresses.

### Possible Conceptual Results

`CheckoutValidationSucceeded`

- Contains checkout item snapshots.

`CheckoutProductsUnavailable`

- Contains unavailable items:
- ProductId.
- ProductName.
- Reason.

Unavailable products are an expected checkout outcome, so they should be returned as a structured result. Current Catalog prices are not a failure outcome; they become `CheckoutItemSnapshot.UnitPrice` values.

Invalid operations such as expired cart or empty cart can still throw domain exceptions.

## Checkout Flow Responsibility Split

| Step | Responsibility Owner | Why |
|---|---|---|
| Receive checkout request | API/Application | Request handling is not domain logic. |
| Load active cart | Application | Data access belongs outside Domain. |
| Load restaurant/current product data | Application | Repository access belongs outside Domain. |
| Load/validate selected customer address | Application + Customer aggregate | Application requests it, Customer owns address collection rule. |
| Create DeliveryAddressSnapshot | Application or Customer method | It copies address value for Order. |
| Run checkout domain validation | CheckoutDomainService | Cross-aggregate business validation. |
| If products are unavailable, return checkout result | Application returns service result | UI needs structured unavailable-item details. |
| If valid, create Order | Order aggregate factory | Order owns OrderItems, total calculation, and immutable snapshots. |
| Mark Cart checked out | Cart aggregate | Cart owns its lifecycle/status. |
| Save changes | Application + UnitOfWork | Transaction persistence is not domain logic. |

## Why Checkout Logic Should Not Be In Controller

Controllers should map HTTP requests to application commands/queries.

Controllers should not enforce cart rules, product availability checks, current-price selection, or order creation rules. Those responsibilities belong in application orchestration, domain entities, aggregate roots, or domain services.

Putting business rules in controllers makes them hard to test and easy to bypass. The same checkout logic may later be used by another delivery mechanism, such as a background process or different API surface. If the rules live in controllers, they are tied to HTTP instead of the domain.

## Why Checkout Logic Should Not Be Fully In Application Handler

The Application handler should orchestrate.

It can decide workflow order: load cart, load restaurant data, load customer address, call the domain service, create the order, mark the cart checked out, and save through UnitOfWork.

Business rules should remain in Domain entities or Domain Services. The handler can coordinate the workflow, but it should delegate business decisions such as cart expiry, restaurant open state, product availability, order creation, and cart lifecycle changes. The handler loads current Catalog prices; Cart may calculate a transient total only from prices supplied by the caller.

This keeps the Application layer thin and keeps the business model testable without API or persistence concerns.

## Why CheckoutDomainService Should Not Use Repositories

Repositories are infrastructure/application-facing dependencies. They are how the Application layer asks persistence to load aggregates.

Domain services should stay pure and easy to test. Passing already-loaded aggregates and snapshots keeps dependencies explicit and prevents hidden database access inside domain logic.

This design also keeps transaction boundaries clear. The Application layer controls what is loaded and when changes are saved. The Domain Service evaluates the business rules using the data it was given.

## Common Mistakes

- Creating a service for every entity.
- Moving all entity behavior into services.
- Letting CheckoutDomainService become a god service.
- Injecting DbContext into Domain Service.
- Returning HTTP response objects from Domain Service.
- Comparing current Catalog prices with an old cart price that no longer exists.
- Passing CartItem directly into Order instead of checkout item snapshots.
- Passing Catalog Product entity directly into Cart instead of catalog product snapshots.
