# Domain Invariants

## Cart
- **Single Active Cart**: Exactly one active cart per customer.
- **Single Restaurant per Cart**: All items in a cart must belong to the same restaurant.
- **Positive Quantity**: The quantity of any cart item must be strictly greater than zero.
- **Duplicate Handling**: Adding a product already in the cart increases its quantity.
- **Expiration**: A cart expires 1 hour after creation and cannot be modified or checked out thereafter.
- **No Stored Price**: CartItem stores ProductId and Quantity, with ProductName optional for display, but never stores product price.

## Order
- **Immutability**: Once created, an order and its items are strictly immutable, ensuring historical record integrity.
- **Order Price Immutability**: The price of items in an order is locked at the time of creation and does not change even if catalog prices change.

## Restaurant
- **Active Status**: A restaurant must be active to accept orders.
- **Opening Hours**: A restaurant must be currently open (based on its defined time range) to accept orders.
- **Product Availability**: Only available products can be added to a cart or ordered.

## Checkout
- **Current Catalog Pricing**: Checkout loads current Product prices from Catalog and uses them to create CheckoutItemSnapshot values. There is no old cart price comparison.
- **Delivery Address Required**: A valid delivery address snapshot must exist before an Order can be created.

## Customer
- **Required Full Name**: FullName cannot be empty or whitespace.
- **Positive Age**: Age must be greater than zero.
- **Optional Phone**: PhoneNumber may be absent in MVP v1.
- **Address Ownership**: Customer owns its addresses, rejects duplicates, and allows only one default address.
