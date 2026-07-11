# Repository Interfaces Design

> Phase 0 scope update: This document was originally written for MVP v1. Repository guidance remains valid, but future multi-customer and delivery-agent flows must replace permanent single-customer assumptions. Identity/Auth remains reserved/TBD and should not shape repository interfaces until the framework decision is approved.

This document defines repository interface design for the current Talabat Domain.

Phase 2 adds the repository interface source files described here. This document still does not define repository implementations, controllers, handlers, EF configurations, or migrations.

Original MVP v1 scope:

- No authentication, authorization, Identity, login/register, JWT, admins, or restaurant owners.
- No payment, delivery drivers, notifications, coupons, or reviews.
- Assume one normal customer profile.
- Restaurants and products are seeded for testing.
- Cart is not created until the first item is added.

Current Phase 2 decision:

- Repository contracts are implemented under `src/Talabat/Talabat.Domain/Interfaces/`.
- Repositories should be designed for multiple customers and delivery agents, not for a permanent single-customer MVP.
- Identity/Auth is reserved/TBD. Do not add Identity-framework-specific repository methods until the account/profile linking decision is approved.
- Delivery and DeliveryAgent are implemented aggregate roots and must be included in the repository design.

## Repository Philosophy

Repositories should exist only for aggregate roots.

Repositories are contracts for loading and saving aggregates. They let the Application layer ask for domain objects without knowing how persistence is implemented.

Domain defines repository contracts because repositories are expressed in terms of aggregate roots. Infrastructure implements those contracts using EF Core later.

Repositories should not contain business logic. Business decisions belong in entities, aggregate roots, value objects, or domain services.

Repositories should not expose EF Core details. They should not expose DbContext, tracking behavior, Include expressions, EF-specific types, or database-specific concepts to the Domain/Application contract.

Repositories should not return `IQueryable`. Returning `IQueryable` leaks query composition and persistence details outside the repository. Repository methods should return materialized aggregate roots or read data shaped for a specific application need.

The Application layer uses repositories to load aggregates, calls domain methods, then saves through UnitOfWork.

Domain entities and domain services should not call repositories directly.

## Layer Placement

Repository interfaces live in the Domain layer:

`Talabat.Domain/Interfaces/`

- `IRestaurantRepository`
- `ICartRepository`
- `IOrderRepository`
- `ICustomerRepository`
- `IDeliveryRepository`
- `IDeliveryAgentRepository`
- `IUnitOfWork`

Repository implementations live in the Infrastructure layer:

`Talabat.Infrastructure/Persistence/Repositories/`

- `RestaurantRepository`
- `CartRepository`
- `OrderRepository`
- `CustomerRepository`
- `DeliveryRepository`
- `DeliveryAgentRepository`
- `UnitOfWork`

The Application layer uses the Domain interfaces to load aggregates, call domain methods, and save changes. The Domain layer defines the contracts, Infrastructure supplies the EF Core implementations, and Application coordinates the use case.

Domain entities and domain services must not call repositories directly.

Repository interfaces must not expose EF Core types, DbContext, IQueryable, HTTP concepts, or API DTOs.

Repositories exist only for aggregate roots. Delivery and DeliveryAgent are included because both are independent aggregate roots in the current Domain model.

Do not create repositories for Product, CartItem, OrderItem, or CustomerAddress.

## Aggregate Roots That Need Repositories

| Aggregate Root | Repository Interface | Why It Needs A Repository | Child Entities Accessed Through Root |
|---|---|---|---|
| Restaurant | `IRestaurantRepository` | Catalog browsing, add-to-cart product lookup, and checkout restaurant/product validation need Restaurant aggregate data. | Product |
| Cart | `ICartRepository` | Cart use cases need to load and save the customer active cart aggregate. | CartItem |
| Order | `IOrderRepository` | Checkout needs to save created orders; order queries need order history. | OrderItem |
| Customer | `ICustomerRepository` | Customer profile, address management, and checkout delivery address validation need Customer aggregate data. | CustomerAddress |
| Delivery | `IDeliveryRepository` | Delivery lifecycle use cases load and save one delivery task aggregate. | None |
| DeliveryAgent | `IDeliveryAgentRepository` | Assignment and agent availability use cases load and save courier aggregates. | None |

Do not create repositories for child entities:

- `IProductRepository`
- `ICartItemRepository`
- `IOrderItemRepository`
- `ICustomerAddressRepository`

Child entities must be loaded and modified through their aggregate root.

Delivery and DeliveryAgent are not parent/child. Application later loads both repositories for assignment or terminal coordination and commits their changes through UnitOfWork.

## IRestaurantRepository

### Purpose

Load seeded restaurants and products for browsing, add-to-cart, and checkout validation.

### Conceptual Methods

| Method | Why It Exists | Use Case |
|---|---|---|
| `GetActiveRestaurants` | Customer browsing should show only active restaurants. This is read filtering over the Catalog aggregate. | BrowseRestaurants |
| `GetById` | Some use cases need restaurant details or restaurant state by id. | Restaurant details, checkout restaurant validation |
| `GetByIdWithProducts` | Basket, cart read flows, and Ordering need current product data for validation and pricing. | AddItemToCart, GetCartDetails, Checkout |
| `GetProductSnapshot` | Optional convenience method that returns the price-free data needed to build `CatalogProductSnapshot`. This is an application/query helper, not Product aggregate access. | AddItemToCart |
| `Exists` | Optional, only if a use case needs a lightweight existence check. | Validation/setup workflows if needed |

### Important Notes

- Product is a child entity of Restaurant, so Product access should happen through Restaurant-oriented methods.
- `GetProductSnapshot` should not become a back door for modifying Product directly.
- Cart detail and checkout flows must obtain current Product prices from Catalog-oriented reads; CartItem stores no price.
- Catalog data may start as seed data. Public catalog-management APIs remain a later admin/restaurant-owner decision.

## ICartRepository

### Purpose

Load and save the customer cart aggregate.

### Conceptual Methods

| Method | Why It Exists | Use Case |
|---|---|---|
| `GetActiveCartByCustomerId` | Most cart operations need the customer's active cart, if one exists. | GetCart, AddItemToCart, UpdateQuantity, RemoveItem, ClearCart, Checkout |
| `Add` | The first cart item creates the cart because the current Domain decision does not persist empty active carts. | AddItemToCart |
| `Update` | Cart mutations change aggregate state and need persistence. | AddItemToCart, UpdateQuantity, RemoveItem, ClearCart, Checkout |
| `Delete` | Optional. Prefer status change or clear behavior unless hard delete is explicitly chosen. | Cleanup only if chosen |

### Important Notes

- Cart is not created until first item is added.
- One active cart per customer is enforced by Application + persistence.
- Do not expose methods that update CartItem directly.
- CartItem must be loaded and changed through Cart.

## IOrderRepository

### Purpose

Save created orders and load order history.

### Conceptual Methods

| Method | Why It Exists | Use Case |
|---|---|---|
| `Add` | Successful checkout creates a new Order aggregate that must be persisted. | Checkout |
| `GetById` | Order details need to load one order by id. | GetOrderDetails |
| `GetByCustomerId` | Order history needs all orders for a customer profile. | GetOrders |
| `GetByIdForCustomer` | Optional helper for a customer-scoped lookup. It prevents returning an order outside the current customer scope. | GetOrderDetails |

### Important Notes

- Do not expose methods that update OrderItem directly.
- Orders are historical records.
- The current order model does not need update/delete order methods because orders are historical records.
- OrderItem must be loaded through Order.

## ICustomerRepository

### Purpose

Load customer profile data and addresses.

### Conceptual Methods

| Method | Why It Exists | Use Case |
|---|---|---|
| `GetById` | Customer-scoped use cases need to load a specific profile by domain customer id. | Cart, address, order, and profile workflows |
| `GetByIdWithAddresses` | Address management and checkout need the Customer aggregate with its address collection. | AddAddress, RemoveAddress, SetDefaultAddress, Checkout |
| `Add` | Customer profile creation may be needed by setup now and by registration later after Identity/Auth decisions. | Seed/setup, future registration |
| `Update` | Profile or address changes mutate the Customer aggregate and need persistence. | UpdateCustomerProfile, AddAddress, RemoveAddress, SetDefaultAddress |

### Important Notes

- Do not add `GetMvpCustomer`; the final system must support multiple customer profiles.
- Do not add identity-framework-specific lookup methods in Phase 1.
- A future framework-neutral account/profile lookup may be added after Identity/Auth design is approved.
- Customer profile includes required FullName, positive Age, and optional PhoneNumber.
- Do not expose methods that update CustomerAddress directly.
- CustomerAddress must be loaded and changed through Customer.

## IUnitOfWork

UnitOfWork coordinates saving changes after one use case.

It wraps the persistence commit conceptually. The Application layer controls when to commit, because it knows the use-case boundary and transaction boundary.

The Domain layer does not call SaveChanges.

Checkout needs one transaction: create Order, mark Cart checked out, then save.

### Conceptual Method

- `SaveChanges`

Do not treat UnitOfWork as a place for business rules. It only represents the commit boundary.

## What Repository Interfaces Must Not Do

- No `IQueryable`.
- No EF Core types.
- No DbContext exposure.
- No HTTP concepts.
- No DTOs tied to API responses.
- No business rule decisions.
- No direct child-entity mutation methods.
- No generic repository as the main design for this project.

## Why Not Generic Repository?

Generic repository hides domain language.

`GetById`, `Add`, `Update`, and `Delete` for everything does not express business needs. It says how to store things, but not why the application needs them.

DDD repositories should be specific to aggregate roots and use cases.

`ICartRepository.GetActiveCartByCustomerId` is more meaningful than `IRepository<Cart>.Find` because it describes the business concept the application needs: the customer's active cart.

Specific repository interfaces also prevent accidental child-entity access. If there is no `ICartItemRepository`, application code is pushed toward loading Cart and using Cart behavior.

## Repository Method Derivation

Every repository method should come from a use case.

Examples:

- BrowseRestaurants needs `GetActiveRestaurants`.
- AddItemToCart needs active cart lookup and a price-free catalog product snapshot.
- GetCartDetails needs Cart selections plus current Catalog product prices, which the Application layer supplies to `Cart.GetTotal(currentPrices)`.
- Checkout needs active cart, restaurant with current products and prices, customer with addresses, order add, and unit of work save.
- GetOrders needs orders by customer.
- Address management needs customer with addresses.

If a repository method cannot be tied to a use case, do not add it yet.

## Common Mistakes

- Creating repositories for every table.
- Returning `IQueryable`.
- Putting business validation inside repositories.
- Updating child entities directly.
- Letting controllers call DbContext directly.
- Creating a generic repository and losing domain-specific methods.
- Letting Domain Services use repositories directly.
- Adding auth/identity repository methods before the Identity/Auth boundary and framework decision are approved.
