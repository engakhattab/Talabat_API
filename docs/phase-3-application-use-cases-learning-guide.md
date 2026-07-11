# Phase 3 Application Layer Use Cases - Learning Guide

This document explains Phase 3 of the Talabat-like backend project. It is written for learning, revision, and mentor discussion. It explains what was added, why it was added, how the design fits Clean Architecture and DDD, and how to answer questions about the implementation.

Important correction: the class is named `IApplicationIdGenerator`, not `IAplicationGenerator`. It exists because the current Domain factories require integer IDs when creating `Cart`, `CustomerAddress`, and `Order`, while the real database ID strategy is deferred to Phase 4.

## 1. Executive Summary

Phase 3 implemented the Application layer use cases for the customer ordering path:

- Browse restaurants.
- View restaurant menus.
- Manage the cart.
- Manage customer profile and saved addresses.
- Checkout.
- Read order history and order details.

Before Phase 3, the project already had important Domain objects and rules: `Restaurant`, `Product`, `Cart`, `Customer`, `Order`, value objects, repository interfaces, and checkout domain logic. What was missing was the Application layer that coordinates full workflows.

For example, checkout is not just an `Order` method. Checkout must load a cart, load the customer and delivery address, load restaurant and current product data, validate the workflow, create an order, mark the cart as checked out, and commit once. That orchestration belongs in the Application layer.

What changed after Phase 3:

- `Talabat.Application` now contains commands, queries, handlers, read models, result contracts, and small abstractions.
- The project now has `tests/Talabat.Application.Tests` with focused xUnit tests.
- Application handlers return transport-neutral results instead of HTTP responses.
- The customer ordering path is now executable through handlers, without EF Core, API endpoints, or Identity/Auth.

What Phase 3 did not do:

- It did not add EF Core.
- It did not add a `DbContext`.
- It did not implement repository persistence.
- It did not add migrations.
- It did not add controllers or API endpoints.
- It did not add IdentityServer, ASP.NET Core Identity, authentication, authorization, JWT, claims, roles, or policies.
- It did not add Delivery workflows.
- It did not add frontend websites.

Phase 3 is about use-case orchestration, not database persistence, not HTTP transport, and not IdentityServer.

## 2. Phase 3 Goal

The goal of Phase 3 was to implement Application use cases while keeping the architecture boundaries clean.

The Application layer should:

- Coordinate Domain aggregate roots through repository interfaces.
- Call Domain behavior instead of duplicating business rules.
- Return transport-neutral results.
- Depend on Domain, but not Infrastructure or API.
- Commit write workflows through `IUnitOfWork`.
- Keep Identity/Auth decisions outside the feature.

Layer responsibilities:

| Layer | Responsibility |
|---|---|
| Domain | Holds business rules, invariants, aggregate behavior, value objects, domain services, and domain exceptions. Examples: `Cart.AddItem`, `Customer.AddAddress`, `CheckoutDomainService.ValidateCheckout`. |
| Application | Orchestrates use cases. Loads aggregates through repository interfaces, calls Domain methods, maps expected outcomes to results, and defines transaction intent. |
| Infrastructure | Future Phase 4 responsibility. Implements EF Core, `DbContext`, repositories, `IUnitOfWork`, ID generation, local time providers, and external service adapters. |
| API | Future responsibility. Handles HTTP routes, controllers/minimal APIs, request/response mapping, authentication middleware, authorization policies, OpenAPI, and dependency injection composition. |

The key difference:

- Domain logic answers: "What business rules must always hold?"
- Application orchestration answers: "Which aggregates are needed for this workflow, in what order, and when do we commit?"
- Infrastructure persistence answers: "How do we store and retrieve this data?"
- API mapping answers: "How do HTTP requests and responses map to Application contracts?"

## 3. Folder Structure Added Or Changed

### `src/Talabat/Talabat.Application/Common/Results/`

Purpose:
Contains the Application result pattern.

Why it belongs in Application:
Expected business outcomes must be returned in a framework-neutral way. The Application layer should not return `IActionResult`, status codes, controller responses, or ASP.NET Core types.

What it contains:

- `ApplicationErrorCategory`
- `ApplicationError`
- `UseCaseResult<T>`
- `ApplicationErrorCodes`
- `DomainExceptionMapper`

Why it matters:
Future API endpoints can map these results to HTTP responses later without changing the use-case logic.

Mentor answer:
This folder exists because Application use cases need a stable way to return success or expected failure without depending on HTTP or ASP.NET Core.

### `src/Talabat/Talabat.Application/Abstractions/`

Purpose:
Contains interfaces needed by Application handlers but implemented elsewhere later.

What it contains:

- `IClock`
- `IApplicationIdGenerator`
- `IRestaurantLocalTimeProvider`

Why it belongs in Application:
Handlers need current time, generated IDs, and restaurant-local time, but Application should not know how the system clock, database, or time-zone service is implemented.

Mentor answer:
These are boundary contracts. Infrastructure can implement them later. Tests can fake them now.

### `src/Talabat/Talabat.Application/Catalog/`

Purpose:
Contains Catalog read use cases.

What it contains:

- `BrowseRestaurants/`
- `GetRestaurantMenu/`
- `Models/`

Why it matters:
Catalog handlers return read models such as `RestaurantSummary`, `RestaurantMenu`, and `MenuProduct` instead of exposing Domain entities.

Mentor answer:
Catalog is a read-heavy area here. The handlers use `IRestaurantRepository`, map Domain data to Application read models, and stay transport-neutral.

### `src/Talabat/Talabat.Application/Basket/`

Purpose:
Contains cart management use cases.

What it contains:

- `GetCart/`
- `AddItem/`
- `UpdateQuantity/`
- `RemoveItem/`
- `ClearCart/`
- `Models/`
- `Mapping/`

Why it matters:
Cart workflows coordinate `Cart` with current Catalog data. The handlers call `Cart` aggregate methods but do not directly edit `CartItem`.

Mentor answer:
Basket use cases belong in Application because they coordinate repositories, current time, current Catalog prices, aggregate behavior, and transaction boundaries.

### `src/Talabat/Talabat.Application/Customers/`

Purpose:
Contains customer profile and saved-address use cases.

What it contains:

- `GetProfile/`
- `UpdateProfile/`
- `AddAddress/`
- `RemoveAddress/`
- `SetDefaultAddress/`
- `Models/`
- `Mapping/`

Why it matters:
The handler loads the `Customer` aggregate and calls aggregate methods. It does not directly modify `CustomerAddress` children.

Mentor answer:
Profile and address rules belong to the `Customer` aggregate. Application only coordinates loading, calling, saving, and mapping.

### `src/Talabat/Talabat.Application/Ordering/`

Purpose:
Contains checkout and order read use cases.

What it contains:

- `Checkout/`
- `GetOrderHistory/`
- `GetOrderDetails/`
- `Models/`
- `Mapping/`

Why it matters:
Checkout crosses multiple aggregates: `Cart`, `Customer`, `Restaurant`, and `Order`. That makes it an Application orchestration workflow, while the business rules remain inside Domain objects and `CheckoutDomainService`.

Mentor answer:
Checkout is not a controller concern and not fully owned by a single aggregate. The Application handler coordinates the full use case and commits once.

### `tests/Talabat.Application.Tests/`

Purpose:
Contains xUnit tests for the Application layer.

Why it matters:
Phase 3 does not have Infrastructure persistence. Tests use fake repositories to verify orchestration, result mapping, Domain delegation, and transaction intent.

Mentor answer:
We are not testing EF Core here. We are testing whether handlers call the right abstractions, delegate rules to Domain, handle outcomes correctly, and commit once.

### `tests/Talabat.Application.Tests/TestDoubles/`

Purpose:
Contains fake repositories and fake infrastructure-like services for tests.

What it contains:

- `FakeClock`
- `FakeApplicationIdGenerator`
- `FakeRestaurantLocalTimeProvider`
- `FakeUnitOfWork`
- `FakeRestaurantRepository`
- `FakeCartRepository`
- `FakeCustomerRepository`
- `FakeOrderRepository`
- `TestData`

Why it matters:
These fakes let Application tests run without EF Core, a database, API host, or Identity system.

## 4. File-by-File Explanation

### Common Results And Abstractions

| File path | Type | What it does | Why it was added | Technical importance | Business/logical importance | Use case depends on it | Mentor might ask |
|---|---|---|---|---|---|---|---|
| `src/Talabat/Talabat.Application/Common/Results/ApplicationErrorCategory.cs` | Result | Defines result categories: `Validation`, `NotFound`, `Conflict`, `Unavailable`, `OwnershipMismatch`. | To classify expected failures without HTTP. | Keeps error handling framework-neutral. | Makes business outcomes clear. | All handlers. | Why not use HTTP status codes here? |
| `src/Talabat/Talabat.Application/Common/Results/ApplicationError.cs` | Result | Holds `Code`, `Category`, and `Message`. | To represent structured Application failures. | Stable contract for future API mapping. | Explains why a workflow failed. | All handlers. | Why have both code and category? |
| `src/Talabat/Talabat.Application/Common/Results/UseCaseResult.cs` | Result | Wraps either success value or `ApplicationError`. | To avoid throwing exceptions for expected business outcomes. | Makes handler results explicit and testable. | Unavailable products or expired carts are normal business outcomes. | All handlers. | Why use a result pattern? |
| `src/Talabat/Talabat.Application/Common/Results/ApplicationErrorCodes.cs` | Result | Defines stable error code constants. | To avoid random string literals. | Supports future logging, metrics, and API mapping. | Makes failures predictable. | All handlers. | Why are stable error codes useful? |
| `src/Talabat/Talabat.Application/Common/Results/DomainExceptionMapper.cs` | Helper | Maps Domain/argument exceptions to `ApplicationError`. | To centralize failure mapping. | Keeps handlers smaller and consistent. | Preserves Domain rules while returning Application results. | Write handlers and checkout. | Why not let exceptions reach controllers? |
| `src/Talabat/Talabat.Application/Abstractions/IClock.cs` | Abstraction | Supplies current UTC time. | To avoid direct `DateTime.UtcNow` usage. | Makes time-based workflows testable. | Used for cart expiration and checkout time. | Basket, checkout. | Why abstract time? |
| `src/Talabat/Talabat.Application/Abstractions/IApplicationIdGenerator.cs` | Abstraction | Supplies new cart, customer address, and order IDs. | Domain factories require IDs while persistence is deferred. | Avoids hardcoded IDs and database dependency. | Allows creation of `Cart`, `CustomerAddress`, and `Order` in Phase 3. | Add item, add address, checkout. | Why does this exist before EF Core? |
| `src/Talabat/Talabat.Application/Abstractions/IRestaurantLocalTimeProvider.cs` | Abstraction | Provides restaurant-local `TimeOnly` from UTC. | Restaurant time-zone policy is not in Domain yet. | Keeps local-time calculation outside Domain. | Enables restaurant open/closed validation. | Browse restaurants, checkout. | Why not calculate local time inside `Restaurant`? |

### Catalog Files

| File path | Type | What it does | Why it was added | Technical importance | Business/logical importance | Use case depends on it | Mentor might ask |
|---|---|---|---|---|---|---|---|
| `src/Talabat/Talabat.Application/Catalog/Models/RestaurantSummary.cs` | ReadModel | Represents a restaurant in browse results. | To avoid returning `Restaurant` aggregate. | Exposes only needed read data. | Shows active/open restaurant data. | Browse restaurants. | Why not return Domain entities? |
| `src/Talabat/Talabat.Application/Catalog/Models/MenuProduct.cs` | ReadModel | Represents a menu product with price and availability. | To expose product selection data safely. | Does not expose mutable child entity. | Shows whether product is available. | Get menu. | Why include unavailable products? |
| `src/Talabat/Talabat.Application/Catalog/Models/RestaurantMenu.cs` | ReadModel | Represents a restaurant menu. | To group restaurant and product read data. | Stable read contract. | Allows customer item selection. | Get menu. | Why create a separate model? |
| `src/Talabat/Talabat.Application/Catalog/BrowseRestaurants/BrowseRestaurantsQuery.cs` | Query | Request object for browsing restaurants. | CQRS-lite read use case. | No transport dependency. | Browse is read-only. | Browse restaurants. | Why is this a query? |
| `src/Talabat/Talabat.Application/Catalog/BrowseRestaurants/BrowseRestaurantsHandler.cs` | Handler | Loads active restaurants and maps summaries. | Implements browse orchestration. | Uses repository, clock, and local-time provider. | Returns only active restaurants with open status. | Browse restaurants. | Why not put this in a controller? |
| `src/Talabat/Talabat.Application/Catalog/GetRestaurantMenu/GetRestaurantMenuQuery.cs` | Query | Holds `RestaurantId`. | Transport-neutral menu request. | Keeps API route models separate. | Identifies requested restaurant. | Get menu. | Why not use HTTP request DTO? |
| `src/Talabat/Talabat.Application/Catalog/GetRestaurantMenu/GetRestaurantMenuHandler.cs` | Handler | Loads restaurant with products and maps menu. | Implements menu query. | Handles `RestaurantNotFound`. | Shows menu with unavailable products flagged. | Get menu. | Why not silently hide unavailable products? |

### Basket Files

| File path | Type | What it does | Why it was added | Technical importance | Business/logical importance | Use case depends on it | Mentor might ask |
|---|---|---|---|---|---|---|---|
| `src/Talabat/Talabat.Application/Basket/Models/CartLineItem.cs` | ReadModel | Represents a cart line with current unit price and line total. | To return calculated price data without storing it in `CartItem`. | Separates read projection from Domain state. | Shows customer current cart total. | Cart responses. | Why is price here but not in `CartItem`? |
| `src/Talabat/Talabat.Application/Basket/Models/CartDetails.cs` | ReadModel | Represents cart details and `CalculatedCurrentTotal`. | Standard cart response. | Supports empty cart response. | Customer can see cart content and current total. | Basket use cases. | What is `CalculatedCurrentTotal`? |
| `src/Talabat/Talabat.Application/Basket/Mapping/CartMapper.cs` | Helper | Maps `Cart` plus `Restaurant` current prices to `CartDetails`. | Cart totals require current Catalog prices. | Calls `cart.GetTotal(currentPrices)`. | Preserves the rule that Cart does not store prices. | Basket use cases. | Why does the mapper need `Restaurant`? |
| `src/Talabat/Talabat.Application/Basket/GetCart/GetCartQuery.cs` | Query | Holds `CustomerId`. | Customer-scoped cart read. | Transport-neutral. | Reads current active cart. | Get cart. | Why explicit `customerId`? |
| `src/Talabat/Talabat.Application/Basket/GetCart/GetCartHandler.cs` | Handler | Loads active cart and returns details or empty cart. | Implements current cart read. | Handles expired cart and current price loading. | Customer sees current cart. | Get cart. | Why not create an empty cart on view? |
| `src/Talabat/Talabat.Application/Basket/AddItem/AddCartItemCommand.cs` | Command | Holds customer, restaurant, product, quantity. | Add-item write request. | CQRS-lite command. | Adds product to cart. | Add item. | Why is this a command? |
| `src/Talabat/Talabat.Application/Basket/AddItem/AddCartItemHandler.cs` | Handler | Loads product snapshot, loads or creates cart, calls Domain, maps details, commits. | Implements add-to-cart workflow. | Uses repositories, `IApplicationIdGenerator`, `IClock`, `IUnitOfWork`. | Creates cart on first item and preserves one-restaurant rule. | Add item. | Why not create `CartItem` directly? |
| `src/Talabat/Talabat.Application/Basket/UpdateQuantity/UpdateCartItemQuantityCommand.cs` | Command | Holds customer, product, quantity. | Quantity update request. | Transport-neutral. | Changes item quantity. | Update quantity. | Where is quantity validation? |
| `src/Talabat/Talabat.Application/Basket/UpdateQuantity/UpdateCartItemQuantityHandler.cs` | Handler | Loads cart, calls `cart.UpdateQuantity`, maps current total, commits. | Implements update quantity workflow. | Delegates invariant checks to `Cart`. | Keeps cart valid. | Update quantity. | Why not modify `CartItem` directly? |
| `src/Talabat/Talabat.Application/Basket/RemoveItem/RemoveCartItemCommand.cs` | Command | Holds customer and product ID. | Remove-item request. | Simple write contract. | Removes product from cart. | Remove item. | Why product ID? |
| `src/Talabat/Talabat.Application/Basket/RemoveItem/RemoveCartItemHandler.cs` | Handler | Calls `cart.RemoveItem`, maps details, commits. | Implements remove workflow. | Uses current prices for remaining items. | Updates cart safely. | Remove item. | Why load restaurant after removal? |
| `src/Talabat/Talabat.Application/Basket/ClearCart/ClearCartCommand.cs` | Command | Holds customer ID. | Clear-cart request. | Simple write contract. | Clears cart. | Clear cart. | Why command? |
| `src/Talabat/Talabat.Application/Basket/ClearCart/ClearCartHandler.cs` | Handler | Calls `cart.Clear`, commits, returns empty response. | Implements clear workflow. | No price loading needed after clear. | Cart becomes cleared and later add creates a new cart. | Clear cart. | Why not reuse cleared cart? |

### Customer Files

| File path | Type | What it does | Why it was added | Technical importance | Business/logical importance | Use case depends on it | Mentor might ask |
|---|---|---|---|---|---|---|---|
| `src/Talabat/Talabat.Application/Customers/Models/CustomerAddressDetails.cs` | ReadModel | Represents saved address details. | To avoid exposing `CustomerAddress` child entity. | Safe read projection. | Shows saved addresses. | Customer reads/writes. | Why not return `CustomerAddress`? |
| `src/Talabat/Talabat.Application/Customers/Models/CustomerProfile.cs` | ReadModel | Represents profile and addresses. | Customer response model. | Separates Domain from presentation. | Shows customer data. | Customer use cases. | Why is Customer separate from Identity user? |
| `src/Talabat/Talabat.Application/Customers/Mapping/CustomerMapper.cs` | Helper | Maps `Customer` to `CustomerProfile`. | Avoid repeated mapping logic. | Keeps handlers focused. | Default address can be shown first. | Customer use cases. | Why use a mapper? |
| `src/Talabat/Talabat.Application/Customers/GetProfile/GetCustomerProfileQuery.cs` | Query | Holds customer ID. | Profile read request. | Customer-scoped. | Reads profile. | Get profile. | Why explicit customer ID now? |
| `src/Talabat/Talabat.Application/Customers/GetProfile/GetCustomerProfileHandler.cs` | Handler | Loads customer with addresses and maps profile. | Implements profile read. | Handles missing customer. | Shows profile safely. | Get profile. | Why repository interface? |
| `src/Talabat/Talabat.Application/Customers/UpdateProfile/UpdateCustomerProfileCommand.cs` | Command | Holds profile fields. | Profile write request. | Transport-neutral. | Updates profile. | Update profile. | Where are profile rules? |
| `src/Talabat/Talabat.Application/Customers/UpdateProfile/UpdateCustomerProfileHandler.cs` | Handler | Calls `customer.UpdateProfile`, commits, maps profile. | Implements update profile. | Domain enforces required name and positive age. | Keeps profile valid. | Update profile. | Why not validate only in handler? |
| `src/Talabat/Talabat.Application/Customers/AddAddress/AddCustomerAddressCommand.cs` | Command | Holds address fields. | Address write request. | Transport-neutral. | Adds saved address. | Add address. | Why create `Address` value object? |
| `src/Talabat/Talabat.Application/Customers/AddAddress/AddCustomerAddressHandler.cs` | Handler | Creates `Address`, calls `customer.AddAddress`, commits. | Implements add address. | Uses `IApplicationIdGenerator` for address ID. | Enforces duplicate/default rules through Domain. | Add address. | Why not modify address collection directly? |
| `src/Talabat/Talabat.Application/Customers/RemoveAddress/RemoveCustomerAddressCommand.cs` | Command | Holds customer and address ID. | Remove address request. | Transport-neutral. | Removes saved address. | Remove address. | What if removed address was default? |
| `src/Talabat/Talabat.Application/Customers/RemoveAddress/RemoveCustomerAddressHandler.cs` | Handler | Calls `customer.RemoveAddress`, commits. | Implements remove address. | Domain checks ownership within aggregate. | Removing default does not choose a new default in Phase 3. | Remove address. | Why no auto default? |
| `src/Talabat/Talabat.Application/Customers/SetDefaultAddress/SetDefaultCustomerAddressCommand.cs` | Command | Holds selected address. | Set default request. | Transport-neutral. | Chooses default address. | Set default. | Why is one-default rule in aggregate? |
| `src/Talabat/Talabat.Application/Customers/SetDefaultAddress/SetDefaultCustomerAddressHandler.cs` | Handler | Calls `customer.SetDefaultAddress`, commits. | Implements default selection. | Aggregate clears other defaults. | Keeps only one default address. | Set default. | Why not edit child directly? |

### Ordering Files

| File path | Type | What it does | Why it was added | Technical importance | Business/logical importance | Use case depends on it | Mentor might ask |
|---|---|---|---|---|---|---|---|
| `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutCommand.cs` | Command | Holds customer and delivery address ID. | Checkout request. | Transport-neutral. | Starts checkout. | Checkout. | Why not HTTP DTO? |
| `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutErrors.cs` | Helper | Creates common checkout errors. | Avoid duplicated error creation. | Stable checkout error codes. | Clear checkout failures. | Checkout. | Why separate errors class? |
| `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutOutcome.cs` | Outcome | Represents checkout success or unavailable products. | Checkout has deterministic business outcomes. | No price-change outcome exists. | Success returns order ID and total; unavailable returns item reasons. | Checkout. | Why unavailable products are a structured outcome? |
| `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutResultMapper.cs` | Mapper | Converts Domain checkout result to Application outcome. | Separates Domain result from Application contract. | Keeps mapping explicit. | Returns productId, productName, reason. | Checkout. | Why mapper? |
| `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutHandler.cs` | Handler | Orchestrates full checkout. | Core Phase 3 workflow. | Coordinates cart, customer, restaurant, order, clock, ID, unit of work. | Creates one order and closes one cart. | Checkout. | Why not controller or `Order` aggregate? |
| `src/Talabat/Talabat.Application/Ordering/Models/OrderSummary.cs` | ReadModel | Represents order history row. | Lightweight read model. | Does not expose aggregate. | Customer order history. | Get history. | Why summary model? |
| `src/Talabat/Talabat.Application/Ordering/Models/OrderLineItem.cs` | ReadModel | Represents historical item data. | Exposes order snapshot. | Uses stored order prices. | Preserves purchase history. | Order details. | Why use snapshots? |
| `src/Talabat/Talabat.Application/Ordering/Models/OrderDeliveryAddress.cs` | ReadModel | Represents historical delivery address. | Exposes address snapshot. | Does not read current customer address. | Shows address used at checkout time. | Order details. | Why not current address? |
| `src/Talabat/Talabat.Application/Ordering/Models/OrderDetails.cs` | ReadModel | Represents full order details. | Detail response model. | Safe read projection. | Shows complete historical order. | Order details. | Why not expose `Order`? |
| `src/Talabat/Talabat.Application/Ordering/Mapping/OrderMapper.cs` | Helper | Maps `Order` to read models. | Keeps handlers focused. | Central read projection. | Preserves snapshot fields. | Order reads. | Why mapper? |
| `src/Talabat/Talabat.Application/Ordering/GetOrderHistory/GetOrderHistoryQuery.cs` | Query | Holds customer ID. | Customer-scoped order history request. | Transport-neutral. | Reads own orders. | Get history. | Why customer-scoped? |
| `src/Talabat/Talabat.Application/Ordering/GetOrderHistory/GetOrderHistoryHandler.cs` | Handler | Loads customer orders and sorts newest first. | Implements history read. | Returns empty collection if none. | Shows order history. | Get history. | Why not return null? |
| `src/Talabat/Talabat.Application/Ordering/GetOrderDetails/GetOrderDetailsQuery.cs` | Query | Holds customer ID and order ID. | Scoped order details request. | Prevents cross-customer exposure. | Reads one own order. | Get details. | Why include customer ID? |
| `src/Talabat/Talabat.Application/Ordering/GetOrderDetails/GetOrderDetailsHandler.cs` | Handler | Loads order by order ID and customer ID. | Implements secure read boundary. | Missing and unauthorized both return not found. | Avoids leaking other customer order existence. | Get details. | Why same not-found outcome? |

### Test Files

| Test file | What it tests | Why it matters | Bug it would catch | Production files it protects |
|---|---|---|---|---|
| `tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj` | xUnit project setup and references. | Enables focused Application tests. | Missing references or wrong target framework. | All Application code. |
| `tests/Talabat.Application.Tests/GlobalUsings.cs` | Shared Xunit using. | Reduces test boilerplate. | Missing `using Xunit`. | All tests. |
| `tests/Talabat.Application.Tests/README.md` | Test boundary guidance. | Documents no EF/API/Auth dependency. | Wrong testing scope. | Test strategy. |
| `TestDoubles/FakeClock.cs` | Controlled UTC time. | Tests cart expiration and checkout time. | Flaky time-dependent behavior. | Basket and checkout handlers. |
| `TestDoubles/FakeApplicationIdGenerator.cs` | Predictable IDs. | Makes created aggregate IDs deterministic. | Hardcoded/random ID behavior. | Add item, add address, checkout. |
| `TestDoubles/FakeRestaurantLocalTimeProvider.cs` | Controlled restaurant local time. | Tests open/closed restaurant behavior. | Incorrect local-time orchestration. | Browse and checkout handlers. |
| `TestDoubles/FakeUnitOfWork.cs` | Save count. | Verifies commit once or no commit. | Missing or extra commits. | Write handlers. |
| `TestDoubles/FakeRestaurantRepository.cs` | In-memory restaurant/catalog data. | Avoids EF in Application tests. | Wrong product/restaurant loading. | Catalog, basket, checkout. |
| `TestDoubles/FakeCartRepository.cs` | In-memory cart data and add/update counts. | Verifies cart orchestration. | Wrong cart creation/update. | Basket and checkout. |
| `TestDoubles/FakeCustomerRepository.cs` | In-memory customer data. | Tests profile/address and checkout. | Missing customer or wrong update behavior. | Customer and checkout. |
| `TestDoubles/FakeOrderRepository.cs` | In-memory order data. | Tests checkout/order reads. | Wrong add/scoping behavior. | Ordering handlers. |
| `TestDoubles/TestData.cs` | Builds valid Domain test objects. | Keeps tests readable. | Invalid test setup. | All handler tests. |
| `Catalog/BrowseRestaurants/BrowseRestaurantsHandlerTests.cs` | Active restaurant filtering and open status. | Protects browsing behavior. | Inactive restaurants shown. | Browse handler. |
| `Catalog/GetRestaurantMenu/GetRestaurantMenuHandlerTests.cs` | Menu read, unavailable flag, not found. | Protects menu contract. | Unavailable products hidden incorrectly. | Menu handler. |
| `Basket/GetCart/GetCartHandlerTests.cs` | Empty cart and current-price totals. | Protects cart read contract. | Stale price total. | Get cart and `CartMapper`. |
| `Basket/AddItem/AddCartItemHandlerTests.cs` | First item creation, unavailable product, invalid quantity, cross-restaurant conflict. | Protects core cart behavior. | Lost existing cart or wrong conflict handling. | Add item handler. |
| `Basket/UpdateQuantity/UpdateCartItemQuantityHandlerTests.cs` | Quantity update, invalid quantity, expired cart, inactive cart, missing item. | Protects cart mutation behavior. | Direct invalid mutation. | Update quantity handler. |
| `Basket/RemoveItem/RemoveCartItemHandlerTests.cs` | Remove item and missing item. | Protects cart mutation and total recalculation. | Wrong total after removal. | Remove item handler. |
| `Basket/ClearCart/ClearCartHandlerTests.cs` | Empty response and add after clear creates new cart. | Protects cart lifecycle. | Reusing a cleared cart. | Clear cart and add item handlers. |
| `Customers/GetProfile/GetCustomerProfileHandlerTests.cs` | Profile read and missing customer. | Protects profile read behavior. | Missing customer not handled. | Get profile handler. |
| `Customers/UpdateProfile/UpdateCustomerProfileHandlerTests.cs` | Trim full name, positive age, optional phone, missing customer. | Protects profile rules. | Invalid profile accepted. | Update profile handler. |
| `Customers/AddAddress/AddCustomerAddressHandlerTests.cs` | Required fields, duplicate address, default behavior. | Protects address invariants. | Duplicate addresses or multiple defaults. | Add address handler. |
| `Customers/RemoveAddress/RemoveCustomerAddressHandlerTests.cs` | Missing address and no auto default after removing default. | Protects Phase 3 address rule. | Automatic default selection. | Remove address handler. |
| `Customers/SetDefaultAddress/SetDefaultCustomerAddressHandlerTests.cs` | Exactly one default. | Protects aggregate invariant. | Multiple defaults. | Set default handler. |
| `Ordering/Checkout/CheckoutHandlerSuccessTests.cs` | One order, one cart checkout, current price, one commit. | Protects checkout transaction intent. | Partial commit or stale price. | Checkout handler. |
| `Ordering/Checkout/CheckoutHandlerUnavailableProductsTests.cs` | Unavailable outcome without order/commit. | Protects deterministic checkout outcome. | Creating order with unavailable products. | Checkout handler. |
| `Ordering/Checkout/CheckoutHandlerFailureTests.cs` | Missing customer/address, empty/expired/non-active cart, inactive/closed restaurant, duplicate checkout. | Protects failure mapping. | Wrong checkout error behavior. | Checkout handler. |
| `Ordering/GetOrderHistory/GetOrderHistoryHandlerTests.cs` | Customer scoping, newest-first, empty history. | Protects order history privacy and sorting. | Other customer orders shown. | History handler. |
| `Ordering/GetOrderDetails/GetOrderDetailsHandlerTests.cs` | Snapshot fields, missing order, unauthorized as not found. | Protects privacy and historical data. | Cross-customer existence leak. | Details handler. |

### Supporting Files

| File path | Type | Explanation |
|---|---|---|
| `src/Talabat/Talabat.slnx` | Solution | Added the Application test project. |
| `specs/001-application-use-cases/tasks.md` | SpecKit task list | Shows 88/88 Phase 3 tasks complete. |
| `specs/001-application-use-cases/plan.md` | SpecKit plan | Readiness wording was corrected before implementation. |
| `docs/phase-3-application-use-cases.md` | Summary document | Short implementation summary and validation notes. |
| `docs/phase-3-application-use-cases-learning-guide.md` | Learning guide | This document. |

## 5. Application Result Pattern

Phase 3 added a result pattern:

```text
UseCaseResult<T>
  -> Success(T value)
  -> Failure(ApplicationError error)
```

Why Application should not return HTTP status codes:

- HTTP belongs to the API layer.
- Application use cases may be called by controllers, background jobs, tests, or future adapters.
- Returning `IActionResult` from Application would make Application depend on ASP.NET Core.

Why Application should not throw for every expected business outcome:

- Expected business failures are not system crashes.
- Examples:
  - `ProductUnavailable`
  - `CartExpired`
  - `RestaurantClosed`
  - `AddressNotFound`
  - `CrossRestaurantCart`

Example:

If checkout fails because a product became unavailable, this is an expected business outcome. The Application layer returns a structured result instead of returning an HTTP response directly.

Actual code examples:

- `UseCaseResult<T>` stores either a success value or an `ApplicationError`.
- `ApplicationError` contains:
  - `Code`
  - `Category`
  - `Message`
- `DomainExceptionMapper` maps Domain exceptions such as `CartExpiredException`, `ProductUnavailableException`, or `RestaurantClosedException` to Application errors.

Future API mapping example:

```text
ApplicationErrorCategory.Validation  -> HTTP 400
ApplicationErrorCategory.NotFound    -> HTTP 404
ApplicationErrorCategory.Conflict    -> HTTP 409
ApplicationErrorCategory.Unavailable -> HTTP 409 or 422 depending on API design
```

That mapping should happen in the API phase, not Phase 3.

## 6. Abstractions Added In Phase 3

### `IApplicationIdGenerator`

This directly answers the question: "Why is there `IApplicationIdGenerator`?"

It exists because current Domain creation APIs require IDs up front:

- `Cart.Create(int id, ...)`
- `Customer.AddAddress(int addressId, ...)`
- `Order.CreateFromCheckout(int id, ...)`

However, Phase 3 intentionally did not implement persistence. There is no EF Core, no database, no identity column, no sequence, and no repository implementation that can assign IDs.

Bad alternatives would be:

- Hardcode IDs inside handlers.
- Generate random IDs inside Domain.
- Make Application depend on a database before Infrastructure exists.
- Change Domain creation design during Phase 3 just to work around missing persistence.

So `IApplicationIdGenerator` acts as a small boundary contract:

```csharp
public interface IApplicationIdGenerator
{
    int NewCartId();
    int NewCustomerAddressId();
    int NewOrderId();
}
```

Who implements it?

- In tests: `FakeApplicationIdGenerator`.
- Later in Infrastructure: a real implementation, or a redesigned ID strategy if EF Core/database-generated IDs become the chosen approach.

Is it final?

No. It is a Phase 3 bridge. Phase 4 must decide the real persistence and ID generation strategy.

Strong mentor answer:

`IApplicationIdGenerator` exists because Application needs to create Domain aggregates now, and those aggregates currently require integer IDs. Since database persistence is deferred to Phase 4, we introduced a small abstraction instead of hardcoding IDs or depending on EF Core. It keeps handlers testable and keeps Application independent from Infrastructure.

### `IRestaurantLocalTimeProvider`

`CheckoutDomainService.ValidateCheckout` needs restaurant-local time to decide whether a restaurant is open.

The Domain `Restaurant` aggregate has opening hours but does not currently have a full time-zone policy. Calculating local time from UTC may depend on Infrastructure data or external rules later.

So Application asks:

```csharp
TimeOnly GetLocalTime(Restaurant restaurant, DateTime utcNow);
```

Infrastructure can implement the real calculation later.

### `IClock`

`IClock` supplies `UtcNow`.

It is used for:

- Cart expiration.
- Checkout timestamps.
- Test-controlled time.

It avoids direct calls to `DateTime.UtcNow` inside handlers.

## 7. Catalog Use Cases

### Browse Restaurants

Files involved:

- `src/Talabat/Talabat.Application/Catalog/BrowseRestaurants/BrowseRestaurantsQuery.cs`
- `src/Talabat/Talabat.Application/Catalog/BrowseRestaurants/BrowseRestaurantsHandler.cs`
- `src/Talabat/Talabat.Application/Catalog/Models/RestaurantSummary.cs`

Request model:

- `BrowseRestaurantsQuery`

Response/read model:

- `IReadOnlyCollection<RestaurantSummary>`

Repository methods used:

- `IRestaurantRepository.GetActiveRestaurantsAsync`

Domain logic used:

- `Restaurant.IsOpenAt(localTime)`

Flow:

```text
BrowseRestaurantsQuery
  -> BrowseRestaurantsHandler
  -> IRestaurantRepository.GetActiveRestaurantsAsync()
  -> IRestaurantLocalTimeProvider.GetLocalTime(...)
  -> Restaurant.IsOpenAt(...)
  -> RestaurantSummary collection
  -> UseCaseResult.Success
```

Why it is a query:
It reads available restaurants and does not change state.

Why not expose Domain entities:
`Restaurant` is an aggregate with behavior. The caller only needs a read projection.

### Get Restaurant Menu

Files involved:

- `src/Talabat/Talabat.Application/Catalog/GetRestaurantMenu/GetRestaurantMenuQuery.cs`
- `src/Talabat/Talabat.Application/Catalog/GetRestaurantMenu/GetRestaurantMenuHandler.cs`
- `src/Talabat/Talabat.Application/Catalog/Models/RestaurantMenu.cs`
- `src/Talabat/Talabat.Application/Catalog/Models/MenuProduct.cs`

Request model:

- `GetRestaurantMenuQuery(int RestaurantId)`

Response/read model:

- `RestaurantMenu`

Repository methods used:

- `IRestaurantRepository.GetByIdWithProductsAsync`

Errors:

- `RestaurantNotFound`

Important behavior:

- Unavailable products are included with `IsAvailable = false`.
- The handler does not expose `Product` child entities directly.

## 8. Basket / Cart Use Cases

### Get Cart

Goal:
Return the current active cart or a deterministic empty cart response.

Files:

- `GetCartQuery.cs`
- `GetCartHandler.cs`
- `CartDetails.cs`
- `CartLineItem.cs`
- `CartMapper.cs`

Flow:

```text
GetCartQuery(customerId)
  -> ICartRepository.GetActiveCartByCustomerIdAsync(customerId)
  -> if null: CartDetails.Empty(customerId)
  -> if expired: CartExpired failure
  -> IRestaurantRepository.GetByIdWithProductsAsync(cart.RestaurantId)
  -> CartMapper.ToDetails(cart, restaurant)
  -> current Catalog prices used
```

Important rule:
`CartDetails.CalculatedCurrentTotal` is calculated using current Catalog prices loaded by Application.

### Add Item To Cart

Goal:
Add an available product to a customer's current active cart, or create a new cart if none exists.

Files:

- `AddCartItemCommand.cs`
- `AddCartItemHandler.cs`
- `CartMapper.cs`
- `CartDetails.cs`

Step-by-step:

```text
AddCartItemCommand
  -> load product snapshot using IRestaurantRepository.GetProductSnapshotAsync
  -> load active cart using ICartRepository.GetActiveCartByCustomerIdAsync
  -> if no cart: Cart.Create(idGenerator.NewCartId(), ...)
  -> else: cart.AddItem(snapshot, quantity, now)
  -> load Restaurant with Products for current prices
  -> CartMapper.ToDetails(cart, restaurant)
  -> IUnitOfWork.SaveChangesAsync()
  -> return UseCaseResult<CartDetails>
```

Domain aggregate methods called:

- `Cart.Create`
- `Cart.AddItem`

Repository methods used:

- `IRestaurantRepository.GetProductSnapshotAsync`
- `IRestaurantRepository.ExistsAsync`
- `IRestaurantRepository.GetByIdWithProductsAsync`
- `ICartRepository.GetActiveCartByCustomerIdAsync`
- `ICartRepository.AddAsync`
- `ICartRepository.Update`

Errors handled:

- `RestaurantNotFound`
- `ProductNotFound`
- `ProductUnavailable`
- `InvalidQuantity`
- `CartExpired`
- `CartNotActive`
- `CrossRestaurantCart`

Key design explanations:

- The handler does not create `CartItem` directly.
- `Cart` decides whether duplicate products are merged.
- `Cart` enforces one-restaurant-per-cart.
- `CartItem` does not store product prices.
- The handler loads current Catalog prices only when returning `CartDetails`.

Mentor Q&A:

**Why not create `CartItem` directly in the handler?**  
Because `CartItem` is a child entity. It must be changed only through the aggregate root `Cart`.

**Why not store price in `CartItem`?**  
Because the cart is a temporary selection. Prices are current Catalog data. The order, not the cart, stores historical price snapshots.

**Why use a product snapshot instead of passing `Product` entity to `Cart`?**  
Because `Product` is a child of `Restaurant`. Passing the full entity would blur aggregate boundaries. A snapshot gives `Cart` only what it needs.

**Why does Application orchestrate but Domain validates invariants?**  
Application knows workflow order and dependencies. Domain owns rules that must always be true.

### Update Quantity

Flow:

```text
UpdateCartItemQuantityCommand
  -> load active cart
  -> cart.UpdateQuantity(productId, quantity, now)
  -> load Restaurant with Products
  -> return updated CartDetails with current prices
  -> commit once
```

Domain method:

- `Cart.UpdateQuantity`

Errors:

- `CartNotFound`
- `CartExpired`
- `CartNotActive`
- `CartItemNotFound`
- `InvalidQuantity`

### Remove Item

Flow:

```text
RemoveCartItemCommand
  -> load active cart
  -> cart.RemoveItem(productId, now)
  -> load Restaurant with Products
  -> return updated CartDetails
  -> commit once
```

Domain method:

- `Cart.RemoveItem`

### Clear Cart

Flow:

```text
ClearCartCommand
  -> load active cart
  -> cart.Clear(now)
  -> return CartDetails.Empty(customerId)
  -> commit once
```

Important behavior:
After clear, the cart is not reused. A later add-item workflow creates a new cart.

## 9. Customer Use Cases

### Get Profile

Files:

- `GetCustomerProfileQuery.cs`
- `GetCustomerProfileHandler.cs`
- `CustomerProfile.cs`
- `CustomerAddressDetails.cs`
- `CustomerMapper.cs`

Flow:

```text
GetCustomerProfileQuery(customerId)
  -> ICustomerRepository.GetByIdWithAddressesAsync(customerId)
  -> CustomerMapper.ToProfile(customer)
  -> UseCaseResult<CustomerProfile>
```

### Update Profile

Handler calls:

```text
customer.UpdateProfile(fullName, age, phoneNumber)
```

Domain protects:

- `FullName` is required and trimmed.
- `Age` must be positive.
- `PhoneNumber` is optional.

### Add Address

Handler creates an `Address` value object and calls:

```text
customer.AddAddress(idGenerator.NewCustomerAddressId(), address, makeDefault)
```

Domain protects:

- Required street/city/building number.
- Duplicate address detection.
- Only one default address.

### Remove Address

Handler calls:

```text
customer.RemoveAddress(addressId)
```

Phase 3 behavior:
Removing the current default address does not automatically select another default address.

### Set Default Address

Handler calls:

```text
customer.SetDefaultAddress(addressId)
```

The aggregate clears the previous default and marks the selected address as default.

Mentor Q&A:

**Why is `Customer` separate from Identity user?**  
`Customer` is a business profile. Identity user is about credentials, login, tokens, claims, and roles. Mixing them would make Domain depend on a framework.

**Why did Phase 3 still use explicit `customerId`?**  
Because Identity/Auth is deferred. Later, API/Auth will resolve the customer profile from the authenticated user and pass the correct `customerId`.

**Why not add IdentityServer now?**  
Because the identity framework has not been selected. Adding it now would force premature decisions into the architecture.

## 10. Ordering / Checkout Use Cases

### Checkout

Files:

- `CheckoutCommand.cs`
- `CheckoutHandler.cs`
- `CheckoutOutcome.cs`
- `CheckoutResultMapper.cs`
- `CheckoutErrors.cs`

Full flow:

```text
CheckoutCommand(customerId, deliveryAddressId)
  -> CheckoutHandler
  -> load active Cart by customerId
  -> load Customer with addresses
  -> Customer.CreateDeliveryAddressSnapshot(deliveryAddressId)
  -> load Restaurant with Products by cart.RestaurantId
  -> resolve restaurant local time
  -> CheckoutDomainService.ValidateCheckout(...)
  -> if unavailable products: return CheckoutProductsUnavailableOutcome without commit
  -> generate order id using IApplicationIdGenerator.NewOrderId()
  -> Order.CreateFromCheckout(...)
  -> IOrderRepository.AddAsync(order)
  -> cart.MarkCheckedOut(now)
  -> ICartRepository.Update(cart)
  -> IUnitOfWork.SaveChangesAsync()
  -> return CheckoutSucceededOutcome(orderId, total)
```

Why checkout is Application orchestration:
Checkout needs multiple aggregates: `Cart`, `Customer`, `Restaurant`, and `Order`. No single aggregate should load or save all of them.

Which checks are in Domain:

- Cart must be active.
- Cart must not be expired.
- Cart must not be empty.
- Restaurant must match cart.
- Restaurant must be active.
- Restaurant must be open.
- Products must exist and be available.
- Order must snapshot valid checkout items and delivery address.

Which checks are Application orchestration:

- Load cart/customer/restaurant/order repositories.
- Resolve restaurant local time.
- Generate order ID.
- Add order.
- Mark cart checked out.
- Commit once.
- Convert Domain results to Application outcomes.

Why `Order` stores immutable snapshots:
Order history must reflect what happened at checkout time. If product price or customer address changes later, old orders must not change.

Why current Catalog price is used:
`Cart` and `CartItem` do not store prices. Checkout reads current `Product.CurrentPrice` from `Restaurant.Products`, creates `CheckoutItemSnapshot`, then `Order.CreateFromCheckout` stores that price.

Why no price-change outcome exists:
Because the cart never stored an old price to compare against. Phase 3 intentionally uses current Catalog prices.

Why commit once matters:
Successful checkout means "create one order and close one cart". If those changes are committed separately, the system can end up in a partial state.

Mentor Q&A:

**Why checkout is not inside Controller?**  
Controller is transport. Checkout is business workflow orchestration and must be reusable outside HTTP.

**Why checkout is not fully inside `Order` aggregate?**  
`Order` does not own `Cart`, `Customer`, or `Restaurant`. Checkout spans multiple aggregates.

**Why use `CheckoutDomainService`?**  
It handles cross-aggregate Domain validation that does not naturally belong to one aggregate.

**Why create `CheckoutItemSnapshot`?**  
To pass validated product name, quantity, and current price into order creation as historical data.

**Why mark cart checked out only after successful order creation?**  
So a failed checkout does not close the cart.

**Why commit once?**  
To preserve atomic business intent.

### Get Order History

Files:

- `GetOrderHistoryQuery.cs`
- `GetOrderHistoryHandler.cs`
- `OrderSummary.cs`
- `OrderMapper.cs`

Flow:

```text
GetOrderHistoryQuery(customerId)
  -> IOrderRepository.GetByCustomerIdAsync(customerId)
  -> order newest first
  -> OrderSummary collection
```

Behavior:

- Customer-scoped.
- Returns empty collection if no orders exist.
- Orders are sorted newest first.

### Get Order Details

Files:

- `GetOrderDetailsQuery.cs`
- `GetOrderDetailsHandler.cs`
- `OrderDetails.cs`
- `OrderLineItem.cs`
- `OrderDeliveryAddress.cs`
- `OrderMapper.cs`

Flow:

```text
GetOrderDetailsQuery(customerId, orderId)
  -> IOrderRepository.GetByIdForCustomerAsync(orderId, customerId)
  -> if null: OrderNotFound
  -> OrderDetails with historical item and address snapshots
```

Behavior:

- Missing order returns `OrderNotFound`.
- Other customer's order also returns `OrderNotFound`.
- This avoids exposing cross-customer order existence.

## 11. Tests Added In Phase 3

`tests/Talabat.Application.Tests` exists to test Application orchestration without Infrastructure.

Why fake repositories instead of EF Core:

- EF Core is not part of Phase 3.
- We are testing handler behavior, not database mapping.
- Fakes keep tests fast and focused.

What fake services do:

- `FakeRestaurantRepository`: stores restaurants in memory.
- `FakeCartRepository`: stores carts and counts add/update calls.
- `FakeCustomerRepository`: stores customers in memory.
- `FakeOrderRepository`: stores orders and supports customer-scoped reads.
- `FakeUnitOfWork`: counts `SaveChangesAsync` calls.
- `FakeClock`: controls `UtcNow`.
- `FakeApplicationIdGenerator`: returns predictable IDs.
- `FakeRestaurantLocalTimeProvider`: controls restaurant local time.
- `TestData`: creates valid Domain objects for tests.

What flows are tested:

- Catalog browsing.
- Menu retrieval.
- Empty cart and current-price cart totals.
- Add item, including unavailable product, invalid quantity, and cross-restaurant conflict.
- Update quantity, remove item, clear cart.
- Customer profile and address rules.
- Checkout success, unavailable products, invalid states, and duplicate checkout.
- Customer-scoped order history and details.

Mentor-friendly statement:

We are not testing EF Core here. We are testing whether the Application handler calls the correct repositories, delegates to Domain, handles outcomes correctly, and commits once.

## 12. Clean Architecture Rules Preserved

Phase 3 preserved these rules:

- `Talabat.Application` references `Talabat.Domain`.
- `Talabat.Application` does not reference `Talabat.Infrastructure`.
- `Talabat.Application` does not reference `Talabat.API`.
- Application does not use EF Core.
- Application does not use ASP.NET Core HTTP types.
- Application does not use IdentityServer or Identity types.
- Domain remains independent.
- Handlers do not directly mutate child entities.
- Repositories are used through interfaces.
- Results are transport-neutral.
- No `ProductRepository` was introduced.

No architecture violation was found in the implemented Application code. Any hits for words like Identity/Auth were documentation guardrails, not implementation dependencies.

## 13. What Was Deliberately Deferred

The following were deliberately not implemented:

- EF Core.
- `DbContext`.
- Repository implementations.
- Migrations.
- Seed data.
- API endpoints.
- Controllers.
- HTTP result mapping.
- IdentityServer.
- Authentication.
- Authorization policies.
- Current authenticated user abstraction.
- Frontend websites.
- Delivery use cases.
- Payment, coupons, reviews, notifications.

Why this is correct:

The roadmap order is:

```text
Domain -> Contracts -> Application -> Infrastructure -> API -> Websites/Auth
```

If EF/API/Identity were implemented before Application use cases, the outer layers would be built around unstable workflows.

## 14. Phase 3 Flow Diagrams

### Add To Cart Flow

```text
Request
  -> AddCartItemCommand
  -> AddCartItemHandler
  -> IRestaurantRepository.GetProductSnapshotAsync(...)
  -> ICartRepository.GetActiveCartByCustomerIdAsync(...)
  -> if no cart: IApplicationIdGenerator.NewCartId()
  -> Cart.Create(...) or Cart.AddItem(...)
  -> IRestaurantRepository.GetByIdWithProductsAsync(...)
  -> CartMapper.ToDetails(cart, restaurant)
  -> IUnitOfWork.SaveChangesAsync()
  -> UseCaseResult<CartDetails>
```

### Checkout Flow

```text
Request
  -> CheckoutCommand
  -> CheckoutHandler
  -> ICartRepository.GetActiveCartByCustomerIdAsync(...)
  -> ICustomerRepository.GetByIdWithAddressesAsync(...)
  -> Customer.CreateDeliveryAddressSnapshot(...)
  -> IRestaurantRepository.GetByIdWithProductsAsync(...)
  -> IRestaurantLocalTimeProvider.GetLocalTime(...)
  -> CheckoutDomainService.ValidateCheckout(...)
  -> if unavailable: CheckoutProductsUnavailableOutcome, no commit
  -> IApplicationIdGenerator.NewOrderId()
  -> Order.CreateFromCheckout(...)
  -> IOrderRepository.AddAsync(order)
  -> Cart.MarkCheckedOut(...)
  -> ICartRepository.Update(cart)
  -> IUnitOfWork.SaveChangesAsync()
  -> CheckoutSucceededOutcome
```

### Application Test Flow

```text
Test
  -> Fake Repository / Fake Clock / Fake ID Generator
  -> Handler
  -> Domain Aggregate or Domain Service
  -> Fake UnitOfWork
  -> Assert UseCaseResult and state changes
```

## 15. Mentor Questions And Strong Answers

1. **What is the purpose of the Application layer?**  
   It coordinates complete use cases. It loads aggregates, calls Domain behavior, maps expected outcomes, and defines transaction boundaries.

2. **Why did not you put the logic in Controllers?**  
   Controllers are transport adapters. If use-case logic lives there, it becomes tied to HTTP and cannot be reused cleanly.

3. **Why did not you implement EF Core in Phase 3?**  
   Phase 3 is about Application orchestration. EF Core is Infrastructure and belongs to Phase 4.

4. **Why are repositories only interfaces here?**  
   Application needs contracts, not database details. Infrastructure will implement the interfaces later.

5. **Why use UnitOfWork?**  
   To commit all changes in one workflow together. Checkout must create one order and close one cart as one business action.

6. **Why use CQRS-lite instead of MediatR?**  
   The project did not need another package yet. Explicit commands, queries, and handlers give the same organization without committing to MediatR.

7. **Why no IdentityServer in Phase 3?**  
   The identity framework is not selected. Phase 3 keeps customer scoping explicit and framework-neutral.

8. **Why does Phase 3 use explicit `customerId`?**  
   Authentication is not implemented yet. Later, API/Auth will resolve the authenticated user to a customer profile and pass the correct `customerId`.

9. **How will authentication be added later?**  
   After choosing an identity framework, the API/Auth boundary will map the authenticated account to a domain profile. Use cases can continue receiving domain IDs.

10. **Why should Application return transport-neutral results?**  
    Because Application should not depend on HTTP. API can map Application results to HTTP later.

11. **What is the difference between Domain invariant and Application validation?**  
    Domain invariant is a rule that must always hold, like one default address or one restaurant per cart. Application validation/orchestration decides workflow loading, ordering, and result mapping.

12. **Why do handlers return read models instead of Domain entities?**  
    Read models expose only the data needed by the use case and avoid leaking mutable aggregates.

13. **Why does Cart not store prices?**  
    Cart is a temporary selection. Current prices come from Catalog. Order stores historical prices.

14. **Why does Order store immutable snapshots?**  
    Order represents history. Later product price or address changes must not change old orders.

15. **Why does Checkout need current Catalog prices?**  
    Because `CartItem` does not store price. Checkout reads current `Product.CurrentPrice` and snapshots it into the order.

16. **Why use fake repositories in tests?**  
    Because we are testing Application orchestration, not EF Core.

17. **What is the difference between Application tests and Infrastructure tests?**  
    Application tests use fakes and verify handler behavior. Infrastructure tests will verify EF mappings, queries, and database constraints.

18. **What did Phase 3 prepare for Phase 4?**  
    It created stable use-case contracts and tests. Phase 4 can now implement real persistence behind the existing interfaces.

19. **What are the main risks if this phase was implemented badly?**  
    Business logic in controllers, HTTP types in Application, prices stored in Cart, child entities modified directly, product repository added, or checkout saved in partial steps.

20. **If your mentor asks: what exactly did you do in Phase 3?**  
    I implemented Application use cases for the customer ordering path: Catalog, Basket, Customer, Checkout, and Orders. I added result contracts, abstractions, read models, handlers, and xUnit tests, while keeping EF Core, API, Identity, and Delivery deferred.

## 16. If I Need To Explain This In 2 Minutes

In Phase 3, I implemented the Application layer use cases. The Domain already had aggregates and business rules, but the project needed a layer that coordinates full workflows like add-to-cart and checkout.

The handlers load aggregates through repository interfaces, call Domain methods such as `Cart.AddItem`, `Customer.AddAddress`, or `CheckoutDomainService.ValidateCheckout`, then return transport-neutral `UseCaseResult<T>` values. They do not return HTTP responses and they do not contain Infrastructure details.

Checkout is the most important example. The handler loads the active cart, loads the customer and delivery address, loads the restaurant and current products, validates checkout through the Domain service, creates an order snapshot using current Catalog prices, marks the cart checked out, and commits once through `IUnitOfWork`.

I did not implement EF Core, API endpoints, or IdentityServer because those are later phases. This keeps the architecture clean: Domain remains independent, Application depends only on Domain contracts, and Infrastructure/API can be added later behind stable use cases.

## 17. If I Need To Explain This In 30 Seconds

Phase 3 added Application use cases for the customer ordering path: browse, cart, profile, checkout, and orders. Handlers coordinate repositories and Domain aggregates, return transport-neutral results, and commit writes once. No EF Core, API endpoints, or IdentityServer were added because those belong to later phases.

## 18. Technical Glossary

| Term | Meaning |
|---|---|
| Application Layer | The layer that coordinates use cases between external adapters and Domain. |
| Use Case | A complete workflow with a business goal, such as add item or checkout. |
| Handler | A class that executes one use case. |
| Command | A request that changes state, such as `AddCartItemCommand`. |
| Query | A request that reads state, such as `GetCartQuery`. |
| DTO | A data transfer object. It carries data and should not contain business behavior. |
| Read Model | A model shaped for returning data from a query or command result. |
| Result Pattern | Returning explicit success/failure values instead of HTTP responses or exceptions for expected outcomes. |
| UnitOfWork | A contract for committing all changes in a workflow together. |
| Repository Interface | A contract for loading and saving aggregate roots without knowing database details. |
| Fake Repository | A test-only in-memory implementation of a repository interface. |
| Domain Invariant | A business rule that must always be protected by the Domain model. |
| Application Validation | Workflow-level checks and result mapping done by Application. |
| Snapshot | A historical copy of data, such as product price or delivery address at checkout time. |
| Transport-neutral result | A result that is not tied to HTTP, controllers, or ASP.NET Core. |
| CQRS-lite | Organizing use cases into commands, queries, and handlers without using MediatR. |

## 19. File Map Summary

```text
src/Talabat/Talabat.Application/
  Abstractions/
    IClock.cs                         -> current UTC time abstraction
    IApplicationIdGenerator.cs         -> cart/address/order ID abstraction
    IRestaurantLocalTimeProvider.cs    -> restaurant local-time abstraction

  Common/Results/
    ApplicationErrorCategory.cs        -> error categories
    ApplicationError.cs                -> error contract
    ApplicationErrorCodes.cs           -> stable error codes
    DomainExceptionMapper.cs           -> Domain exception to ApplicationError mapping
    UseCaseResult.cs                   -> success/failure wrapper

  Catalog/
    BrowseRestaurants/                 -> browse active restaurants
    GetRestaurantMenu/                 -> read restaurant menu
    Models/                            -> RestaurantSummary, RestaurantMenu, MenuProduct

  Basket/
    GetCart/                           -> read current active cart
    AddItem/                           -> add product to cart
    UpdateQuantity/                    -> update cart item quantity
    RemoveItem/                        -> remove cart item
    ClearCart/                         -> clear cart
    Mapping/                           -> CartMapper with current prices
    Models/                            -> CartDetails, CartLineItem

  Customers/
    GetProfile/                        -> read customer profile
    UpdateProfile/                     -> update customer profile
    AddAddress/                        -> add saved address
    RemoveAddress/                     -> remove saved address
    SetDefaultAddress/                 -> choose default address
    Mapping/                           -> CustomerMapper
    Models/                            -> CustomerProfile, CustomerAddressDetails

  Ordering/
    Checkout/                          -> checkout orchestration and outcomes
    GetOrderHistory/                   -> customer-scoped order history
    GetOrderDetails/                   -> customer-scoped order details
    Mapping/                           -> OrderMapper
    Models/                            -> order read models

tests/Talabat.Application.Tests/
  TestDoubles/                         -> fake repos, fake clock, fake ID/time providers
  Catalog/                             -> catalog handler tests
  Basket/                              -> cart handler tests
  Customers/                           -> customer handler tests
  Ordering/                            -> checkout/order read tests
```

## 20. Final Review Checklist

- [ ] I can explain why Application layer exists.
- [ ] I can explain every important file added in Phase 3.
- [ ] I can explain the add-to-cart flow.
- [ ] I can explain the checkout flow.
- [ ] I can explain why no EF Core yet.
- [ ] I can explain why no IdentityServer yet.
- [ ] I can explain why tests use fake repositories.
- [ ] I can explain how Phase 3 prepares for Phase 4.
- [ ] I can explain the difference between Domain logic and Application orchestration.
- [ ] I can answer why handlers do not return HTTP responses.

## Not Implemented / Missing / Deferred

No incomplete Phase 3 task was found in `specs/001-application-use-cases/tasks.md`; it shows 88/88 tasks complete.

These items are still deliberately deferred:

- Real implementation of `IApplicationIdGenerator`.
- Real implementation of `IRestaurantLocalTimeProvider`.
- Real implementation of `IClock`.
- Real `IUnitOfWork` implementation.
- EF Core repositories.
- API mapping for `UseCaseResult<T>`.
- Dependency injection registration for Application/Infrastructure.
- Current authenticated user abstraction.
- Identity/Auth.
- Delivery Application workflows.

These are not Phase 3 bugs. They are later-phase responsibilities.

## Verification Notes From Repository

- `git status --short` shows Phase 3 files as untracked/modified in the working tree.
- Recent commit history includes `91dc805 New PLAN, clean domain, Add Repo interfaces to Domain and Application and speckit to implement phase 3`.
- `specs/001-application-use-cases/tasks.md` shows all Phase 3 tasks completed.
- `docs/phase-3-application-use-cases.md` records that build and tests passed, with the only warning being the pre-existing `Microsoft.OpenApi` NU1903 advisory in `Talabat.API`.
- Phase 3 changes have not been verified as committed; they are currently working tree changes.
