# Talabat DDD Project — Implementation Roadmap

> A mentor-guided, step-by-step architecture plan for building a Talabat-like food delivery backend using **DDD**, **Clean Architecture**, **ASP.NET Core**, and **EF Core Code First**.

---

## Phase 1 — Strategic Design (Steps 1–3)

> **Goal:** Understand the problem space before writing any code.

### Step 1: Validate Requirements

| Item | Detail |
|------|--------|
| **Objective** | Confirm every business rule is explicit, unambiguous, and testable. |
| **Why** | Vague requirements → vague domain model → refactors later. |
| **Deliverables** | A checklist of all business rules written as *Given/When/Then* statements. |
| **Difficulty** | ⭐ Easy |

**What to do:**
- Re-read every rule in the prompt and rewrite it as a testable statement.
- Example: *"Given a cart older than 1 hour, When the customer tries to add an item, Then the system rejects it with `CartExpiredException`."*
- List edge cases: What if the restaurant closes *during* checkout? What if a product is deleted while in a cart?

**Common beginner mistakes:**
- Jumping to code without clarifying ambiguity.
- Assuming requirements are complete — always ask "what happens if…?"

**What to postpone:** Payment, delivery, notifications — already excluded.

**Learning outcome:** Requirements engineering. A domain model is only as good as the requirements it captures.

---

### Step 2: Review Bounded Contexts

| Item | Detail |
|------|--------|
| **Objective** | Identify the boundaries where different parts of the system speak different "languages." |
| **Why** | Without boundaries, the domain model becomes a Big Ball of Mud. |
| **Deliverables** | A Context Map diagram showing 4 bounded contexts and their relationships. |
| **Difficulty** | ⭐⭐ Medium |

**What to do — define these 4 bounded contexts:**

| Context | Responsibility | Key Language |
|---------|---------------|--------------|
| **Catalog** | Restaurants & Products (the "menu") | Restaurant, Product, Availability |
| **Basket** | Active shopping cart with price snapshots | Cart, CartItem, UnitPriceSnapshot |
| **Ordering** | Immutable order records | Order, OrderItem, LineTotal |
| **Identity** | Authentication & customer profile | Customer, CustomerAddress |

**Context relationships (MVP):**
- `Basket` → depends on `Catalog` (reads product prices).
- `Ordering` → depends on `Basket` (converts cart to order) and `Catalog` (final price validation).
- `Identity` → `Basket` and `Ordering` reference CustomerId but don't own Customer.

**DDD reasoning:** Each context owns its own models. `Product` in Catalog has full details; `CartItem` in Basket only stores a price snapshot — they are *not* the same object.

**Common beginner mistakes:**
- Making one giant `Product` entity used everywhere.
- Sharing EF entities across contexts.

**What to postpone:** Anti-corruption layers, domain events between contexts.

**Learning outcome:** Bounded Context is the #1 most important DDD pattern.

---

### Step 3: Review Aggregates

| Item | Detail |
|------|--------|
| **Objective** | Define the transactional consistency boundaries. |
| **Why** | Aggregates enforce invariants. Wrong boundaries = broken rules or performance issues. |
| **Deliverables** | Aggregate diagram showing roots and their children. |
| **Difficulty** | ⭐⭐ Medium |

**The 4 aggregates:**

```
Catalog Context:
  └── Restaurant (Aggregate Root)
        └── Product (Entity, owned by Restaurant)

Basket Context:
  └── Cart (Aggregate Root)
        └── CartItem (Entity, owned by Cart)

Ordering Context:
  └── Order (Aggregate Root)
        └── OrderItem (Entity, owned by Order)

Identity Context:
  └── Customer (Aggregate Root)
        └── CustomerAddress (Entity, owned by Customer)
```

**Why these boundaries?**
- `Restaurant` owns `Product` because you can't have a product without a restaurant, and restaurant-level rules (like "is open?") must be checked when accessing products.
- `Cart` owns `CartItem` because the rule "all items must be from the same restaurant" spans all items — the aggregate root enforces this.
- `Order` owns `OrderItem` because order data is immutable and must be loaded/saved together.

**Key rule:** Access children *only through* the aggregate root. Never load a `CartItem` directly from the DB — always load the `Cart` first.

**Common beginner mistakes:**
- Making every table an aggregate root.
- Creating aggregates that are too large (e.g., Restaurant owning Orders).
- Directly querying child entities bypassing the root.

**Learning outcome:** Aggregate = transactional boundary + invariant enforcer.

---

## Phase 2 — Tactical Design (Steps 4–8)

> **Goal:** Design the building blocks of the domain model.

### Step 4: Design Entities

| Item | Detail |
|------|--------|
| **Objective** | Define entities with identity, state, and behavior. |
| **Why** | Entities are not just data bags — they encapsulate business logic. |
| **Deliverables** | Class diagrams for all 8 entities with their properties and methods. |
| **Difficulty** | ⭐⭐ Medium |

**Entity designs:**

**Restaurant** — Properties: `Id`, `Name`, `Description`, `OpensAt (TimeOnly)`, `ClosesAt (TimeOnly)`, `IsActive`. Methods: `IsCurrentlyOpen()`, `Deactivate()`, `AddProduct()`, `RemoveProduct()`.

**Product** — Properties: `Id`, `Name`, `Description`, `Price (decimal)`, `IsAvailable`, `RestaurantId`. Methods: `UpdatePrice()`, `MarkUnavailable()`.

**Cart** — Properties: `Id`, `CustomerId`, `RestaurantId`, `CreatedAt`, `Items (list)`. Methods: `AddItem()`, `RemoveItem()`, `UpdateQuantity()`, `IsExpired()`, `Clear()`.

**CartItem** — Properties: `Id`, `ProductId`, `ProductName`, `Quantity`, `UnitPriceSnapshot`. Methods: `IncreaseQuantity()`, `SetQuantity()`.

**Order** — Properties: `Id`, `CustomerId`, `RestaurantId`, `OrderDate`, `Status`, `TotalAmount`, `Items (list)`. Read-only after creation.

**OrderItem** — Properties: `Id`, `ProductId`, `ProductName`, `UnitPrice`, `Quantity`, `LineTotal`. Fully immutable.

**Customer** — Properties: `Id`, `IdentityUserId`, `FirstName`, `LastName`, `Addresses (list)`. Methods: `AddAddress()`, `RemoveAddress()`, `SetDefaultAddress()`.

**CustomerAddress** — Properties: `Id`, `Street`, `City`, `BuildingNumber`, `Floor`, `IsDefault`.

**Common beginner mistakes:**
- Anemic domain model: entities with only getters/setters and no behavior.
- Public setters everywhere — use private setters + methods.
- Putting business logic in controllers or services.

**Learning outcome:** Rich domain models with encapsulated behavior.

---

### Step 5: Design Value Objects

| Item | Detail |
|------|--------|
| **Objective** | Identify concepts defined by their attributes, not identity. |
| **Why** | Value objects make the domain model more expressive and type-safe. |
| **Deliverables** | Value object classes. |
| **Difficulty** | ⭐ Easy |

**MVP Value Objects:**

| Value Object | Used In | Why |
|-------------|---------|-----|
| `Money` | Product.Price, CartItem.UnitPriceSnapshot, OrderItem.UnitPrice | Prevents raw `decimal` mistakes, prepares for multi-currency later. |
| `Address` | CustomerAddress | Groups street/city/building into a cohesive concept. |
| `TimeRange` | Restaurant.OpensAt + ClosesAt | Encapsulates "is now within range?" logic. |

**Implementation pattern (C# record):**
- Immutable (no setters).
- Equality by value (two `Money(10, "EGP")` are equal).
- Self-validating (constructor throws if amount < 0).

**What to postpone:** Don't create value objects for everything. If it's just a `string Name`, leave it as a string for now.

**Common beginner mistakes:**
- Skipping value objects entirely → primitive obsession.
- Over-engineering: making value objects for every single field.

**Learning outcome:** Value Objects vs Entities. When to use each.

---

### Step 6: Design Domain Methods

| Item | Detail |
|------|--------|
| **Objective** | Put business logic *inside* the entities and aggregate roots. |
| **Why** | This is the core of DDD — behavior belongs with the data it operates on. |
| **Deliverables** | Method signatures with validation logic defined. |
| **Difficulty** | ⭐⭐⭐ Hard |

**Critical methods to implement:**

**`Cart.AddItem(Product product, int quantity)`**
- Guard: `IsExpired()` → throw `CartExpiredException`
- Guard: `product.RestaurantId != this.RestaurantId` → throw `CrossRestaurantCartException`
- Guard: `!product.IsAvailable` → throw `ProductUnavailableException`
- Guard: `quantity <= 0` → throw `InvalidQuantityException`
- If product already in cart → increase quantity
- Else → add new `CartItem` with price snapshot

**`Cart.IsExpired()`**
- Returns `DateTime.UtcNow - CreatedAt > TimeSpan.FromHours(1)`

**`Restaurant.IsCurrentlyOpen()`**
- Compares current time against `OpensAt`/`ClosesAt`

**`CheckoutService.Checkout(Cart cart)` (Domain Service)**
- Validate restaurant is open
- Validate all products still available
- Compare current prices with cart snapshots
- If any price changed → return `PriceChangedResult` with details
- If all valid → create `Order` with immutable snapshots, clear cart

**DDD reasoning:** `Cart.AddItem()` lives on `Cart` (the aggregate root) because it enforces cross-item invariants. A standalone service can't guarantee consistency.

**Common beginner mistakes:**
- Putting all logic in application services or controllers.
- Not validating inside the domain — relying on API validation only.

**Learning outcome:** Rich domain model. The domain protects its own invariants.

---

### Step 7: Design Domain Exceptions

| Item | Detail |
|------|--------|
| **Objective** | Create typed exceptions for every business rule violation. |
| **Why** | Domain exceptions communicate *why* something failed in business terms. |
| **Deliverables** | Exception class hierarchy. |
| **Difficulty** | ⭐ Easy |

**Exception hierarchy:**

```
DomainException (abstract base)
├── CartExpiredException
├── CrossRestaurantCartException
├── InvalidQuantityException
├── ProductUnavailableException
├── RestaurantClosedException
├── RestaurantInactiveException
├── PriceChangedException
├── EmptyCartCheckoutException
├── DuplicateAddressException
└── CustomerNotFoundException
```

**Pattern:** Each exception extends a base `DomainException` class. The API layer catches `DomainException` and maps it to HTTP 400/409/422.

**Common beginner mistakes:**
- Throwing generic `Exception` or using `ArgumentException` for business rules.
- Returning error codes instead of exceptions inside the domain.

**Learning outcome:** Expressing business failures in the ubiquitous language.

---

### Step 8: Design Repository Interfaces

| Item | Detail |
|------|--------|
| **Objective** | Define data access contracts in the domain layer. |
| **Why** | The domain defines *what* it needs; infrastructure decides *how*. |
| **Deliverables** | Repository interfaces (one per aggregate root). |
| **Difficulty** | ⭐ Easy |

**Interfaces (defined in Domain layer):**

```
IRestaurantRepository
  - GetByIdAsync(int id)
  - GetAllAsync(bool includeInactive = false)
  - AddAsync(Restaurant restaurant)
  - Update(Restaurant restaurant)

ICartRepository
  - GetActiveCartByCustomerIdAsync(int customerId)
  - AddAsync(Cart cart)
  - Update(Cart cart)
  - DeleteAsync(Cart cart)

IOrderRepository
  - GetByIdAsync(int id)
  - GetByCustomerIdAsync(int customerId)
  - AddAsync(Order order)

ICustomerRepository
  - GetByIdAsync(int id)
  - GetByIdentityUserIdAsync(string identityUserId)
  - AddAsync(Customer customer)
  - Update(Customer customer)
```

**Also define:** `IUnitOfWork` with `SaveChangesAsync()` — wraps EF's `SaveChanges` so the application layer controls transaction commits.

**Key rule:** No `IQueryable` in repository interfaces. Return materialized collections. This keeps the domain independent of EF Core.

**Common beginner mistakes:**
- Generic `IRepository<T>` for everything — loses domain expressiveness.
- Returning `IQueryable` — leaks infrastructure into the domain.
- Putting repositories in the Infrastructure project namespace.

**Learning outcome:** Dependency Inversion Principle in action.

---

## Phase 3 — Application Layer (Steps 9–10)

> **Goal:** Orchestrate use cases without containing business logic.

### Step 9: Design Application Use Cases

| Item | Detail |
|------|--------|
| **Objective** | Define the application's entry points — what the system *does*. |
| **Why** | Use cases orchestrate domain objects; they don't contain business rules. |
| **Deliverables** | List of use case classes (one class per use case). |
| **Difficulty** | ⭐⭐ Medium |

**MVP Use Cases by context:**

**Catalog:** BrowseRestaurants, GetRestaurantDetails, GetProductsByRestaurant, CreateRestaurant, AddProduct, UpdateProduct.

**Basket:** GetActiveCart, AddItemToCart, RemoveItemFromCart, UpdateCartItemQuantity, ClearCart.

**Ordering:** Checkout (validate prices → create order), GetOrderDetails, GetCustomerOrders.

**Identity:** RegisterCustomer, GetCustomerProfile, AddAddress, RemoveAddress.

**DDD reasoning:** The application layer is *thin*. It loads aggregates via repositories, calls domain methods, and saves. Example:

```
AddItemToCartHandler:
  1. Load cart (or create new one)
  2. Load product from catalog
  3. Call cart.AddItem(product, quantity)  ← domain logic
  4. Save via unit of work
```

**Common beginner mistakes:**
- Fat application services with business logic that should be in the domain.
- One massive service class per context instead of focused use case classes.

**Learning outcome:** The difference between orchestration (application) and business rules (domain).

---

### Step 10: Commands & Queries (CQRS-Lite)

| Item | Detail |
|------|--------|
| **Objective** | Separate read and write operations. |
| **Why** | Reads and writes have different concerns. Separating them keeps code clean. |
| **Deliverables** | Command/Query classes + Handlers using MediatR. |
| **Difficulty** | ⭐⭐ Medium |

**Pattern:**

| Type | Example | Returns |
|------|---------|---------|
| Command | `AddItemToCartCommand { CustomerId, ProductId, Quantity }` | `void` or `CartDto` |
| Query | `GetActiveCartQuery { CustomerId }` | `CartDto` |

**Use MediatR** for dispatching. Each handler is a focused use case from Step 9.

**What to postpone:** Full CQRS with separate read/write databases. MVP uses the same DB for both.

**Common beginner mistakes:**
- Using MediatR as a service locator (hiding dependencies).
- Creating commands for simple reads — use queries instead.

**Learning outcome:** CQRS basics, MediatR pattern, single-responsibility handlers.

---

## Phase 4 — Implementation (Steps 11–16)

> **Goal:** Build the actual solution, layer by layer.

### Step 11: Project Structure

| Item | Detail |
|------|--------|
| **Objective** | Set up the solution with proper layer separation. |
| **Deliverables** | Visual Studio solution with 5 projects. |
| **Difficulty** | ⭐ Easy |

**Solution structure:**

```
Talabat.sln
│
├── src/
│   ├── Talabat.Domain/            ← Entities, Value Objects, Interfaces, Exceptions
│   ├── Talabat.Application/       ← Use Cases, DTOs, Commands, Queries, Validators
│   ├── Talabat.Infrastructure/    ← EF Core, Repositories, Identity, External Services
│   └── Talabat.API/               ← Controllers, Middleware, DI Configuration
│
└── tests/
    ├── Talabat.Domain.Tests/
    ├── Talabat.Application.Tests/
    └── Talabat.API.Tests/
```

**Dependency rules (enforced by project references):**
- `Domain` → references **nothing** (zero dependencies)
- `Application` → references `Domain` only
- `Infrastructure` → references `Application` and `Domain`
- `API` → references all (composition root)

**Common beginner mistakes:**
- Domain referencing Infrastructure (breaks Clean Architecture).
- Putting EF entities in the Domain layer.

---

### Step 12: Domain Layer Implementation

| Item | Detail |
|------|--------|
| **Objective** | Implement all entities, value objects, exceptions, and interfaces from Phase 2. |
| **Deliverables** | Compilable Domain project with zero external dependencies. |
| **Difficulty** | ⭐⭐⭐ Hard |
| **Implementation order** | 1) Base classes → 2) Value Objects → 3) Exceptions → 4) Entities → 5) Repository Interfaces |

**Key principle:** The Domain project's `.csproj` should have **no NuGet packages**. If you need to add a package, something is wrong.

---

### Step 13: Infrastructure Layer Implementation

| Item | Detail |
|------|--------|
| **Objective** | Implement repositories, DbContext, and Identity. |
| **Deliverables** | Working data access layer. |
| **Difficulty** | ⭐⭐ Medium |
| **Implementation order** | 1) DbContext → 2) Entity Configurations → 3) Repositories → 4) UnitOfWork → 5) Identity Setup |

**Key decisions:**
- One `TalabatDbContext` for MVP (separate DbContexts per context is over-engineering for now).
- Use `IEntityTypeConfiguration<T>` for each entity — keep `OnModelCreating` clean.
- Implement Identity with a separate `ApplicationUser` that maps to your `Customer` entity.

---

### Step 14: EF Core Configuration

| Item | Detail |
|------|--------|
| **Objective** | Configure entity mappings, relationships, and constraints. |
| **Deliverables** | One configuration class per entity. |
| **Difficulty** | ⭐⭐ Medium |

**Key configurations:**
- `Restaurant` → owns `List<Product>` (cascade delete).
- `Cart` → owns `List<CartItem>`. Add a query filter for non-expired carts.
- `Order` → owns `List<OrderItem>`. All properties read-only after creation.
- Value Objects (`Money`, `Address`) → configure as Owned Types.
- Integer Identity PKs as specified.

---

### Step 15: Database Migrations

| Item | Detail |
|------|--------|
| **Objective** | Generate and apply initial migration. |
| **Deliverables** | Working database schema. |
| **Difficulty** | ⭐ Easy |

**Steps:** `dotnet ef migrations add InitialCreate -p Talabat.Infrastructure -s Talabat.API` → `dotnet ef database update -s Talabat.API`.

**Rule:** Review every generated migration file. Understand what EF created.

---

### Step 16: API Endpoints

| Item | Detail |
|------|--------|
| **Objective** | Expose the application use cases as REST endpoints. |
| **Deliverables** | Controllers with proper HTTP methods and status codes. |
| **Difficulty** | ⭐⭐ Medium |

**Endpoint map:**

| Method | Endpoint | Handler |
|--------|----------|---------|
| GET | `/api/restaurants` | BrowseRestaurants |
| GET | `/api/restaurants/{id}` | GetRestaurantDetails |
| GET | `/api/restaurants/{id}/products` | GetProductsByRestaurant |
| GET | `/api/cart` | GetActiveCart |
| POST | `/api/cart/items` | AddItemToCart |
| DELETE | `/api/cart/items/{productId}` | RemoveItemFromCart |
| POST | `/api/orders/checkout` | Checkout |
| GET | `/api/orders` | GetCustomerOrders |
| GET | `/api/orders/{id}` | GetOrderDetails |
| POST | `/api/auth/register` | RegisterCustomer |
| POST | `/api/auth/login` | Login |

**API layer responsibilities:** Map HTTP → Commands/Queries, handle auth, map `DomainException` → HTTP status codes via middleware.

---

## Phase 5 — Quality & Future (Steps 17–18)

### Step 17: Testing Strategy

| Item | Detail |
|------|--------|
| **Objective** | Ensure domain logic is correct and stays correct. |
| **Deliverables** | Unit tests for domain, integration tests for repositories. |
| **Difficulty** | ⭐⭐ Medium |

**Priority order:**
1. **Domain unit tests** (highest value) — test `Cart.AddItem()`, `IsExpired()`, price validation, all exceptions.
2. **Application handler tests** — mock repositories, test orchestration.
3. **Integration tests** — test EF configurations against a real DB.

**Minimum test coverage for MVP:** Every business invariant from the prompt must have at least one test.

---

### Step 18: Future Improvements

| Item | Detail |
|------|--------|
| **Objective** | Document what comes after MVP. |
| **Difficulty** | N/A — planning only |

**Post-MVP roadmap (in priority order):**
1. Domain Events (e.g., `OrderPlacedEvent` → clear cart automatically).
2. Payment Gateway integration.
3. Delivery Driver context.
4. Real-time notifications (SignalR).
5. Product Options & Variants.
6. Discounts, Coupons, Offers.
7. Multi-currency support (evolve `Money` value object).
8. Restaurant Branches.
9. Inventory Management.
10. Separate read models (full CQRS).

---

## Implementation Order Summary

| Order | Step | Phase | Est. Time |
|-------|------|-------|-----------|
| 1 | Validate Requirements | Strategic Design | 1 day |
| 2 | Bounded Contexts | Strategic Design | 1 day |
| 3 | Aggregates | Strategic Design | 1 day |
| 4 | Entities | Tactical Design | 2 days |
| 5 | Value Objects | Tactical Design | 1 day |
| 6 | Domain Methods | Tactical Design | 2 days |
| 7 | Domain Exceptions | Tactical Design | 0.5 day |
| 8 | Repository Interfaces | Tactical Design | 0.5 day |
| 9 | Use Cases | Application Layer | 2 days |
| 10 | Commands & Queries | Application Layer | 1 day |
| 11 | Project Structure | Implementation | 0.5 day |
| 12 | Domain Layer Code | Implementation | 3 days |
| 13 | Infrastructure Layer | Implementation | 2 days |
| 14 | EF Core Config | Implementation | 1 day |
| 15 | Migrations | Implementation | 0.5 day |
| 16 | API Endpoints | Implementation | 2 days |
| 17 | Testing | Quality | 3 days |
| 18 | Future Planning | Future | 0.5 day |
| | **Total** | | **~24 days** |

---

> **Mentor's Note:** We will tackle these steps **one at a time, in order**. At each step, I will explain the *why* before the *how*. Ask questions at any point — that's how real mentorship works. When you're ready, say **"Let's start Step 1"** and we'll begin validating your requirements together.
