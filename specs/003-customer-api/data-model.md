# Data Model: Talabat Customer API (Phase 7)

**Date**: 2026-07-16  
**Spec**: [spec.md](spec.md)

## Domain Changes (Phase 7 Only)

### Customer Aggregate — New Property

| Property | Type | Nullable | Description |
|----------|------|----------|-------------|
| `IdentityUserId` | `string` | Yes | Framework-neutral linkage key from the Identity `sub` claim. Unique-indexed when non-null. Provisional — Phase 9 may change the linkage strategy. |

Creation path: add a domain factory `Customer.CreateForAccount(string identityUserId, string fullName, int age, string? phoneNumber)` that applies the existing `Guard.RequiredText(fullName)` / `Guard.Positive(age)` checks and sets `IdentityUserId`. `Customer` invariants are unchanged — a full name and a positive age remain required, so no empty or placeholder profile can be created. No other Domain changes; all other entities, value objects, and repositories are stable from Phase 4/5.

### ICustomerRepository — New Method

| Method | Signature | Description |
|--------|-----------|-------------|
| `GetByIdentityUserIdAsync` | `Task<Customer?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken)` | Lookup by Identity linkage key for read-only account->profile resolution and the create-profile existence check. |

## Application Layer Changes

### New Abstraction: ICurrentUser

| Property | Type | Description |
|----------|------|-------------|
| `IdentityUserId` | `string` | The `sub` claim from the validated token |
| `IsAuthenticated` | `bool` | Whether the request carries a valid token |
| `HasProfile` | `bool` | Whether a `Customer` exists for this account (an `IdentityUserId` match) |
| `CustomerId` | `int?` | The resolved `Customer.Id`, or `null` when no profile has been created yet |

Location: `Talabat.Application/Abstractions/ICurrentUser.cs`. Resolution is **read-only**: the API host
reads the `sub` claim and calls `ICustomerRepository.GetByIdentityUserIdAsync` to populate
`HasProfile`/`CustomerId`. It MUST NOT create a `Customer` as a side effect.

### New Use Case: CreateCustomerProfileHandler

| Field | Value |
|-------|-------|
| Namespace | `Talabat.Application.Customers.CreateProfile` |
| Command | `CreateCustomerProfileCommand(string IdentityUserId, string FullName, int Age, string? PhoneNumber)` |
| Result | `UseCaseResult<int>` (the new `Customer.Id`) |
| Behavior | Looks up by `IdentityUserId`; if a profile already exists → failure `Conflict` (`ProfileAlreadyExists`). Otherwise creates the `Customer` via `Customer.CreateForAccount(...)` with the supplied name/age/phone, saves, and returns the new `Id`. Never creates an empty profile. |
| Concurrency | The unique index on `IdentityUserId` guards a double-create race; a unique-constraint violation is mapped to `Conflict` (`ProfileAlreadyExists`). |

### Existing Handlers — No Signature Changes

All 15 existing handlers accept explicit `customerId` parameters in their commands/queries. The API
layer resolves `ICurrentUser`; when `HasProfile` is `false` it short-circuits owner-scoped requests
with `409 Conflict` (`ProfileNotCreated`) before invoking any handler. When a profile exists, the API
passes `ICurrentUser.CustomerId` into the existing command constructors. No handler interface changes
are required.

| Handler | Command/Query | CustomerId Source |
|---------|---------------|-------------------|
| BrowseRestaurantsHandler | (no customerId) | N/A — anonymous |
| GetRestaurantMenuHandler | (no customerId) | N/A — anonymous |
| GetCartHandler | `GetCartQuery(int CustomerId)` | `ICurrentUser.CustomerId` |
| AddCartItemHandler | `AddCartItemCommand(int CustomerId, ...)` | `ICurrentUser.CustomerId` |
| UpdateCartItemQuantityHandler | `UpdateCartItemQuantityCommand(int CustomerId, ...)` | `ICurrentUser.CustomerId` |
| RemoveCartItemHandler | `RemoveCartItemCommand(int CustomerId, ...)` | `ICurrentUser.CustomerId` |
| ClearCartHandler | `ClearCartCommand(int CustomerId)` | `ICurrentUser.CustomerId` |
| GetCustomerProfileHandler | `GetCustomerProfileQuery(int CustomerId)` | `ICurrentUser.CustomerId` |
| UpdateCustomerProfileHandler | `UpdateCustomerProfileCommand(int CustomerId, ...)` | `ICurrentUser.CustomerId` |
| AddCustomerAddressHandler | `AddCustomerAddressCommand(int CustomerId, ...)` | `ICurrentUser.CustomerId` |
| RemoveCustomerAddressHandler | `RemoveCustomerAddressCommand(int CustomerId, ...)` | `ICurrentUser.CustomerId` |
| SetDefaultCustomerAddressHandler | `SetDefaultCustomerAddressCommand(int CustomerId, ...)` | `ICurrentUser.CustomerId` |
| CheckoutHandler | `CheckoutCommand(int CustomerId, int DeliveryAddressId)` | `ICurrentUser.CustomerId` |
| GetOrderHistoryHandler | `GetOrderHistoryQuery(int CustomerId)` | `ICurrentUser.CustomerId` |
| GetOrderDetailsHandler | `GetOrderDetailsQuery(int OrderId, int CustomerId)` | `ICurrentUser.CustomerId` |

## Infrastructure Changes

### Customer EF Configuration Update

Add `IdentityUserId` column mapping with a unique filtered index (`WHERE IdentityUserId IS NOT NULL`).

### New Repository Method

`CustomerRepository.GetByIdentityUserIdAsync` — EF query filtered by `IdentityUserId`.

### Migration

One migration adding `IdentityUserId` column (nullable `nvarchar(450)`) with a unique filtered
index to the `Customers` table.

## API Layer — New Types

### Controllers

| Controller | Route Prefix | Auth | Endpoints |
|-----------|-------------|------|-----------|
| `CatalogController` | `/api/catalog` | Anonymous | GET `/restaurants`, GET `/restaurants/{id}/menu` |
| `CartController` | `/api/me/cart` | `[Authorize]` | GET, POST `/items`, PUT `/items/{productId}`, DELETE `/items/{productId}`, DELETE |
| `CustomerController` | `/api/me/profile` | `[Authorize]` | GET, PUT |
| `AddressController` | `/api/me/addresses` | `[Authorize]` | POST, DELETE `/{id}`, PUT `/{id}/default` |
| `CheckoutController` | `/api/me/checkout` | `[Authorize]` | POST |
| `OrderController` | `/api/me/orders` | `[Authorize]` | GET, GET `/{id}` |

### Request/Response DTOs

Each controller has its own request/response DTOs in `Talabat.Customer.API/Contracts/`. These map
to/from Application commands, queries, and read models. They are never shared with other projects.

### CurrentUser Implementation

`Talabat.Customer.API/Auth/CurrentUser.cs` — scoped service implementing `ICurrentUser`, reading
`sub` from `HttpContext.User` and calling `ICustomerRepository.GetByIdentityUserIdAsync` once per
request (read-only) to populate `HasProfile`/`CustomerId`. It does not create a `Customer`.

### Exception Handler

`Talabat.Customer.API/Middleware/DomainExceptionHandler.cs` — implements `IExceptionHandler`,
catches `DomainException` subtypes, and converts them to `ProblemDetails`.

### UseCaseResult → IActionResult Extension

`Talabat.Customer.API/Extensions/UseCaseResultExtensions.cs` — maps `ApplicationErrorCategory` to
HTTP status codes, converting `UseCaseResult<T>` to `Ok(value)`, `BadRequest(problem)`,
`NotFound(problem)`, etc.
