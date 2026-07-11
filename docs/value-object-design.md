# Value Object Design

> Phase 0 scope update: This document was written for MVP v1. Its value object guidance remains useful, but Delivery value objects now exist in code and Identity/Auth remains a deferred boundary outside value objects.

This document defines the value object and temporary snapshot design for Talabat MVP v1.

This is a design document only. It does not generate C# code and does not create entities, repositories, controllers, handlers, EF configurations, or migrations.

MVP v1 scope:

- No authentication, authorization, Identity, login/register, JWT, admins, or restaurant owners.
- No payment, delivery drivers, notifications, coupons, or reviews.
- Assume one normal customer profile.
- Restaurants and products are seeded for testing.
- Cart is not created until the first item is added.

## How To Decide If Something Is A Value Object

A Value Object:

- Has no identity.
- Is defined by its values.
- Is immutable.
- Has equality by value.
- Usually contains validation.
- May contain behavior related to the value.
- May be persisted as part of an entity.

Value objects make the domain model more explicit. Instead of passing raw strings, numbers, or time fields everywhere, the domain can group related values and protect their rules in one place.

Not every immutable object is a persisted Value Object. Some immutable objects are temporary input snapshots used only during a workflow. For example, `CatalogProductSnapshot` and `CheckoutItemSnapshot` safely pass data between contexts without sharing child entities across aggregate boundaries, but they are not persisted as independent value objects in MVP v1.

## Value Object Summary

| Name | Classification | Persisted? | Used By | Why |
|---|---|---|---|---|
| `Money` | Persisted Value Object | Yes where owned | Product current price, transient Cart total calculation, OrderItem unit price, OrderItem line total, Order total | Monetary amount with validation, value equality, and arithmetic behavior. Cart does not persist money. |
| `TimeRange` | Persisted Value Object | Yes | Restaurant opening hours | Start/end time form one business concept and need `Contains(time)` behavior, including midnight-crossing ranges. |
| `Address` | Persisted Value Object | Yes | CustomerAddress | Street/city/building/floor form one value and support validation and duplicate detection. |
| `DeliveryAddressSnapshot` | Persisted Value Object | Yes | Order | Order needs a copied immutable address at checkout time so historical orders remain correct after customer address edits. |
| `CatalogProductSnapshot` | Temporary Input Snapshot | No | Cart.AddItem | Prevents passing Catalog Product entity directly into Basket Cart; Cart uses it to create CartItem snapshots. |
| `CheckoutItemSnapshot` | Temporary Input Snapshot | No | Order.CreateFromCheckout | Prevents Order from depending directly on CartItem; Order uses it to create OrderItem children. |

## Money

### Purpose

`Money` represents a non-negative monetary amount in the domain. MVP v1 assumes a single currency, EGP, so the value object stores only the amount for now.

`Money` exists because current Catalog prices, order item prices, line totals, and order totals must not be treated as arbitrary decimals. The domain needs one place to enforce non-negative money and safe arithmetic.

### Fields Conceptually Needed

- Amount.

MVP v1 does not store currency because all money is assumed to be EGP.

### Validation Rules

- Amount must be greater than or equal to zero.
- Arithmetic must not produce a negative amount.
- Money(0) is valid.
- Multiplying a unit price by an item quantity should reject quantity less than or equal to zero because cart/order item quantities must be positive.

### Behavior

- Add two monetary amounts.
- Multiply a monetary amount by a positive quantity.
- Compare money values by amount.

Subtract may be added later if needed. MVP v1 mainly needs Add, Multiply, and Compare. Avoid adding unused monetary behavior until the domain needs it.

### Equality Rules

Two `Money` values are equal when their amounts are equal. Because MVP v1 assumes EGP, currency is not part of equality yet.

If currency is added later, equality must include both amount and currency.

### Where It Is Used

- Product current price.
- Transient Cart line totals and total calculated from caller-supplied current prices.
- OrderItem unit price.
- OrderItem line total.
- Order total.

### EF Core Persistence Notes

- Persist `Money` as part of the owning entity, not as a separate table.
- Store the amount in the owner table or owned component columns.
- Add database protection for price and total amounts greater than or equal to zero where practical.

### Common Mistakes

- Using raw numeric types everywhere and duplicating validation.
- Allowing negative prices or totals.
- Letting callers provide order totals instead of calculating totals from order items.
- Adding currency too early when MVP v1 only needs EGP.
- Multiplying by zero or negative quantity without rejecting it.

### Future Improvement

- Add Currency when the project needs multi-currency support.

## TimeRange

### Purpose

`TimeRange` represents restaurant opening hours as one business concept. It keeps start/end validation and open-at-time checks together.

Restaurant opening hours are not just two unrelated primitive times. The domain needs behavior that answers whether a time falls inside the range, including ranges that cross midnight.

### Fields Conceptually Needed

- Start time.
- End time.

### Validation Rules

- Start time is required.
- End time is required.
- Start and end should not be equal in MVP v1.
- Equal start/end times could mean 24-hour open in a future version, but MVP v1 treats equal times as invalid to avoid ambiguity.

### Behavior

- Check whether a given time is inside a normal range, such as 09:00 to 17:00.
- Check whether a given time is inside a midnight-crossing range, such as 22:00 to 02:00.

For normal ranges, the checked time should be between start and end. For midnight-crossing ranges, the checked time is valid when it is after the start or before the end.

### Equality Rules

Two `TimeRange` values are equal when their start and end times are equal.

### Where It Is Used

- Restaurant opening hours.
- Restaurant open/closed checkout validation.

### EF Core Persistence Notes

- Persist `TimeRange` as part of `Restaurant`.
- Store start and end time values in the restaurant table or owned component columns.
- Do not persist it as a separate aggregate or separate root table.

### Common Mistakes

- Treating opening hours as unrelated primitive fields.
- Forgetting midnight-crossing ranges.
- Treating equal start/end times as 24-hour open without an explicit rule.
- Using date/time concepts when the domain only needs time-of-day for MVP v1.

## Address

### Purpose

`Address` represents customer address details as one value. It groups related address fields, validates required parts, and supports duplicate address detection.

The Customer aggregate owns `CustomerAddress` entities, but the address details themselves are value-like: street, city, building number, and optional floor define the value.

Address and DeliveryAddressSnapshot may contain the same fields, but they have different intent. Address represents a saved customer address. DeliveryAddressSnapshot represents the copied historical delivery address stored inside an Order at checkout time.

### Fields Conceptually Needed

- Street.
- City.
- BuildingNumber.
- Floor.

### Validation Rules

- Street is required and cannot be empty.
- City is required and cannot be empty.
- BuildingNumber is required and cannot be empty.
- Floor is optional.
- Normalization should be considered for duplicate detection, such as trimming whitespace and comparing case-insensitively where appropriate.

### Behavior

- Validate required address fields.
- Normalize address fields for comparison.
- Compare address details for duplicate detection inside the Customer aggregate.

### Equality Rules

Two `Address` values are equal when their normalized address fields are equal.

Normalization matters because `Street A`, ` street a `, and similar user-entered variations may represent the same address.

### Where It Is Used

- CustomerAddress address details.
- Duplicate address checks.
- Delivery address snapshot creation at checkout time.

### EF Core Persistence Notes

- Persist `Address` as part of `CustomerAddress`.
- Store address fields in the customer address table or owned component columns.
- Address is not an aggregate root and should not have its own repository.

### Common Mistakes

- Treating address fields as unrelated strings.
- Allowing empty required fields.
- Comparing duplicate addresses without normalization.
- Putting authentication or user identity concepts into CustomerAddress.

## DeliveryAddressSnapshot

### Purpose

`DeliveryAddressSnapshot` is the immutable address copied into an Order at checkout time.

It exists so historical orders remain correct even if the customer later edits or deletes a saved address. The order should preserve the delivery address used when checkout succeeded.

Address and DeliveryAddressSnapshot may contain the same fields, but they have different intent. Address represents a saved customer address. DeliveryAddressSnapshot represents the copied historical delivery address stored inside an Order at checkout time.

### Fields Conceptually Needed

- Street.
- City.
- BuildingNumber.
- Floor.

Optional future audit data may include the original customer address id, but the order must not rely only on `CustomerAddressId`.

### Validation Rules

- Street is required and cannot be empty.
- City is required and cannot be empty.
- BuildingNumber is required and cannot be empty.
- Floor is optional.
- Snapshot data must be complete enough to display or audit an old order without reading CustomerAddress.

### Behavior

- Preserve copied address details.
- Provide displayable delivery address data for order history.

It should not update itself when the customer changes an address.

### Equality Rules

Two `DeliveryAddressSnapshot` values are equal when their copied address fields are equal.

### Where It Is Used

- Order delivery address snapshot.
- Order history display.
- Checkout order creation.

### EF Core Persistence Notes

- Persist `DeliveryAddressSnapshot` as part of `Order`.
- Store copied address fields in the order table or owned component columns.
- Do not map it as a separate aggregate.
- Do not rely only on a foreign key to CustomerAddress for historical order delivery data.

### Common Mistakes

- Storing only `CustomerAddressId` on Order.
- Letting address edits change old order delivery data.
- Treating delivery address as a mutable reference after order creation.
- Reusing CustomerAddress directly inside Order instead of copying the value.

## CatalogProductSnapshot

### Purpose

`CatalogProductSnapshot` is a temporary input snapshot, not a persisted Value Object.

It safely passes the Catalog product data needed by Basket without giving Cart a direct dependency on the Catalog `Product` entity.

### Fields Conceptually Needed

- ProductId.
- RestaurantId.
- ProductName.
- IsAvailable.

### How It Is Created And Used

- The Application layer creates it after reading Catalog data.
- Cart uses it to enforce add-to-cart rules.
- Cart uses it to create or merge a CartItem without copying a price.
- It prevents sharing Catalog entities directly across bounded contexts.

### Validation Rules

The Application layer should create CatalogProductSnapshot only after loading a valid Catalog product. The snapshot itself can validate simple structural rules such as positive ids, non-empty product name, and availability flag presence. It should not contain price, query the database, or prove that the product exists.

### Persistence Notes

- It should not be mapped as an EF entity.
- It should not be stored as a separate table.
- Persisted state belongs to CartItem after Cart accepts the add-item operation.

### Common Mistakes

- Passing Catalog `Product` directly into Cart.
- Persisting `CatalogProductSnapshot` as its own table.
- Adding current price to this snapshot or persisting price in CartItem.
- Letting Basket own product lifecycle data.

## CheckoutItemSnapshot

### Purpose

`CheckoutItemSnapshot` is a temporary input snapshot, not a persisted Value Object.

It safely passes checkout item data into Order creation without making Order depend directly on CartItem, which is a child entity owned by the Cart aggregate.

### Fields Conceptually Needed

- ProductId.
- ProductName.
- UnitPrice.
- Quantity.

### How It Is Created And Used

- The checkout use case creates it from validated Cart selections plus current Catalog product names and prices.
- Order uses it to create OrderItem children.
- It prevents Order from depending directly on CartItem.
- It keeps aggregate boundaries clear between Basket and Ordering.

### Validation Rules

The checkout use case should create CheckoutItemSnapshot only after validating Cart data and loading current Catalog product data. Its `UnitPrice` is the final current price accepted for Order creation, not an old cart price. The snapshot itself can validate simple structural rules such as positive ProductId, non-empty ProductName, valid UnitPrice, and Quantity greater than zero. It should not know about Cart or query persistence.

### Persistence Notes

- It should not be mapped as an EF entity.
- It should not be stored as a separate table.
- Persisted state belongs to OrderItem after Order creation.

### Common Mistakes

- Passing CartItem entities directly into Order.
- Persisting CheckoutItemSnapshot as its own table.
- Treating it as an order line instead of an input used to create an order line.
- Letting Order mutate CartItem.

## Value Objects vs Temporary Snapshots

Persisted Value Objects become part of entity state. They are stored with their owning entity and help protect domain rules over time. In MVP v1, `Money`, `TimeRange`, `Address`, and `DeliveryAddressSnapshot` are persisted value objects.

Temporary snapshots only pass data safely across workflow boundaries. They are useful when one context needs data from another context but should not receive or mutate the other context's entities. In MVP v1, `CatalogProductSnapshot` and `CheckoutItemSnapshot` are temporary snapshots.

Temporary snapshots may be immutable, but immutability alone does not make them persisted value objects. The key question is whether the object becomes part of the owning entity's long-term state. If it only exists to carry data during one workflow, it should stay a temporary input model.
