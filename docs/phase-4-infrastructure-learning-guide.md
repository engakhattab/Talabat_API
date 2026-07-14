# Phase 4 Infrastructure Learning Guide

> This guide is educational only. It explains the current Phase 4 implementation in `D:\link-dev\talabat` so you can understand and discuss the Infrastructure layer confidently.

## 1. Executive Summary

Phase 4 added real SQL Server persistence through EF Core in `Talabat.Infrastructure`.

Before Phase 4, the project had strong Domain aggregates, Application handlers, repository interfaces, and `IUnitOfWork`, but no real database implementation. Application code could describe what it needed, but there was no `DbContext`, no repository implementation, no migration, and no SQL Server schema.

After Phase 4, the project has:

- `TalabatDbContext` as the EF Core database session.
- EF Core mappings for all current aggregate roots.
- Repository implementations behind the existing Domain contracts.
- `UnitOfWork` as the single commit boundary.
- An initial migration that creates tables, keys, constraints, indexes, and seed data.
- Deterministic catalog seed data for local/customer-facing flows.
- API composition-root wiring through `AddInfrastructure()`.
- SQL Server-backed integration tests using Testcontainers with LocalDB fallback.

The key idea: Phase 4 makes the existing Domain and Application layers persist to SQL Server without moving business rules into Infrastructure.

## 2. Before Phase 4 vs After Phase 4

| Area | Before Phase 4 | After Phase 4 |
|---|---|---|
| Domain | Aggregates, invariants, value objects, repository contracts | Still framework-independent; only private parameterless constructors were added for EF materialization |
| Application | Handlers depended on repository interfaces and `IUnitOfWork` | Same contracts, now backed by SQL Server implementations |
| Infrastructure | Mostly empty project | EF Core packages, `TalabatDbContext`, mappings, repositories, `UnitOfWork`, migrations, seed data |
| API | Template API plus Application reference | Composition root now references Infrastructure and calls `AddInfrastructure()` |
| Database | No project-managed schema | Initial migration creates the persistence schema |
| Tests | Application tests only | Added SQL Server integration tests for Infrastructure behavior |

Layer responsibilities remain separated:

| Layer | Owns | Must Not Own |
|---|---|---|
| Domain | Business rules, entities, value objects, repository contracts | EF Core, SQL Server, HTTP, Identity/Auth |
| Application | Use-case orchestration and result mapping | DbContext, migrations, SQL provider details |
| Infrastructure | EF Core persistence, repository implementations, UnitOfWork implementation | Business-rule changes, API endpoints |
| API | Composition root, request pipeline | Persistence logic inside controllers |

## 3. Phase 4 Goal in the Roadmap

The Phase 4 goal in `D:\link-dev\talabat\PROJECT_IMPLEMENTATION_ROADMAP.md` is to persist the core business model behind the existing repository contracts.

The roadmap called for:

- EF Core.
- A `DbContext`.
- Explicit mappings/configurations.
- Repository implementations.
- `UnitOfWork`.
- Migrations.
- Catalog seed data.
- API composition-root wiring.
- No Identity/Auth.
- No API endpoints.
- No business-rule changes.

The implementation followed that direction:

- `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\TalabatDbContext.cs`
- `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\`
- `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\`
- `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\`
- `D:\link-dev\talabat\tests\Talabat.Infrastructure.Tests\Persistence\`

One documentation note: the top roadmap Status Snapshot marks Phase 4 completed, while the older Phase 4 section still contains `Status: Next`. That is a documentation cleanup issue, not an implementation issue.

## 4. Infrastructure Layer Responsibility

Infrastructure is responsible for technical persistence details:

- Connecting EF Core to SQL Server.
- Mapping Domain entities to tables.
- Implementing repository contracts.
- Implementing `IUnitOfWork`.
- Managing migrations.
- Defining seed data.
- Registering persistence services in DI.

Infrastructure must not contain:

- New business rules.
- API endpoints.
- Controllers.
- Identity/Auth behavior in Phase 4.
- Repository interface changes.
- Application use cases.

Current dependency direction:

```text
API -> Infrastructure -> Application -> Domain
```

Why this direction matters:

- `Domain` is the core and must not know EF Core or SQL Server.
- `Application` coordinates use cases through abstractions.
- `Infrastructure` implements those abstractions.
- `API` is the composition root, so it knows how to wire everything together.

The reverse direction is forbidden because it would make business logic depend on persistence details.

## 5. Packages and Tools Used

| Project/Tool | Package | Version | Runtime or Design-Time | Purpose |
|---|---|---:|---|---|
| `Talabat.Infrastructure` | `Microsoft.EntityFrameworkCore.SqlServer` | `10.0.9` | Runtime | EF Core SQL Server provider |
| `Talabat.Infrastructure` | `Microsoft.EntityFrameworkCore.Design` | `10.0.9` | Design-time, `PrivateAssets=all` | Required by EF tooling and migrations |
| `Talabat.Infrastructure` | `Microsoft.EntityFrameworkCore.Tools` | `10.0.9` | Design-time, `PrivateAssets=all` | EF migration/tooling support |
| `Talabat.API` | `Microsoft.EntityFrameworkCore.Design` | `10.0.9` | Design-time, `PrivateAssets=all` | Needed because EF commands use API as startup project |
| `Talabat.API` | `Microsoft.AspNetCore.OpenApi` | `10.0.9` | Runtime/API tooling | OpenAPI support |
| `Talabat.API` | `Microsoft.OpenApi` | `2.7.5` | Runtime/API tooling | Direct vulnerability-warning fix |
| `Talabat.Infrastructure.Tests` | `Microsoft.EntityFrameworkCore.SqlServer` | `10.0.9` | Test runtime | Run tests against the real SQL Server provider |
| `Talabat.Infrastructure.Tests` | `Testcontainers.MsSql` | `4.13.0` | Test runtime | Start SQL Server containers for integration tests |
| Global tool | `dotnet-ef` | `10.0.9` | CLI tool | Create/list/apply EF migrations |

There is no local `.config\dotnet-tools.json` manifest in the repo. The EF CLI tool is installed globally:

```powershell
dotnet tool list -g
dotnet ef --version
```

Why EF packages do not belong in Domain/Application:

- Domain should stay pure business code.
- Application should depend on contracts, not provider details.
- EF Core is an implementation detail of Infrastructure.

## 6. Project References and Composition Root

Important project references:

| Project | References |
|---|---|
| `Talabat.Application` | `Talabat.Domain` |
| `Talabat.Infrastructure` | `Talabat.Application` |
| `Talabat.API` | `Talabat.Application`, `Talabat.Infrastructure` |
| `Talabat.Infrastructure.Tests` | `Talabat.Domain`, `Talabat.Infrastructure` |

Main DI file:

`D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\DependencyInjection.cs`

Main API wiring:

`D:\link-dev\talabat\src\Talabat\Talabat.API\Program.cs`

The API calls:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

`AddInfrastructure()` does this:

- Reads `ConnectionStrings:TalabatDb`.
- Registers `TalabatDbContext`.
- Configures `UseSqlServer(connectionString)`.
- Registers `AuditableEntitySaveChangesInterceptor`.
- Registers `IUnitOfWork`.
- Registers all aggregate-root repositories.

Controllers should not use `TalabatDbContext` directly. They should call Application handlers, and the handlers should use repository interfaces.

## 7. DbContext Explanation

File:

`D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\TalabatDbContext.cs`

Class:

`TalabatDbContext : DbContext`

Simple definition:

```text
DbContext = EF Core unit of database session/change tracking
```

It is responsible for:

- Representing a logical database session.
- Tracking loaded and new entities.
- Knowing how Domain types map to tables.
- Sending inserts/updates/deletes through `SaveChangesAsync`.

Current `DbSet` properties:

- `Restaurants`
- `Products`
- `Carts`
- `Customers`
- `Orders`
- `Deliveries`
- `DeliveryAgents`

Example:

```csharp
public DbSet<Restaurant> Restaurants => Set<Restaurant>();
```

`OnModelCreating` uses:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(TalabatDbContext).Assembly);
```

This tells EF Core to find every `IEntityTypeConfiguration<T>` in the Infrastructure assembly and apply it automatically.

Why configurations are separated:

- `DbContext` stays small.
- Each aggregate mapping is easy to review.
- Constraints and indexes are close to the aggregate they protect.
- Future mapping changes are easier to isolate.

No custom SQL schema is configured. Tables are created in the default SQL Server schema, normally `dbo`.

## 8. Entity Configurations

| Configuration file | Maps | Important decisions | Why |
|---|---|---|---|
| `RestaurantConfiguration.cs` | `Restaurant`, `Product` | `Restaurants`, `Products`, IDENTITY keys, `_products` backing field, `TimeRange`, `Money`, unique `(RestaurantId, Name)` | Product is a child of Restaurant but needs its own table, FK, and ID |
| `CustomerConfiguration.cs` | `Customer`, `CustomerAddress` collection | `Customers`, `CustomerAddresses`, CustomerAddress IDENTITY, owned Address columns, one default address index | Addresses are managed through Customer, not through an address repository |
| `CartConfiguration.cs` | `Cart`, `CartItem` collection | `Carts`, `CartItems`, composite `(CartId, ProductId)`, one active cart per customer | CartItem has no independent ID in Domain |
| `OrderConfiguration.cs` | `Order`, `OrderItem` collection | `Orders`, `OrderItems`, composite `(OrderId, ProductId)`, delivery snapshot columns, money constraints | Order stores historical checkout snapshots |
| `DeliveryAgentConfiguration.cs` | `DeliveryAgent` | `DeliveryAgents`, enum checks, optional `GeoLocation`, coordinate constraints | DeliveryAgent is an aggregate root |
| `DeliveryConfiguration.cs` | `Delivery` | `Deliveries`, unique `OrderId`, active assignment unique index, delivery address snapshot | Delivery is an aggregate root linked by scalar IDs |
| `MappingConventions.cs` | Shared mapping helpers | IDENTITY, audit columns, soft-delete query filter, owned value object mappings | Keeps mapping rules consistent |
| `CatalogSeedData.cs` | Catalog seed rows | Explicit IDs for two restaurants and four products | Catalog has no management API in Phase 4 |

Important table decisions:

- `Restaurants`: PK `Id`, index `IX_Restaurants_IsActive`.
- `Products`: PK `Id`, FK `RestaurantId`, unique `UX_Products_RestaurantId_Name`, check `CurrentPriceAmount >= 0`.
- `Customers`: PK `Id`, check `Age > 0`.
- `CustomerAddresses`: PK `Id`, shadow FK `CustomerId`, filtered unique `UX_CustomerAddresses_CustomerId_Default`.
- `Carts`: PK `Id`, filtered unique `UX_Carts_CustomerId_Active`.
- `CartItems`: PK `(CartId, ProductId)`, check `Quantity > 0`.
- `Orders`: PK `Id`, index `IX_Orders_CustomerId_CreatedAt`.
- `OrderItems`: PK `(OrderId, ProductId)`, quantity and money checks.
- `DeliveryAgents`: PK `Id`, enum and coordinate checks.
- `Deliveries`: PK `Id`, unique `UX_Deliveries_OrderId`, filtered unique `UX_Deliveries_AssignedAgentId_Active`.

## 9. ID Strategy

The current strategy is SQL Server IDENTITY.

Confirmed by:

- `D:\link-dev\talabat\docs\phase-3.5-id-strategy-refactor.md`
- `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\MappingConventions.cs`
- `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\20260711171406_InitialPersistence.cs`

IDENTITY means SQL Server generates the integer `Id` during insert.

Migration example:

```csharp
Id = table.Column<int>(type: "int", nullable: false)
    .Annotation("SqlServer:Identity", "1, 1")
```

Meaning:

- Start at `1`.
- Increment by `1`.

A new Domain entity can start with:

```text
Id = 0
```

After:

```csharp
await _unitOfWork.SaveChangesAsync(cancellationToken);
```

EF Core receives the generated ID from SQL Server and fills it back into the tracked entity.

Phase 3.5 made this possible by:

- Removing `IApplicationIdGenerator`.
- Removing self-ID constructor/factory parameters.
- Making entity IDs writable through private setters.
- Moving response mapping that needs generated IDs until after `SaveChangesAsync`.

Example:

`D:\link-dev\talabat\src\Talabat\Talabat.Application\Basket\AddItem\AddCartItemHandler.cs`

`CartMapper.ToDetails(cart, restaurant)` runs after `SaveChangesAsync` so `cart.Id` is available.

Checkout example:

`D:\link-dev\talabat\src\Talabat\Talabat.Application\Ordering\Checkout\CheckoutHandler.cs`

```csharp
await _unitOfWork.SaveChangesAsync(cancellationToken);
return CheckoutResultMapper.ToOutcome(order.Id, checkoutSucceeded);
```

The current code does not use SQL Server sequences, does not use `ValueGeneratedNever()` for generated IDs, and does not contain `IApplicationIdGenerator`.

## 10. Value Objects and Owned Types

A value object has no independent identity. It belongs to an entity.

EF Core maps these value objects as owned columns instead of separate repositories.

| Value object | Domain file | EF mapping | Columns | Why owned |
|---|---|---|---|---|
| `Money` | `D:\link-dev\talabat\src\Talabat\Talabat.Domain\ValueObjects\Money.cs` | `ConfigureMoney()` | `CurrentPriceAmount`, `TotalAmount`, `UnitPriceAmount`, `LineTotalAmount` as `decimal(18,2)` | Money is a value, not an aggregate |
| `TimeRange` | `D:\link-dev\talabat\src\Talabat\Talabat.Domain\ValueObjects\TimeRange.cs` | `ConfigureTimeRange()` | `OpeningStart`, `OpeningEnd` as SQL `time` | Opening hours belong to Restaurant |
| `Address` | `D:\link-dev\talabat\src\Talabat\Talabat.Domain\ValueObjects\Address.cs` | `ConfigureAddress()` | `Street`, `City`, `BuildingNumber`, `Floor` | Address details belong to CustomerAddress |
| `DeliveryAddressSnapshot` | `D:\link-dev\talabat\src\Talabat\Talabat.Domain\ValueObjects\DeliveryAddressSnapshot.cs` | `ConfigureDeliveryAddressSnapshot()` | `DeliveryStreet`, `DeliveryCity`, `DeliveryBuildingNumber`, `DeliveryFloor` | Historical order/delivery address snapshot |
| `GeoLocation` | `D:\link-dev\talabat\src\Talabat\Talabat.Domain\ValueObjects\GeoLocation.cs` | `ConfigureGeoLocation()` | `CurrentLatitude`, `CurrentLongitude` as `decimal(9,6)` | Current location belongs to DeliveryAgent |

Important constraints:

- Money columns have non-negative checks.
- `GeoLocation` has latitude and longitude range checks.
- `GeoLocation` also has a paired-null check: both coordinates are null, or both are present.

## 11. Relationships and Aggregate Boundaries

Only aggregate roots have repositories:

- `IRestaurantRepository`
- `ICustomerRepository`
- `ICartRepository`
- `IOrderRepository`
- `IDeliveryAgentRepository`
- `IDeliveryRepository`

There is no:

- `ProductRepository`
- `CartItemRepository`
- `OrderItemRepository`
- `CustomerAddressRepository`

Why:

- `Product` belongs to `Restaurant`.
- `CartItem` belongs to `Cart`.
- `OrderItem` belongs to `Order`.
- `CustomerAddress` belongs to `Customer`.

Database relationships reflect that:

- `Products` has FK `RestaurantId`.
- `CartItems` has FK `CartId` and composite key `(CartId, ProductId)`.
- `OrderItems` has FK `OrderId` and composite key `(OrderId, ProductId)`.
- `CustomerAddresses` has a shadow FK `CustomerId`.

Delivery relationship:

- `Delivery` is its own aggregate root.
- `Delivery` stores `OrderId`, `CustomerId`, `RestaurantId`, and optional `AssignedAgentId`.
- `DeliveryAgent` is its own aggregate root.
- There is no delivery collection on `DeliveryAgent`.
- Active assignment is protected by a filtered unique index on `Deliveries`.

## 12. Repositories

Repositories implement Domain interfaces and hide EF Core from Application.

| Repository file | Interface | Main methods | Tracking behavior |
|---|---|---|---|
| `RestaurantRepository.cs` | `IRestaurantRepository` | `GetActiveRestaurantsAsync`, `GetByIdAsync`, `GetByIdWithProductsAsync`, `GetProductSnapshotAsync`, `ExistsAsync` | Read-only queries use `AsNoTracking`; product-loaded aggregate can be tracked |
| `CustomerRepository.cs` | `ICustomerRepository` | `GetByIdAsync`, `GetByIdWithAddressesAsync`, `AddAsync`, `Update` | Profile reads can be no-tracking; address-loaded aggregate is tracked for mutation |
| `CartRepository.cs` | `ICartRepository` | `GetActiveCartByCustomerIdAsync`, `AddAsync`, `Update` | Active cart is tracked because handlers mutate it |
| `OrderRepository.cs` | `IOrderRepository` | `GetByIdAsync`, `GetByIdForCustomerAsync`, `GetByCustomerIdAsync`, `AddAsync` | History uses `AsNoTracking`; details include `_items` |
| `DeliveryAgentRepository.cs` | `IDeliveryAgentRepository` | `GetByIdAsync`, `GetAvailableAgentsAsync`, `AddAsync`, `Update` | Available query is no-tracking; by-id query can be tracked |
| `DeliveryRepository.cs` | `IDeliveryRepository` | `GetByIdAsync`, `GetByOrderIdAsync`, `GetActiveByAgentIdAsync`, `GetPendingAssignmentAsync`, `GetAssignedToAgentAsync`, `AddAsync`, `Update` | Command reads can be tracked; list reads are no-tracking |

Private backing fields are loaded with string-based includes:

- `Include("_products")`
- `Include("_addresses")`
- `Include("_items")`

Repositories do not call `SaveChangesAsync`.

Reason:

- A use case may touch multiple aggregates.
- Checkout creates an `Order` and updates a `Cart`.
- Those changes must commit together.
- `IUnitOfWork` owns the commit.

Repositories should not expose `IQueryable` because that leaks EF Core and database query composition outside Infrastructure.

## 13. UnitOfWork

File:

`D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\UnitOfWork.cs`

Class:

`UnitOfWork : IUnitOfWork`

Implementation:

```csharp
public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    return _dbContext.SaveChangesAsync(cancellationToken);
}
```

Why this exists when `DbContext` already has `SaveChangesAsync`:

- Application must not know `DbContext`.
- `IUnitOfWork` is a persistence abstraction.
- Repositories stage changes.
- UnitOfWork commits changes.

Checkout example:

```text
Load active cart
Load customer and address
Load restaurant and products
Validate checkout in Domain service
Create Order
Mark Cart checked out
Save once through UnitOfWork
```

This protects consistency:

- No saved order without a checked-out cart.
- No checked-out cart without a saved order.
- EF Core wraps `SaveChangesAsync` in a transaction for the pending changes.

## 14. Database Constraints and Indexes

| Constraint/Index | Table | Rule Protected | Duplicates Domain Rule? | Why Still Useful |
|---|---|---|---|---|
| `CK_CartItems_Quantity_Positive` | `CartItems` | Quantity > 0 | Yes | Protects against raw SQL or future bugs |
| `CK_OrderItems_Quantity_Positive` | `OrderItems` | Quantity > 0 | Yes | Protects order snapshots |
| `CK_Products_CurrentPriceAmount_NonNegative` | `Products` | Money >= 0 | Yes | Prevents corrupted product prices |
| `CK_Orders_TotalAmount_NonNegative` | `Orders` | Total amount >= 0 | Yes | Protects order totals |
| `CK_OrderItems_UnitPriceAmount_NonNegative` | `OrderItems` | Unit price >= 0 | Yes | Protects historical line prices |
| `CK_OrderItems_LineTotalAmount_NonNegative` | `OrderItems` | Line total >= 0 | Yes | Protects line totals |
| `UX_Carts_CustomerId_Active` | `Carts` | One active cart per customer | Yes | Protects against concurrency |
| `UX_CustomerAddresses_CustomerId_Default` | `CustomerAddresses` | One default address per customer | Yes | Protects against concurrency |
| `UX_Deliveries_OrderId` | `Deliveries` | One delivery per order | Yes | Protects order-delivery uniqueness |
| `UX_Deliveries_AssignedAgentId_Active` | `Deliveries` | One active delivery per agent | Yes | Protects delivery assignment concurrency |
| `UX_Products_RestaurantId_Name` | `Products` | Unique product name per restaurant | Yes | Database backstop for Phase 3.5 name dedupe |
| `PK_CartItems (CartId, ProductId)` | `CartItems` | One line per product in cart | Yes | Represents child identity |
| `PK_OrderItems (OrderId, ProductId)` | `OrderItems` | One line per product in order | Yes | Represents child identity |
| Foreign keys | Multiple tables | Valid references | Partly | Prevents orphan rows |

Database constraints do not replace Domain validation. They are a second layer of protection.

## 15. Migrations

Migration files are stored in:

`D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\`

Current files:

- `20260711171406_InitialPersistence.cs`
- `20260711171406_InitialPersistence.Designer.cs`
- `TalabatDbContextModelSnapshot.cs`
- `InitialPersistence.idempotent.sql`

What each file means:

| File | Meaning |
|---|---|
| `20260711171406_InitialPersistence.cs` | Main migration file with `Up()` and `Down()` |
| `20260711171406_InitialPersistence.Designer.cs` | EF metadata for this migration |
| `TalabatDbContextModelSnapshot.cs` | Latest EF model snapshot used to calculate future migration diffs |
| `InitialPersistence.idempotent.sql` | SQL script generated from migrations for review/deployment |

Why there are two C# files plus SQL:

When EF Core creates a migration, it normally creates:

1. A main migration file: `*_InitialPersistence.cs`.
2. A designer metadata file: `*_InitialPersistence.Designer.cs`.
3. It also creates or updates the model snapshot: `*ModelSnapshot.cs`.

The `.sql` file is not another C# migration. It is a generated script based on the migration.

Command to create the migration:

```powershell
dotnet ef migrations add InitialPersistence --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API --output-dir Persistence\Migrations
```

Command to create the idempotent SQL script:

```powershell
dotnet ef migrations script --idempotent --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API --output src\Talabat\Talabat.Infrastructure\Persistence\Migrations\InitialPersistence.idempotent.sql
```

Command to apply the migration to the configured SQL Server:

```powershell
dotnet ef database update --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API
```

Important difference:

- Migration files describe schema changes in code.
- `database update` applies those changes to an actual SQL Server database.

So seeing migration files in the repo does not guarantee that the database already exists in SSMS. The database appears after the migration is applied to the same SQL Server instance you are viewing.

## 16. Seed Data

Seed data is implemented in:

`D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\CatalogSeedData.cs`

Seeded data:

| Type | IDs | Names |
|---|---|---|
| Restaurants | `1`, `2` | `Cairo Grill`, `Nile Pizza` |
| Products | `101`, `102`, `201`, `202` | `Mixed Grill Plate`, `Chicken Shawarma`, `Margherita Pizza`, `Baked Penne` |

It uses:

```csharp
builder.HasData(...)
```

Why explicit seed IDs are allowed:

- Seed data is deterministic.
- EF needs stable IDs to connect products to restaurants.
- Runtime entity IDs are still generated by SQL Server IDENTITY.

This data is useful because Phase 4 does not add catalog management endpoints. Without seed data, local customer-facing catalog reads would return nothing.

No Identity/Auth seed data exists because Identity/Auth is outside Phase 4.

## 17. API Wiring and appsettings

Changed file:

`D:\link-dev\talabat\src\Talabat\Talabat.API\Program.cs`

Added:

```csharp
using Talabat.Infrastructure;
builder.Services.AddInfrastructure(builder.Configuration);
```

Current development connection string:

`D:\link-dev\talabat\src\Talabat\Talabat.API\appsettings.Development.json`

```json
"TalabatDb": "Server=DESKTOP-5IHGJ9F\\SQLEXPRESS;Database=Talabat;Trusted_Connection=True;TrustServerCertificate=True"
```

This targets:

```text
SQL Server instance: DESKTOP-5IHGJ9F\SQLEXPRESS
Database: Talabat
```

If you do not see the database in SSMS, the usual reasons are:

- You are connected to a different SQL Server instance.
- `dotnet ef database update` has not been run.
- The connection string was changed after the migration was applied somewhere else.

API wiring does not mean API owns persistence logic. API only wires dependencies.

## 18. How a Request Flows Now

### Example A: Add cart item

```text
Future API endpoint in Phase 5
  -> AddCartItemHandler
  -> IRestaurantRepository.GetProductSnapshotAsync
  -> ICartRepository.GetActiveCartByCustomerIdAsync
  -> Cart.Create or cart.AddItem
  -> ICartRepository.AddAsync or Update
  -> IUnitOfWork.SaveChangesAsync
  -> SQL Server insert/update
  -> CartMapper.ToDetails after generated IDs exist
```

Phase 4 did not add the endpoint. It made the persistence path ready for the existing Application handler.

### Example B: Checkout

```text
CheckoutHandler
  -> Load active Cart
  -> Load Customer with addresses
  -> Create DeliveryAddressSnapshot
  -> Load Restaurant with products
  -> CheckoutDomainService.ValidateCheckout
  -> Order.CreateFromCheckout
  -> cart.MarkCheckedOut
  -> orderRepository.AddAsync
  -> cartRepository.Update
  -> unitOfWork.SaveChangesAsync once
  -> order.Id becomes available
```

The important separation:

- Domain validates business rules.
- Application coordinates the use case.
- Infrastructure persists data.
- UnitOfWork commits once.

## 19. Phase 4 Does NOT Do

Phase 4 did not add:

- No IdentityServer.
- No ASP.NET Core Identity.
- No JWT/auth.
- No frontend.
- No business logic in controllers.
- No Domain dependency on EF Core.
- No Application dependency on Infrastructure.
- No child entity repositories.
- No new Application use cases beyond the existing scope.
- No Delivery Application use cases.
- No MediatR.
- No new API endpoints.

## 20. Important Technical Concepts Explained

| Concept | Explanation |
|---|---|
| EF Core | ORM that maps C# objects to database rows |
| DbContext | Database session and change tracker |
| DbSet | Query/save entry point for an entity type |
| Entity Configuration | Class that tells EF how to map an entity |
| Migration | Code file describing a database schema change |
| Provider | Package that teaches EF how to talk to a database engine |
| Design-time package | Package used by tooling such as migrations |
| Repository | Abstraction for loading/saving aggregate roots |
| UnitOfWork | Abstraction for one commit boundary |
| Change tracking | EF tracking which entities changed |
| Owned type | Value object persisted as part of an owner |
| Foreign key | Column linking one row to another table row |
| Index | Database structure for lookup speed or uniqueness |
| Constraint | Database rule that rejects invalid data |
| Seed data | Initial deterministic data applied through EF model/migration |
| Composition root | Startup location that wires dependencies |
| Connection string | Text that identifies server, database, and authentication settings |

## 21. File-by-File Map

| File | Purpose | Why it exists | What to remember |
|---|---|---|---|
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Talabat.Infrastructure.csproj` | Infrastructure packages/references | Adds EF Core SQL Server support | EF packages stay out of Domain/Application |
| `D:\link-dev\talabat\src\Talabat\Talabat.API\Talabat.API.csproj` | API packages/references | Adds Infrastructure reference and OpenAPI fix | API is the composition root |
| `D:\link-dev\talabat\src\Talabat\Talabat.API\Program.cs` | Startup wiring | Calls `AddInfrastructure()` | No new business endpoint was added |
| `D:\link-dev\talabat\src\Talabat\Talabat.API\appsettings.Development.json` | Local connection string | Defines `TalabatDb` | Database appears only on that configured server |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\DependencyInjection.cs` | DI registration | Registers DbContext, interceptor, repos, UnitOfWork | Main Infrastructure entry point |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\TalabatDbContext.cs` | EF DbContext | DbSets and configuration loading | Represents the database session |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\UnitOfWork.cs` | Commit boundary | Implements `IUnitOfWork` | Repositories do not save |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Auditing\AuditableEntitySaveChangesInterceptor.cs` | Audit timestamps | Stamps created/modified timestamps | User fields remain null until Identity |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\MappingConventions.cs` | Shared mapping helpers | IDENTITY, audit, owned type conventions | Best file for understanding mapping style |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\RestaurantConfiguration.cs` | Catalog mapping | Restaurants and Products | Unique product name per restaurant |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\CatalogSeedData.cs` | Catalog seed | Restaurants/products with explicit IDs | Seed IDs only, not runtime ID generation |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\CustomerConfiguration.cs` | Customer mapping | Customers and CustomerAddresses | One default address index |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\CartConfiguration.cs` | Cart mapping | Carts and CartItems | One active cart per customer |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\OrderConfiguration.cs` | Order mapping | Orders and OrderItems | Checkout snapshots |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\DeliveryAgentConfiguration.cs` | DeliveryAgent mapping | DeliveryAgents | Coordinate checks |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\DeliveryConfiguration.cs` | Delivery mapping | Deliveries | Unique delivery per order |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\RestaurantRepository.cs` | Catalog repository | Implements `IRestaurantRepository` | Returns product snapshots |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\CustomerRepository.cs` | Customer repository | Implements `ICustomerRepository` | Loads `_addresses` when needed |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\CartRepository.cs` | Cart repository | Implements `ICartRepository` | Loads `_items` for active carts |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\OrderRepository.cs` | Order repository | Implements `IOrderRepository` | Supports history/details |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\DeliveryAgentRepository.cs` | Agent repository | Implements `IDeliveryAgentRepository` | Available-agent query |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\DeliveryRepository.cs` | Delivery repository | Implements `IDeliveryRepository` | Active assignment/order queries |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\20260711171406_InitialPersistence.cs` | Main migration | Creates/drops schema | Read `Up()` and `Down()` |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\20260711171406_InitialPersistence.Designer.cs` | Migration metadata | EF tooling support | Usually not manually edited |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\TalabatDbContextModelSnapshot.cs` | Current model snapshot | Diff source for next migration | Very important for future migrations |
| `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\InitialPersistence.idempotent.sql` | SQL script | Review/deploy script | Generated from migrations |
| `D:\link-dev\talabat\tests\Talabat.Infrastructure.Tests\Talabat.Infrastructure.Tests.csproj` | Test project | SQL Server integration tests | Uses Testcontainers |
| `D:\link-dev\talabat\tests\Talabat.Infrastructure.Tests\Persistence\SqlServerDatabaseFixture.cs` | Test DB fixture | Testcontainers then LocalDB fallback | Applies migrations per test database |
| `D:\link-dev\talabat\tests\Talabat.Infrastructure.Tests\Persistence\InfrastructureTestServices.cs` | Test DI helper | Registers Infrastructure services | Mirrors production DI for tests |
| `D:\link-dev\talabat\tests\Talabat.Infrastructure.Tests\Persistence\*Tests.cs` | Integration tests | Verify mappings, constraints, repos | SQL Server behavior tests |

## 22. Architecture Rules Checklist

- [x] Domain has no EF Core package references.
- [x] Application has no Infrastructure reference.
- [x] Infrastructure implements existing contracts.
- [x] API wires dependencies through the composition root.
- [x] No Identity/Auth implementation was added.
- [x] Repositories exist only for aggregate roots.
- [x] UnitOfWork is used for commits.
- [x] Database constraints align with Domain/roadmap rules.
- [x] Repository contracts do not expose `IQueryable`.
- [x] No child entity repositories were added.
- [x] Runtime IDs are generated by SQL Server IDENTITY, not the Application layer.

## 23. Common Mentor Questions and Answers

**Q: Why did we add Infrastructure after Application?**  
A: Application first defines the use cases and contracts. Infrastructure then implements those contracts. This keeps the design driven by business behavior instead of database details.

**Q: Why not put EF Core in Application?**  
A: Application should coordinate use cases. EF Core is a persistence detail and belongs in Infrastructure.

**Q: Why does API reference Infrastructure?**  
A: API is the composition root. It wires concrete implementations into DI. That does not mean controllers should use Infrastructure directly.

**Q: Why use repositories if EF Core already has DbContext?**  
A: Repositories hide EF Core from Application and expose aggregate-focused operations.

**Q: Why use UnitOfWork if DbContext already has SaveChanges?**  
A: Application should not know `DbContext`. `IUnitOfWork` gives Application a clean commit abstraction.

**Q: Why are configurations in separate files?**  
A: It keeps mappings reviewable and prevents `TalabatDbContext` from becoming a large configuration file.

**Q: Why owned types for value objects?**  
A: Value objects such as `Money` and `Address` have no identity. They are stored as part of their owner.

**Q: Why database constraints if Domain already validates?**  
A: Domain protects normal code paths. Database constraints also protect against concurrency, raw SQL, and future bugs.

**Q: Why no repositories for CartItem, OrderItem, Product, or CustomerAddress?**  
A: They are child entities. They are managed through their aggregate roots.

**Q: Why no Identity/Auth in Phase 4?**  
A: The roadmap reserves Identity/Auth for a later phase. Phase 4 is persistence only.

**Q: How does checkout stay transactional?**  
A: The handler creates an `Order`, marks the `Cart` checked out, and calls `IUnitOfWork.SaveChangesAsync` once.

**Q: How do migrations relate to the Domain model?**  
A: EF configurations map Domain entities to a database model. Migrations turn that model into schema changes.

**Q: Why can `Id` be zero in memory?**  
A: SQL Server generates the ID only when the row is inserted. Before save, `Id = 0` is normal.

**Q: Why is there an idempotent SQL file?**  
A: It is a generated deployment/review script that checks `__EFMigrationsHistory` before applying migration steps.

## 24. Two-Minute Explanation

In Phase 4, we moved from Domain and Application contracts to real SQL Server persistence. EF Core is isolated in `Talabat.Infrastructure`. We added `TalabatDbContext`, aggregate mappings, repository implementations, `UnitOfWork`, an initial migration, catalog seed data, and SQL Server integration tests. Clean Architecture is preserved: Domain does not know EF Core, Application depends on repository abstractions, Infrastructure implements them, and API only wires everything through `AddInfrastructure()`.

The project uses SQL Server IDENTITY for IDs, so new entities start with `Id = 0` and receive their database ID after `SaveChangesAsync`. Child collections like `CartItems` and `OrderItems` use composite keys because Domain does not give them independent IDs. Value objects like `Money`, `Address`, `TimeRange`, `DeliveryAddressSnapshot`, and `GeoLocation` are mapped as owned columns, not separate aggregates.

Repositories load aggregate roots and do not save. `UnitOfWork` commits once, which is important for checkout because the use case creates an order and closes the cart in one save. Database constraints and filtered unique indexes protect rules such as one active cart per customer, one default address per customer, unique delivery per order, and unique product names per restaurant.

## 25. 30-Second Explanation

Phase 4 added EF Core SQL Server persistence inside `Talabat.Infrastructure` only. It introduced `TalabatDbContext`, mappings, repositories, `UnitOfWork`, migration files, seed data, and SQL Server integration tests. Domain and Application remain independent, IDs are generated by SQL Server IDENTITY after `SaveChangesAsync`, and API only wires Infrastructure through `AddInfrastructure()`.

## 26. What To Review Before Phase 5

Before starting API endpoints, review:

- `dotnet build src\Talabat\Talabat.slnx`.
- `dotnet test src\Talabat\Talabat.slnx`.
- `dotnet ef database update` if you want to create/see the database in SSMS.
- Seed data in `Restaurants` and `Products`.
- Repository behavior.
- The active connection string.
- No EF packages in Domain/Application.
- No Identity/Auth additions.
- Migration files and the idempotent SQL script.

## Potential Issues / Things To Ask Mentor

- `PROJECT_IMPLEMENTATION_ROADMAP.md` has a completed Phase 4 status at the top, but the older Phase 4 section still says `Status: Next`.
- `appsettings.Development.json` currently points to `DESKTOP-5IHGJ9F\SQLEXPRESS`. That is local-machine-specific. Ask whether to keep it or use a generic LocalDB placeholder.
- `UseAuthorization()` remains from the API template even though Phase 4 did not add Identity/Auth.
- `WeatherForecastController` still exists from the template. Phase 4 did not require removing it; Phase 5 may clean API endpoints.
- Audit user fields such as `CreatedBy` and `ModifiedBy` are currently null because Identity/Auth is deferred.
- Infrastructure tests require Docker/Testcontainers or LocalDB.

## Commands and Verification Status

Useful commands:

```powershell
dotnet build src\Talabat\Talabat.slnx
dotnet test src\Talabat\Talabat.slnx
dotnet list src\Talabat\Talabat.slnx package --vulnerable
dotnet ef migrations list --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API
dotnet ef database update --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API
```

Migration commands:

```powershell
dotnet ef migrations add InitialPersistence --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API --output-dir Persistence\Migrations
dotnet ef migrations script --idempotent --project src\Talabat\Talabat.Infrastructure --startup-project src\Talabat\Talabat.API --output src\Talabat\Talabat.Infrastructure\Persistence\Migrations\InitialPersistence.idempotent.sql
```

Verification status recorded in `D:\link-dev\talabat\docs\phase-4-persistence-and-infrastructure.md`:

- Build passed with zero warnings.
- Application tests passed: 45 passed, 0 failed.
- Infrastructure tests passed: 19 passed, 0 failed, 0 skipped.
- Vulnerability audit reported no vulnerable packages.
- `Talabat.Domain` and `Talabat.Application` still have no package references.

I did not rerun build/test while translating this guide because this request was documentation-only and did not ask to update the database or run migrations.

## Next Recommended Learning Step

Read these files in order:

1. `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\DependencyInjection.cs`
2. `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\TalabatDbContext.cs`
3. `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\MappingConventions.cs`
4. `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Configurations\CartConfiguration.cs`
5. `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Repositories\CartRepository.cs`
6. `D:\link-dev\talabat\src\Talabat\Talabat.Application\Ordering\Checkout\CheckoutHandler.cs`
7. `D:\link-dev\talabat\tests\Talabat.Infrastructure.Tests\Persistence\CheckoutPersistenceTests.cs`
8. `D:\link-dev\talabat\src\Talabat\Talabat.Infrastructure\Persistence\Migrations\20260711171406_InitialPersistence.cs`

If you understand that chain, you understand how the project moves from a business use case to SQL Server persistence without breaking Clean Architecture.
