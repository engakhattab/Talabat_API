# IdentityServer4 Readiness Report

> Phase 0 status: Historical compatibility research only. This report does not select IdentityServer4 and must not be used as an implementation plan. The current roadmap keeps IdentityServer/Auth Portal reserved/TBD, with no framework selected and no identity packages to be installed until a later approved phase.

Generated: 2026-07-10  
Repository root: `D:\link-dev\talabat`  
Solution file inspected: `src/Talabat/Talabat.slnx`

This report is inspection-only for production code. No production projects were modified. A temporary scratch project was created at `.codex-scratch/ids4-net10-compat` for the requested IdentityServer4 compatibility spike and was not added to the solution.

## 1. Solution Overview

The repository does not contain a root `.sln` file. The buildable solution file is `src/Talabat/Talabat.slnx`.

Projects in `Talabat.slnx`:

| Project | TargetFramework | Nullable | ImplicitUsings | Existing PackageReferences | Existing ProjectReferences |
| --- | --- | --- | --- | --- | --- |
| `Talabat.API/Talabat.API.csproj` | `net10.0` | `enable` | `enable` | `Microsoft.AspNetCore.OpenApi` `10.0.9` | `..\Talabat.Application\Talabat.Application.csproj` |
| `Talabat.Application/Talabat.Application.csproj` | `net10.0` | `enable` | `enable` | None | `..\Talabat.Domain\Talabat.Domain.csproj` |
| `Talabat.Domain/Talabat.Domain.csproj` | `net10.0` | `enable` | `enable` | None | None |
| `Talabat.Infrastructure/Talabat.Infrastructure.csproj` | `net10.0` | `enable` | `enable` | None | `..\Talabat.Application\Talabat.Application.csproj` |

Additional project file note:

- `Talabat.Domain.csproj` contains a folder include for `Interfaces\`, but the folder has no source files.

## 2. Build Status

Commands were run from `D:\link-dev\talabat\src\Talabat`, because that directory contains `Talabat.slnx`.

`dotnet --info` summary:

```text
.NET SDK:
 Version:           10.0.301
 MSBuild version:   18.6.4+96856fd72

Host:
  Version:      10.0.9
  Architecture: x64

.NET SDKs installed:
  9.0.100
  10.0.301

global.json file:
  Not found
```

`dotnet --list-sdks`:

```text
9.0.100 [C:\Program Files\dotnet\sdk]
10.0.301 [C:\Program Files\dotnet\sdk]
```

`dotnet restore` result: succeeded with one NuGet vulnerability warning.

```text
D:\link-dev\talabat\src\Talabat\Talabat.API\Talabat.API.csproj : warning NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [D:\link-dev\talabat\src\Talabat\Talabat.slnx]
Restored D:\link-dev\talabat\src\Talabat\Talabat.API\Talabat.API.csproj
3 of 4 projects are up-to-date for restore.
```

`dotnet build` result: succeeded with the same warning repeated during restore/build.

```text
Build succeeded.

D:\link-dev\talabat\src\Talabat\Talabat.API\Talabat.API.csproj : warning NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [D:\link-dev\talabat\src\Talabat\Talabat.slnx]
D:\link-dev\talabat\src\Talabat\Talabat.API\Talabat.API.csproj : warning NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc
    2 Warning(s)
    0 Error(s)
```

Likely cause of warning:

- `Talabat.API` directly references `Microsoft.AspNetCore.OpenApi` `10.0.9`, which resolves vulnerable transitive package `Microsoft.OpenApi` `2.0.0`.
- This is not an IdentityServer4 issue, but it should be handled before production hardening.

## 3. Current Architecture

Current project dependency direction:

```text
Talabat.API            -> Talabat.Application
Talabat.Infrastructure -> Talabat.Application
Talabat.Application    -> Talabat.Domain
Talabat.Domain         -> no project dependencies
```

Clean Architecture alignment:

- `Application -> Domain`: confirmed.
- `Domain -> no dependencies`: confirmed.
- `Infrastructure -> Application`: confirmed.
- `API -> Infrastructure/Application`: partially confirmed. `Talabat.API` references `Talabat.Application`, but it does not currently reference `Talabat.Infrastructure`.

Dependency implications for Identity/Auth:

- The dependency direction is clean enough for adding auth.
- `Talabat.API` will eventually need access to infrastructure registration, usually through a `Talabat.Infrastructure` project reference and a DI extension method.
- `Talabat.Delivery.API` should follow the same API composition-root pattern.
- `Talabat.Identity` should stay outside the Domain model. Domain entities should store scalar identity linkage such as `IdentityUserId`, not `ApplicationUser` navigation properties.
- If `Talabat.Infrastructure` maps domain aggregates with EF Core, it may need direct access to Domain types. That is acceptable for an outer infrastructure layer, but the project references should be made explicit when implementation starts.

## 4. Domain Model Inventory

All domain source is under `src/Talabat/Talabat.Domain`.

### Customer Aggregate

- Namespace: `Talabat.Domain.Aggregates.Customer`
- Class: `Customer`
- Id type: `int`
- Base type: `AuditableEntity`
- Constructor/factory methods:
  - `public Customer(int id, string fullName, int age, string? phoneNumber = null)`
- Public methods:
  - `UpdateProfile(string fullName, int age, string? phoneNumber = null)`
  - `AddAddress(int addressId, Address address, bool makeDefault = false)`
  - `RemoveAddress(int addressId)`
  - `SetDefaultAddress(int addressId)`
  - `CreateDeliveryAddressSnapshot(int addressId)`
- Existing properties:
  - `Id`
  - `FullName`
  - `Age`
  - `PhoneNumber`
  - `Addresses`
  - inherited audit/soft-delete properties from `AuditableEntity`
- Identity linkage:
  - No `IdentityUserId`, `UserId`, `ApplicationUserId`, or similar property exists.
- Identity integration impact:
  - Adding a required `IdentityUserId` constructor parameter would break the current constructor signature.
  - Adding a new overload or static factory would be safer.
  - Current creation requires a positive `id`, required `fullName`, positive `age`, and optional normalized `phoneNumber`.
  - If database identity columns generate aggregate IDs, the current constructor shape is awkward because it requires a positive ID before persistence.

Related entity:

- `CustomerAddress`
  - Namespace: `Talabat.Domain.Aggregates.Customer`
  - Id type: `int`
  - Constructor: `internal CustomerAddress(int id, Address details, bool isDefault)`
  - Properties: `Id`, `Details`, `IsDefault`
  - Methods: `MarkAsDefault()`, `MarkAsNonDefault()`

### DeliveryAgent Aggregate

- Namespace: `Talabat.Domain.Aggregates.DeliveryManagement`
- Class: `DeliveryAgent`
- Id type: `int`
- Base type: `AuditableEntity`
- Constructor/factory methods:
  - `public DeliveryAgent(int id, string fullName, VehicleType vehicleType, DateTime createdAt, string? phoneNumber = null, GeoLocation? currentLocation = null)`
- Public methods:
  - `IsAvailable()`
  - `GoOnline()`
  - `GoOffline()`
  - `Suspend()`
  - `UpdateLocation(GeoLocation location)`
- Internal methods:
  - `MarkBusy()`
  - `MarkAvailable()`
- Existing properties:
  - `Id`
  - `FullName`
  - `PhoneNumber`
  - `VehicleType`
  - `Status`
  - `CurrentLocation`
  - inherited audit/soft-delete properties
- Identity linkage:
  - No `IdentityUserId`, `UserId`, `ApplicationUserId`, or similar property exists.
- Identity integration impact:
  - Adding a required `IdentityUserId` constructor parameter would break the current constructor signature.
  - Current creation requires positive `id`, required `fullName`, valid `vehicleType`, UTC `createdAt`, optional phone number, and optional location.
  - A registration-specific factory such as `Register(...)` or `CreateRegistered(...)` would keep identity linkage explicit without coupling the aggregate to ASP.NET Identity.

Related enums:

- `VehicleType`: `Bike`, `Motorcycle`, `Car`
- `DeliveryAgentStatus`: `Offline`, `Available`, `Busy`, `Suspended`

### Delivery Aggregate

- Namespace: `Talabat.Domain.Aggregates.DeliveryManagement`
- Class: `Delivery`
- Id type: `int`
- Base type: `AuditableEntity`
- Constructor/factory methods:
  - `public Delivery(int id, int orderId, int customerId, int restaurantId, DeliveryAddressSnapshot deliveryAddress, DateTime createdAt)`
- Public methods:
  - `AssignAgent(int agentId, DateTime currentTime)`
  - `MarkArrivedAtRestaurant(int agentId, DateTime currentTime)`
  - `MarkPickedUp(int agentId, DateTime currentTime)`
  - `MarkOutForDelivery(int agentId, DateTime currentTime)`
  - `MarkDelivered(int agentId, DateTime currentTime)`
  - `Cancel(DateTime currentTime)`
  - `Fail(string reason, DateTime currentTime)`
  - `IsTerminal()`
  - `IsActive()`
- Internal methods:
  - `CancelAssigned(int agentId, DateTime currentTime)`
  - `FailAssigned(int agentId, string reason, DateTime currentTime)`
- Existing properties:
  - `Id`
  - `OrderId`
  - `CustomerId`
  - `RestaurantId`
  - `AssignedAgentId`
  - `Status`
  - `DeliveryAddress`
  - transition timestamps: `AssignedAt`, `ArrivedAtRestaurantAt`, `PickedUpAt`, `OutForDeliveryAt`, `DeliveredAt`, `CancelledAt`, `FailedAt`
  - `FailureReason`
  - inherited audit/soft-delete properties
- Identity linkage:
  - No identity user linkage. It links to `Customer` and `DeliveryAgent` by domain integer IDs.
- Identity integration impact:
  - No direct IdentityUserId should be needed here.
  - Authorization should resolve the current delivery agent profile from the authenticated `sub`, then use the domain `DeliveryAgent.Id` when calling delivery methods.

Related enum:

- `DeliveryStatus`: `PendingAssignment`, `Assigned`, `ArrivedAtRestaurant`, `PickedUp`, `OutForDelivery`, `Delivered`, `Cancelled`, `Failed`

### Cart Aggregate

- Namespace: `Talabat.Domain.Aggregates.Basket`
- Class: `Cart`
- Id type: `int`
- Base type: `AuditableEntity`
- Constructor/factory methods:
  - `private Cart(int id, int customerId, DateTime createdAt)`
  - `public static Cart Create(int id, int customerId, CatalogProductSnapshot firstProduct, int quantity, DateTime createdAt)`
- Public methods:
  - `IsExpired(DateTime currentTime)`
  - `AddItem(CatalogProductSnapshot productSnapshot, int quantity, DateTime currentTime)`
  - `UpdateQuantity(int productId, int quantity, DateTime currentTime)`
  - `RemoveItem(int productId, DateTime currentTime)`
  - `Clear(DateTime currentTime)`
  - `MarkCheckedOut(DateTime currentTime)`
  - `GetTotal(IReadOnlyDictionary<int, Money> currentPrices)`
- Existing properties:
  - `Id`
  - `CustomerId`
  - `RestaurantId`
  - `Status`
  - `Items`
  - inherited audit/soft-delete properties
- Identity linkage:
  - No identity user linkage. It uses domain `CustomerId`.
- Identity integration impact:
  - No direct IdentityUserId should be added to `Cart`.
  - Customer API should resolve the current `Customer.Id` from `ApplicationUser.Id` before creating or reading carts.

Related entity and enum:

- `CartItem`: `ProductId`, `ProductName`, `Quantity`, plus internal quantity and line total behavior.
- `CartStatus`: `Active`, `CheckedOut`, `Cleared`

### Order Aggregate

- Namespace: `Talabat.Domain.Aggregates.Ordering`
- Class: `Order`
- Id type: `int`
- Base type: `AuditableEntity`
- Constructor/factory methods:
  - `private Order(int id, int customerId, int restaurantId, List<OrderItem> items, DeliveryAddressSnapshot deliveryAddress, DateTime createdAt, Money totalAmount)`
  - `public static Order CreateFromCheckout(int id, int customerId, int restaurantId, IEnumerable<CheckoutItemSnapshot> checkoutItems, DeliveryAddressSnapshot deliveryAddress, DateTime currentTime)`
- Public methods:
  - `GetTotal()`
- Existing properties:
  - `Id`
  - `CustomerId`
  - `RestaurantId`
  - `DeliveryAddress`
  - `Items`
  - `TotalAmount`
  - inherited audit/soft-delete properties
- Identity linkage:
  - No identity user linkage. It uses domain `CustomerId`.
- Identity integration impact:
  - No direct IdentityUserId should be added to `Order`.
  - Registration only affects order creation through the availability of a linked `Customer` profile.

Related entity:

- `OrderItem`: `ProductId`, `ProductName`, `UnitPrice`, `Quantity`, `LineTotal`

### Restaurant Aggregate

- Namespace: `Talabat.Domain.Aggregates.Catalog`
- Class: `Restaurant`
- Id type: `int`
- Base type: `AuditableEntity`
- Constructor/factory methods:
  - `public Restaurant(int id, string name, string description, string? imageUrl, TimeRange openingHours, bool isActive = true)`
- Public methods:
  - `IsOpenAt(TimeOnly time)`
  - `Activate()`
  - `Deactivate()`
  - `AddProduct(int productId, string name, string description, Money currentPrice, string? imageUrl, bool isAvailable = true)`
  - `FindProduct(int productId)`
  - `UpdateProductPrice(int productId, Money currentPrice)`
  - `MarkProductAvailable(int productId)`
  - `MarkProductUnavailable(int productId)`
- Existing properties:
  - `Id`
  - `Name`
  - `Description`
  - `ImageUrl`
  - `OpeningHours`
  - `IsActive`
  - `Products`
  - inherited audit/soft-delete properties
- Identity linkage:
  - No owner identity linkage.
- Identity integration impact:
  - No change is required for initial `Customer` and `DeliveryAgent` auth.
  - The planned `RestaurantOwner` role will eventually need a domain model decision, such as owner profile aggregate or scalar owner identity field.

Related entity:

- `Product`: `Id`, `RestaurantId`, `Name`, `Description`, `CurrentPrice`, `IsAvailable`, `ImageUrl`

### Value Objects

Namespace: `Talabat.Domain.ValueObjects`

| Value object | Key data | Notes |
| --- | --- | --- |
| `Address` | `Street`, `City`, `BuildingNumber`, `Floor` | Implements value equality with case-insensitive string comparison. |
| `CatalogProductSnapshot` | `ProductId`, `RestaurantId`, `ProductName`, `IsAvailable` | Used by cart operations. |
| `CheckoutItemSnapshot` | `ProductId`, `ProductName`, `UnitPrice`, `Quantity` | Used by checkout/order creation. |
| `DeliveryAddressSnapshot` | `Street`, `City`, `BuildingNumber`, `Floor` | Used by orders and deliveries. |
| `GeoLocation` | `Latitude`, `Longitude` | Validates latitude and longitude ranges. |
| `Money` | `Amount` | Non-negative amount, `Zero`, `Add`, `Multiply`, comparable. |
| `TimeRange` | `Start`, `End` | Supports same-day and overnight ranges; rejects equal start/end. |

### Domain Exceptions

Namespace: `Talabat.Domain.Exceptions`

Base exception:

- `DomainException : Exception`

Concrete domain exceptions:

- `AddressNotFoundException`
- `AgentNotAvailableException`
- `CartExpiredException`
- `CartItemNotFoundException`
- `CartNotActiveException`
- `CartRestaurantMismatchException`
- `CrossRestaurantCartException`
- `CurrentProductPriceMissingException`
- `DeliveryAgentCoordinationRequiredException`
- `DeliveryAgentMismatchException`
- `DeliveryAlreadyAssignedException`
- `DeliveryAlreadyCompletedException`
- `DeliveryNotAssignedException`
- `DuplicateAddressException`
- `DuplicateProductException`
- `EmptyCartCheckoutException`
- `InvalidDeliveryAgentStatusTransitionException`
- `InvalidDeliveryStatusTransitionException`
- `InvalidDeliveryTimestampException`
- `InvalidQuantityException`
- `MissingDeliveryAddressException`
- `ProductNotFoundException`
- `ProductUnavailableException`
- `RestaurantClosedException`
- `RestaurantInactiveException`

### Domain Services

Namespace: `Talabat.Domain.DomainServices.DeliveryManagement`

- `DeliveryAssignmentDomainService`
  - `Assign(Delivery delivery, DeliveryAgent agent, DateTime currentTime)`
  - `CompleteDelivery(Delivery delivery, DeliveryAgent agent, DateTime currentTime)`
  - `CancelDelivery(Delivery delivery, DeliveryAgent agent, DateTime currentTime)`
  - `FailDelivery(Delivery delivery, DeliveryAgent agent, string reason, DateTime currentTime)`

Namespace: `Talabat.Domain.DomainServices.Checkout`

- `CheckoutDomainService`
  - `ValidateCheckout(Cart cart, Restaurant restaurant, DeliveryAddressSnapshot deliveryAddress, DateTime currentTime, TimeOnly restaurantLocalTime)`
- `CheckoutResult`
- `CheckoutSucceeded`
- `CheckoutProductsUnavailable`
- `UnavailableCheckoutItem`

### Interfaces

- `Talabat.Domain` has an `Interfaces\` folder include in the project file.
- No interface source files currently exist.
- No repository, unit-of-work, current-user, clock, or ID generator abstractions exist yet.

## 5. Identity Integration Impact

Planned linkage:

- `ApplicationUser.Id`: `string`
- `Customer.IdentityUserId`: `string`
- `DeliveryAgent.IdentityUserId`: `string`

Domain classes that need modification:

- `Customer`
  - Add scalar `IdentityUserId`.
  - Prefer required domain validation through a factory or constructor overload.
  - Avoid any reference to `ApplicationUser` or ASP.NET Identity packages.
- `DeliveryAgent`
  - Add scalar `IdentityUserId`.
  - Prefer required domain validation through a factory or constructor overload.
  - Avoid any reference to `ApplicationUser` or ASP.NET Identity packages.

Domain classes that should not get direct IdentityUserId:

- `Cart`, `Order`, and `Delivery` should continue using domain integer IDs.
- `Restaurant` does not need identity linkage for initial customer/delivery-agent auth.

Customer creation today:

- Requires `FullName`.
- Requires `Age` as a positive `int`.
- Allows `PhoneNumber` to be `null`.
- Requires caller-provided positive `Id`.

DeliveryAgent creation today:

- Requires `FullName`.
- Requires `VehicleType`.
- Requires UTC `createdAt`.
- Allows `PhoneNumber` to be `null`.
- Allows `CurrentLocation` to be `null`.
- Requires caller-provided positive `Id`.

Can the domain currently create registration profiles?

- Partially.
- It can create a `Customer` or `DeliveryAgent` if the application supplies valid profile data and a positive integer aggregate ID.
- It cannot link either profile to an ASP.NET Identity user because `IdentityUserId` does not exist.
- It has no registration-specific factory methods.
- It has no ID-generation abstraction or persistence model to resolve the positive integer ID requirement.

Missing domain/application pieces:

- `Customer.IdentityUserId` and `DeliveryAgent.IdentityUserId`.
- Factories or constructor overloads for registered profile creation.
- Clear ID-generation strategy for int aggregate IDs.
- Application use cases for `RegisterCustomer` and `RegisterDeliveryAgent`.
- Repository abstractions and implementations.
- Unit-of-work or transaction boundary that can coordinate Identity and domain profile creation.

## 6. Infrastructure Status

`Talabat.Infrastructure` currently contains no production source files beyond `Talabat.Infrastructure.csproj`.

Findings:

- DbContext exists: no.
- EF Core installed: no direct EF Core package references.
- Repository implementations exist: no.
- Migrations exist: no.
- Connection strings exist: no connection strings found in API appsettings files.
- Seed data exists: no.
- Source code exists in Infrastructure: no source files were found outside generated `bin` and `obj` output.

This means Identity integration will require foundational infrastructure work before auth can create linked domain profiles.

## 7. API Status

Project: `src/Talabat/Talabat.API`

### `Program.cs`

Current setup:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
```

Observations:

- Controllers are enabled.
- OpenAPI is enabled via `AddOpenApi()` and `MapOpenApi()` only in Development.
- HTTPS redirection is enabled.
- `UseAuthorization()` is present.
- There is no `AddAuthentication()`.
- There is no `UseAuthentication()`.
- There is no `AddAuthorization()` policy setup.
- There is no JWT bearer setup.
- There is no IdentityServer integration.
- There is no Swagger UI package or configuration.

### Controllers or Minimal APIs

- `WeatherForecastController` exists.
- Route: `[Route("[controller]")]`
- Endpoint: `GET /WeatherForecast`
- No domain/business endpoints exist.
- No minimal API endpoints exist except framework/OpenAPI mapping.

### Appsettings

`appsettings.json`:

- Logging config.
- `AllowedHosts: "*"`
- No `ConnectionStrings`.
- No auth settings.

`appsettings.Development.json`:

- Logging config only.
- No `ConnectionStrings`.
- No auth settings.

### Launch Settings

Profiles:

- `http`
  - `applicationUrl`: `http://localhost:5213`
  - `ASPNETCORE_ENVIRONMENT`: `Development`
- `https`
  - `applicationUrl`: `https://localhost:7056;http://localhost:5213`
  - `ASPNETCORE_ENVIRONMENT`: `Development`

## 8. Delivery API Plan

Recommended project:

- Name: `Talabat.Delivery.API`
- Type: ASP.NET Core Web API project.
- Target framework: `net10.0`, matching the solution.
- Role: separate API surface for delivery agents.

Recommended references:

- `Talabat.Application`
- `Talabat.Infrastructure` once infrastructure DI registration exists
- Do not reference `Talabat.Identity` unless a shared contract package is intentionally introduced.
- Do not reference `Talabat.Domain` directly from controllers unless the existing API style changes; prefer Application use cases/DTOs.

Recommended initial endpoints:

- `GET /api/delivery/secure-test`
  - Requires authenticated user.
  - Requires `DeliveryAgent` role.
  - Returns a simple success payload containing `sub` and roles for token validation.
- `GET /api/delivery/me`
  - Requires authenticated user.
  - Requires `DeliveryAgent` role.
  - Resolves the current delivery agent profile by `IdentityUserId == User.FindFirst("sub").Value`.
  - Returns delivery-agent profile data such as domain agent ID, full name, status, vehicle type, phone number, and current location.

## 9. IdentityServer4 + .NET 10 Compatibility Spike

Scratch project path:

```text
D:\link-dev\talabat\.codex-scratch\ids4-net10-compat
```

Created with:

```text
dotnet new web --framework net10.0 --no-restore
```

The scratch project was not added to `Talabat.slnx`.

### Package Add Behavior

Attempting to add IdentityServer4 without a version failed:

```text
dotnet add package IdentityServer4
error: There are no versions available for the package 'IdentityServer4'.
```

Adding the known final version explicitly succeeded:

```text
dotnet add package IdentityServer4 --version 4.1.2
```

The same pattern occurred for `IdentityServer4.AspNetIdentity`:

```text
dotnet add package IdentityServer4.AspNetIdentity
error: There are no versions available for the package 'IdentityServer4.AspNetIdentity'.

dotnet add package IdentityServer4.AspNetIdentity --version 4.1.2
```

### Direct Package Versions Resolved

From `dotnet list package --include-transitive`:

| Top-level package | Requested | Resolved |
| --- | --- | --- |
| `IdentityServer4` | `4.1.2` | `4.1.2` |
| `IdentityServer4.AspNetIdentity` | `4.1.2` | `4.1.2` |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | `10.0.9` | `10.0.9` |
| `Microsoft.EntityFrameworkCore.Design` | `10.0.9` | `10.0.9` |
| `Microsoft.EntityFrameworkCore.SqlServer` | `10.0.9` | `10.0.9` |

Key transitive packages resolved:

| Transitive package | Resolved |
| --- | --- |
| `IdentityModel` | `4.4.0` |
| `IdentityServer4.Storage` | `4.1.2` |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | `3.1.0` |
| `Microsoft.Data.SqlClient` | `6.1.1` |
| `Microsoft.EntityFrameworkCore` | `10.0.9` |
| `Microsoft.EntityFrameworkCore.Relational` | `10.0.9` |
| `Microsoft.IdentityModel.JsonWebTokens` | `7.7.1` |
| `Microsoft.IdentityModel.Protocols` | `7.7.1` |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect` | `7.7.1` |
| `Microsoft.IdentityModel.Tokens` | `7.7.1` |
| `Newtonsoft.Json` | `13.0.3` |
| `System.IdentityModel.Tokens.Jwt` | `7.7.1` |

### Minimal Compile Test

The scratch `Program.cs` was updated to compile a minimal IdentityServer4 setup:

```csharp
using IdentityServer4.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryIdentityResources(Array.Empty<IdentityResource>())
    .AddInMemoryApiScopes(Array.Empty<ApiScope>())
    .AddInMemoryClients(Array.Empty<Client>());

var app = builder.Build();

app.UseIdentityServer();

app.MapGet("/", () => "Hello World!");

app.Run();
```

### Restore Result

Final `dotnet restore` succeeded with IdentityServer4 vulnerability warnings:

```text
D:\link-dev\talabat\.codex-scratch\ids4-net10-compat\ids4-net10-compat.csproj : warning NU1902: Package 'IdentityServer4' 4.1.2 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-55p7-v223-x366
D:\link-dev\talabat\.codex-scratch\ids4-net10-compat\ids4-net10-compat.csproj : warning NU1902: Package 'IdentityServer4' 4.1.2 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-ff4q-64jc-gx98
Restored D:\link-dev\talabat\.codex-scratch\ids4-net10-compat\ids4-net10-compat.csproj
```

### Build Result

Final `dotnet build` succeeded with warnings:

```text
Build succeeded.

D:\link-dev\talabat\.codex-scratch\ids4-net10-compat\ids4-net10-compat.csproj : warning NU1902: Package 'IdentityServer4' 4.1.2 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-55p7-v223-x366
D:\link-dev\talabat\.codex-scratch\ids4-net10-compat\ids4-net10-compat.csproj : warning NU1902: Package 'IdentityServer4' 4.1.2 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-ff4q-64jc-gx98
    4 Warning(s)
    0 Error(s)
```

### Compatibility Conclusion

IdentityServer4 appears technically usable at the package restore and compile level in a `net10.0` ASP.NET Core project, including a minimal `AddIdentityServer()` and `UseIdentityServer()` setup.

However, this does not prove production suitability:

- The final IdentityServer4 version is `4.1.2`.
- The unversioned package add failed, so exact version pinning is required.
- The package reports known moderate vulnerabilities.
- IdentityServer4 depends on old ASP.NET Core 3.1-era packages such as `Microsoft.AspNetCore.Authentication.OpenIdConnect` `3.1.0`.
- Runtime behavior, token issuance, ASP.NET Identity integration, EF stores, migrations, persisted grants, signing credentials, refresh tokens, and Swagger/Postman flows were not validated in this spike.
- The archived repository states the project is not maintained and went out of support when .NET Core 3.1 reached end of support. See the archived repository at [DuendeArchive/IdentityServer4](https://github.com/DuendeArchive/IdentityServer4) and Duende's 2025 archive note [IdentityServer4 is public again](https://duendesoftware.com/blog/20250306-identityserver4-public-again).

## 10. Proposed Identity Project Design

Project name:

- `Talabat.Identity`

Project type:

- ASP.NET Core Web/API host.
- It should own ASP.NET Identity registration/login/token endpoints and IdentityServer4 hosting.

Target framework recommendation:

- Use `net10.0` if the solution must remain homogeneous.
- Pin IdentityServer4 packages explicitly to `4.1.2`.
- Treat this as a risk acceptance decision because IdentityServer4 is archived and unsupported.

Required packages:

- `IdentityServer4` `4.1.2`
- `IdentityServer4.AspNetIdentity` `4.1.2`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` `10.0.9`
- `Microsoft.EntityFrameworkCore.SqlServer` `10.0.9`
- `Microsoft.EntityFrameworkCore.Design` `10.0.9`
- Consider `IdentityServer4.EntityFramework` `4.1.2` if clients, resources, scopes, persisted grants, or operational data should be stored in SQL Server instead of in-memory config.

DbContext recommendation:

- `TalabatIdentityDbContext : IdentityDbContext<ApplicationUser>`
- Use the same SQL Server physical database as business data.
- Prefer a separate schema such as `identity` for ASP.NET Identity tables.
- If IdentityServer4 EF stores are used, keep configuration and persisted-grant contexts explicit and use the same connection string.

ApplicationUser location:

- `Talabat.Identity/Users/ApplicationUser.cs`

Suggested `ApplicationUser` shape:

- Inherit from `IdentityUser`.
- Keep identity/account fields only.
- Do not add navigation properties to domain aggregates.
- Domain profile linkage should live on domain profiles as scalar `IdentityUserId`.

Roles:

- `Customer`
- `DeliveryAgent`
- `Admin`
- `RestaurantOwner`

Initial implemented roles:

- `Customer`
- `DeliveryAgent`

## 11. Auth Flows

### Postman

Recommended for development:

- Resource Owner Password Credentials grant can be used only for local development/testing if IdentityServer4 is kept and real users are registered.
- Use a confidential Postman client with a client secret.
- Request scopes for the API being tested.
- Do not treat password grant as the frontend strategy.

### Swagger

Recommended:

- For API docs/testing, prefer Authorization Code with PKCE if Swagger UI is introduced.
- If the project keeps the current `Microsoft.AspNetCore.OpenApi` setup, there is no Swagger UI yet; only the OpenAPI document is mapped.
- A temporary password-grant Swagger client may be acceptable for local API testing, but should not be the long-term browser-based flow.

### Later Angular Frontend

Recommended:

- Authorization Code Flow with PKCE.
- Public SPA client.
- No client secret in Angular.
- Use short-lived access tokens.
- Prefer refresh token rotation only if explicitly needed and configured securely.

Password grant suitability:

- Suitable only for local development/testing with Postman or temporary tooling.
- Not recommended for Angular or production user-facing clients.

## 12. API Resources and Scopes

Recommended names:

| Item | Proposed name |
| --- | --- |
| Customer API resource | `talabat.customer-api` |
| Delivery API resource | `talabat.delivery-api` |
| Customer API scope | `talabat.customer-api.full_access` |
| Delivery API scope | `talabat.delivery-api.full_access` |
| Postman client | `talabat.postman` |
| Swagger customer client | `talabat.swagger.customer` |
| Swagger delivery client | `talabat.swagger.delivery` |
| Future Angular customer client | `talabat.angular.customer` |
| Future Angular delivery client | `talabat.angular.delivery` |

Authorization policy recommendation:

- Customer API policy should require:
  - authenticated user
  - `scope` contains `talabat.customer-api.full_access`
  - `role` contains `Customer`
- Delivery API policy should require:
  - authenticated user
  - `scope` contains `talabat.delivery-api.full_access`
  - `role` contains `DeliveryAgent`

## 13. Register Flow Design

### Register Customer

Input should include:

- Email or username
- Password
- Full name
- Age
- Phone number, optional unless business decides otherwise

Flow:

1. Validate request in the Identity/API layer.
2. Create `ApplicationUser`.
3. Persist the user through `UserManager<ApplicationUser>`.
4. Assign role `Customer`.
5. Create a matching `Customer` domain profile with:
   - int domain ID
   - `IdentityUserId = ApplicationUser.Id`
   - `FullName`
   - `Age`
   - optional `PhoneNumber`
6. Save Identity and business profile data in the same SQL Server physical database.
7. Return an API result such as:
   - `userId`
   - `customerId`
   - `email`
   - `roles`
   - optional token response if login-after-register is desired.

Missing pieces before this can be implemented properly:

- `ApplicationUser`
- `TalabatIdentityDbContext`
- business DbContext for domain profiles
- `Customer.IdentityUserId`
- Customer repository
- ID generation strategy for `Customer.Id`
- application use case/result model
- transaction boundary coordinating Identity and profile creation

### Register DeliveryAgent

Input should include:

- Email or username
- Password
- Full name
- Phone number, optional or required based on business rules
- Vehicle type

Flow:

1. Validate request in the Identity/API layer.
2. Create `ApplicationUser`.
3. Persist the user through `UserManager<ApplicationUser>`.
4. Assign role `DeliveryAgent`.
5. Create a matching `DeliveryAgent` domain profile with:
   - int domain ID
   - `IdentityUserId = ApplicationUser.Id`
   - `FullName`
   - `PhoneNumber`
   - `VehicleType`
   - UTC `CreatedAt`
   - default status `Offline`
6. Save Identity and business profile data in the same SQL Server physical database.
7. Return an API result such as:
   - `userId`
   - `deliveryAgentId`
   - `email`
   - `roles`
   - `status`
   - optional token response if login-after-register is desired.

Missing pieces before this can be implemented properly:

- `DeliveryAgent.IdentityUserId`
- DeliveryAgent repository
- ID generation strategy for `DeliveryAgent.Id`
- application use case/result model
- transaction boundary coordinating Identity and profile creation

Same physical database recommendation:

- Use one SQL Server database.
- Keep Identity tables and business tables separated by schema.
- If using multiple DbContexts, coordinate writes with an explicit transaction or `TransactionScope`.
- Do not add IdentityUser navigation properties to Domain entities.

## 14. Security/Authorization Design

Required token claims:

- `sub`: stable ASP.NET Identity user ID (`ApplicationUser.Id`)
- `role`: user roles such as `Customer` or `DeliveryAgent`
- `scope`: API scopes
- `aud`: API resource/audience
- `email`: useful for diagnostics and profile display, if allowed
- `name` or `preferred_username`: optional

Role claim type:

- Emit roles as `role`.
- Configure API JWT validation with `TokenValidationParameters.RoleClaimType = "role"` if needed.

`sub` usage:

- Treat `sub` as the canonical identity account key.
- Store `sub`/`ApplicationUser.Id` in `Customer.IdentityUserId` and `DeliveryAgent.IdentityUserId`.
- Resolve domain profile IDs from `sub`; do not put ASP.NET Identity types in Domain.

Customer API authorization:

- Protect customer endpoints with a policy requiring:
  - authenticated JWT
  - customer API scope
  - `Customer` role
- For profile-specific operations, resolve `Customer` by `IdentityUserId == sub` and use its integer `Customer.Id` for domain operations.

Delivery API authorization:

- Protect delivery endpoints with a policy requiring:
  - authenticated JWT
  - delivery API scope
  - `DeliveryAgent` role
- Resolve `DeliveryAgent` by `IdentityUserId == sub` and use its integer `DeliveryAgent.Id` for delivery assignment/status operations.

Application layer current-user abstraction:

- Add an abstraction in `Talabat.Application`, for example:
  - `ICurrentUser`
  - `UserId` / `IdentityUserId`
  - `Roles`
  - `IsAuthenticated`
- Implement it in each API host using `HttpContext.User`.
- Do not inject `HttpContext` directly into application use cases.
- Domain should not depend on current user, claims, `HttpContext`, Identity, or JWT packages.

## 15. Recommended Implementation Phases

Phase 1: Add Identity project and compatibility setup

- Add `Talabat.Identity` as a separate project.
- Pin IdentityServer4 packages explicitly.
- Add minimal health/openid configuration compile path.
- Do not wire production APIs yet.

Phase 2: Add ASP.NET Identity database

- Add `ApplicationUser`.
- Add `TalabatIdentityDbContext`.
- Add SQL Server connection string.
- Add Identity migrations.
- Create initial roles `Customer` and `DeliveryAgent`.

Phase 3: Add IdentityServer config

- Define API resources, scopes, and clients.
- Use developer signing credentials only for local development.
- Decide whether IdentityServer config is in-memory or EF-backed.

Phase 4: Add register endpoints

- Add customer registration.
- Add delivery-agent registration.
- Assign roles.
- Create linked domain profiles.
- Return API-friendly results.

Phase 5: Add token testing with Postman

- Add Postman client.
- Test password grant only for local development.
- Validate `sub`, `role`, `scope`, and `aud` claims.

Phase 6: Protect Customer API

- Add JWT bearer authentication to `Talabat.API`.
- Add customer authorization policy.
- Protect a small test endpoint first.
- Then protect real customer endpoints as they are added.

Phase 7: Add Delivery API

- Add `Talabat.Delivery.API`.
- Add JWT bearer authentication.
- Add delivery-agent authorization policy.
- Add `GET /api/delivery/secure-test` and `GET /api/delivery/me`.

Phase 8: Add CurrentUser abstraction

- Add current-user abstraction in Application.
- Implement it in API hosts.
- Use it in application use cases instead of `HttpContext`.

Phase 9: Link Identity users to domain profiles

- Add `IdentityUserId` to `Customer` and `DeliveryAgent`.
- Add unique constraints/indexes.
- Add repository queries by `IdentityUserId`.
- Ensure registration creates both identity account and domain profile in one transaction.

## 16. Risks and Questions

### Risks

- IdentityServer4 is unsupported/archived.
  - The archived GitHub repository states it is not maintained and went out of support with .NET Core 3.1 end of support.
  - Duende publicly restored it as a read-only archive in 2025.
  - References: [DuendeArchive/IdentityServer4](https://github.com/DuendeArchive/IdentityServer4), [Duende archive note](https://duendesoftware.com/blog/20250306-identityserver4-public-again).
- IdentityServer4 package vulnerabilities.
  - Scratch restore/build reports `NU1902` warnings for `IdentityServer4` `4.1.2`.
- .NET 10 compatibility is not guaranteed by restore/build alone.
  - Minimal compile works.
  - Runtime token issuance, persisted grants, Identity integration, external login, refresh tokens, and production certs still need testing.
- Password grant should be local/testing only.
  - It is not suitable for the future Angular frontend.
- Same physical database with multiple DbContexts needs careful transaction handling.
  - Registration must not leave an Identity account without a domain profile, or a profile without an Identity account.
- Domain purity risk.
  - Do not add `ApplicationUser` navigation properties to Domain.
  - Keep linkage as scalar `IdentityUserId`.
- Current Domain constructors require positive integer IDs.
  - This may conflict with SQL identity column generation.
  - A decision is needed before implementing EF mappings and registration.
- Infrastructure is empty.
  - Auth registration depends on DbContext, repositories, migrations, connection strings, and transaction strategy that do not exist yet.
- API has no authentication setup.
  - `UseAuthorization()` exists, but JWT bearer authentication and policies do not.
- Current OpenAPI setup is not Swagger UI.
  - Swagger auth testing will require adding/configuring a UI package or using generated OpenAPI with another client.
- Existing API package warning.
  - `Microsoft.OpenApi` `2.0.0` is flagged with high severity vulnerability via `Microsoft.AspNetCore.OpenApi`.

### Questions Before Implementation

- Is the team explicitly accepting IdentityServer4's unsupported/archived status for this project?
- Should supported alternatives such as Duende IdentityServer or OpenIddict be considered before committing to IdentityServer4?
- Should domain aggregate IDs be application-assigned integers or database-generated identity columns?
- Should `Customer.PhoneNumber` be required during registration, even though the domain currently allows it to be optional?
- Should `DeliveryAgent.PhoneNumber` be required during registration, even though the domain currently allows it to be optional?
- Should registration immediately issue tokens, or require a separate login call?
- Should IdentityServer clients/resources/scopes be stored in code, seed data, or SQL tables?
- Which schema names should separate Identity, IdentityServer operational data, and business data in the shared SQL Server database?
- Should `RestaurantOwner` have a domain profile now, or remain a future role only?
- What is the production signing credential strategy?
- What exact Angular apps are planned: one customer SPA, one delivery SPA, or a shared portal with role-based routing?
- What account lifecycle rules are required: email confirmation, phone confirmation, lockout, password policy, password reset, refresh token lifetime, and account deletion?
