# Domain Failures Design

This document defines the domain failure model for Talabat MVP v1.

This is a design document only. It does not generate C# code and does not create entities, repositories, controllers, handlers, EF configurations, or migrations.

MVP v1 scope:

- No authentication, authorization, Identity, login/register, JWT, admins, or restaurant owners.
- No payment, delivery drivers, notifications, coupons, or reviews.
- Assume one normal customer profile.
- Restaurants and products are seeded for testing.
- Cart is not created until the first item is added.

## Domain Failures Philosophy

A domain failure happens when a business rule cannot be satisfied.

Not every domain failure should be modeled as an exception. Some failures represent invalid operations that must stop immediately. Other failures represent expected business outcomes where the caller needs structured details to decide what happens next.

Use exceptions for invalid operations that must stop immediately. These are cases where a domain invariant is being violated and the operation should not continue.

Use result objects for expected business outcomes where the caller or UI needs structured details. Checkout is the main MVP v1 example: a price change or product availability change is expected in a food ordering system, and the UI may need to show exactly which items changed.

Domain exceptions should use business language. They should describe the rule that failed, not infrastructure or HTTP details.

Application/API layers later map domain failures to HTTP responses, but HTTP status codes do not belong in the Domain layer.

## Exceptions vs Results Decision Rule

Use a Domain Exception when:

- A domain invariant is violated.
- The operation should stop immediately.
- The caller cannot meaningfully continue without changing the request.
- Example: invalid quantity, expired cart, cross-restaurant cart.

Use a Domain Result when:

- The failure is an expected business outcome.
- The caller/UI needs structured details to show the user.
- The user may make a decision based on the result.
- Example: product price changed during checkout.

## Domain Exception Summary

| Failure Name | Type | Source Rule | Owner | Why Exception or Result? | Suggested Message / Returned Details |
|---|---|---|---|---|---|
| `InvalidQuantityException` | Exception | Quantity must be greater than zero. | CartItem / OrderItem / Money multiplication by quantity where relevant | Invalid operation. Positive quantity is a domain invariant and the operation cannot continue. | `Quantity must be greater than zero.` |
| `CartExpiredException` | Exception | Cart expires after 1 hour; expired cart cannot be modified or checked out. | Cart | Invalid operation. Expired carts cannot be changed or checked out. | `This cart has expired. Please start a new cart.` |
| `CartNotActiveException` | Exception | Only Active carts can be modified. | Cart | Invalid operation. CheckedOut and Cleared carts are closed for mutation. | `Only active carts can be modified.` |
| `CrossRestaurantCartException` | Exception | Cart can contain items from only one restaurant. | Cart | Invalid operation. The request violates a cart aggregate invariant. | `Cannot add items from a different restaurant. Clear the cart first.` |
| `ProductUnavailableException` | Exception for add-to-cart | Unavailable products cannot be added to cart. | Cart add-item behavior | Invalid add-to-cart operation. The customer must choose a different available product. | `This product is currently unavailable.` |
| `EmptyCartCheckoutException` | Exception | Empty cart cannot be checked out. | Checkout domain service or Order factory | Invalid checkout operation. There is no meaningful checkout to continue. | `Cannot checkout an empty cart.` |
| `RestaurantInactiveException` | Exception | Restaurant must be active during checkout. | Restaurant / checkout validation | Invalid checkout operation. The restaurant cannot accept orders. | `This restaurant is currently inactive.` |
| `RestaurantClosedException` | Exception | Restaurant must be open during checkout. | Restaurant / checkout validation | Invalid checkout operation. The restaurant cannot accept orders at the current time. | `This restaurant is currently closed.` |
| `DuplicateAddressException` | Exception | Duplicate address should be rejected. | Customer | Invalid profile operation. Customer address duplicates violate the Customer aggregate rule. | `This address already exists for the customer.` |
| `AddressNotFoundException` | Exception | Checkout requires selected address to belong to customer; remove/set-default needs existing address. | Customer / Application validation | Invalid operation. The requested address cannot be used or changed. | `Address was not found for this customer.` |
| `MissingDeliveryAddressException` | Exception | Checkout requires a delivery address. | Checkout use case / Order factory | Invalid checkout operation. Order creation cannot continue without delivery address data. | `Checkout requires a delivery address.` |
| `PriceChangedCheckoutResult` | Result, not exception | Checkout validates current prices. | Checkout use case / domain service | Expected checkout outcome. The customer may accept changes or update the cart. | Returned details: product id, product name, old cart price, current catalog price. |
| `UnavailableProductsCheckoutResult` | Result for MVP v1 | Checkout validates product availability again. | Checkout use case / domain service | Expected checkout outcome. The UI needs item-level details so the customer can revise the cart. | Returned details: product id, product name, reason unavailable. |

## Base Domain Exception

All domain exceptions should inherit from a base `DomainException`.

The base type lets the API layer catch known business errors later without catching unrelated technical failures. This keeps business-rule failures separate from infrastructure failures such as database connection errors.

The base exception belongs in the Domain layer because it represents domain failure semantics. It should not contain HTTP status codes, response shapes, route names, controller names, or API-specific metadata.

Intended inheritance hierarchy, conceptually:

- `DomainException`
- Cart-related failures inherit from `DomainException`.
- Checkout-related failures inherit from `DomainException`.
- Restaurant/catalog rule failures inherit from `DomainException`.
- Customer/address rule failures inherit from `DomainException`.

The exact class names can be implemented later. This document defines the intended failure vocabulary, not code.

## Checkout Results

Checkout can return structured results instead of throwing for every case.

Suggested conceptual checkout results:

- `CheckoutSucceeded`
- `PriceChanged`
- `ProductsUnavailable`

Price changes and unavailable products during checkout are expected business outcomes. They can happen because catalog data may change after an item was added to the cart but before checkout.

Returning structured result details helps the UI tell the customer what changed. For price changes, the result should identify the product and show old cart price versus current catalog price. For unavailable products, the result should identify the product and explain that it can no longer be ordered.

The final implementation can choose a `CheckoutResult` model later. The key design decision is that these checkout outcomes should carry structured details, not just strings.

## What Does Not Belong In Domain Exceptions

- HTTP 400/404/409/422 status codes do not belong in Domain.
- Controller response messages do not belong in Domain.
- Database exceptions do not belong in Domain.
- Authentication/authorization failures are out of scope for MVP v1.
- Validation for request shape, such as missing JSON fields, belongs in API/Application validation, not Domain.
- Login, register, JWT, roles, admin, and restaurant-owner failures should not be modeled for MVP v1.

## Mapping Later In API Layer

The Application/API layer will catch domain exceptions and map them to HTTP responses later. These mappings may be documented for API design, but the Domain layer should not know HTTP.

Example conceptual mappings:

| Domain Failure | Later API Mapping |
|---|---|
| `InvalidQuantityException` | 400 Bad Request |
| `CartExpiredException` | 409 Conflict |
| `CrossRestaurantCartException` | 400 Bad Request |
| `EmptyCartCheckoutException` | 400 Bad Request |
| `RestaurantClosedException` | 422 Unprocessable Entity |
| `DuplicateAddressException` | 409 Conflict |
| `AddressNotFoundException` | 404 Not Found |
| `MissingDeliveryAddressException` | 400 Bad Request |
| `PriceChangedCheckoutResult` | 409 Conflict with changed item details |
| `UnavailableProductsCheckoutResult` | 409 Conflict with unavailable item details |

These mappings are API-layer policy. They should not be embedded into domain exception classes.

## Common Mistakes

- Throwing generic Exception.
- Putting HTTP status codes in Domain exceptions.
- Treating every business outcome as an exception.
- Returning strings instead of structured details for price changes.
- Letting controllers enforce domain invariants instead of entities/aggregates.
- Creating exceptions for out-of-scope authentication/authorization in MVP v1.
