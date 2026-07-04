# Aggregates and Invariants

This document defines the MVP v1 aggregate boundaries for the Talabat-like backend using DDD and Clean Architecture.

MVP v1 does not include authentication, authorization, Identity, login/register, JWT, admins, restaurant owners, payment, delivery drivers, notifications, coupons, or reviews. The system assumes one normal customer profile. Restaurants and products are seeded for testing.

## Aggregate Summary

| Context | Aggregate Root | Child Entities | Main Invariants |
|---|---|---|---|
| Catalog | `Restaurant` | `Product` | Product belongs to one restaurant; product price cannot be negative; opening hours must be valid; product availability is controlled through catalog behavior. |
| Basket | `Cart` | `CartItem` | Cart belongs to one customer; only active carts can be modified; cart contains items from one restaurant; cart expires after 1 hour; quantities are positive; duplicate products merge; cart stores no product prices. |
| Ordering | `Order` | `OrderItem` | Order belongs to one customer and one restaurant; order cannot be created from an empty cart; order items are immutable snapshots; total is calculated from items; delivery address is snapshotted. |
| Customer | `Customer` | `CustomerAddress` | Full name is required; age is positive; phone is optional; multiple addresses are allowed; only one default address is allowed; duplicate addresses are rejected. |

## Restaurant Aggregate

Root: `Restaurant`  
Children: `Product`  
Context: Catalog

### Value Objects Used

- `Money` for product price.
- `TimeRange` for restaurant opening hours.

### Invariants Protected By The Aggregate

- A product belongs to exactly one restaurant.
- Product price cannot be negative.
- Restaurant opening hours must be valid.
- Product availability is controlled through restaurant/product behavior.
- Restaurant active state determines whether it appears to customers.

### Methods That Should Exist On The Aggregate Root

- `IsOpenAt(currentTime)`
- `Activate()`
- `Deactivate()`
- `AddProduct(name, description, price)`
- `UpdateProductPrice(productId, newPrice)`
- `MarkProductAvailable(productId)`
- `MarkProductUnavailable(productId)`

Restaurant/product mutation methods may exist in the domain model for completeness and seeding/testing, but MVP v1 will not expose public API endpoints for catalog management because there is no admin or restaurant owner in this version.

### What Must NOT Be Modified Directly

- External code must not directly add or remove products from the product collection.
- External code must not directly set product price.
- External code must not directly toggle product availability.
- Product should be created through `Restaurant.AddProduct`.

### Notes For EF Core Mapping

- `Restaurant` is the aggregate root and should have the repository.
- `Product` is a child entity and should be loaded through `Restaurant` when enforcing product invariants.
- `Product.RestaurantId` should be required.
- Product price should be stored as an owned value object or mapped columns for `Money`.
- Opening hours should be stored from `TimeRange` as owned value object columns or equivalent scalar columns.
- Product collection should be backed by a private field and exposed as a read-only collection.

## Cart Aggregate

Root: `Cart`  
Children: `CartItem`  
Context: Basket

### Input Models Used

- `CatalogProductSnapshot` as price-free input to add-to-cart behavior.

### Concepts Used

`CartStatus`

- `Active`
- `CheckedOut`
- `Cleared`

### Invariants Protected By The Aggregate

- Cart belongs to one customer.
- Cart can contain items from only one restaurant.
- Only `Active` carts can be modified.
- Cart expires after 1 hour.
- Expired cart cannot be modified.
- Quantity must be greater than zero.
- Duplicate products are merged.
- CartItem stores ProductId and Quantity, with ProductName optional for simple display.
- CartItem does not store product price.
- Unavailable products cannot be added.

`IsExpired(currentTime)` may be calculated from `CreatedAt` plus the expiry duration or from a stored `ExpiresAt` value. `Status` tracks lifecycle state: whether the cart is `Active`, `CheckedOut`, or `Cleared`.

`Cart.RestaurantId` is assigned from the first item added to the cart. In MVP v1, a cart should not be created or persisted as an empty active cart before the first item is added. This keeps the one-restaurant-per-cart rule anchored to the first product snapshot.

### Methods That Should Exist On The Aggregate Root

- `AddItem(productSnapshot, quantity, currentTime)`
- `RemoveItem(productId, currentTime)`
- `UpdateQuantity(productId, quantity, currentTime)`
- `Clear(currentTime)`
- `IsExpired(currentTime)`
- `GetTotal(currentPrices)`
- `MarkCheckedOut(currentTime)`

### Important Input Boundary

Do not pass the Catalog `Product` entity directly into `Cart`. Use a small input model or value object such as `ProductSnapshot` or `CatalogProductSnapshot` containing:

- `ProductId`
- `RestaurantId`
- `ProductName`
- `IsAvailable`

This prevents sharing Catalog entities directly across bounded contexts.

### What Must NOT Be Modified Directly

- External code must not directly add, remove, or update `CartItem` rows.
- External code must not directly change cart item quantity.
- External code must not bypass cart expiry checks.
- External code must not modify a `CheckedOut` or `Cleared` cart.
- Application use cases should call `Cart` methods instead of changing child entities.

### Notes For EF Core Mapping

- `Cart` is the aggregate root and should have the repository.
- `CartItem` is a child entity and should be configured through the cart relationship.
- `CartItem.CartId` should be required.
- Cart should have a required customer reference.
- Cart status should be persisted if the system needs to distinguish active, checked-out, and cleared carts.
- If cart status is used, one active cart per customer should be protected by a filtered unique index.
- Quantity should have a database check constraint greater than zero.
- Cart item collection should be backed by a private field and exposed as a read-only collection.

Cart stores no price or total. A later Application/read flow loads current Product prices from Catalog and supplies them to `Cart.GetTotal(currentPrices)`, which multiplies each supplied price by the CartItem quantity and returns a transient total. The checkout flow also loads current Catalog prices and uses them when creating `CheckoutItemSnapshot` values.

## Order Aggregate

Root: `Order`  
Children: `OrderItem`  
Context: Ordering

### Value Objects Used

- `Money` for order item unit price, line total, and order total.
- `DeliveryAddressSnapshot` for the delivery address copied at checkout time.

### Invariants Protected By The Aggregate

- Order belongs to one customer.
- Order belongs to one restaurant.
- Empty cart cannot create an order.
- Order item product name, unit price, quantity, and line total are immutable snapshots.
- Order total is calculated from order items.
- Order must contain a delivery address snapshot.
- Order should not trust caller-provided totals.

### Methods That Should Exist On The Aggregate Root

- Factory method: `CreateFromCheckout(customerId, restaurantId, checkoutItems, deliveryAddressSnapshot, currentTime)`
- `GetTotal()`

`checkoutItems` should be a simple snapshot/input model, not `CartItem` entities from the Cart aggregate.

`CheckoutItemSnapshot`

- `ProductId`
- `ProductName`
- `UnitPrice`
- `Quantity`

Order should not depend directly on `CartItem` because `CartItem` is a child entity owned by the Cart aggregate. The application checkout flow should translate cart data into checkout item snapshots before creating an order.

### Important Snapshot Boundary

Order should store a delivery address snapshot, not only a `CustomerAddressId`, so old orders remain correct if the customer changes their address later. Customer owns customer addresses, Ordering requires a `DeliveryAddressSnapshot` when creating an order, and the application layer validates that the selected address belongs to the customer before calling the order factory.

### What Must NOT Be Modified Directly

- External code must not directly add, remove, or update `OrderItem` rows.
- External code must not directly set order total.
- External code must not directly overwrite order item prices, product names, quantities, or line totals.
- External code must not replace the delivery address snapshot after order creation.

### Notes For EF Core Mapping

- `Order` is the aggregate root and should have the repository.
- `OrderItem` is a child entity and should be configured through the order relationship.
- `OrderItem.OrderId` should be required.
- Order should have required customer and restaurant references.
- Delivery address should be stored as owned value object columns or equivalent immutable snapshot columns.
- Order item prices and line totals should be stored as owned value objects or mapped columns for `Money`.
- Order item collection should be backed by a private field and exposed as a read-only collection.

## Customer Aggregate

Root: `Customer`  
Children: `CustomerAddress`  
Context: Customer

### Value Objects Used

- `Address` or `CustomerAddressDetails` for street, city, building, floor, and any other address fields.

### Invariants Protected By The Aggregate

- Customer can have multiple addresses.
- Customer can have only one default address.
- Duplicate addresses are rejected.
- Address data cannot be empty.
- FullName is required and cannot be empty.
- Age must be greater than zero.
- PhoneNumber is optional in MVP v1.

### Methods That Should Exist On The Aggregate Root

- `AddAddress(address)`
- `RemoveAddress(addressId)`
- `SetDefaultAddress(addressId)`
- `GetDefaultAddress()`
- `UpdateProfile(fullName, age, phoneNumber)`

### Important MVP v1 Boundary

Customer is a simple profile in MVP v1. Do not include `IdentityUserId`, password, email confirmation, roles, JWT, or login data.

The Customer constructor or factory requires `FullName` and `Age`, accepts an optional `PhoneNumber`, and starts with an encapsulated address collection. Profile behavior must trim and reject an empty full name and reject age less than or equal to zero.

Customer owns customer addresses and protects address invariants. Checkout requiring a delivery address is not only a Customer aggregate invariant; it is a cross-aggregate checkout rule that the application layer validates before Ordering creates an order with a `DeliveryAddressSnapshot`.

### What Must NOT Be Modified Directly

- External code must not directly add or remove `CustomerAddress` rows.
- External code must not directly set multiple addresses as default.
- External code must not bypass duplicate address checks.
- Address changes should go through `Customer` methods.

### Notes For EF Core Mapping

- `Customer` is the aggregate root and should have the repository.
- `CustomerAddress` is a child entity and should be configured through the customer relationship.
- `CustomerAddress.CustomerId` should be required.
- Address fields should be required where the domain requires non-empty data.
- One default address per customer should be protected by a filtered unique index where supported.
- Address collection should be backed by a private field and exposed as a read-only collection.

## Cross-Aggregate Rules

Some rules cannot be enforced by one aggregate alone:

- One active cart per customer requires application, repository, and database support.
- Checkout pricing requires Cart quantities plus current Catalog prices; there is no old cart price comparison.
- Checkout product availability validation requires Cart data plus current Catalog data.
- Checkout restaurant open/active validation requires Catalog data.
- Checkout delivery address validation requires Customer data.
- Ordering requires a `DeliveryAddressSnapshot` when creating an order.
- The application layer validates that the selected address belongs to the customer.
- Checkout requiring a delivery address is a cross-aggregate checkout rule, not only a Customer aggregate invariant.

The application layer should orchestrate these workflows by loading the required aggregate roots, delegating business checks to domain methods or domain services, and saving changes through a unit of work.

## Aggregate Access Rules

- Load and modify child entities only through the aggregate root.
- Do not expose mutable child collections.
- Do not query or update `CartItem`, `OrderItem`, `Product`, or `CustomerAddress` directly from application use cases.
- Application handlers should load the aggregate root, call domain methods, then save changes.
- Repositories should exist only for aggregate roots.

## Repositories Needed

- `IRestaurantRepository`
- `ICartRepository`
- `IOrderRepository`
- `ICustomerRepository`
- `IUnitOfWork`
