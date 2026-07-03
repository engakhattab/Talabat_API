# Bounded Contexts

This document defines the DDD bounded contexts for Talabat MVP v1.

MVP v1 does not include authentication, authorization, Identity, login/register, JWT, admins, restaurant owners, payment, delivery drivers, notifications, coupons, or reviews. The system assumes one normal customer profile. Restaurants and products are seeded for testing.

## Catalog Context

### Purpose

Catalog represents the restaurant menu space. It answers what restaurants exist, which restaurants are active, what products they offer, whether products are available, and what the current product prices are.

### What It Owns

- Restaurant lifecycle state for MVP v1 seeded data.
- Product lifecycle state for MVP v1 seeded data.
- Restaurant active state.
- Restaurant opening hours.
- Product availability.
- Product current price.

### Main Entities

- `Restaurant`
- `Product`

### Main Business Rules

- Only active restaurants are visible to customers.
- A product belongs to exactly one restaurant.
- Customers can only see available products.
- Product price cannot be negative.
- Restaurant must have valid opening hours.

### What It Does NOT Own

- Carts.
- Cart items.
- Orders.
- Order items.
- Customers.
- Customer addresses.
- Checkout orchestration.
- Authentication or authorization.
- Product ownership by restaurant users.

### How It Communicates With Other Contexts

- Basket reads product information from Catalog when adding items to a cart.
- Ordering reads current restaurant and product data from Catalog during checkout.
- Catalog does not mutate Basket, Ordering, or Customer data.

## Basket Context

### Purpose

Basket represents the customer's active shopping cart before checkout. It protects cart-level invariants and stores the price snapshot taken when a product is added to the cart.

### What It Owns

- The active customer cart.
- Cart items.
- One-restaurant-per-cart rule.
- Cart item quantity rules.
- Cart expiry.
- Duplicate product merging.
- Cart item product name and price snapshots.
- Clearing the cart.

### Main Entities

- `Cart`
- `CartItem`

### Main Business Rules

- Customer can have only one active cart.
- Cart expires after 1 hour.
- Expired cart cannot be modified.
- Expired cart cannot be checked out.
- Cart can contain items from only one restaurant.
- Quantity must be greater than zero.
- Duplicate products are merged.
- Product price is snapshotted in cart.
- Unavailable products cannot be added to cart.
- Customer can clear cart.

### What It Does NOT Own

- Product lifecycle.
- Product current price.
- Restaurant lifecycle.
- Restaurant opening hours.
- Orders or order history.
- Customer address management.
- Authentication or authorization.

### How It Communicates With Other Contexts

- Basket reads product id, product name, restaurant id, availability, and current price from Catalog when adding items.
- Ordering reads Basket cart data during checkout.
- Basket links carts to the single MVP customer profile from Customer Context.
- Basket does not directly create orders; checkout is handled by the Ordering use case.

## Ordering Context

### Purpose

Ordering represents checkout and historical order records. It validates the final checkout decision, creates immutable order snapshots, calculates totals, and provides order history for the MVP customer profile.

### What It Owns

- Checkout result.
- Order records.
- Order items.
- Immutable order item snapshots.
- Order total calculation.
- Order history.

### Main Entities

- `Order`
- `OrderItem`

### Main Business Rules

- Empty cart cannot be checked out.
- Restaurant must be active during checkout.
- Restaurant must be open during checkout.
- Checkout validates product availability again.
- Checkout validates current prices.
- Order stores immutable item snapshots.
- Order total is calculated from order items.
- Customer can only view orders linked to the MVP customer profile.
- Checkout requires a delivery address.

### What It Does NOT Own

- Product lifecycle.
- Product current price changes.
- Cart mutation outside the checkout use case.
- Customer profile creation.
- Customer address management.
- Payment.
- Delivery assignment.
- Notifications.
- Authentication or authorization.

### How It Communicates With Other Contexts

- Ordering uses Basket cart data during checkout.
- Ordering reads Catalog current product data during checkout.
- Ordering uses Customer profile and delivery address data during checkout.
- Ordering may clear or close the cart only as part of the checkout use case.

## Customer Context

### Purpose

Customer represents the single normal customer profile used by MVP v1. It owns profile data and customer addresses without modeling authentication, login, registration, roles, or Identity users.

### What It Owns

- Customer profile.
- Customer addresses.
- Default address rule.
- Duplicate address rejection.

### Main Entities

- `Customer`
- `CustomerAddress`

### Main Business Rules

- Customer profile exists before using customer features.
- Customer can have multiple addresses.
- Customer can have only one default address.
- Duplicate address should be rejected.
- Checkout requires a delivery address.

### What It Does NOT Own

- Authentication.
- Authorization.
- Identity users.
- Login/register.
- JWT.
- Roles.
- Admins.
- Restaurant owners.
- Carts.
- Orders.
- Catalog data.

### How It Communicates With Other Contexts

- Basket links carts to the MVP customer profile.
- Ordering links orders to the MVP customer profile.
- Ordering uses Customer addresses as delivery addresses during checkout.
- Customer does not inspect or mutate Catalog data.

## Context Map

```text
Customer -> Basket
Customer -> Ordering

Basket -> Catalog

Ordering -> Basket
Ordering -> Catalog
Ordering -> Customer
```

## Ubiquitous Language

| Term | Context | Meaning |
|---|---|---|
| Restaurant | Catalog | A seeded food provider that has active state, opening hours, and products. |
| Product | Catalog | A menu item owned by one restaurant, with current price and availability. |
| Cart | Basket | The customer's active pre-checkout container for selected products. |
| CartItem | Basket | A cart line storing product id, product name, quantity, and price snapshot. |
| PriceSnapshot | Basket | The product price copied into a cart item at add-to-cart time. |
| Order | Ordering | An immutable record created after successful checkout. |
| OrderItem | Ordering | An immutable order line storing historical product, quantity, price, and line total. |
| Customer | Customer | The single normal MVP customer profile, not an authenticated identity user. |
| CustomerAddress | Customer | An address owned by the customer profile. |
| DeliveryAddress | Ordering / Customer | The customer address selected for checkout and referenced by the order. |

## Important Boundary Decisions

- Product in Catalog is not the same as CartItem in Basket. Product represents the current menu item; CartItem represents the customer's selected item snapshot.
- Product price in Catalog is the current price.
- CartItem price snapshot is the price at add-to-cart time.
- OrderItem price is immutable historical price.
- Customer is a simple profile in MVP v1, not an authenticated identity user.
- Catalog data is seeded in MVP v1.
- Basket may read Catalog data, but it does not own product lifecycle or current prices.
- Ordering may use Basket, Catalog, and Customer data during checkout, but it owns only the resulting order record.
