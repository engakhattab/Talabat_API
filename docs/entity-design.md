# Entity Design

> Phase 0 scope update: This document was written for the original MVP v1. Customer remains a domain profile, not an identity account, but future Identity/Auth integration is reserved/TBD. Do not add framework-specific identity types to these entities during current phases.

This document derives the current entity design from the business rules, bounded contexts, and aggregate boundaries.

This is a design document only. It does not define C# code, repositories, controllers, handlers, EF configurations, or migrations.

Original MVP v1 scope:

- No authentication, authorization, Identity, login/register, JWT, admins, or restaurant owners.
- No payment, delivery drivers, notifications, coupons, or reviews.
- Assume one normal customer profile.
- Restaurants and products are seeded for testing.
- Cart is not created until the first item is added.

Current Phase 1 decision:

- The old scope is historical. Authentication/authorization, Customer Website, Delivery Website, and Identity/Auth Portal support are deferred future concerns.
- Domain entities must remain independent from Identity/Auth frameworks.
- Do not add `ApplicationUser`, `IdentityUser`, `ClaimsPrincipal`, `HttpContext`, JWT claims, or IdentityServer-specific types to entities.
- Future account/profile linkage is reserved/TBD and must be handled without making the Domain depend on an identity framework.

## Restaurant

### Context

Catalog Context.

### Role

`Restaurant` is the Catalog aggregate root. It groups products under one restaurant and protects catalog rules around active state, opening hours, product ownership, product availability, and product prices.

### Identity

A restaurant is identified by its restaurant id.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Restaurant id | Primitive | Distinguishes one seeded restaurant from another and lets carts/orders reference the restaurant. | BR-CAT-002, BR-CART-004, BR-ORD-002 |
| Name | Primitive | Needed so customers can browse and recognize restaurants. | BR-CAT-001 |
| Description | Primitive | Supports catalog display for seeded restaurant data. | BR-CAT-001 |
| Opening hours | Value Object | Encapsulates valid opening-hours data and checkout open/closed checks. | BR-CAT-005, BR-ORD-003 |
| Active state | Primitive | Determines whether a restaurant appears to customers and whether checkout can proceed. | BR-CAT-001, BR-ORD-002 |
| Products | Collection | Products are child entities owned by the restaurant. External code should not create orphan products. | BR-CAT-002, aggregate invariant: product belongs to one restaurant |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Check whether restaurant is open at a given time | BR-CAT-005, BR-ORD-003 | Opening hours belong to the restaurant, so the restaurant should answer whether it can accept checkout at a time. | Query |
| Activate restaurant | BR-CAT-001, BR-ORD-002 | Active state belongs to the restaurant aggregate. | Command |
| Deactivate restaurant | BR-CAT-001, BR-ORD-002 | Active state changes must stay inside the aggregate so visibility and checkout rules use one source of truth. | Command |
| Add product | BR-CAT-002, BR-CAT-004 | Product belongs to one restaurant, so product creation should happen through the restaurant aggregate. | Command |
| Update product price | BR-CAT-004, BR-ORD-005 | The current catalog price is owned by Catalog and must remain non-negative. | Command |
| Mark product available | BR-CAT-003, BR-CART-008, BR-ORD-004 | Product availability is catalog state owned by the restaurant/product aggregate. | Command |
| Mark product unavailable | BR-CAT-003, BR-CART-008, BR-ORD-004 | Product availability affects menu visibility, cart add, and checkout validation. | Command |

### Encapsulation Rules

- External code must not directly add or remove products from the product collection.
- External code must not directly set product price.
- External code must not directly toggle product availability.
- Product creation should go through the restaurant aggregate.
- Catalog mutation behavior may exist for completeness and seeding/testing, but MVP v1 exposes no public catalog-management API.

### Value Object Candidates

- `Money`: product price is a value because it is defined by amount, has no identity, and must reject negative values.
- `TimeRange`: opening hours are a value because they are defined by start/end times and contain the "is this time inside the range" rule.

### Notes

Restaurants and products are seeded for MVP v1. There is no admin or restaurant owner workflow.

## Product

### Context

Catalog Context.

### Role

`Product` is a child entity inside the Restaurant aggregate. It represents the current catalog menu item, not a cart item and not an order item.

### Identity

A product is identified by its product id. It also belongs to exactly one restaurant.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Product id | Primitive | Identifies the menu item so Basket can reference current Catalog data and Ordering can keep historical product references. | BR-CAT-002, BR-CART-007, BR-ORD-006 |
| Restaurant id | Entity reference id | Enforces that a product belongs to one restaurant and supports the one-restaurant-per-cart check. | BR-CAT-002, BR-CART-004 |
| Name | Primitive | Supplies current Catalog display data and is copied into immutable OrderItem history at checkout. | BR-CART-007, BR-ORD-006 |
| Description | Primitive | Supports menu display for seeded catalog data. | BR-CAT-003 |
| Current price | Value Object | Represents the current Catalog price used for cart display and final checkout pricing. | BR-CAT-004, BR-CART-007, BR-ORD-005 |
| Availability state | Primitive | Determines whether customers can see/add/order the product. | BR-CAT-003, BR-CART-008, BR-ORD-004 |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Change current price | BR-CAT-004, BR-CART-007, BR-ORD-005 | Current product price belongs to Catalog; price changes must preserve the non-negative price invariant. | Command |
| Mark available | BR-CAT-003, BR-CART-008, BR-ORD-004 | Availability is part of product state. | Command |
| Mark unavailable | BR-CAT-003, BR-CART-008, BR-ORD-004 | Availability changes affect visibility, cart add, and checkout validation. | Command |
| Report whether product is available | BR-CAT-003, BR-CART-008, BR-ORD-004 | Other workflows need to know availability without mutating product state. | Query |

### Encapsulation Rules

- Product may contain internal behavior such as changing price or availability, but application use cases should access those behaviors through the Restaurant aggregate root, not by loading or modifying Product directly.
- Product price must not be represented as a raw unvalidated amount.
- Product availability should not be changed by Basket or Ordering.

### Value Object Candidates

- `Money`: price is a value object because equality and validity are based on amount, not identity.

### Notes

Product in Catalog is current menu data. CartItem references a selected product but stores no price. OrderItem stores an immutable historical snapshot created from current Product data at checkout.

## Cart

### Context

Basket Context.

### Role

`Cart` is the Basket aggregate root. It represents the customer's active pre-checkout selection and protects cart invariants such as expiry, status, one restaurant per cart, positive quantities, and duplicate merging. It does not own product prices.

### Identity

A cart is identified by its cart id. In MVP v1, a cart is created only when the first item is added.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Cart id | Primitive | Identifies the cart aggregate once the first item is added. | BR-CART-001 |
| Customer id | Entity reference id | Links the cart to a customer profile. | BR-CART-001, BR-CUS-001 |
| Restaurant id | Entity reference id | Assigned from the first item and used to enforce one restaurant per cart. | BR-CART-004 |
| Created time | Primitive | Supports the 1-hour expiry rule. | BR-CART-002, BR-CART-003 |
| Status | Enum | Distinguishes Active, CheckedOut, and Cleared carts so only active carts can be modified. | Aggregate invariant: only Active carts can be modified |
| Items | Collection | CartItem children hold selected product references and quantities without prices. | BR-CART-005, BR-CART-006, BR-CART-007 |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Create with first product snapshot | BR-CART-001, BR-CART-004, BR-CART-005, BR-CART-008 | The factory prevents an empty newly-created Cart and establishes RestaurantId from the first valid item. | Command |
| Add item from product snapshot | BR-CART-004, BR-CART-005, BR-CART-006, BR-CART-008 | Add item enforces multiple cart-wide invariants and must see existing items. It does not copy a price. | Command |
| Remove item | BR-CART-002, BR-CART-009 | Removing an item mutates the cart and must respect expiry/status. | Command |
| Update item quantity | BR-CART-002, BR-CART-005 | Quantity changes must preserve positive quantity and active cart rules. | Command |
| Clear cart | BR-CART-009 | Clearing cart contents is a cart aggregate operation. | Command |
| Check whether cart is expired | BR-CART-002, BR-CART-003 | Expiry is based on cart creation/expiry state. | Query |
| Calculate total from supplied current prices | BR-CART-007 | Cart owns quantities and can calculate line totals when the caller supplies current Catalog prices keyed by ProductId. | Query |
| Mark checked out | Aggregate invariant: only Active carts can be modified | Checkout lifecycle changes the cart status and prevents later mutation. | Command |

### Encapsulation Rules

- External code must not directly add, remove, or update CartItem children.
- External code must not directly set quantity, restaurant id, or status.
- Application use cases should load Cart, call Cart behavior, then save.
- Cart must not receive the Catalog Product entity directly. It should receive a simple product snapshot containing product id, restaurant id, product name, and availability.

### Value Object Candidates

- `CatalogProductSnapshot`: add-to-cart input is a value-like snapshot crossing from Catalog to Basket.

### Notes

`Cart.RestaurantId` comes from the first added item. An empty active cart should not be created or persisted before the first item is added.

For MVP v1, cart expiration is calculated from CreatedAt and currentTime. ExpiresAt is not stored to avoid inconsistency.

`ExpiresAt` may be considered later if expiration needs to be persisted for querying or reporting, but it should not be part of the MVP v1 main cart state.

Cart exposes `GetTotal(currentPrices)` but stores no price or total. A later Application/read flow loads current Product prices from Catalog and passes them into the calculation. Cart never queries Catalog or repositories.

## CartItem

### Context

Basket Context.

### Role

`CartItem` is a child entity inside the Cart aggregate. It stores the selected Product id, required product name, and quantity. It never stores a product price.

### Identity

Business identity inside a Cart is ProductId. MVP v1 does not add a separate CartItemId to the Domain model. Persistence can use CartId + ProductId as the owner/composite key.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Product id | Entity reference id | Identifies which current Catalog product was selected and enables duplicate merging. | BR-CART-006, BR-CART-007 |
| Product name | Primitive | Required Basket display data; current Catalog data remains authoritative at checkout. | BR-CART-007 |
| Quantity | Primitive | Tracks selected amount and must stay greater than zero. | BR-CART-005, BR-CART-006 |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Increase quantity | BR-CART-006, BR-CART-005 | Duplicate merge changes the existing line quantity while preserving positive quantity. | Command |
| Set quantity | BR-CART-005 | Quantity validation belongs where quantity is stored, while Cart decides whether the operation is allowed. | Command |
| Match selected product id | BR-CART-006 | Cart needs to find existing items for duplicate merging. | Query |

### Encapsulation Rules

- Application code must not query or update CartItem directly.
- CartItem quantity should only change through Cart behavior.
- CartItem must not acquire or expose a unit price. Product name, if retained, is convenience data rather than pricing authority.

### Value Object Candidates

- No monetary value object belongs to CartItem.

### Notes

CartItem is not a Product. It records a selection and quantity; Catalog remains the source of current product details and price.

A database unique constraint on CartId + ProductId can protect against duplicate cart lines.

## Order

### Context

Ordering Context.

### Role

`Order` is the Ordering aggregate root. It represents the immutable result of successful checkout and owns historical item snapshots, delivery address snapshot, and total calculation.

### Identity

An order is identified by its order id.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Order id | Primitive | Identifies the historical order record. | BR-ORD-008 |
| Customer id | Entity reference id | Links the order to a customer profile and scopes order history. | BR-ORD-008, BR-CUS-001 |
| Restaurant id | Entity reference id | Records which restaurant the order belongs to. | BR-ORD-002, BR-ORD-003 |
| Created time | Primitive | Records when checkout succeeded. | BR-ORD-006 |
| Delivery address snapshot | Value Object | Preserves delivery address at checkout time even if the customer later changes addresses. | BR-CUS-005, aggregate invariant: order must contain delivery address snapshot |
| Items | Collection | OrderItem children store immutable product and price snapshots. | BR-ORD-006, BR-ORD-007 |
| Total amount | Value Object | Stores the total calculated from order items, not caller input. | BR-ORD-007 |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Create from checkout snapshots | BR-ORD-001, BR-ORD-006, BR-ORD-007, BR-CUS-005 | Order creation is where immutable item and delivery snapshots become the historical record. | Command |
| Calculate total from order items | BR-ORD-007 | Order owns its items and should not trust caller-provided totals. | Query |

### Encapsulation Rules

- External code must not directly add, remove, or update OrderItem children.
- External code must not directly set total amount.
- External code must not replace item snapshots or delivery address snapshot after creation.
- Order should not depend directly on CartItem. The checkout flow should pass checkout item snapshots.

### Value Object Candidates

- `Money`: order total, item unit prices, and line totals are monetary values.
- `DeliveryAddressSnapshot`: delivery address at checkout is a value because the order needs the copied address data, not a mutable address identity.
- `CheckoutItemSnapshot`: checkout input is an immutable input model used to create OrderItems from checkout data. It is not necessarily a persisted value object.

### Notes

Order stores historical facts. It should not point only to CustomerAddress for delivery data, because old orders must remain correct after address edits.

Order query handlers can read Order data and map it to order history DTOs. This is application/read-model behavior, not core domain behavior.

## OrderItem

### Context

Ordering Context.

### Role

`OrderItem` is a child entity inside the Order aggregate. It stores immutable historical product, quantity, unit price, and line total data.

### Identity

An OrderItem is identified by ProductId within its owning Order for MVP v1. Checkout merges duplicate cart products, so one Order contains one line per ProductId. No separate OrderItemId is currently part of the Domain model.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Product id snapshot | Entity reference id | Preserves which catalog product was ordered. | BR-ORD-006 |
| Product name snapshot | Primitive | Preserves the product name at checkout time. | BR-ORD-006 |
| Unit price snapshot | Value Object | Preserves the accepted unit price at checkout time. | BR-ORD-006, BR-ORD-005 |
| Quantity | Primitive | Preserves the ordered quantity. | BR-ORD-006 |
| Line total | Value Object | Preserves unit price multiplied by quantity for total calculation. | BR-ORD-006, BR-ORD-007 |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Calculate line total at creation | BR-ORD-006, BR-ORD-007 | The line total belongs to the order line and should be derived from its own unit price and quantity. | Query during creation |
| Expose immutable snapshot data | BR-ORD-006 | Order history needs item details without relying on current Catalog data. | Query |

### Encapsulation Rules

- Application code must not query or update OrderItem directly.
- OrderItem state should be created through Order and then remain immutable.
- Unit price, quantity, product name, and line total should not be changed after order creation.

### Value Object Candidates

- `Money`: unit price and line total are monetary values with non-negative validation.

### Notes

OrderItem is not Product and not CartItem. It is Ordering's immutable historical line.

## Customer

### Context

Customer Context.

### Role

`Customer` is the Customer aggregate root. It is a business profile that owns addresses and links carts/orders to a customer. It is not an authentication account.

### Identity

A customer is identified by its customer id. There is no identity-framework-specific account reference in the Domain model during Phase 1.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Customer id | Primitive | Links carts, orders, and addresses to the customer profile. | BR-CUS-001, BR-CART-001, BR-ORD-008 |
| Full name | Primitive | Gives the customer profile a required human-readable name. | BR-CUS-006 |
| Age | Primitive | Stores valid positive profile age. | BR-CUS-007 |
| Phone number | Primitive | Stores optional contact information without introducing identity or authentication. | BR-CUS-008 |
| Addresses | Collection | Customer owns multiple address children and enforces default/duplicate rules. | BR-CUS-002, BR-CUS-003, BR-CUS-004 |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Create customer profile | BR-CUS-001, BR-CUS-006, BR-CUS-007, BR-CUS-008 | Construction must establish a valid name and positive age while allowing an omitted phone number. | Command |
| Update profile details | BR-CUS-006, BR-CUS-007, BR-CUS-008 | Customer owns and validates changes to its profile data. | Command |
| Add address | BR-CUS-002, BR-CUS-004 | Customer owns the address collection and can detect duplicates. | Command |
| Remove address | BR-CUS-002 | Removing an address changes the customer-owned collection. | Command |
| Set default address | BR-CUS-003 | Only Customer can coordinate all addresses so exactly one is default. | Command |
| Create delivery address snapshot | BR-CUS-005 | Customer validates address ownership and returns immutable address data without exposing a mutable child lookup. | Query |

### Encapsulation Rules

- External code must not directly add or remove CustomerAddress children.
- External code must not directly set default flags on addresses.
- External code must not bypass full-name or age validation when changing profile details.
- Customer must not contain authentication or authorization data.
- Address changes should go through Customer behavior.

### Value Object Candidates

- `Address`: address details are value-like because they are defined by street/city/building/floor fields, not by separate identity.

### Notes

Customer is a profile, not an authenticated identity user. Do not add password, login, JWT, role, email-confirmation, admin, restaurant-owner, `ApplicationUser`, `IdentityUser`, or IdentityServer concepts to this aggregate in Phase 1.

The Customer constructor or factory should require customer id, full name, and age, with phone number optional. It should normalize the full name, reject an empty name, reject age less than or equal to zero, and start with an encapsulated address collection.

Future authenticated flows should resolve the authenticated account to a Customer profile at the Application/API boundary, then pass `Customer.Id` into Domain operations.

## CustomerAddress

### Context

Customer Context.

### Role

`CustomerAddress` is a child entity inside the Customer aggregate. It represents one saved delivery address for a customer profile.

### Identity

A customer address is identified by its customer address id inside the Customer aggregate.

### State / Properties

| Property | Type category only, not exact C# code | Why it exists | Related rule/invariant |
|---|---|---|---|
| Customer address id | Primitive | Identifies one address in the customer's address collection. | BR-CUS-002 |
| Address details | Value Object | Groups non-empty address fields and supports duplicate detection. | BR-CUS-004, invariant: address data cannot be empty |
| Default flag | Primitive | Marks the one address used by default when checkout needs delivery data. | BR-CUS-003, BR-CUS-005 |

### Behavior

| Behavior | Source rule | Why this entity owns it | Command or Query |
|---|---|---|---|
| Mark as default | BR-CUS-003 | The flag lives on the address, but Customer coordinates that only one address is default. | Command |
| Mark as non-default | BR-CUS-003 | Other addresses must become non-default when one address is selected. | Command |
| Compare address details for duplicate detection | BR-CUS-004 | Duplicate detection depends on address value equality. | Query |
| Expose address snapshot data for checkout | BR-CUS-005 | Ordering needs delivery address data copied into the order. | Query |

### Encapsulation Rules

- Application code must not directly change CustomerAddress default flags.
- Application code must not add duplicate addresses directly to the database.
- CustomerAddress should be modified through Customer as the aggregate root.

### Value Object Candidates

- `Address`: street, city, building, floor, and similar fields form a value object because equality and validation come from the fields.
- `DeliveryAddressSnapshot`: checkout/order should copy address details into an immutable order snapshot rather than depending only on CustomerAddress identity.

### Notes

CustomerAddress belongs to Customer. Ordering can use a copied delivery address snapshot, but it should not mutate CustomerAddress.
