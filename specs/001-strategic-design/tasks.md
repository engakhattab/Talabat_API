# Implementation Tasks: Strategic Design & Tactical Foundations

## Dependency Graph
1. **Phase 1 (Setup)** → 2. **Phase 2 (Foundational)**
2. **Phase 2** → **US1 (Catalog)** & **US2 (Identity)**
3. **US1** & **US2** → **US3 (Basket)**
4. **US3** → **US4 (Ordering)**
5. **US4** → **US5 (Checkout Service)**
6. All Stories → Final Polish Phase

## Phase 1: Setup
- [X] T001 Create blank solution `Talabat.sln` in project root
- [X] T002 Create class library `Talabat.Domain` in `src/Talabat.Domain` and add to solution
- [X] T003 Delete default `Class1.cs` from `src/Talabat.Domain`
- [X] T004 Create `src/Talabat.Domain/Common/` and `src/Talabat.Domain/Exceptions/` directories
- [X] T005 [P] Create `src/Talabat.Domain/ValueObjects/`, `src/Talabat.Domain/Entities/`, and `src/Talabat.Domain/Interfaces/` directories
- [X] T005a Create `docs/glossary.md` defining ubiquitous language (Customer, Restaurant, Product, Cart, Cart Item, Order, Order Item, Checkout, Aggregate, Aggregate Root, Entity, Value Object, Price Snapshot, Current Price, Immutable Order Price, Availability, Restaurant Opening Hours)
- [X] T005b Create `docs/domain-invariants.md` documenting all business invariants enforced by the Domain layer (Cart, Order, Restaurant rules)

## Phase 2: Foundational
- [ ] T006 Implement abstract base `DomainException` in `src/Talabat.Domain/Exceptions/DomainException.cs`
- [ ] T007 Implement generic `EntityNotFoundException` inheriting from `DomainException` in `src/Talabat.Domain/Exceptions/EntityNotFoundException.cs`
- [ ] T008 [P] Implement `Money` value object record with validation (≥ 0) in `src/Talabat.Domain/ValueObjects/Money.cs`
- [ ] T009 [P] Implement `TimeRange` value object record with `Contains(TimeOnly)` method in `src/Talabat.Domain/ValueObjects/TimeRange.cs`
- [ ] T010 [P] Implement `IUnitOfWork` interface with `SaveChangesAsync` in `src/Talabat.Domain/Interfaces/IUnitOfWork.cs`

## Phase 3: [US1] Catalog Domain Model
- **Goal**: Implement the Catalog bounded context entities and repository interface.
- **Independent Test Criteria**: Restaurant and Product models compile, and Restaurant enforces active status and opening hours.
- [ ] T011 [US1] Implement `RestaurantInactiveException` and `RestaurantClosedException` in `src/Talabat.Domain/Exceptions/`
- [ ] T012 [P] [US1] Implement `Product` entity with private setters, `UpdatePrice`, and `MarkUnavailable` methods in `src/Talabat.Domain/Entities/Catalog/Product.cs`
- [ ] T013 [US1] Implement `Restaurant` aggregate root with `TimeRange` opening hours, `IsCurrentlyOpen`, and product management methods in `src/Talabat.Domain/Entities/Catalog/Restaurant.cs`
- [ ] T014 [P] [US1] Implement `IRestaurantRepository` interface in `src/Talabat.Domain/Interfaces/IRestaurantRepository.cs`

## Phase 4: [US2] Identity Domain Model
- **Goal**: Implement the Identity bounded context for customer profiles.
- **Independent Test Criteria**: Customer can manage multiple addresses and specify one default address.
- [ ] T015 [P] [US2] Implement `CustomerAddress` entity with private setters in `src/Talabat.Domain/Entities/Identity/CustomerAddress.cs`
- [ ] T016 [US2] Implement `Customer` aggregate root with `AddAddress` and `SetDefaultAddress` invariants in `src/Talabat.Domain/Entities/Identity/Customer.cs`
- [ ] T017 [P] [US2] Implement `ICustomerRepository` interface in `src/Talabat.Domain/Interfaces/ICustomerRepository.cs`

## Phase 5: [US3] Basket Domain Model
- **Goal**: Implement the Basket bounded context including cart expiration and cross-restaurant validation invariants.
- **Independent Test Criteria**: Cart enforces exactly one restaurant per cart, positive quantities, and rejects modification if 1 hour has passed since creation.
- [ ] T018 [US3] Implement `CartExpiredException`, `CrossRestaurantCartException`, `InvalidQuantityException`, and `ProductUnavailableException` in `src/Talabat.Domain/Exceptions/`
- [ ] T019 [P] [US3] Implement `CartItem` entity tracking `ProductId`, `Quantity`, and `UnitPriceSnapshot` (`Money`) in `src/Talabat.Domain/Entities/Basket/CartItem.cs`
- [ ] T020 [US3] Implement `Cart` aggregate root with `AddItem()` in `src/Talabat.Domain/Entities/Basket/Cart.cs`. The `AddItem()` method must explicitly: reject products from another restaurant, merge duplicate products by increasing quantity, validate Quantity > 0, store a UnitPriceSnapshot, and preserve all cart invariants (including 1-hour expiration).
- [ ] T021 [P] [US3] Implement `ICartRepository` interface in `src/Talabat.Domain/Interfaces/ICartRepository.cs`

## Phase 6: [US4] Ordering Domain Model
- **Goal**: Implement the Ordering bounded context for immutable historical orders.
- **Independent Test Criteria**: Order total is correctly calculated from OrderItems, and the entire structure is completely immutable after construction.
- [ ] T022 [P] [US4] Implement `OrderItem` entity calculating `LineTotal` from `UnitPrice` × `Quantity` in `src/Talabat.Domain/Entities/Ordering/OrderItem.cs`
- [ ] T023 [US4] Implement `Order` aggregate root with status tracking and `TotalAmount` aggregation in `src/Talabat.Domain/Entities/Ordering/Order.cs`
- [ ] T024 [P] [US4] Implement `IOrderRepository` interface in `src/Talabat.Domain/Interfaces/IOrderRepository.cs`

## Phase 7: [US5] Checkout Domain Service
- **Goal**: Implement the complex cross-aggregate invariant that validates current prices against cart snapshots.
- **Independent Test Criteria**: Service throws `PriceChangedException` containing detailed item differences if catalog prices differ from cart snapshots.
- [ ] T025 [US5] Implement `PriceChangedException` and `EmptyCartCheckoutException` in `src/Talabat.Domain/Exceptions/`
- [ ] T026 [US5] Implement `CheckoutService` orchestrating `Cart`, `Restaurant`, and `Product` validations in `src/Talabat.Domain/Services/CheckoutService.cs`

## Final Phase: Polish & Cross-Cutting Concerns
- [ ] T027 Verify `Talabat.Domain.csproj` contains absolutely zero NuGet package references.
- [ ] T028 Review all entities to ensure properties use `public get; private set;` (or `init;`) to prevent anemic data models.
