# Data Model: Application Use Cases

This model documents the business data and Application contract shapes needed for Phase 3 planning. It is not a database schema and does not define EF Core mappings.

## Existing Domain Entities

### Restaurant

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Catalog/Restaurant.cs`

**Fields used by Phase 3**:

- `Id`
- `Name`
- `Description`
- `ImageUrl`
- `OpeningHours`
- `IsActive`
- `Products`

**Relationships**:

- Owns `Product` child entities.
- Supplies menu data and checkout availability.

**Validation and invariants**:

- `Id` must be positive.
- Name and description are required.
- Opening hours are required.
- Products are added through `Restaurant.AddProduct`.
- Duplicate product IDs are rejected.

### Product

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Catalog/Product.cs`

**Fields used by Phase 3**:

- `Id`
- `RestaurantId`
- `Name`
- `Description`
- `CurrentPrice`
- `ImageUrl`
- `IsAvailable`

**Relationships**:

- Child entity of `Restaurant`.
- Referenced by `CartItem` through product ID and product name snapshots.
- Converted to `CheckoutItemSnapshot` during checkout.

**Validation and invariants**:

- Product is mutated only through `Restaurant`.
- Current price is a non-negative `Money`.
- Availability affects add-to-cart and checkout behavior.

### Cart

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Basket/Cart.cs`

**Fields used by Phase 3**:

- `Id`
- `CustomerId`
- `RestaurantId`
- `Status`
- `Items`
- `CreatedAt`

**Relationships**:

- Aggregate root for `CartItem`.
- Belongs to one customer.
- Contains items from one restaurant only.

**Validation and invariants**:

- Created only through `Cart.Create` with a first valid product.
- No normal empty active cart workflow.
- Cart expires one hour after `CreatedAt`.
- Only active, non-expired carts can be modified.
- Cross-restaurant additions are rejected.
- Checked-out and cleared carts cannot be modified.

**State transitions**:

```text
Active -> CheckedOut
Active -> Cleared
Active -> Expired (derived from CreatedAt and current time)
```

Expired is derived behavior, not a stored `CartStatus` value.

### CartItem

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Basket/CartItem.cs`

**Fields used by Phase 3**:

- `ProductId`
- `ProductName`
- `Quantity`

**Relationships**:

- Child entity of `Cart`.

**Validation and invariants**:

- Product ID must be positive.
- Product name is required.
- Quantity must be positive.
- Quantity changes occur through `Cart`.

### Customer

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Customer/Customer.cs`

**Fields used by Phase 3**:

- `Id`
- `FullName`
- `Age`
- `PhoneNumber`
- `Addresses`

**Relationships**:

- Aggregate root for `CustomerAddress`.
- Owns saved delivery addresses.
- Provides delivery address snapshots for orders.

**Validation and invariants**:

- Customer ID and age must be positive.
- Full name is required.
- Phone number is optional.
- Duplicate saved addresses are rejected by ID or address value.
- Only one default address is allowed.
- Address snapshots are created through the customer aggregate.

### CustomerAddress

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Customer/CustomerAddress.cs`

**Fields used by Phase 3**:

- `Id`
- `Details`
- `IsDefault`

**Relationships**:

- Child entity of `Customer`.
- Wraps `Address`.

**Validation and invariants**:

- Address ID must be positive.
- Address details are required.
- Default status is coordinated by `Customer`.

### Order

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Ordering/Order.cs`

**Fields used by Phase 3**:

- `Id`
- `CustomerId`
- `RestaurantId`
- `DeliveryAddress`
- `Items`
- `TotalAmount`
- `CreatedAt`

**Relationships**:

- Aggregate root for `OrderItem`.
- Created from checkout snapshots.
- Belongs to one customer and one restaurant.

**Validation and invariants**:

- Created only after checkout succeeds.
- Requires at least one checkout item.
- Requires a delivery address snapshot.
- Preserves historical item name, quantity, unit price, line total, total amount, and address snapshot.

### OrderItem

**Source**: `src/Talabat/Talabat.Domain/Aggregates/Ordering/OrderItem.cs`

**Fields used by Phase 3**:

- `ProductId`
- `ProductName`
- `UnitPrice`
- `Quantity`
- `LineTotal`

**Relationships**:

- Child entity of `Order`.

**Validation and invariants**:

- Created from checkout item snapshots.
- Preserves historical product and price data.

## Existing Value Objects

### Money

- Non-negative amount.
- Supports addition and quantity multiplication.
- Used for product prices, cart totals, order item line totals, and order totals.

### Address

- Required street, city, and building number.
- Optional floor.
- Equality is case-insensitive by field.
- Used for saved customer addresses.

### DeliveryAddressSnapshot

- Required street, city, and building number.
- Optional floor.
- Stored on orders as historical delivery address data.

### CatalogProductSnapshot

- Product ID, restaurant ID, product name, and availability.
- Used by cart creation and add-item workflow.

### CheckoutItemSnapshot

- Product ID, product name, unit price, and quantity.
- Produced by checkout validation and used to create an order.

## Application Request Models

Request models are transport-neutral. They are not HTTP request DTOs.

### Catalog

- `BrowseRestaurantsQuery`
  - No customer ID required.
- `GetRestaurantMenuQuery`
  - `restaurantId`

### Basket

- `GetCartQuery`
  - `customerId`
- `AddCartItemCommand`
  - `customerId`
  - `restaurantId`
  - `productId`
  - `quantity`
- `UpdateCartItemQuantityCommand`
  - `customerId`
  - `productId`
  - `quantity`
- `RemoveCartItemCommand`
  - `customerId`
  - `productId`
- `ClearCartCommand`
  - `customerId`

### Customers

- `GetCustomerProfileQuery`
  - `customerId`
- `UpdateCustomerProfileCommand`
  - `customerId`
  - `fullName`
  - `age`
  - `phoneNumber`
- `AddCustomerAddressCommand`
  - `customerId`
  - `street`
  - `city`
  - `buildingNumber`
  - `floor`
  - `makeDefault`
- `RemoveCustomerAddressCommand`
  - `customerId`
  - `addressId`
- `SetDefaultCustomerAddressCommand`
  - `customerId`
  - `addressId`

### Ordering

- `CheckoutCommand`
  - `customerId`
  - `deliveryAddressId`
- `GetOrderHistoryQuery`
  - `customerId`
- `GetOrderDetailsQuery`
  - `customerId`
  - `orderId`

## Application Response Models

Response models should be immutable and transport-neutral.

### Catalog

- `RestaurantSummary`
  - `id`
  - `name`
  - `description`
  - `imageUrl`
  - `isOpen`
- `RestaurantMenu`
  - `restaurantId`
  - `restaurantName`
  - `products`
- `MenuProduct`
  - `id`
  - `name`
  - `description`
  - `currentPrice`
  - `imageUrl`
  - `isAvailable`

### Basket

- `CartDetails`
  - `id`
  - `customerId`
  - `restaurantId`
  - `status`
  - `items`
  - `totalAmount`
  - `expiresAtUtc`
- `CartLineItem`
  - `productId`
  - `productName`
  - `quantity`
  - `currentUnitPrice`
  - `lineTotal`

### Customers

- `CustomerProfile`
  - `id`
  - `fullName`
  - `age`
  - `phoneNumber`
  - `addresses`
- `CustomerAddressDetails`
  - `id`
  - `street`
  - `city`
  - `buildingNumber`
  - `floor`
  - `isDefault`

### Ordering

- `CheckoutOutcome`
  - Existing Application type with success and unavailable-products outcomes.
- `OrderSummary`
  - `id`
  - `restaurantId`
  - `createdAtUtc`
  - `totalAmount`
- `OrderDetails`
  - `id`
  - `customerId`
  - `restaurantId`
  - `createdAtUtc`
  - `deliveryAddress`
  - `items`
  - `totalAmount`
- `OrderLineItem`
  - `productId`
  - `productName`
  - `unitPrice`
  - `quantity`
  - `lineTotal`

## Application Result Model

Use cases should return a transport-neutral result shape such as:

```text
UseCaseResult<T>
|-- Success(T value)
`-- Failure(ApplicationError error)
```

Expected error categories:

- `Validation`
- `NotFound`
- `Conflict`
- `Unavailable`
- `OwnershipMismatch`

These are not HTTP statuses. API mapping is deferred.

Expected error examples:

- `CustomerNotFound`
- `RestaurantNotFound`
- `ProductNotFound`
- `ProductUnavailable`
- `CartNotFound`
- `CartExpired`
- `CartNotActive`
- `CrossRestaurantCart`
- `CartItemNotFound`
- `AddressNotFound`
- `OrderNotFound`
- `CheckoutUnavailableProducts`

## Relationships

```text
Restaurant 1 -> many Product
Customer 1 -> many CustomerAddress
Customer 1 -> zero-or-one active Cart
Cart 1 -> many CartItem
Cart many -> one Restaurant by RestaurantId
Order 1 -> many OrderItem
Order many -> one Customer by CustomerId
Order many -> one Restaurant by RestaurantId
Order 1 -> one DeliveryAddressSnapshot
```

## Phase 3 Deferred Data

The following data is intentionally not modeled for Phase 3:

- Identity user/account IDs.
- Roles, permissions, claims, tokens, or sessions.
- Payment state.
- Coupon, offer, review, notification, or restaurant-owner data.
- Delivery task, delivery agent, delivery assignment, route, location, and status workflows.
- Database keys, indexes, EF Core mappings, migrations, and persistence schemas.
