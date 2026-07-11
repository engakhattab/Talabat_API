# Domain Invariants

> Phase 0 scope update: These invariants describe the current business model, not authentication policy. Future ownership checks for Customer and Delivery websites belong in Application/API authorization, while Domain remains independent from Identity/Auth frameworks.

## Cart
- **Single Active Cart**: Exactly one active cart per customer.
- **Single Restaurant per Cart**: All items in a cart must belong to the same restaurant.
- **Positive Quantity**: The quantity of any cart item must be strictly greater than zero.
- **Duplicate Handling**: Adding a product already in the cart increases its quantity.
- **Expiration**: A cart expires 1 hour after creation and cannot be modified or checked out thereafter.
- **No Stored Price**: CartItem stores ProductId, required ProductName, and Quantity, but never stores product price.

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
- **Optional Phone**: PhoneNumber may be absent in the current Domain model.
- **Address Ownership**: Customer owns its addresses, rejects duplicates, and allows only one default address.

## Delivery
- **Separate Lifecycle**: Delivery is separate from Order and represents courier task progress.
- **Single Assignment**: Delivery can be assigned at most once in this phase.
- **Assigned Agent Identity**: Agent-driven delivery transitions must use the assigned agent ID.
- **Ordered Transitions**: Delivery progresses through the documented status sequence and rejects invalid transitions.
- **Terminal Protection**: Delivered, Cancelled, and Failed deliveries cannot be changed.
- **Assigned Terminal Coordination**: Assigned cancellation or failure must coordinate with DeliveryAgent release through the domain service.
- **Monotonic Timestamps**: Delivery transition timestamps must be UTC and cannot move backward.

## DeliveryAgent
- **Required Profile**: FullName is required and VehicleType must be supported.
- **Availability**: Only Available agents can be assigned.
- **Busy Protection**: Busy agents cannot go offline or be reassigned.
- **Release Through Coordination**: Completion, cancellation, or failure releases a Busy assigned agent through the domain service.
- **Optional Location**: CurrentLocation may be absent; supplied coordinates must be valid.

## Identity/Auth Boundary
- **No Framework Types In Domain**: Domain must not reference IdentityServer, ASP.NET Core Identity, JWT, `ClaimsPrincipal`, `HttpContext`, or API authorization policies.
- **Ownership Checks Outside Domain**: Future authenticated Customer and Delivery website ownership checks belong in Application/API authorization, then Domain receives business IDs and values.
