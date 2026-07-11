# Tasks: Application Use Cases

**Input**: Design documents from `specs/001-application-use-cases/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/application-use-cases.md`, `quickstart.md`
**Testing Note**: Application test tasks are included because `plan.md` and `quickstart.md` require focused Application tests with fake repositories and fake clocks.

## Phase 1: Setup

- [X] T001 Create xUnit Application test project in `tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj`
- [X] T002 Add `tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj` to `src/Talabat/Talabat.slnx`
- [X] T003 Add shared test usings in `tests/Talabat.Application.Tests/GlobalUsings.cs`
- [X] T004 Create test project folder guide in `tests/Talabat.Application.Tests/README.md`

## Phase 2: Foundational

- [X] T005 Create result category enum in `src/Talabat/Talabat.Application/Common/Results/ApplicationErrorCategory.cs`
- [X] T006 Create application error value type in `src/Talabat/Talabat.Application/Common/Results/ApplicationError.cs`
- [X] T007 Create generic use-case result type in `src/Talabat/Talabat.Application/Common/Results/UseCaseResult.cs`
- [X] T008 Create shared error code constants in `src/Talabat/Talabat.Application/Common/Results/ApplicationErrorCodes.cs`
- [X] T009 Create domain exception to application error mapper in `src/Talabat/Talabat.Application/Common/Results/DomainExceptionMapper.cs`
- [X] T010 Create application ID generator abstraction in Application abstractions (superseded and removed in Phase 3.5)
- [X] T011 Create restaurant local-time abstraction in `src/Talabat/Talabat.Application/Abstractions/IRestaurantLocalTimeProvider.cs`
- [X] T012 [P] Create fake clock test double in `tests/Talabat.Application.Tests/TestDoubles/FakeClock.cs`
- [X] T013 [P] Create fake application ID generator in test doubles (superseded and removed in Phase 3.5)
- [X] T014 [P] Create fake restaurant local-time provider in `tests/Talabat.Application.Tests/TestDoubles/FakeRestaurantLocalTimeProvider.cs`
- [X] T015 [P] Create fake unit of work in `tests/Talabat.Application.Tests/TestDoubles/FakeUnitOfWork.cs`
- [X] T016 [P] Create fake restaurant repository in `tests/Talabat.Application.Tests/TestDoubles/FakeRestaurantRepository.cs`
- [X] T017 [P] Create fake cart repository in `tests/Talabat.Application.Tests/TestDoubles/FakeCartRepository.cs`
- [X] T018 [P] Create fake customer repository in `tests/Talabat.Application.Tests/TestDoubles/FakeCustomerRepository.cs`
- [X] T019 [P] Create fake order repository in `tests/Talabat.Application.Tests/TestDoubles/FakeOrderRepository.cs`

## Phase 3: User Story 1 - Catalog Browsing And Menu

**Goal**: Customers can browse active restaurants and view a restaurant menu with product availability clearly represented.
**Independent Test**: Catalog handlers can be executed with only a fake restaurant repository, fake clock, and fake restaurant local-time provider; no cart, customer, order, API, or infrastructure dependency is required.

- [X] T020 [P] [US1] Add browse restaurants handler tests in `tests/Talabat.Application.Tests/Catalog/BrowseRestaurants/BrowseRestaurantsHandlerTests.cs`
- [X] T021 [P] [US1] Add restaurant menu handler tests in `tests/Talabat.Application.Tests/Catalog/GetRestaurantMenu/GetRestaurantMenuHandlerTests.cs`
- [X] T022 [P] [US1] Create restaurant summary read model in `src/Talabat/Talabat.Application/Catalog/Models/RestaurantSummary.cs`
- [X] T023 [P] [US1] Create menu product read model in `src/Talabat/Talabat.Application/Catalog/Models/MenuProduct.cs`
- [X] T024 [P] [US1] Create restaurant menu read model in `src/Talabat/Talabat.Application/Catalog/Models/RestaurantMenu.cs`
- [X] T025 [P] [US1] Create browse restaurants query in `src/Talabat/Talabat.Application/Catalog/BrowseRestaurants/BrowseRestaurantsQuery.cs`
- [X] T026 [US1] Implement browse restaurants handler in `src/Talabat/Talabat.Application/Catalog/BrowseRestaurants/BrowseRestaurantsHandler.cs`
- [X] T027 [P] [US1] Create get restaurant menu query in `src/Talabat/Talabat.Application/Catalog/GetRestaurantMenu/GetRestaurantMenuQuery.cs`
- [X] T028 [US1] Implement get restaurant menu handler in `src/Talabat/Talabat.Application/Catalog/GetRestaurantMenu/GetRestaurantMenuHandler.cs`

## Phase 4: User Story 2 - Basket Management

**Goal**: Customers can view a current cart, add items, update quantities, remove items, and clear the cart while cart invariants remain Domain-owned.
**Independent Test**: Basket handlers can be executed with fake cart and restaurant repositories plus fake clock, ID generator, and unit of work; no customer, order, API, or infrastructure dependency is required.

- [X] T029 [P] [US2] Add get cart handler tests in `tests/Talabat.Application.Tests/Basket/GetCart/GetCartHandlerTests.cs`
- [X] T030 [P] [US2] Add add cart item handler tests covering first-item cart creation, unavailable product, invalid quantity, and cross-restaurant conflict in `tests/Talabat.Application.Tests/Basket/AddItem/AddCartItemHandlerTests.cs`
- [X] T031 [P] [US2] Add update cart item quantity handler tests covering invalid quantity, expired cart, non-active cart, and missing item outcomes in `tests/Talabat.Application.Tests/Basket/UpdateQuantity/UpdateCartItemQuantityHandlerTests.cs`
- [X] T032 [P] [US2] Add remove cart item handler tests in `tests/Talabat.Application.Tests/Basket/RemoveItem/RemoveCartItemHandlerTests.cs`
- [X] T033 [P] [US2] Add clear cart handler tests covering cleared-cart response and later new-cart expectation in `tests/Talabat.Application.Tests/Basket/ClearCart/ClearCartHandlerTests.cs`
- [X] T034 [P] [US2] Create cart line item read model in `src/Talabat/Talabat.Application/Basket/Models/CartLineItem.cs`
- [X] T035 [P] [US2] Create cart details read model in `src/Talabat/Talabat.Application/Basket/Models/CartDetails.cs`
- [X] T036 [US2] Create cart mapping helper in `src/Talabat/Talabat.Application/Basket/Mapping/CartMapper.cs`
- [X] T037 [P] [US2] Create get cart query in `src/Talabat/Talabat.Application/Basket/GetCart/GetCartQuery.cs`
- [X] T038 [US2] Implement get cart handler in `src/Talabat/Talabat.Application/Basket/GetCart/GetCartHandler.cs`
- [X] T039 [P] [US2] Create add cart item command in `src/Talabat/Talabat.Application/Basket/AddItem/AddCartItemCommand.cs`
- [X] T040 [US2] Implement add cart item handler with active cart lookup, Catalog product snapshot lookup, `Cart` aggregate mutation, current Catalog price loading for returned `CartDetails`, and one `IUnitOfWork` commit in `src/Talabat/Talabat.Application/Basket/AddItem/AddCartItemHandler.cs`
- [X] T041 [P] [US2] Create update cart item quantity command in `src/Talabat/Talabat.Application/Basket/UpdateQuantity/UpdateCartItemQuantityCommand.cs`
- [X] T042 [US2] Implement update cart item quantity handler with active cart lookup, `Cart` aggregate mutation, current Catalog price loading for returned `CartDetails`, and one `IUnitOfWork` commit in `src/Talabat/Talabat.Application/Basket/UpdateQuantity/UpdateCartItemQuantityHandler.cs`
- [X] T043 [P] [US2] Create remove cart item command in `src/Talabat/Talabat.Application/Basket/RemoveItem/RemoveCartItemCommand.cs`
- [X] T044 [US2] Implement remove cart item handler with active cart lookup, `Cart` aggregate mutation, current Catalog price loading for remaining items in returned `CartDetails`, and one `IUnitOfWork` commit in `src/Talabat/Talabat.Application/Basket/RemoveItem/RemoveCartItemHandler.cs`
- [X] T045 [P] [US2] Create clear cart command in `src/Talabat/Talabat.Application/Basket/ClearCart/ClearCartCommand.cs`
- [X] T046 [US2] Implement clear cart handler with active cart lookup, `Cart` aggregate mutation, deterministic empty response or zero-total empty `CartDetails`, and one `IUnitOfWork` commit in `src/Talabat/Talabat.Application/Basket/ClearCart/ClearCartHandler.cs`

## Phase 5: User Story 3 - Customer Profile And Addresses

**Goal**: Customers can retrieve and update profile information, manage saved delivery addresses, and preserve one-default-address and duplicate-address rules.
**Independent Test**: Customer handlers can be executed with only a fake customer repository, fake ID generator, and fake unit of work; no cart, restaurant, order, API, or infrastructure dependency is required.

- [X] T047 [P] [US3] Add get customer profile handler tests in `tests/Talabat.Application.Tests/Customers/GetProfile/GetCustomerProfileHandlerTests.cs`
- [X] T048 [P] [US3] Add update customer profile handler tests covering required trimmed full name, positive age, optional phone number, and missing customer in `tests/Talabat.Application.Tests/Customers/UpdateProfile/UpdateCustomerProfileHandlerTests.cs`
- [X] T049 [P] [US3] Add add customer address handler tests covering required fields, duplicate normalized address, and default-address behavior in `tests/Talabat.Application.Tests/Customers/AddAddress/AddCustomerAddressHandlerTests.cs`
- [X] T050 [P] [US3] Add remove customer address handler tests covering missing address and no automatic new default after removing default address in `tests/Talabat.Application.Tests/Customers/RemoveAddress/RemoveCustomerAddressHandlerTests.cs`
- [X] T051 [P] [US3] Add set default customer address handler tests in `tests/Talabat.Application.Tests/Customers/SetDefaultAddress/SetDefaultCustomerAddressHandlerTests.cs`
- [X] T052 [P] [US3] Create customer address details read model in `src/Talabat/Talabat.Application/Customers/Models/CustomerAddressDetails.cs`
- [X] T053 [P] [US3] Create customer profile read model in `src/Talabat/Talabat.Application/Customers/Models/CustomerProfile.cs`
- [X] T054 [US3] Create customer mapping helper in `src/Talabat/Talabat.Application/Customers/Mapping/CustomerMapper.cs`
- [X] T055 [P] [US3] Create get customer profile query in `src/Talabat/Talabat.Application/Customers/GetProfile/GetCustomerProfileQuery.cs`
- [X] T056 [US3] Implement get customer profile handler in `src/Talabat/Talabat.Application/Customers/GetProfile/GetCustomerProfileHandler.cs`
- [X] T057 [P] [US3] Create update customer profile command in `src/Talabat/Talabat.Application/Customers/UpdateProfile/UpdateCustomerProfileCommand.cs`
- [X] T058 [US3] Implement update customer profile handler in `src/Talabat/Talabat.Application/Customers/UpdateProfile/UpdateCustomerProfileHandler.cs`
- [X] T059 [P] [US3] Create add customer address command in `src/Talabat/Talabat.Application/Customers/AddAddress/AddCustomerAddressCommand.cs`
- [X] T060 [US3] Implement add customer address handler in `src/Talabat/Talabat.Application/Customers/AddAddress/AddCustomerAddressHandler.cs`
- [X] T061 [P] [US3] Create remove customer address command in `src/Talabat/Talabat.Application/Customers/RemoveAddress/RemoveCustomerAddressCommand.cs`
- [X] T062 [US3] Implement remove customer address handler in `src/Talabat/Talabat.Application/Customers/RemoveAddress/RemoveCustomerAddressHandler.cs`
- [X] T063 [P] [US3] Create set default customer address command in `src/Talabat/Talabat.Application/Customers/SetDefaultAddress/SetDefaultCustomerAddressCommand.cs`
- [X] T064 [US3] Implement set default customer address handler in `src/Talabat/Talabat.Application/Customers/SetDefaultAddress/SetDefaultCustomerAddressHandler.cs`

## Phase 6: User Story 4 - Checkout

**Goal**: Customers can checkout a valid active cart with a saved delivery address, producing exactly one order and closing exactly one cart, while unavailable products return a structured non-committing outcome.
**Independent Test**: Checkout handler can be executed with fake cart, customer, restaurant, and order repositories plus fake clock, ID generator, restaurant local-time provider, and unit of work; no API or infrastructure dependency is required.

- [X] T065 [P] [US4] Add successful checkout handler tests covering one order creation, one cart checkout, current Catalog pricing, and one unit-of-work commit in `tests/Talabat.Application.Tests/Ordering/Checkout/CheckoutHandlerSuccessTests.cs`
- [X] T066 [P] [US4] Add unavailable products checkout tests in `tests/Talabat.Application.Tests/Ordering/Checkout/CheckoutHandlerUnavailableProductsTests.cs`
- [X] T067 [P] [US4] Add checkout invalid state tests covering missing customer, missing address, empty cart, expired cart, non-active cart, inactive/closed restaurant, and duplicate checkout submission in `tests/Talabat.Application.Tests/Ordering/Checkout/CheckoutHandlerFailureTests.cs`
- [X] T068 [P] [US4] Create checkout command in `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutCommand.cs`
- [X] T069 [P] [US4] Create checkout error definitions in `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutErrors.cs`
- [X] T070 [P] [US4] Define checkout outcome contracts so `CheckoutSucceededOutcome` returns created order id and final total amount, `CheckoutProductsUnavailableOutcome` returns productId/productName/reason items, and no price-change outcome exists in `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutOutcome.cs`
- [X] T071 [P] [US4] Create checkout domain-result mapper in `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutResultMapper.cs`
- [X] T072 [US4] Implement checkout handler orchestration in `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutHandler.cs`

## Phase 7: User Story 5 - Order History And Details

**Goal**: Customers can retrieve their own order history and order details without exposing orders owned by another customer.
**Independent Test**: Order read handlers can be executed with only a fake order repository; no cart, customer, restaurant, API, or infrastructure dependency is required.

- [X] T073 [P] [US5] Add order history handler tests covering customer scoping, newest-first ordering, and empty history in `tests/Talabat.Application.Tests/Ordering/GetOrderHistory/GetOrderHistoryHandlerTests.cs`
- [X] T074 [P] [US5] Add order details handler tests covering customer scoping, missing order, unauthorized order returning not found, and historical snapshot fields in `tests/Talabat.Application.Tests/Ordering/GetOrderDetails/GetOrderDetailsHandlerTests.cs`
- [X] T075 [P] [US5] Create order summary read model in `src/Talabat/Talabat.Application/Ordering/Models/OrderSummary.cs`
- [X] T076 [P] [US5] Create order line item read model in `src/Talabat/Talabat.Application/Ordering/Models/OrderLineItem.cs`
- [X] T077 [P] [US5] Create order delivery address read model in `src/Talabat/Talabat.Application/Ordering/Models/OrderDeliveryAddress.cs`
- [X] T078 [P] [US5] Create order details read model in `src/Talabat/Talabat.Application/Ordering/Models/OrderDetails.cs`
- [X] T079 [US5] Create order mapping helper in `src/Talabat/Talabat.Application/Ordering/Mapping/OrderMapper.cs`
- [X] T080 [P] [US5] Create get order history query in `src/Talabat/Talabat.Application/Ordering/GetOrderHistory/GetOrderHistoryQuery.cs`
- [X] T081 [US5] Implement get order history handler in `src/Talabat/Talabat.Application/Ordering/GetOrderHistory/GetOrderHistoryHandler.cs`
- [X] T082 [P] [US5] Create get order details query in `src/Talabat/Talabat.Application/Ordering/GetOrderDetails/GetOrderDetailsQuery.cs`
- [X] T083 [US5] Implement get order details handler in `src/Talabat/Talabat.Application/Ordering/GetOrderDetails/GetOrderDetailsHandler.cs`

## Final Phase: Polish & Cross-Cutting Concerns

- [X] T084 Review `src/Talabat/Talabat.Application/Talabat.Application.csproj` to confirm no MediatR, EF Core, Identity/Auth, ASP.NET Core, or web packages were added
- [X] T085 Run Application test project and resolve failures in `tests/Talabat.Application.Tests/Talabat.Application.Tests.csproj`
- [X] T086 Run solution build and resolve failures in `src/Talabat/Talabat.slnx`
- [X] T087 Verify implemented Application contracts, result codes, current Catalog price rules, and Phase 3 scope guardrails against `specs/001-application-use-cases/contracts/application-use-cases.md`
- [X] T088 Create Phase 3 implementation summary in `docs/phase-3-application-use-cases.md`

## Dependencies

### Phase Dependencies

```text
Phase 1 Setup
  -> Phase 2 Foundational
  -> US1 Catalog Browsing And Menu
  -> US2 Basket Management
  -> US3 Customer Profile And Addresses
  -> US4 Checkout
  -> US5 Order History And Details
  -> Final Phase
```

### User Story Dependencies

- **US1 Catalog Browsing And Menu**: Depends on Phase 2 only. This is the smallest MVP slice.
- **US2 Basket Management**: Depends on Phase 2. It can be implemented after or alongside US1 once foundational result contracts and fakes exist.
- **US3 Customer Profile And Addresses**: Depends on Phase 2. It can be implemented after or alongside US1/US2.
- **US4 Checkout**: Depends on US1, US2, and US3 concepts because checkout coordinates restaurant/product availability, cart state, customer addresses, and order creation.
- **US5 Order History And Details**: Depends on Phase 2 and can be implemented before or after US4, but it becomes more meaningful once checkout can create orders.

## Parallel Execution Examples

### Foundational

```text
T012 FakeClock
T013 Fake application ID generator
T014 FakeRestaurantLocalTimeProvider
T015 FakeUnitOfWork
T016 FakeRestaurantRepository
T017 FakeCartRepository
T018 FakeCustomerRepository
T019 FakeOrderRepository
```

### US1 Catalog

```text
T020 Catalog browse tests
T021 Menu tests
T022 RestaurantSummary
T023 MenuProduct
T024 RestaurantMenu
T025 BrowseRestaurantsQuery
T027 GetRestaurantMenuQuery
```

### US2 Basket

```text
T029 GetCart tests
T030 AddItem tests
T031 UpdateQuantity tests
T032 RemoveItem tests
T033 ClearCart tests
T034 CartLineItem
T035 CartDetails
T037 GetCartQuery
T039 AddCartItemCommand
T041 UpdateCartItemQuantityCommand
T043 RemoveCartItemCommand
T045 ClearCartCommand
```

### US3 Customers

```text
T047 GetProfile tests
T048 UpdateProfile tests
T049 AddAddress tests
T050 RemoveAddress tests
T051 SetDefaultAddress tests
T052 CustomerAddressDetails
T053 CustomerProfile
T055 GetCustomerProfileQuery
T057 UpdateCustomerProfileCommand
T059 AddCustomerAddressCommand
T061 RemoveCustomerAddressCommand
T063 SetDefaultCustomerAddressCommand
```

### US4 Checkout

```text
T065 Checkout success tests
T066 Checkout unavailable-products tests
T067 Checkout failure tests
T068 CheckoutCommand
T069 CheckoutErrors
T070 CheckoutOutcome
T071 CheckoutResultMapper
```

### US5 Orders

```text
T073 Order history tests
T074 Order details tests
T075 OrderSummary
T076 OrderLineItem
T077 OrderDeliveryAddress
T078 OrderDetails
T080 GetOrderHistoryQuery
T082 GetOrderDetailsQuery
```

## Implementation Strategy

### MVP First

Deliver US1 first: Catalog browsing and menu reads. This verifies the Application handler pattern, result type, read models, fakes, and no-infrastructure testing approach with the least coupling.

### Incremental Delivery

1. Complete Setup and Foundational tasks.
2. Complete US1 and verify Catalog tests pass independently.
3. Complete US2 and verify Basket tests pass independently.
4. Complete US3 and verify Customer tests pass independently.
5. Complete US4 and verify checkout success, unavailable-products, and invalid-state tests pass independently.
6. Complete US5 and verify order read tests pass independently.
7. Run final build and full test suite.

### Scope Guardrails

- Do not implement EF Core repositories, DbContext, migrations, seed data, or persistence mappings.
- Do not implement API controllers, endpoints, middleware, HTTP status mapping, or OpenAPI contracts.
- Do not implement Identity/Auth, current-user resolution, roles, claims, tokens, login, or registration.
- Do not implement Delivery task creation, assignment, lifecycle, delivery-agent workflows, or delivery status use cases.
- Do not move Domain invariants into Application handlers.
