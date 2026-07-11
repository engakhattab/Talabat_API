# Phase 0 Research: Persistence And Infrastructure

## Decision: EF Core SQL Server Package Set

**Decision**: Pin the Phase 4 EF Core package set to the current stable .NET 10 patch line:

- `Microsoft.EntityFrameworkCore.SqlServer` `10.0.9` in `Talabat.Infrastructure`
- `Microsoft.EntityFrameworkCore.Design` `10.0.9` in `Talabat.Infrastructure` with private assets
- `Microsoft.EntityFrameworkCore.Tools` `10.0.9` in `Talabat.Infrastructure` with private assets
- `Testcontainers.MsSql` `4.13.0` in the Infrastructure integration test project when Docker-based tests are enabled

The SQL Server provider package targets `.NET 10.0` and is the provider package for Microsoft SQL Server and Azure SQL. The design and tools packages are pinned to the same patch level to avoid design-time/runtime drift.

**OpenAPI vulnerability update**: The existing API project references `Microsoft.AspNetCore.OpenApi` `10.0.9`; NuGet audit currently flags transitive `Microsoft.OpenApi` `2.0.0` under GHSA-v5pm-xwqc-g5wc. If no newer stable `Microsoft.AspNetCore.OpenApi` patch is available during implementation, add an explicit API project reference to patched `Microsoft.OpenApi` `2.7.5` or later in the 2.x line, then run `dotnet list package --vulnerable` to prove the warning is gone. Do not move API behavior forward while doing this.

**Sources verified**:

- NuGet: `Microsoft.EntityFrameworkCore.SqlServer` `10.0.9`
- NuGet: `Microsoft.EntityFrameworkCore.Design` `10.0.9`
- NuGet: `Microsoft.EntityFrameworkCore.Tools` `10.0.9`
- NuGet/Testcontainers: `Testcontainers.MsSql` `4.13.0`
- GitHub Advisory GHSA-v5pm-xwqc-g5wc: patched `Microsoft.OpenApi` versions are `2.7.5+` for 2.x and `3.5.4+` for 3.x

**Alternatives considered**:

- Use EF Core 11 preview packages: rejected because Phase 4 should stay on stable packages.
- Use only runtime EF Core packages and omit design packages: rejected because migrations are an explicit Phase 4 deliverable.
- Use unpinned floating versions: rejected because migrations and generated model snapshots must be reproducible.

## Decision: Migrations Workflow

**Decision**: Generate migrations from the Infrastructure project while using the API project as the startup project:

```powershell
dotnet ef migrations add InitialPersistence `
  --project src\Talabat\Talabat.Infrastructure `
  --startup-project src\Talabat\Talabat.API `
  --output-dir Persistence\Migrations
```

Apply or script migrations with the same `--project` and `--startup-project` pairing. The API project is the composition root and supplies configuration, while migrations live with the DbContext and configurations in Infrastructure.

**Constraints**:

- Create the migration only after mapping and constraint configuration has been reviewed.
- Keep a single reviewed migration for Phase 4 unless review finds a defect before merge.
- Do not expose DbContext to API controllers.

## Decision: Owned-Type Mappings

**Decision**: Persist value objects as owned data inside their owning tables:

- `Money`: one decimal column per property, precision `decimal(18,2)`, non-negative check constraint.
- `Address`: owned columns on `CustomerAddresses`: `Street`, `City`, `BuildingNumber`, `Floor`.
- `DeliveryAddressSnapshot`: owned columns on `Orders` and `Deliveries` using prefixed delivery-address column names.
- `TimeRange`: owned columns on `Restaurants` as two `TimeOnly` SQL `time` columns: `OpeningStart` and `OpeningEnd`.
- `GeoLocation`: owned columns on `DeliveryAgents`: `CurrentLatitude` and `CurrentLongitude`, `decimal(9,6)`, with paired-null and coordinate-range check constraints.

**Rationale**: These value objects have no independent lifecycle or repository contract. Owned columns preserve aggregate boundaries and keep snapshots as historical data rather than separate mutable tables.

**Alternatives considered**:

- Separate value-object tables: rejected because snapshots and value objects are owned by aggregate roots.
- Serialize value objects as JSON: rejected because required constraints and query filters are relational and should stay enforceable.

## Decision: Key Strategy

**Decision**: Use SQL Server IDENTITY-compatible integer keys everywhere an entity has an `Id`, and use composite keys for child rows that do not have Domain IDs.

- Aggregate roots: `Restaurant`, `Cart`, `Customer`, `Order`, `Delivery`, and `DeliveryAgent` use identity keys.
- Child entities with IDs: `Product` and `CustomerAddress` use identity keys.
- Child entities without IDs: `CartItem` uses `(CartId, ProductId)` and `OrderItem` uses `(OrderId, ProductId)`.
- No sequences.
- No `ValueGeneratedNever` for generated IDs.
- No application-side ID generation.

**Rationale**: Phase 3.5 already refactored constructors and handlers for database-generated IDs. Phase 4 must preserve that decision.

## Decision: Constraints And Indexes

**Decision**: Configure the following database constraints and indexes.

### Global audit columns

- All `AuditableEntity` tables include `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`, `IsDeleted`, `DeletedAt`, and `DeletedBy`.
- `CreatedAt`, `ModifiedAt`, and delete timestamps use `datetime2` and must store UTC values supplied by the application.

### Restaurants and Products

- `Restaurants.Id` primary key, identity.
- Required restaurant `Name`, `Description`, `OpeningStart`, `OpeningEnd`, and `IsActive`.
- `Products.Id` primary key, identity.
- `Products.RestaurantId` required foreign key to `Restaurants.Id`.
- `Products.CurrentPriceAmount` `decimal(18,2)` with check `>= 0`.
- `Products.Name`, `Description`, and `IsAvailable` required.
- Unique index `UX_Products_RestaurantId_Name` on `(RestaurantId, Name)`.
- Index on active restaurants for browse queries.

### Carts and CartItems

- `Carts.Id` primary key, identity.
- `Carts.CustomerId` and `Carts.RestaurantId` required.
- `Carts.Status` constrained to known values: `1` Active, `2` CheckedOut, `3` Cleared.
- Filtered unique index `UX_Carts_CustomerId_Active` on `CustomerId` where `Status = 1` and `IsDeleted = 0`.
- `CartItems` primary key `(CartId, ProductId)`.
- `CartItems.CartId` required foreign key to `Carts.Id`.
- `CartItems.ProductId` required foreign key to `Products.Id`.
- `CartItems.ProductName` required.
- `CartItems.Quantity` check `> 0`.

### Customers and CustomerAddresses

- `Customers.Id` primary key, identity.
- `Customers.FullName` and `Age` required; `Age` check `> 0`; `PhoneNumber` optional.
- `CustomerAddresses.Id` primary key, identity.
- `CustomerAddresses.CustomerId` required foreign key to `Customers.Id`.
- Address fields `Street`, `City`, `BuildingNumber` required; `Floor` optional.
- Filtered unique index `UX_CustomerAddresses_CustomerId_Default` on `CustomerId` where `IsDefault = 1` and `IsDeleted = 0`.
- Duplicate saved-address value remains a Domain rule in Phase 4; do not add normalized address columns or a database duplicate-address-value constraint solely for this rule.

### Orders and OrderItems

- `Orders.Id` primary key, identity.
- `Orders.CustomerId` required foreign key to `Customers.Id`.
- `Orders.RestaurantId` required foreign key to `Restaurants.Id`.
- `Orders.TotalAmount` `decimal(18,2)` with check `>= 0`.
- Delivery address snapshot columns required except floor.
- Index on `(CustomerId, CreatedAt)` for order history reads.
- `OrderItems` primary key `(OrderId, ProductId)`.
- `OrderItems.OrderId` required foreign key to `Orders.Id`.
- `OrderItems.ProductId` required foreign key to `Products.Id`.
- `OrderItems.ProductName` required.
- `OrderItems.UnitPriceAmount`, `LineTotalAmount` `decimal(18,2)` with checks `>= 0`.
- `OrderItems.Quantity` check `> 0`.

### Deliveries and DeliveryAgents

- `DeliveryAgents.Id` primary key, identity.
- `DeliveryAgents.FullName`, `VehicleType`, and `Status` required.
- `VehicleType` constrained to `1` Bike, `2` Motorcycle, `3` Car.
- `DeliveryAgentStatus` constrained to `1` Offline, `2` Available, `3` Busy, `4` Suspended.
- Optional `CurrentLatitude` and `CurrentLongitude` use `decimal(9,6)`; both must be null or both populated; populated coordinates must be in valid ranges.
- `Deliveries.Id` primary key, identity.
- `Deliveries.OrderId` required foreign key to `Orders.Id` and unique.
- `Deliveries.CustomerId` required foreign key to `Customers.Id`.
- `Deliveries.RestaurantId` required foreign key to `Restaurants.Id`.
- `Deliveries.AssignedAgentId` nullable foreign key to `DeliveryAgents.Id`.
- `DeliveryStatus` constrained to `1` through `8`.
- Delivery address snapshot columns required except floor.
- Unique index `UX_Deliveries_OrderId` on `OrderId`.
- Filtered unique index `UX_Deliveries_AssignedAgentId_Active` on `AssignedAgentId` where `AssignedAgentId IS NOT NULL` and `Status IN (2,3,4,5)` and `IsDeleted = 0`.
- Timestamp monotonicity remains a Domain rule; do not encode every transition ordering as database checks in Phase 4.

## Decision: Audit SaveChanges Interceptor And Soft-Delete Filters

**Decision**: Add an Infrastructure-level save interceptor that stamps audit timestamps for `AuditableEntity` instances:

- Added entities: set `CreatedAt` when default, preserve existing explicit UTC `CreatedAt`, set `CreatedBy` to null until Identity exists.
- Modified entities: set `ModifiedAt` to current UTC and `ModifiedBy` to null.
- Soft-deleted entities: Domain methods already set delete fields; interceptor may ensure modified audit is consistent.

Add a global query filter for `IsDeleted == false` on all auditable aggregate roots and auditable child entities that are mapped as tables. Repository methods use the filter by default.

**Rationale**: The Domain already exposes audit fields and soft-delete behavior. Infrastructure can consistently stamp timestamps without introducing identity concerns or leaking persistence into Domain.

## Decision: Seed Data

**Decision**: Use migration-managed seed data via `HasData` with explicit IDs for:

- A small set of active restaurants.
- Products for those restaurants.

Delivery agents may be seeded only for local development and integration-test scenarios if the seed is deterministic and documented. Do not seed customers, carts, orders, or deliveries as normal application data.

**Rationale**: Catalog has no management API in this phase, so customer-facing catalog flows need deterministic read data. Explicit IDs are acceptable for seed data and do not undermine runtime IDENTITY generation.

## Decision: Integration-Test Database

**Decision**: Prefer SQL Server in Testcontainers using `Testcontainers.MsSql` `4.13.0`. Use LocalDB only as a fallback when Docker is unavailable. SQLite is forbidden for Phase 4 persistence tests.

**Rationale**: Filtered unique indexes, SQL Server identity behavior, and provider-specific mappings are load-bearing acceptance criteria. SQLite would give false confidence.

**Fallback policy**:

- Testcontainers path: start an isolated SQL Server container per test collection or fixture, apply migrations, run tests, dispose container.
- LocalDB path: use a uniquely named database per test run, apply migrations, run tests, drop database in cleanup.
- Tests must fail or skip with an explicit reason if neither Docker nor LocalDB is available; do not silently switch to SQLite.

## Decision: Connection-String Handling

**Decision**: Register a single named connection string: `ConnectionStrings:TalabatDb`.

- API composition root reads the connection string and passes it to Infrastructure registration.
- Local developer secrets should use user secrets for real credentials.
- `appsettings.Development.json` may contain only a nonsecret LocalDB fallback.
- Integration tests provide connection strings dynamically from Testcontainers or generated LocalDB database names.
- No production credentials or passwords are committed.

## Decision: Repository And UnitOfWork Boundary

**Decision**: Infrastructure implements the existing aggregate-root repository interfaces and `IUnitOfWork` without changing Domain contracts.

- Repositories return aggregate roots with the child data required by each method.
- No repository returns `IQueryable`.
- No child repositories are introduced.
- `IUnitOfWork.SaveChangesAsync` delegates to the DbContext save boundary and is the only commit path for Application use cases.

**Rationale**: This preserves Clean Architecture boundaries and keeps Application independent of persistence.
