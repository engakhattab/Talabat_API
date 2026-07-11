# Data Model: Persistence And Infrastructure

This document describes the Phase 4 persistence mapping plan. It is not a migration script and does not authorize implementation before review.

## Shared Mapping Standards

- All entities with an `Id` column use SQL Server IDENTITY-compatible integer keys.
- `CartItem` and `OrderItem` use composite keys because the Domain has no separate line-item ID.
- All `Money` values map to `decimal(18,2)` amount columns with non-negative checks.
- All absolute timestamps map to `datetime2` and are UTC by application policy.
- Audit columns are mapped for every `AuditableEntity` table.
- Backing fields and private constructors are used where needed for materialization; do not add public mutable collections or public setters for persistence.

## Restaurant Aggregate

| Entity | Table | Keys | Owned types | Indexes and constraints | Backing-field/private-ctor notes |
|---|---|---|---|---|---|
| `Restaurant` | `Restaurants` | PK `Id` IDENTITY | `OpeningHours` as `OpeningStart`/`OpeningEnd` `time` columns | Required `Name`, `Description`, `OpeningStart`, `OpeningEnd`; index active restaurants for browse; audit soft-delete filter | Map `_products` backing field; private parameterless constructor may be added only for materialization |
| `Product` | `Products` | PK `Id` IDENTITY; FK `RestaurantId` | `CurrentPrice` as `CurrentPriceAmount decimal(18,2)` | Unique `(RestaurantId, Name)`; `CurrentPriceAmount >= 0`; required `Name`, `Description`, `IsAvailable`; FK to `Restaurants` | Child is modified only through `Restaurant`; no product repository |

## Cart Aggregate

| Entity | Table | Keys | Owned types | Indexes and constraints | Backing-field/private-ctor notes |
|---|---|---|---|---|---|
| `Cart` | `Carts` | PK `Id` IDENTITY; references `CustomerId`, `RestaurantId` | None | Filtered unique `(CustomerId)` where `Status = 1` and `IsDeleted = 0`; `Status` enum check `1,2,3`; required reference IDs; audit soft-delete filter | Map `_items` backing field; `CustomerId` get-only mapping may require field/property access configuration |
| `CartItem` | `CartItems` | Composite PK `(CartId, ProductId)`; FK `CartId`; FK `ProductId` | None | `Quantity > 0`; required `ProductName`; cascade with owning cart; product FK protects catalog references | No `CartItemId`; child loaded through `Cart.Items` only |

## Customer Aggregate

| Entity | Table | Keys | Owned types | Indexes and constraints | Backing-field/private-ctor notes |
|---|---|---|---|---|---|
| `Customer` | `Customers` | PK `Id` IDENTITY | None | Required `FullName`; `Age > 0`; optional `PhoneNumber`; audit soft-delete filter | Map `_addresses` backing field; private parameterless constructor may be added only for materialization |
| `CustomerAddress` | `CustomerAddresses` | PK `Id` IDENTITY; FK `CustomerId` | `Details` as `Street`, `City`, `BuildingNumber`, `Floor` | Required street/city/building; filtered unique `(CustomerId)` where `IsDefault = 1` and `IsDeleted = 0`; duplicate address value remains a Domain rule in Phase 4 | Child loaded through `Customer.Addresses`; no address repository |

## Order Aggregate

| Entity | Table | Keys | Owned types | Indexes and constraints | Backing-field/private-ctor notes |
|---|---|---|---|---|---|
| `Order` | `Orders` | PK `Id` IDENTITY; FK `CustomerId`; FK `RestaurantId` | `DeliveryAddress` snapshot columns; `TotalAmount` as `TotalAmountAmount decimal(18,2)` or configured column name `TotalAmount` | `TotalAmount >= 0`; required delivery street/city/building; index `(CustomerId, CreatedAt)`; audit soft-delete filter | Map `_items` backing field; delivery snapshot is owned, not a table |
| `OrderItem` | `OrderItems` | Composite PK `(OrderId, ProductId)`; FK `OrderId`; FK `ProductId` | `UnitPrice` and `LineTotal` as decimal amount columns | `Quantity > 0`; `UnitPrice >= 0`; `LineTotal >= 0`; required `ProductName`; cascade with owning order | No `OrderItemId`; historical product name and price are snapshots |

## Delivery Aggregate

| Entity | Table | Keys | Owned types | Indexes and constraints | Backing-field/private-ctor notes |
|---|---|---|---|---|---|
| `Delivery` | `Deliveries` | PK `Id` IDENTITY; unique FK `OrderId`; FK `CustomerId`; FK `RestaurantId`; nullable FK `AssignedAgentId` | `DeliveryAddress` snapshot columns | Unique `OrderId`; filtered unique `AssignedAgentId` where assigned active status in `2,3,4,5` and not deleted; status enum check `1..8`; required delivery street/city/building; optional failure reason length cap | No navigation collection required; assigned agent is an ID in Domain |

## DeliveryAgent Aggregate

| Entity | Table | Keys | Owned types | Indexes and constraints | Backing-field/private-ctor notes |
|---|---|---|---|---|---|
| `DeliveryAgent` | `DeliveryAgents` | PK `Id` IDENTITY | `CurrentLocation` as nullable `CurrentLatitude decimal(9,6)` and `CurrentLongitude decimal(9,6)` | Required `FullName`, `VehicleType`, `Status`; enum checks; paired-null coordinate check; latitude and longitude range checks; audit soft-delete filter | Do not add `CurrentDeliveryId`; active assignment lives on `Delivery` |

## Repository Mapping Coverage

| Contract | Persistence behavior |
|---|---|
| `IRestaurantRepository.GetActiveRestaurantsAsync` | Query active, not-deleted restaurants; product loading not required unless method says with products |
| `IRestaurantRepository.GetByIdWithProductsAsync` | Load restaurant with `_products` |
| `IRestaurantRepository.GetProductSnapshotAsync` | Query product by restaurant and product ID and return snapshot without exposing EF types |
| `ICartRepository.GetActiveCartByCustomerIdAsync` | Load not-deleted active cart with `_items` |
| `ICustomerRepository.GetByIdWithAddressesAsync` | Load customer with `_addresses` |
| `IOrderRepository.GetByCustomerIdAsync` | Return not-deleted orders for customer, newest first at handler or repository boundary as already expected by Application tests |
| `IOrderRepository.GetByIdForCustomerAsync` | Scope by order ID and customer ID |
| `IDeliveryRepository` methods | Query by ID, order ID, active agent assignment, pending assignment, and assigned agent |
| `IDeliveryAgentRepository.GetAvailableAgentsAsync` | Query not-deleted agents with `Status = Available` |
| `IUnitOfWork.SaveChangesAsync` | Commit DbContext changes and trigger audit stamping |
