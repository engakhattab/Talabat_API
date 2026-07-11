# Application Use Case Contracts

These contracts describe Phase 3 Application-layer behavior. They are not HTTP endpoints, OpenAPI contracts, controller actions, persistence contracts, or frontend contracts.

## Contract Rules

- Every contract is transport-neutral.
- Every command/query accepts `CancellationToken` during implementation.
- Customer-scoped use cases receive `customerId` explicitly during Phase 3 as placeholder profile context.
- Future API/Auth layers must resolve the customer profile from authenticated user context; public API contracts must not treat user-submitted `customerId` as final authorization.
- Use cases return Application result types, not HTTP status codes.
- Handlers depend on Domain repository interfaces, `IUnitOfWork` for writes, `IClock`, and planned Application abstractions only.
- Handlers must not reference ASP.NET Core, EF Core, Identity/Auth, controllers, or database-specific APIs.
- `Basket` is the context/folder name; `Cart` is the aggregate and customer workflow object used in Phase 3 contracts.

## Common Result Contract

```text
UseCaseResult<T>
|-- IsSuccess
|-- Value
`-- Error

ApplicationError
|-- Code
|-- Category
`-- Message
```

Expected categories:

- `Validation`
- `NotFound`
- `Conflict`
- `Unavailable`
- `OwnershipMismatch`

## Common Abstractions

### Existing

- `IClock`
  - Supplies current UTC time.

### Planned

- Application-side ID generation was a Phase 3 bridge and is superseded by Phase 3.5.
  - Cart, customer address, and order IDs are now database-generated after save.
- `IRestaurantLocalTimeProvider`
  - Supplies restaurant local `TimeOnly` values from UTC time for opening-hours validation.

## Catalog Contracts

### Browse Restaurants

**Request**: `BrowseRestaurantsQuery`

**Dependencies**:

- `IRestaurantRepository.GetActiveRestaurantsAsync`
- `IRestaurantLocalTimeProvider`
- `IClock`

**Success response**: collection of `RestaurantSummary`.

**Expected failures**:

- None for an empty catalog; return an empty collection.

**Notes**:

- Return only active restaurants.
- Include local open/closed status if local time can be calculated by the abstraction.

### Get Restaurant Menu

**Request**: `GetRestaurantMenuQuery`

Required fields:

- `restaurantId`

**Dependencies**:

- `IRestaurantRepository.GetByIdWithProductsAsync`

**Success response**: `RestaurantMenu`.

**Expected failures**:

- `RestaurantNotFound`

**Notes**:

- Include unavailable products with `isAvailable = false`.
- Do not allow callers to mutate `Restaurant` or `Product` through the response.

## Basket Contracts

### Basket Total Contract Rules

- `CalculatedCurrentTotal` means the sum of all cart item line totals using current Catalog prices.
- `LineTotal` equals `CurrentCatalogUnitPrice * Quantity`.
- For this phase, `CalculatedCurrentTotal` includes item prices only and excludes delivery fees, service fees, taxes, tips, coupons, discounts, and payment fees.
- If the cart is empty, `CalculatedCurrentTotal` is `0`.
- `Cart` and `CartItem` do not persist product prices.
- Basket handlers that return `CartDetails` must calculate totals from current Catalog prices supplied by the Application layer.
- Do not introduce a `Product` repository. Product data is obtained through Restaurant/Catalog aggregate-root repository contracts.

### Cart State Contract Rules

- A current active cart is owned by the customer profile, has `Active` status, belongs to one restaurant, and is not expired at workflow time.
- Checked-out, cleared, and expired carts are not modifiable.
- After clear, later add-item workflows create a new cart rather than reusing the cleared cart.
- Cross-restaurant add-item attempts return `CrossRestaurantCart` and preserve the existing cart unchanged.
- Zero or negative quantities return an invalid-quantity style outcome.
- Concurrent cart changes are represented by re-evaluating current cart state when the handler runs. Storage-specific concurrency control is deferred to persistence planning.

### Get Cart

**Request**: `GetCartQuery`

Required fields:

- `customerId`

**Dependencies**:

- `ICartRepository.GetActiveCartByCustomerIdAsync`
- `IRestaurantRepository.GetByIdWithProductsAsync`
- `IClock`

**Success response**: `CartDetails` or an empty-cart response.

**Expected failures**:

- `CustomerNotFound` only if the handler validates customer existence.

**Notes**:

- If no active cart exists, return a successful empty-cart response rather than creating a cart.
- Current totals require current Catalog prices loaded from the restaurant that owns the cart items.
- Empty-cart response has no line items and `CalculatedCurrentTotal` equal to `0`.

### Add Cart Item

**Request**: `AddCartItemCommand`

Required fields:

- `customerId`
- `restaurantId`
- `productId`
- `quantity`

**Dependencies**:

- `ICartRepository.GetActiveCartByCustomerIdAsync`
- `IRestaurantRepository.GetProductSnapshotAsync`
- `IRestaurantRepository.GetByIdWithProductsAsync` for current Catalog prices after mutation when returning `CartDetails`
- `IClock`
- `IUnitOfWork`

**Success response**: updated `CartDetails`.

**Expected failures**:

- `RestaurantNotFound`
- `ProductNotFound`
- `ProductUnavailable`
- `InvalidQuantity`
- `CartExpired`
- `CartNotActive`
- `CrossRestaurantCart`

**Notes**:

- If no active cart exists, create one with the first valid product.
- If a cart exists for a different restaurant, preserve the existing cart and return `CrossRestaurantCart`.
- After mutation, load current Catalog prices for all remaining cart item product IDs from the relevant restaurant before mapping the returned `CartDetails`.
- The returned `CartDetails` total is calculated from current Catalog prices only; cart items do not store prices.
- Commit once after the cart is created or updated.

### Update Cart Item Quantity

**Request**: `UpdateCartItemQuantityCommand`

Required fields:

- `customerId`
- `productId`
- `quantity`

**Dependencies**:

- `ICartRepository.GetActiveCartByCustomerIdAsync`
- `IRestaurantRepository.GetByIdWithProductsAsync` for current Catalog prices after mutation when returning `CartDetails`
- `IClock`
- `IUnitOfWork`

**Success response**: updated `CartDetails`.

**Expected failures**:

- `CartNotFound`
- `CartExpired`
- `CartNotActive`
- `CartItemNotFound`
- `InvalidQuantity`

**Notes**:

- Mutate quantity through the `Cart` aggregate.
- After mutation, load current Catalog prices for all remaining cart item product IDs from the cart restaurant before mapping the returned `CartDetails`.
- The returned `CartDetails` total is calculated from current Catalog prices only; cart items do not store prices.

### Remove Cart Item

**Request**: `RemoveCartItemCommand`

Required fields:

- `customerId`
- `productId`

**Dependencies**:

- `ICartRepository.GetActiveCartByCustomerIdAsync`
- `IRestaurantRepository.GetByIdWithProductsAsync` for current Catalog prices after mutation when returning `CartDetails`
- `IClock`
- `IUnitOfWork`

**Success response**: updated `CartDetails`.

**Expected failures**:

- `CartNotFound`
- `CartExpired`
- `CartNotActive`
- `CartItemNotFound`

**Notes**:

- Remove the item through the `Cart` aggregate.
- After mutation, load current Catalog prices for all remaining cart item product IDs from the cart restaurant before mapping the returned `CartDetails`.
- The returned `CartDetails` total is calculated from current Catalog prices only; cart items do not store prices.

### Clear Cart

**Request**: `ClearCartCommand`

Required fields:

- `customerId`

**Dependencies**:

- `ICartRepository.GetActiveCartByCustomerIdAsync`
- `IClock`
- `IUnitOfWork`

**Success response**: deterministic empty-cart response or `CartDetails` with an empty item list and `CalculatedCurrentTotal` equal to `0`.

**Expected failures**:

- `CartNotFound`
- `CartExpired`
- `CartNotActive`

**Notes**:

- Clear the cart through the `Cart` aggregate.
- If `CartDetails` is returned, it must contain no items and a zero total.
- No product price loading is required after clear because the returned cart contains no line items.

## Customer Contracts

### Customer Profile Contract Rules

- `FullName` is required.
- `FullName` is trimmed before validation.
- `FullName` cannot be empty or whitespace.
- `Age` is required and must be greater than zero.
- `PhoneNumber` is optional.
- Profile update workflows preserve the same validation rules as profile creation.
- `customerId` is caller-supplied placeholder profile context in Phase 3, not final authorization.

### Customer Address Contract Rules

- Address `street`, `city`, and `buildingNumber` are required.
- Address `floor` is optional.
- Duplicate addresses are detected by normalized address value: street, city, building number, and floor compared case-insensitively after required text normalization.
- Only one address can be default.
- Setting a default address clears the previous default before marking the selected address.
- Removing a default address does not automatically choose a new default in Phase 3.
- Address operations are scoped to the loaded customer profile.

### Get Customer Profile

**Request**: `GetCustomerProfileQuery`

Required fields:

- `customerId`

**Dependencies**:

- `ICustomerRepository.GetByIdWithAddressesAsync`

**Success response**: `CustomerProfile`.

**Expected failures**:

- `CustomerNotFound`
- `InvalidCustomerProfile`

### Update Customer Profile

**Request**: `UpdateCustomerProfileCommand`

Required fields:

- `customerId`
- `fullName`
- `age`
- `phoneNumber`

**Dependencies**:

- `ICustomerRepository.GetByIdAsync`
- `IUnitOfWork`

**Success response**: updated `CustomerProfile`.

**Expected failures**:

- `CustomerNotFound`
- `InvalidCustomerProfile`

### Add Customer Address

**Request**: `AddCustomerAddressCommand`

Required fields:

- `customerId`
- `street`
- `city`
- `buildingNumber`
- `floor`
- `makeDefault`

**Dependencies**:

- `ICustomerRepository.GetByIdWithAddressesAsync`
- `IUnitOfWork`

**Success response**: updated `CustomerProfile`.

**Expected failures**:

- `CustomerNotFound`
- `InvalidAddress`
- `DuplicateAddress`

### Remove Customer Address

**Request**: `RemoveCustomerAddressCommand`

Required fields:

- `customerId`
- `addressId`

**Dependencies**:

- `ICustomerRepository.GetByIdWithAddressesAsync`
- `IUnitOfWork`

**Success response**: updated `CustomerProfile`.

**Expected failures**:

- `CustomerNotFound`
- `AddressNotFound`

### Set Default Customer Address

**Request**: `SetDefaultCustomerAddressCommand`

Required fields:

- `customerId`
- `addressId`

**Dependencies**:

- `ICustomerRepository.GetByIdWithAddressesAsync`
- `IUnitOfWork`

**Success response**: updated `CustomerProfile`.

**Expected failures**:

- `CustomerNotFound`
- `AddressNotFound`

## Ordering Contracts

### Checkout

**Request**: `CheckoutCommand`

Required fields:

- `customerId`
- `deliveryAddressId`

**Dependencies**:

- `ICartRepository.GetActiveCartByCustomerIdAsync`
- `ICustomerRepository.GetByIdWithAddressesAsync`
- `IRestaurantRepository.GetByIdWithProductsAsync`
- `IOrderRepository.AddAsync`
- `IRestaurantLocalTimeProvider`
- `IClock`
- `IUnitOfWork`
- `CheckoutDomainService`

**Success response**: `CheckoutSucceededOutcome`.

**Expected failures/outcomes**:

- `CustomerNotFound`
- `CartNotFound`
- `CartExpired`
- `CartNotActive`
- `EmptyCart`
- `AddressNotFound`
- `RestaurantNotFound`
- `RestaurantInactive`
- `RestaurantClosed`
- `CheckoutProductsUnavailableOutcome`

**Outcome contract rules**:

- `CheckoutSucceededOutcome` returns the created order identifier and final total amount calculated from current Catalog prices.
- `CheckoutProductsUnavailableOutcome` returns unavailable items with `productId`, `productName`, and `reason`.
- No price-change checkout outcome exists in Phase 3 because checkout always uses current Catalog prices and `Cart` stores no old product price.
- Duplicate checkout submissions are represented through cart state. After the first successful checkout marks the cart checked out, later submissions for that cart return a cart-not-active style outcome.
- Concurrent checkout changes are represented by re-evaluating current cart state when checkout runs. Storage-specific optimistic concurrency, locks, and retries are deferred to Phase 4.
- If checkout returns unavailable products, no order is created, the cart is not checked out, and the cart remains editable when it has not otherwise expired or changed state.

**Orchestration sequence**:

1. Load the active cart for `customerId`.
2. Load the customer with addresses for `customerId`.
3. Create a delivery address snapshot through the customer aggregate.
4. Load the restaurant with products for `cart.RestaurantId`.
5. Resolve restaurant local time.
6. Call `CheckoutDomainService.ValidateCheckout`.
7. If products are unavailable, return `CheckoutProductsUnavailableOutcome` without committing.
8. Generate a new order ID.
9. Create the order from checkout snapshots.
10. Add the order to `IOrderRepository`.
11. Mark the cart checked out.
12. Update the cart.
13. Commit once through `IUnitOfWork`.
14. Return `CheckoutSucceededOutcome`.

### Get Order History

**Request**: `GetOrderHistoryQuery`

Required fields:

- `customerId`

**Dependencies**:

- `IOrderRepository.GetByCustomerIdAsync`

**Success response**: collection of `OrderSummary`.

**Expected failures**:

- None for no orders; return an empty collection.

**Notes**:

- Return only orders owned by the requested customer profile.
- Return an empty collection when the customer has no orders.
- Sort order history from newest to oldest by order creation time.

### Get Order Details

**Request**: `GetOrderDetailsQuery`

Required fields:

- `customerId`
- `orderId`

**Dependencies**:

- `IOrderRepository.GetByIdForCustomerAsync`

**Success response**: `OrderDetails`.

**Expected failures**:

- `OrderNotFound`

**Notes**:

- `OrderNotFound` covers missing orders and orders not owned by the customer. Do not expose cross-customer existence.
- Return historical item names, quantities, unit prices, line totals, total amount, and delivery address snapshot.

## Non-Functional Contract Expectations

- Application results must expose stable categories and codes so future logging, metrics, and tracing can use them without HTTP-specific response types.
- Phase 3 has no production latency target because persistence and API transport are deferred.
- Use cases should load only the aggregate-root data required for the workflow described by the corresponding contract.
- Formal audit logging, telemetry sinks, retention policies, and compliance workflows are deferred to later infrastructure/compliance phases.

## Out Of Contract For Phase 3

- API routes, controllers, filters, middleware, HTTP status mapping, and OpenAPI.
- EF Core, DbContext, repository implementations, migrations, and seed data.
- Identity/Auth, current-user resolution, roles, policies, claims, tokens, login, and registration.
- Delivery task and delivery-agent workflows.
- Payment, coupons, reviews, notifications, restaurant-owner workflows, and frontend screens.
