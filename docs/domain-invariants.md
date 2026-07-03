# Domain Invariants

## Cart
- **Single Active Cart**: Exactly one active cart per customer.
- **Single Restaurant per Cart**: All items in a cart must belong to the same restaurant.
- **Positive Quantity**: The quantity of any cart item must be strictly greater than zero.
- **Duplicate Handling**: Adding a product already in the cart increases its quantity.
- **Expiration**: A cart expires 1 hour after creation and cannot be modified or checked out thereafter.

## Order
- **Immutability**: Once created, an order and its items are strictly immutable, ensuring historical record integrity.
- **Order Price Immutability**: The price of items in an order is locked at the time of creation and does not change even if catalog prices change.

## Restaurant
- **Active Status**: A restaurant must be active to accept orders.
- **Opening Hours**: A restaurant must be currently open (based on its defined time range) to accept orders.
- **Product Availability**: Only available products can be added to a cart or ordered.

## Checkout
- **Price Validation**: During checkout, the current catalog prices must be validated against the cart's price snapshots. If prices have changed, checkout fails.
