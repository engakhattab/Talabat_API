# Business Rule Classification

This document classifies each business rule from `docs/business-rules.md` for MVP v1.

MVP v1 does not include authentication or authorization. There is no login, register, JWT, Identity, roles, admin, restaurant owner, payment, delivery, notifications, coupons, or reviews.

| Rule ID | Short Description | Primary Owner | Where It Should Be Enforced | Notes |
|---|---|---|---|---|
| BR-CAT-001 | Only active restaurants are visible to customers | Application Rule | Restaurant browse query / catalog read filtering | Visibility is a read-use-case concern. The domain owns restaurant active state, but the application decides what browse results include. |
| BR-CAT-002 | Product belongs to exactly one restaurant | Cross-layer Rule | Restaurant/product model and database foreign key | The model should not allow orphan products, and the database must enforce the required restaurant relationship. |
| BR-CAT-003 | Customers can only see available products | Application Rule | Restaurant menu query / catalog read filtering | Product availability is domain state, but hiding unavailable products from menu responses is query behavior. |
| BR-CAT-004 | Product price cannot be negative | Cross-layer Rule | Money value object / product price rules / database check constraint | The domain should reject negative prices immediately; the database should also protect persisted price amounts. |
| BR-CAT-005 | Restaurant must have valid opening hours | Domain Rule | TimeRange value object / restaurant creation or update rules | The domain should define valid opening hours, including how midnight-crossing ranges work if supported. |
| BR-CART-001 | Customer can have only one active cart | Cross-layer Rule | Add-to-cart use case / cart repository / database unique constraint if cart status is used | A single Cart aggregate cannot prove uniqueness across all carts, so this also needs application and persistence protection. |
| BR-CART-002 | Cart expires after 1 hour | Domain Rule | Cart expiration method and all cart mutation methods | Expiration is a cart invariant and should be checked before changing cart state. |
| BR-CART-003 | Expired cart cannot be checked out | Cross-layer Rule | Checkout use case delegates to Cart expiration validation | Checkout is application orchestration, but the expired-cart business check belongs in the domain. |
| BR-CART-004 | Cart can contain items from only one restaurant | Domain Rule | Cart add-item behavior | This is a core Cart aggregate invariant. |
| BR-CART-005 | Quantity must be greater than zero | Cross-layer Rule | Cart and CartItem quantity rules / database check constraint | The domain should reject invalid quantities, and the database should prevent invalid persisted rows. |
| BR-CART-006 | Duplicate products are merged | Domain Rule | Cart add-item behavior | The Cart aggregate should merge duplicate products by increasing quantity. |
| BR-CART-007 | Product price is snapshotted in cart | Domain Rule | Cart add-item behavior / CartItem creation | CartItem should store product name and unit price at the moment the item is added. |
| BR-CART-008 | Unavailable products cannot be added to cart | Domain Rule | Cart add-item behavior using product availability | The application loads product data, but the domain rejects unavailable products. |
| BR-CART-009 | Customer can clear cart | Domain Rule | Cart clear behavior | Clearing cart items is aggregate behavior. |
| BR-ORD-001 | Empty cart cannot be checked out | Cross-layer Rule | Checkout use case delegates to checkout domain validation | Checkout starts in the application layer, but empty-cart rejection is business validation. |
| BR-ORD-002 | Restaurant must be active during checkout | Cross-layer Rule | Checkout use case delegates to restaurant/domain validation | The application loads the restaurant; the domain decides whether inactive restaurants can be checked out. |
| BR-ORD-003 | Restaurant must be open during checkout | Cross-layer Rule | Checkout use case delegates to restaurant opening-hours validation | Time-based restaurant availability should stay in the domain model or a domain service. |
| BR-ORD-004 | Checkout validates product availability again | Cross-layer Rule | Checkout use case delegates to checkout domain service | This protects against stale cart state after catalog availability changes. |
| BR-ORD-005 | Checkout validates current prices | Cross-layer Rule | Checkout use case delegates to checkout domain service | Current-price comparison is business validation, but checkout orchestration belongs to the application layer. |
| BR-ORD-006 | Order stores immutable item snapshots | Domain Rule | Order and OrderItem creation | The order aggregate should preserve historical product name, price, quantity, and line total. |
| BR-ORD-007 | Order total is calculated from order items | Domain Rule | Order creation / order total calculation | The domain should calculate totals instead of trusting caller-provided totals. |
| BR-ORD-008 | Customer can only view their own orders | Application Rule | Order query filtering by the MVP customer profile | MVP v1 has one normal customer profile, so order reads should be scoped to that profile without authentication. |
| BR-CUS-001 | Customer profile exists before using customer features | Cross-layer Rule | Application setup / customer profile model / database relationships | Cart, address, and order records should link to the MVP customer profile. |
| BR-CUS-002 | Customer can have multiple addresses | Domain Rule | Customer address collection behavior | The Customer aggregate owns address management. |
| BR-CUS-003 | Customer can have only one default address | Cross-layer Rule | Customer set-default-address behavior / database filtered unique index | The domain should unset other defaults; the database should prevent conflicting default rows. |
| BR-CUS-004 | Duplicate address should be rejected | Domain Rule | Customer add-address behavior | The Customer aggregate should compare against existing addresses before adding a new one. |
| BR-CUS-005 | Checkout requires a delivery address | Application Rule | Checkout use case validation before order creation | The application should ensure a selected customer address exists before creating the order. |

## Rules That Need Database Protection

- `Quantity > 0`.
- `Price amount >= 0`.
- `Product` belongs to one `Restaurant`.
- `Cart` belongs to one `Customer`.
- `Order` belongs to one `Customer`.
- `OrderItem` belongs to one `Order`.
- `CartItem` belongs to one `Cart`.
- One default address per customer.
- One active cart per customer, if using cart status.
- Customer address belongs to one `Customer`.

## Out of Scope For MVP v1

- Authentication.
- Authorization.
- Admin roles.
- Customer login/register.
- JWT.
- Payment.
- Delivery drivers.
- Notifications.
- Discounts/coupons.
- Reviews.
- Restaurant owners.
