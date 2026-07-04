# Talabat DDD — Implementation Plan

> **Project:** Talabat-like Food Delivery Backend
> **Architecture:** Clean Architecture + Domain-Driven Design
> **Stack:** ASP.NET Core · C# · EF Core Code First · SQL Server · Identity
> **Goal:** Learning DDD properly through a practical, buildable MVP

---

## Phase 1 — Strategic Design

> 🎯 **No code in this phase.** We design on paper first.

---

### 1.1 Validate Requirements

**Objective:** Turn every business rule into a testable Given/When/Then statement.

**What we'll do:**
1. Extract all business rules from the prompt into a flat checklist.
2. Rewrite each as a testable scenario:
   - *Given* a cart older than 1 hour, *When* customer adds an item, *Then* reject with `CartExpiredException`.
   - *Given* a cart with Restaurant A items, *When* customer adds a Restaurant B product, *Then* reject with `CrossRestaurantCartException`.
   - *Given* a product price changed after being added to cart, *When* customer views the cart or checks out, *Then* use the current Catalog price.
3. Identify edge cases and document decisions for each:
   - What if restaurant closes *during* checkout?
   - What if a product is deleted while sitting in a cart?
   - What if the customer has an expired cart and tries to checkout?

**Deliverable:** `docs/business-rules.md` — a complete list of ~15-20 testable business rules.

**Why this matters:** Every unit test we write later maps directly to one of these rules. If a rule isn't documented here, it won't be tested.

---

### 1.2 Define Bounded Contexts

**Objective:** Draw the boundaries of our system — where each "language" lives.

**What we'll do:**
1. Define 4 bounded contexts and what each one owns:

```
┌─────────────────────────────────────────────────────┐
│                    TALABAT MVP                       │
│                                                     │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ CATALOG   │  │ BASKET   │  │ ORDERING │          │
│  │           │  │          │  │          │          │
│  │ Restaurant│  │ Cart     │  │ Order    │          │
│  │ Product   │  │ CartItem │  │ OrderItem│          │
│  └─────┬────┘  └────┬─────┘  └────┬─────┘          │
│        │             │             │                 │
│        └──────┬──────┘             │                 │
│               │ reads prices       │ validates &     │
│               │                    │ creates order   │
│        ┌──────┴──────────┐         │                 │
│        │    IDENTITY     ├─────────┘                 │
│        │                 │  CustomerId ref            │
│        │ Customer        │                           │
│        │ CustomerAddress │                           │
│        └─────────────────┘                           │
└─────────────────────────────────────────────────────┘
```

2. Document how contexts communicate:
   - Basket reads product identity, restaurant, name, and availability from Catalog when adding items. Application/read flows load current prices separately.
   - Ordering reads from Basket + Catalog at checkout time.
   - Identity provides CustomerId — referenced by Basket and Ordering but not owned by them.

**Deliverable:** Context Map diagram (above) + a table of context responsibilities.

**Key DDD learning:** "Product" means different things in different contexts. Catalog owns current product data and price. Basket stores only a selected product reference and quantity.

---

### 1.3 Define Aggregates & Invariants

**Objective:** Decide what gets loaded/saved together and what rules each aggregate enforces.

**What we'll do:**
1. Map out 4 aggregate roots with their children:

| Aggregate Root | Children | Key Invariants |
|---------------|----------|---------------|
| `Restaurant` | `Product` | Products belong to exactly one restaurant. Restaurant has opening hours. |
| `Cart` | `CartItem` | One active cart per customer. All items from same restaurant. Cart expires after 1 hour. Quantity > 0. Duplicate products merge. |
| `Order` | `OrderItem` | Immutable after creation. Stores price snapshots. |
| `Customer` | `CustomerAddress` | Required full name, positive age, optional phone. Multiple addresses. One default address. |

2. Write down the 8 business invariants that MUST live in the domain:
   1. One active cart per customer
   2. Cart belongs to one restaurant
   3. All CartItems from same restaurant
   4. Quantity > 0
   5. Duplicate products increase quantity
   6. Expired carts cannot be modified
   7. Checkout uses current Catalog prices
   8. Orders store immutable prices

**Deliverable:** Aggregate boundary diagrams + invariant list.

**Rule:** Access children ONLY through the aggregate root. Never `dbContext.CartItems.Where(...)` — always load the `Cart` first.

---

## Phase 2 — Tactical Design

> 🎯 Design every class, method, and exception before writing C# code.

---

### 2.1 Design Entities

**Objective:** Define all 8 entities with properties, methods, and encapsulation rules.

**What we'll do — design each entity:**

#### Restaurant (Aggregate Root)
```
Properties:  Id, Name, Description, OpensAt (TimeOnly), ClosesAt (TimeOnly), IsActive
             Products (IReadOnlyCollection<Product>)
Methods:     IsCurrentlyOpen() → bool
             Deactivate() → void
             AddProduct(name, description, price) → Product
             RemoveProduct(productId) → void
Constructor: Restaurant(name, description, opensAt, closesAt)
Rules:       - Private setter on all properties
             - Products exposed as IReadOnlyCollection (no external Add)
```

#### Product (Entity, child of Restaurant)
```
Properties:  Id, Name, Description, Price (Money), IsAvailable, RestaurantId
Methods:     UpdatePrice(newPrice) → void
             MarkUnavailable() → void
             MarkAvailable() → void
Constructor: internal Product(name, description, price, restaurantId)
Rules:       - Constructor is internal (only Restaurant.AddProduct creates products)
             - Price uses Money value object
```

#### Cart (Aggregate Root)
```
Properties:  Id, CustomerId, RestaurantId, CreatedAt (DateTime), 
             Items (IReadOnlyCollection<CartItem>)
Methods:     AddItem(product, quantity) → void  [CORE METHOD - enforces all cart rules]
             RemoveItem(productId) → void
             UpdateItemQuantity(productId, newQuantity) → void
             IsExpired() → bool
             Clear() → void
             GetTotal(currentPrices) → Money
Constructor: Cart(customerId, restaurantId)
Rules:       - CreatedAt set once in constructor (UTC)
             - Every mutation checks IsExpired() first
```

#### CartItem (Entity, child of Cart)
```
Properties:  Id, ProductId, ProductName, Quantity (int)
Methods:     IncreaseQuantity(amount) → void
             SetQuantity(newQuantity) → void
Constructor: internal CartItem(productId, productName, quantity)
Rules:       - Quantity always > 0
             - No product price is stored in CartItem
```

#### Order (Aggregate Root)
```
Properties:  Id, CustomerId, RestaurantId, OrderDate, Status (enum), 
             TotalAmount (Money), DeliveryAddressId,
             Items (IReadOnlyCollection<OrderItem>)
Methods:     None that mutate (immutable after creation for MVP)
Constructor: Order(customerId, restaurantId, items, deliveryAddressId)
Rules:       - TotalAmount calculated from items in constructor
             - No public setters at all
             - Status starts as Pending
```

#### OrderItem (Entity, child of Order)
```
Properties:  Id, ProductId, ProductName, UnitPrice (Money), Quantity, LineTotal (Money)
Constructor: OrderItem(productId, productName, unitPrice, quantity)
Rules:       - LineTotal = UnitPrice × Quantity (calculated in constructor)
             - Fully immutable — zero methods that mutate state
```

#### Customer (Aggregate Root)
```
Properties:  Id, FullName, Age, PhoneNumber (optional),
             Addresses (IReadOnlyCollection<CustomerAddress>)
Methods:     UpdateProfile(fullName, age, phoneNumber) → void
             AddAddress(street, city, building, floor) → CustomerAddress
             RemoveAddress(addressId) → void
             SetDefaultAddress(addressId) → void
Constructor: Customer(fullName, age, phoneNumber = null)
Rules:       - FullName is required and trimmed
             - Age > 0
             - No authentication or identity fields
```

#### CustomerAddress (Entity, child of Customer)
```
Properties:  Id, Street, City, BuildingNumber, Floor, IsDefault
Constructor: internal CustomerAddress(street, city, building, floor)
Rules:       - Only one address can be IsDefault=true at a time
             - Managed by Customer aggregate root
```

**Deliverable:** Entity design document with all properties, methods, and rules.

---

### 2.2 Design Value Objects

**Objective:** Create immutable, self-validating value types.

**What we'll do:**

#### Money
```csharp
public record Money(decimal Amount)
{
    // Self-validating: Amount >= 0
    // Equality by value
    // Arithmetic: Add, Subtract, Multiply(int quantity)
    // MVP: single currency (EGP assumed), add Currency field post-MVP
}
```

#### TimeRange
```csharp
public record TimeRange(TimeOnly OpensAt, TimeOnly ClosesAt)
{
    // Contains(TimeOnly time) → bool
    // Handles midnight-crossing (e.g., 22:00 → 02:00)
}
```

#### Address (optional for MVP — could stay as entity properties)
```csharp
public record Address(string Street, string City, string BuildingNumber, string Floor)
{
    // Self-validating: no empty strings
}
```

**Deliverable:** 2-3 value object designs with validation rules.

---

### 2.3 Design Domain Methods (Detailed Logic)

**Objective:** Pseudocode every domain method with guards and business rules.

**Critical method — `Cart.AddItem()`:**
```
Cart.AddItem(Product product, int quantity):
  1. IF IsExpired() → throw CartExpiredException
  2. IF quantity ≤ 0 → throw InvalidQuantityException
  3. IF product.RestaurantId ≠ this.RestaurantId → throw CrossRestaurantCartException
  4. IF !product.IsAvailable → throw ProductUnavailableException
  5. existingItem = Items.FirstOrDefault(i => i.ProductId == product.Id)
  6. IF existingItem != null → existingItem.IncreaseQuantity(quantity)
  7. ELSE → Items.Add(new CartItem(product.Id, product.Name, quantity))
```

**Domain Service — `CheckoutService.Checkout()`:**
```
CheckoutDomainService.Validate(Cart cart, Restaurant restaurant, DeliveryAddressSnapshot address, currentTime):
  1. Ensure cart is active, not expired, and not empty
  2. Ensure restaurant is active and open
  3. Ensure delivery address snapshot exists
  4. Find unavailable products using current Catalog data
  5. IF unavailable products exist → return CheckoutProductsUnavailable
  6. Create CheckoutItemSnapshot values using current Product prices
  7. return CheckoutValidationSucceeded(checkoutItems)
```

**Deliverable:** Pseudocode for all domain methods with complete guard clauses.

---

### 2.4 Design Domain Exceptions

**Objective:** One typed exception per business rule violation.

**What we'll create:**

```
Talabat.Domain/Exceptions/
├── DomainException.cs              ← abstract base
├── CartExpiredException.cs
├── CrossRestaurantCartException.cs
├── InvalidQuantityException.cs
├── ProductUnavailableException.cs
├── RestaurantClosedException.cs
├── RestaurantInactiveException.cs
├── EmptyCartCheckoutException.cs
├── DuplicateAddressException.cs
└── EntityNotFoundException.cs      ← generic "not found" with entity name
```

Each exception carries a meaningful message in business language:
- `CartExpiredException` → "This cart has expired. Please create a new cart."
- `CrossRestaurantCartException` → "Cannot add items from a different restaurant. Clear your cart first."

**Deliverable:** Exception class list with messages.

---

### 2.5 Design Repository Interfaces

**Objective:** Define data access contracts in the Domain layer.

**What we'll create:**

```
Talabat.Domain/Interfaces/
├── IRestaurantRepository.cs
├── ICartRepository.cs
├── IOrderRepository.cs
├── ICustomerRepository.cs
└── IUnitOfWork.cs
```

Key design decisions:
- Return `Task<T?>` for single lookups (nullable = might not exist).
- Return `Task<IReadOnlyList<T>>` for collections (never null, can be empty).
- No `IQueryable` — returns materialized data only.
- `IUnitOfWork.SaveChangesAsync()` — the application layer decides when to commit.

**Deliverable:** Interface files with method signatures.

---

## Phase 3 — Application Layer Design

> 🎯 Define the thin orchestration layer.

---

### 3.1 Application Use Cases

**Objective:** One handler class per use case.

**What we'll create — organized by feature:**

```
Talabat.Application/
├── Catalog/
│   ├── Commands/
│   │   ├── CreateRestaurant/
│   │   │   ├── CreateRestaurantCommand.cs
│   │   │   └── CreateRestaurantHandler.cs
│   │   ├── AddProduct/
│   │   └── UpdateProduct/
│   └── Queries/
│       ├── BrowseRestaurants/
│       ├── GetRestaurantDetails/
│       └── GetProductsByRestaurant/
├── Basket/
│   ├── Commands/
│   │   ├── AddItemToCart/
│   │   ├── RemoveItemFromCart/
│   │   ├── UpdateCartItemQuantity/
│   │   └── ClearCart/
│   └── Queries/
│       └── GetActiveCart/
├── Ordering/
│   ├── Commands/
│   │   └── Checkout/
│   └── Queries/
│       ├── GetOrderDetails/
│       └── GetCustomerOrders/
└── Customer/
    ├── Commands/
    │   ├── UpdateCustomerProfile/
    │   ├── AddAddress/
    │   └── RemoveAddress/
    └── Queries/
        └── GetCustomerProfile/
```

**Handler pattern (every handler follows this):**
1. Receive command/query via MediatR
2. Load aggregate(s) from repository
3. Call domain method (for commands)
4. Save via `IUnitOfWork` (for commands)
5. Return DTO (for queries) or result

**Also create:**
- DTOs: `RestaurantDto`, `ProductDto`, `CartDto`, `OrderDto`, `CustomerDto`
- Mapping profiles (AutoMapper or manual mapping)
- `IAuthService` interface (for Identity — implemented in Infrastructure)

---

### 3.2 Commands & Queries with MediatR

**Objective:** Wire up CQRS-lite using MediatR.

**NuGet packages for Application layer:**
- `MediatR`
- `FluentValidation` (command validation)
- `AutoMapper` (optional, can use manual mapping)

**Pattern for each command:**
```
// Command (record)
public record AddItemToCartCommand(int CustomerId, int ProductId, int Quantity) 
    : IRequest<CartDto>;

// Validator
public class AddItemToCartValidator : AbstractValidator<AddItemToCartCommand> { }

// Handler
public class AddItemToCartHandler : IRequestHandler<AddItemToCartCommand, CartDto> { }
```

**Deliverable:** Command/Query/Handler stubs for all 18+ use cases.

---

## Phase 4 — Implementation

> 🎯 Build it, layer by layer, inside-out.

---

### 4.1 Create Solution & Project Structure

**Objective:** Scaffold the solution with correct project references.

**Commands to run:**
```powershell
# Create solution
dotnet new sln -n Talabat

# Create projects
dotnet new classlib -n Talabat.Domain -o src/Talabat.Domain
dotnet new classlib -n Talabat.Application -o src/Talabat.Application
dotnet new classlib -n Talabat.Infrastructure -o src/Talabat.Infrastructure
dotnet new webapi -n Talabat.API -o src/Talabat.API

# Create test projects
dotnet new xunit -n Talabat.Domain.Tests -o tests/Talabat.Domain.Tests
dotnet new xunit -n Talabat.Application.Tests -o tests/Talabat.Application.Tests

# Add projects to solution
dotnet sln add src/Talabat.Domain
dotnet sln add src/Talabat.Application
dotnet sln add src/Talabat.Infrastructure
dotnet sln add src/Talabat.API
dotnet sln add tests/Talabat.Domain.Tests
dotnet sln add tests/Talabat.Application.Tests

# Set up project references (CRITICAL — enforces dependency rules)
dotnet add src/Talabat.Application reference src/Talabat.Domain
dotnet add src/Talabat.Infrastructure reference src/Talabat.Domain
dotnet add src/Talabat.Infrastructure reference src/Talabat.Application
dotnet add src/Talabat.API reference src/Talabat.Application
dotnet add src/Talabat.API reference src/Talabat.Infrastructure
dotnet add tests/Talabat.Domain.Tests reference src/Talabat.Domain
dotnet add tests/Talabat.Application.Tests reference src/Talabat.Application
```

**Deliverable:** Compilable solution with correct dependency graph.

---

### 4.2 Implement Domain Layer

**Objective:** Code all entities, value objects, exceptions, and interfaces.

**Implementation order:**
1. `Common/` — `BaseEntity` base class (Id property)
2. `ValueObjects/` — `Money`, `TimeRange`
3. `Exceptions/` — `DomainException` + all child exceptions
4. `Entities/Catalog/` — `Restaurant`, `Product`
5. `Entities/Basket/` — `Cart`, `CartItem`
6. `Entities/Ordering/` — `Order`, `OrderItem`
7. `Entities/Identity/` — `Customer`, `CustomerAddress`
8. `Interfaces/` — All repository interfaces + `IUnitOfWork`

**Verification:** `dotnet build src/Talabat.Domain` — must compile with **zero NuGet packages**.

---

### 4.3 Implement Application Layer

**Objective:** Code all handlers, DTOs, validators, and mappings.

**Implementation order:**
1. Install NuGet packages: `MediatR`, `FluentValidation`, `AutoMapper`
2. Create DTOs for each entity
3. Create mapping profiles
4. Implement Catalog commands & queries (simplest context — good warmup)
5. Implement Basket commands & queries (most business logic)
6. Implement Ordering commands & queries (checkout is the hardest handler)
7. Implement Identity commands & queries
8. Add `DependencyInjection.cs` extension method for registering services

**Verification:** `dotnet build src/Talabat.Application` — must compile.

---

### 4.4 Implement Infrastructure Layer

**Objective:** EF Core DbContext, entity configurations, repository implementations.

**Implementation order:**
1. Install NuGet package: `Microsoft.EntityFrameworkCore.SqlServer`
2. Create `TalabatDbContext` inheriting from `DbContext`
3. Create `Configurations/` folder with one `IEntityTypeConfiguration<T>` per entity
4. Implement each repository class
5. Implement `UnitOfWork` class wrapping `DbContext.SaveChangesAsync()`
6. Add `DependencyInjection.cs` for registering DbContext and repositories

**Key EF configurations:**
- `Restaurant` → HasMany Products, cascade delete
- `Cart` → HasMany CartItems, cascade delete
- `Order` → HasMany OrderItems, cascade delete
- `Money` → OwnsOne (stored as columns in parent table)
- All entities → integer identity PKs

---

### 4.5 Database Migration

**Objective:** Generate and apply the initial schema.

**Commands:**
```powershell
dotnet ef migrations add InitialCreate -p src/Talabat.Infrastructure -s src/Talabat.API
# STOP — review the migration file!
dotnet ef database update -s src/Talabat.API
```

**What to review in the migration:**
- All tables created with correct column types
- Foreign keys pointing in the right direction
- Cascade delete behavior is correct
- Value objects stored as columns (not separate tables)

---

### 4.6 Implement API Layer

**Objective:** Controllers, middleware, DI wiring.

**Implementation order:**
1. Configure DI in `Program.cs` — call Application + Infrastructure registration methods
2. Create global exception handling middleware (maps `DomainException` → HTTP status)
3. Create `RestaurantsController` → 3 endpoints
4. Create `CartController` → 4 endpoints
5. Create `OrdersController` → 3 endpoints
6. Create `CustomerController` for the single MVP profile and addresses
7. Add Swagger/OpenAPI configuration

**Error mapping middleware:**
| Exception | HTTP Status | Response |
|-----------|-------------|----------|
| `EntityNotFoundException` | 404 | `{ error: "..." }` |
| `CartExpiredException` | 409 Conflict | `{ error: "..." }` |
| `CrossRestaurantCartException` | 400 Bad Request | `{ error: "..." }` |
| `RestaurantClosedException` | 422 Unprocessable | `{ error: "..." }` |
| Other `DomainException` | 400 Bad Request | `{ error: "..." }` |
| Unhandled | 500 Internal | `{ error: "An error occurred" }` |

---

## Phase 5 — Testing & Future

---

### 5.1 Testing

**Objective:** Test every business invariant.

**Domain unit tests (Priority 1 — write these first):**
- `Cart_AddItem_WhenExpired_ThrowsCartExpiredException`
- `Cart_AddItem_WhenDifferentRestaurant_ThrowsCrossRestaurantException`
- `Cart_AddItem_WhenDuplicateProduct_IncreasesQuantity`
- `Cart_AddItem_WhenQuantityZero_ThrowsInvalidQuantityException`
- `Restaurant_IsCurrentlyOpen_WhenWithinHours_ReturnsTrue`
- `Money_WhenNegative_ThrowsArgumentException`
- `Order_Constructor_CalculatesTotalFromItems`

**Application handler tests (Priority 2):**
- Mock repositories, verify orchestration logic.

**Integration tests (Priority 3):**
- Test EF configurations against a real database.

**Tools:** xUnit, Moq (or NSubstitute), FluentAssertions.

---

### 5.2 Future Improvements (Post-MVP)

| Priority | Feature | Evolves From |
|----------|---------|-------------|
| 1 | Domain Events | Manual cart clearing → automatic on order |
| 2 | Payment Gateway | New bounded context |
| 3 | Delivery Drivers | New bounded context |
| 4 | Notifications (SignalR) | New infrastructure service |
| 5 | Product Options/Variants | Extend Product entity |
| 6 | Discounts & Coupons | New Pricing context |
| 7 | Multi-currency | Evolve Money value object |
| 8 | Restaurant Branches | Extend Restaurant aggregate |
| 9 | Inventory Management | New bounded context |
| 10 | Full CQRS | Separate read DB |

---

## Quick Reference — Dependency Rules

```
   ┌─────────────┐
   │   Talabat    │     Composition Root
   │     API      │     References everything
   └──────┬───────┘
          │
   ┌──────▼───────┐
   │  Talabat     │     EF Core, Identity, Repositories
   │Infrastructure│     References Application + Domain
   └──────┬───────┘
          │
   ┌──────▼───────┐
   │  Talabat     │     Handlers, DTOs, MediatR
   │ Application  │     References Domain only
   └──────┬───────┘
          │
   ┌──────▼───────┐
   │  Talabat     │     Entities, Value Objects, Interfaces
   │   Domain     │     References NOTHING
   └──────────────┘
```

---

> **Next step:** Say **"Let's start Phase 1"** or **"Let's start Step 1"** and we begin together. I'll guide you through each step, explain every decision, and review your code like a senior architect would. 🚀
