# Domain Data Model & Entity Specifications

## Value Objects

### Money
- **Properties**: `decimal Amount`
- **Rules**: Must be ≥ 0.
- **Methods**: `Add()`, `Multiply()`, `Compare()`

### TimeRange
- **Properties**: `TimeOnly OpensAt`, `TimeOnly ClosesAt`
- **Methods**: `Contains(TimeOnly time)`

## Aggregates & Entities

### 1. Catalog Context
**Aggregate Root: Restaurant**
- **Properties**: `Id`, `Name`, `Description`, `OpensAt` (TimeRange), `IsActive`
- **Methods**: `IsCurrentlyOpen()`, `AddProduct()`, `Deactivate()`

**Child Entity: Product**
- **Properties**: `Id`, `RestaurantId`, `Name`, `Description`, `Price` (Money), `IsAvailable`
- **Methods**: `UpdatePrice()`, `MarkUnavailable()`

### 2. Basket Context
**Aggregate Root: Cart**
- **Properties**: `Id`, `CustomerId`, `RestaurantId`, `CreatedAt`, `Items` (IReadOnlyCollection)
- **Rules**: Exactly one active cart per customer. All items must belong to `RestaurantId`. Expires 1 hour after `CreatedAt`.
- **Methods**: `AddItem(Product, int)`, `RemoveItem()`, `IsExpired()`, `Clear()`, `GetTotal(currentPrices)`

**Child Entity: CartItem**
- **Properties**: `Id`, `ProductId`, optional `ProductName`, `Quantity`
- **Rules**: Stores no product price. Application/read flows use current Catalog prices for cart display and checkout.
- **Methods**: `IncreaseQuantity()`

### 3. Ordering Context
**Aggregate Root: Order**
- **Properties**: `Id`, `CustomerId`, `RestaurantId`, `OrderDate`, `Status`, `TotalAmount` (Money), `DeliveryAddressId`
- **Rules**: Immutable after creation.
- **Methods**: Status transition methods (e.g., `MarkAsDelivered()` - deferred).

**Child Entity: OrderItem**
- **Properties**: `Id`, `ProductId`, `ProductName`, `UnitPrice` (Money), `Quantity`, `LineTotal` (Money)
- **Rules**: Fully immutable. `LineTotal` calculated in constructor.

### 4. Customer Context
**Aggregate Root: Customer**
- **Properties**: `Id`, `FullName`, `Age`, optional `PhoneNumber`
- **Rules**: FullName is required, Age is greater than zero, and no authentication identity is stored.
- **Methods**: `UpdateProfile()`, `AddAddress()`, `SetDefaultAddress()`

**Child Entity: CustomerAddress**
- **Properties**: `Id`, `Street`, `City`, `BuildingNumber`, `Floor`, `IsDefault`
