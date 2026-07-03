# Domain Services Design

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
- Comparing cart item price snapshots with current catalog prices.
- Checking product availability again during checkout.
- Checking restaurant active/open state during checkout.
- Preparing checkout item snapshots for Order creation.

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
- currentTime.

It may receive current catalog product data through the Restaurant aggregate or through a prepared list of current product snapshots. The important boundary is that the Application layer gathers the data; the Domain Service evaluates the business rules.

Customer owns saved addresses. The Application layer may ask Customer for the selected address and then create a DeliveryAddressSnapshot, or Customer may expose a method that creates the snapshot. In both cases, the selected address must belong to the customer before checkout can continue.

### Responsibilities

The domain service should:

- Ensure cart is active.
- Ensure cart is not expired.
- Ensure cart is not empty.
- Ensure restaurant is active.
- Ensure restaurant is open at current time.
- Validate every cart item against current catalog product data.
- Detect price changes.
- Detect products that became unavailable.
- Return structured checkout results for price changes and unavailable products.
- Produce checkout item snapshots that can be passed to `Order.CreateFromCheckout` when checkout is valid.

### Suggested Validation Order

1. Ensure cart is active.
2. Ensure cart is not expired.
3. Ensure cart is not empty.
4. Ensure restaurant is active.
5. Ensure restaurant is open at current time.
6. Validate product availability using current catalog data.
7. Validate current prices against cart price snapshots.
8. If valid, produce checkout item snapshots.

Earlier failures should stop later checks when later checks are unnecessary. For example, there is no reason to compare prices if the cart is expired, and there is no reason to create checkout item snapshots if the restaurant is closed.

### Multiple Checkout Problems

MVP v1 decision:

- If one or more products became unavailable, return `CheckoutProductsUnavailable` first.
- Only check and return `CheckoutPriceChanged` when all products are still available.
- If all products are available and prices match, checkout validation succeeds.

Reason: unavailable products cannot be ordered at all, so availability has priority over price changes.

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

`CheckoutPriceChanged`

- Contains changed items:
- ProductId.
- ProductName.
- OldCartPrice.
- CurrentCatalogPrice.

`CheckoutProductsUnavailable`

- Contains unavailable items:
- ProductId.
- ProductName.
- Reason.

Price changes and unavailable products are expected checkout outcomes, so they should be returned as structured results.

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
| If price changed or products unavailable, return checkout result | Application returns service result | UI needs structured details. |
| If valid, create Order | Order aggregate factory | Order owns OrderItems, total calculation, and immutable snapshots. |
| Mark Cart checked out | Cart aggregate | Cart owns its lifecycle/status. |
| Save changes | Application + UnitOfWork | Transaction persistence is not domain logic. |

## Why Checkout Logic Should Not Be In Controller

Controllers should map HTTP requests to application commands/queries.

Controllers should not enforce cart rules, price checks, or order creation rules. Those rules belong in domain entities, aggregate roots, or domain services.

Putting business rules in controllers makes them hard to test and easy to bypass. The same checkout logic may later be used by another delivery mechanism, such as a background process or different API surface. If the rules live in controllers, they are tied to HTTP instead of the domain.

## Why Checkout Logic Should Not Be Fully In Application Handler

The Application handler should orchestrate.

It can decide workflow order: load cart, load restaurant data, load customer address, call the domain service, create the order, mark the cart checked out, and save through UnitOfWork.

Business rules should remain in Domain entities or Domain Services. The handler can coordinate the workflow, but it should delegate business decisions such as cart expiry, restaurant open state, product availability, price changes, order creation, and cart lifecycle changes.

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
- Throwing exceptions for price changes instead of returning structured checkout result.
- Passing CartItem directly into Order instead of checkout item snapshots.
- Passing Catalog Product entity directly into Cart instead of catalog product snapshots.
