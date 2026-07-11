# Tasks: Persistence And Infrastructure

**Input**: Design documents from `specs/002-persistence-infrastructure/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/persistence-boundary.md`, `quickstart.md`

Tests are required by `FR-012`. UnitOfWork, DbContext registration, SQL Server test infrastructure, and EF materialization guardrails are foundational because every user-story integration test calls `SaveChangesAsync`.

## Phase 1: Setup

- [X] T001 Review Phase 4 scope guard in `.specify/memory/constitution.md` and `PROJECT_IMPLEMENTATION_ROADMAP.md`
- [X] T002 Add EF Core package references to `src/Talabat/Talabat.Infrastructure/Talabat.Infrastructure.csproj`
- [X] T003 Create Infrastructure integration test project in `tests/Talabat.Infrastructure.Tests/Talabat.Infrastructure.Tests.csproj`
- [X] T004 Add Infrastructure integration test project to `src/Talabat/Talabat.slnx`
- [X] T005 Apply the approved NU1903 OpenAPI vulnerability fix in `src/Talabat/Talabat.API/Talabat.API.csproj`
- [X] T006 Verify Domain and Application remain package-free in `src/Talabat/Talabat.Domain/Talabat.Domain.csproj` and `src/Talabat/Talabat.Application/Talabat.Application.csproj`

## Phase 2: Foundational

- [X] T007 Create `TalabatDbContext` with aggregate DbSets, configuration loading, and global soft-delete filter application in `src/Talabat/Talabat.Infrastructure/Persistence/TalabatDbContext.cs`
- [X] T008 Create `AddInfrastructure` shell registering DbContext and SQL Server options only in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T009 Implement `IUnitOfWork` using the DbContext save boundary in `src/Talabat/Talabat.Infrastructure/Persistence/UnitOfWork.cs`
- [X] T010 Register `IUnitOfWork` in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T011 Add API project reference to Infrastructure in `src/Talabat/Talabat.API/Talabat.API.csproj`
- [X] T012 Call `AddInfrastructure` from the API composition root in `src/Talabat/Talabat.API/Program.cs`
- [X] T013 Add nonsecret `ConnectionStrings:TalabatDb` development placeholder in `src/Talabat/Talabat.API/appsettings.Development.json`
- [X] T014 Create SQL Server Testcontainers and LocalDB fallback fixture in `tests/Talabat.Infrastructure.Tests/Persistence/SqlServerDatabaseFixture.cs`
- [X] T015 Create test service-provider helper for Infrastructure tests in `tests/Talabat.Infrastructure.Tests/Persistence/InfrastructureTestServices.cs`
- [X] T016 Create shared EF mapping helpers in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/MappingConventions.cs`
- [X] T017 Apply EF materialization mechanics in Domain only where an integration test fails without them under `src/Talabat/Talabat.Domain/Aggregates/`
- [X] T018 Implement and register audit SaveChanges interceptor and its integration test in `src/Talabat/Talabat.Infrastructure/Persistence/Auditing/AuditableEntitySaveChangesInterceptor.cs` and `tests/Talabat.Infrastructure.Tests/Persistence/AuditAndSoftDeleteTests.cs`

T017 is limited to private parameterless constructors and backing-field mapping only. It must not add public setters, public mutable collections, or new Domain members for the ORM, and every Domain file touched under T017 must be listed in `docs/phase-4-persistence-and-infrastructure.md`.

## Phase 3: User Story 1 - Catalog Persistence

**Goal**: Restaurants and products persist with SQL Server IDENTITY keys, owned opening hours and money values, product-name uniqueness, repository reads, and deterministic catalog seed data.

**Independent Test**: Restaurant integration tests can save and read restaurants/products, verify generated IDs, round-trip `TimeRange` and `Money`, reject duplicate product names per restaurant, and prove the browseable seeded catalog persists and round-trips.

- [X] T019 [P] [US1] Create restaurant and product EF configuration with constraints in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/RestaurantConfiguration.cs`
- [X] T020 [US1] Add deterministic catalog seed data with explicit IDs in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/CatalogSeedData.cs`
- [X] T021 [US1] Implement `IRestaurantRepository` in `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/RestaurantRepository.cs`
- [X] T022 [US1] Register `IRestaurantRepository` in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T023 [US1] Add restaurant/product persistence and seed tests in `tests/Talabat.Infrastructure.Tests/Persistence/RestaurantPersistenceTests.cs`

## Phase 4: User Story 2 - Customer Persistence

**Goal**: Customers and saved addresses persist with SQL Server IDENTITY keys, owned address columns, one-default-address database enforcement, and Domain-only duplicate address value detection.

**Independent Test**: Customer integration tests can save and read customers with addresses, verify generated customer/address IDs, round-trip `Address`, reject two defaults for one customer, and confirm no database duplicate-address-value constraint is introduced.

- [X] T024 [P] [US2] Create customer and customer-address EF configuration in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`
- [X] T025 [US2] Implement `ICustomerRepository` in `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/CustomerRepository.cs`
- [X] T026 [US2] Register `ICustomerRepository` in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T027 [US2] Add customer/address persistence tests in `tests/Talabat.Infrastructure.Tests/Persistence/CustomerPersistenceTests.cs`

## Phase 5: User Story 3 - Basket Persistence

**Goal**: Active carts and cart items persist with generated cart IDs, composite `(CartId, ProductId)` child keys, quantity checks, and one-active-cart-per-customer enforcement.

**Independent Test**: Cart integration tests can save and read active carts with items, verify generated cart IDs, verify composite cart-item keys, reject invalid quantities, and reject two active carts for one customer.

- [X] T028 [P] [US3] Create cart and cart-item EF configuration in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/CartConfiguration.cs`
- [X] T029 [US3] Implement `ICartRepository` in `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/CartRepository.cs`
- [X] T030 [US3] Register `ICartRepository` in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T031 [US3] Add cart/cart-item persistence tests in `tests/Talabat.Infrastructure.Tests/Persistence/CartPersistenceTests.cs`

## Phase 6: User Story 4 - Ordering And Checkout Atomicity

**Goal**: Checkout saves order snapshots and closes carts atomically, using generated order IDs, composite order-item keys, owned delivery address snapshots, and customer-scoped repository reads.

**Independent Test**: Order integration tests can save and read order history/details, round-trip `Money` and `DeliveryAddressSnapshot`, preserve historical product snapshots, and prove one order plus one checked-out cart are committed in one UnitOfWork save.

- [X] T032 [P] [US4] Create order and order-item EF configuration in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`
- [X] T033 [US4] Implement `IOrderRepository` in `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/OrderRepository.cs`
- [X] T034 [US4] Register `IOrderRepository` in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T035 [US4] Add order/order-item persistence tests in `tests/Talabat.Infrastructure.Tests/Persistence/OrderPersistenceTests.cs`
- [X] T036 [US4] Add checkout atomic persistence test using UnitOfWork in `tests/Talabat.Infrastructure.Tests/Persistence/CheckoutPersistenceTests.cs`

## Phase 7: User Story 5 - DeliveryAgent Persistence

**Goal**: Delivery agents persist with generated IDs, enum checks, optional `GeoLocation`, coordinate constraints, and available-agent repository reads.

**Independent Test**: Delivery-agent integration tests can save and read agents, query available agents, round-trip nullable `GeoLocation`, and reject invalid coordinate persistence.

- [X] T037 [P] [US5] Create delivery-agent EF configuration in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryAgentConfiguration.cs`
- [X] T038 [US5] Implement `IDeliveryAgentRepository` in `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/DeliveryAgentRepository.cs`
- [X] T039 [US5] Register `IDeliveryAgentRepository` in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T040 [US5] Add delivery-agent persistence tests in `tests/Talabat.Infrastructure.Tests/Persistence/DeliveryAgentPersistenceTests.cs`

## Phase 8: User Story 6 - Delivery Persistence

**Goal**: Deliveries persist with generated IDs, unique order relationship, owned delivery address snapshot, assigned-agent references, active-assignment uniqueness, and no Delivery Application use cases.

**Independent Test**: Delivery integration tests can save and read deliveries through repository methods, reject duplicate deliveries for one order, and reject two active deliveries assigned to one agent.

- [X] T041 [P] [US6] Create delivery EF configuration in `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/DeliveryConfiguration.cs`
- [X] T042 [US6] Implement `IDeliveryRepository` in `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/DeliveryRepository.cs`
- [X] T043 [US6] Register `IDeliveryRepository` in `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
- [X] T044 [US6] Add delivery persistence tests in `tests/Talabat.Infrastructure.Tests/Persistence/DeliveryPersistenceTests.cs`

## Final Phase: Polish And Cross-Cutting Concerns

- [X] T045 Add soft-delete repository filter tests in `tests/Talabat.Infrastructure.Tests/Persistence/AuditAndSoftDeleteTests.cs`
- [X] T046 Add cross-aggregate constraint coverage tests in `tests/Talabat.Infrastructure.Tests/Persistence/ConstraintPersistenceTests.cs`
- [X] T047 Verify deterministic catalog seed data applies cleanly via migrations in `tests/Talabat.Infrastructure.Tests/Persistence/SeedDataMigrationTests.cs`
- [X] T048 Generate the reviewed initial migration and idempotent SQL script in `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/`
- [X] T049 Review generated migration `.cs` files and idempotent SQL script in `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/`
- [X] T050 Review the migration model snapshot in `src/Talabat/Talabat.Infrastructure/Persistence/Migrations/TalabatDbContextModelSnapshot.cs`
- [X] T051 Run solution build validation for `src/Talabat/Talabat.slnx`
- [X] T052 Run full test validation for `src/Talabat/Talabat.slnx`
- [X] T053 Run vulnerable package validation for `src/Talabat/Talabat.slnx`
- [X] T054 Verify Domain and Application project files still have no package references in `src/Talabat/Talabat.Domain/Talabat.Domain.csproj` and `src/Talabat/Talabat.Application/Talabat.Application.csproj`
- [X] T055 Verify no API endpoints, Identity/Auth, Delivery Application use cases, MediatR, child repositories, or repository interface changes were added under `src/Talabat/`
- [X] T056 Create Phase 4 completion report in `docs/phase-4-persistence-and-infrastructure.md`
- [X] T057 Update Phase 4 status only after completion in `PROJECT_IMPLEMENTATION_ROADMAP.md`

T049 must check IDENTITY on all integer keys, owned value objects mapped as columns with no separate snapshot tables, composite child keys `(CartId, ProductId)` and `(OrderId, ProductId)`, filtered unique indexes for active cart per customer, default address per customer, unique `Delivery(OrderId)`, unique `(RestaurantId, Name)`, and check constraints for `Quantity > 0` and `Money >= 0`.

## Requirement Coverage Mapping

| Requirement Key | Task IDs | Notes |
|---|---|---|
| FR-001 | T019-T044 | All aggregate roots have mapping, repository, registration, and tests. |
| FR-002 | T021, T025, T029, T033, T038, T042, T055 | Repositories remain aggregate-root only; final guard checks no child repositories. |
| FR-003 | T023, T027, T031, T035, T040, T044, T049, T050 | Generated IDs and IDENTITY migration shape are covered. |
| FR-004 | T028, T032, T049, T050 | Cart/order child composite keys are configured and reviewed. |
| FR-005 | T019, T024, T032, T037, T041, T023, T027, T035, T040, T044 | Owned value objects are mapped and round-tripped. |
| FR-006 | T019, T024, T028, T037, T041, T046, T049, T050 | Required database constraints and indexes are configured, tested, and reviewed. |
| FR-006a | T024, T027, T049 | Duplicate address value stays Domain-only and is not added to migration shape. |
| FR-007 | T021, T025, T029, T033, T038, T042 | Existing aggregate-root repository contracts are implemented. |
| FR-008 | T009, T010, T036 | UnitOfWork is foundational and checkout atomicity is tested. |
| FR-009 | T018 | Audit timestamp interceptor is implemented, registered, and tested. |
| FR-010 | T007, T045 | Soft-delete filters are configured and tested. |
| FR-011 | T020, T047, T049 | Catalog seed data has one source of truth and is verified through migrations. |
| FR-012 | T014, T015, T018, T023, T027, T031, T035, T036, T040, T044, T045, T046, T052 | SQL Server-backed integration coverage is explicit. |
| FR-013 | T048, T049, T050 | Migration generation, migration SQL review, and snapshot review are fully covered. |
| FR-014 | T006, T054 | Domain/Application package boundaries are checked. |
| FR-015 | T001, T055 | Scope guard is checked before and after implementation. |
| FR-016 | T005, T053 | OpenAPI vulnerability fix and audit are covered. |
| SC-001 | T021, T025, T029, T033, T038, T042, T023, T027, T031, T035, T040, T044 | Repository contracts are implemented and tested. |
| SC-002 | T023, T027, T031, T035, T040, T044 | Generated IDs are verified after save. |
| SC-003 | T009, T010, T036 | Checkout atomicity uses the foundational UnitOfWork. |
| SC-004 | T023, T027, T031, T044, T046 | Uniqueness rejection tests are covered. |
| SC-005 | T023, T027, T035, T040, T044 | Owned value-object round trips are covered. |
| SC-006 | T051, T052, T053 | Build, tests, and vulnerability audit are covered. |
| SC-007 | T006, T054 | Domain/Application package-free verification is covered. |

## Dependencies

```text
Phase 1 Setup
  -> Phase 2 Foundational
  -> US1 Catalog Persistence
  -> US3 Basket Persistence
  -> US4 Ordering And Checkout Atomicity
  -> US6 Delivery Persistence
  -> Final Phase

Phase 2 Foundational
  -> US2 Customer Persistence
  -> US5 DeliveryAgent Persistence

US1 Catalog Persistence + US2 Customer Persistence + US3 Basket Persistence
  -> US4 Ordering And Checkout Atomicity

US4 Ordering And Checkout Atomicity + US5 DeliveryAgent Persistence
  -> US6 Delivery Persistence

All user stories
  -> Final Phase migration, validation, documentation, and scope checks
```

## Parallel Execution Examples

After T018 is complete, these aggregate configuration tasks can be drafted in parallel where their dependencies are satisfied:

```text
T019 RestaurantConfiguration
T024 CustomerConfiguration
T028 CartConfiguration
T037 DeliveryAgentConfiguration
```

After US1, US2, and US3 are complete, order configuration and tests can proceed:

```text
T032 OrderConfiguration
T035 OrderPersistenceTests
T036 CheckoutPersistenceTests
```

After US4 and US5 are complete, delivery work can proceed:

```text
T041 DeliveryConfiguration
T042 DeliveryRepository
T044 DeliveryPersistenceTests
```

Do not parallelize tasks that edit `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs` unless merge conflicts are coordinated.

## Implementation Strategy

1. Complete setup and foundational tasks first; no user story is independently executable before DbContext, UnitOfWork, DI shell, SQL Server test fixture, and EF materialization guardrails exist.
2. MVP for first review is US1 Catalog Persistence because seed catalog and product constraints unlock later customer ordering tests.
3. First customer-ordering acceptance requires US1, US2, US3, and US4 together.
4. Delivery persistence can be completed after order persistence and delivery-agent persistence without adding Delivery Application use cases.
5. Generate the migration only after mapping, repository, constraint, seed, audit, and soft-delete tests are reviewed.
6. Finish with build, full tests, vulnerability audit, package-boundary checks, and scope-guard checks.
