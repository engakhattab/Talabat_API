# Phase 2 Domain And Application Contracts

Date: 2026-07-11

This document records the Phase 2 implementation from `PROJECT_IMPLEMENTATION_ROADMAP.md`.

## Scope

Phase 2 defines contracts only.

This phase does not add:

- Repository implementations.
- EF Core.
- DbContext.
- Migrations.
- Application use-case handlers.
- API endpoints.
- Identity/Auth contracts tied to a framework.
- IdentityServer/Auth Portal code.
- Frontend or website code.

## Contract Placement

Repository and Unit of Work contracts were added to Domain because the existing design documents place repository interfaces there and the contracts are expressed in aggregate-root terms.

Application-level utility/result contracts were added to Application because they support use-case orchestration without becoming Domain concepts.

## Domain Contracts Added

Location: `src/Talabat/Talabat.Domain/Interfaces/`

Implemented repository contracts:

- `IRestaurantRepository`
- `ICartRepository`
- `ICustomerRepository`
- `IOrderRepository`
- `IDeliveryRepository`
- `IDeliveryAgentRepository`
- `IUnitOfWork`

These interfaces:

- Are aggregate-root specific.
- Do not expose EF Core.
- Do not expose `DbContext`.
- Do not expose `IQueryable`.
- Do not expose HTTP or API DTOs.
- Do not include Identity/Auth framework assumptions.
- Do not create repositories for child entities.

No repository interfaces were created for:

- `Product`
- `CartItem`
- `OrderItem`
- `CustomerAddress`

## Repository Method Decisions

### `IRestaurantRepository`

Supports Catalog browse, restaurant details, add-to-cart product snapshot lookup, and checkout validation.

Methods:

- `GetActiveRestaurantsAsync`
- `GetByIdAsync`
- `GetByIdWithProductsAsync`
- `GetProductSnapshotAsync`
- `ExistsAsync`

### `ICartRepository`

Supports active cart workflows.

Methods:

- `GetActiveCartByCustomerIdAsync`
- `AddAsync`
- `Update`

No direct CartItem update methods exist.

### `ICustomerRepository`

Supports profile/address workflows and checkout address validation.

Methods:

- `GetByIdAsync`
- `GetByIdWithAddressesAsync`
- `AddAsync`
- `Update`

No `GetMvpCustomer` method exists because the final system must support multiple customer profiles.

No identity-framework-specific lookup exists in Phase 2.

### `IOrderRepository`

Supports checkout persistence and customer-scoped order reads.

Methods:

- `GetByIdAsync`
- `GetByIdForCustomerAsync`
- `GetByCustomerIdAsync`
- `AddAsync`

No OrderItem repository or order mutation methods exist.

### `IDeliveryRepository`

Supports delivery task creation, assignment queues, agent dashboard reads, and lifecycle workflows.

Methods:

- `GetByIdAsync`
- `GetByOrderIdAsync`
- `GetActiveByAgentIdAsync`
- `GetPendingAssignmentAsync`
- `GetAssignedToAgentAsync`
- `AddAsync`
- `Update`

### `IDeliveryAgentRepository`

Supports delivery-agent profile, assignment, and availability workflows.

Methods:

- `GetByIdAsync`
- `GetAvailableAgentsAsync`
- `AddAsync`
- `Update`

### `IUnitOfWork`

Supports persistence commit only.

Method:

- `SaveChangesAsync`

`IUnitOfWork` must not contain business rules.

## Application Contracts Added

### `IClock`

Location: `src/Talabat/Talabat.Application/Abstractions/IClock.cs`

Purpose:

- Supplies current UTC time to Application use cases without tying use cases to system time.
- Keeps UTC acquisition outside Domain entities.

Current contract:

- `DateTime UtcNow`

### Checkout Outcomes

Location: `src/Talabat/Talabat.Application/Ordering/Checkout/CheckoutOutcome.cs`

Purpose:

- Gives the future checkout use case a framework-neutral result shape.
- Keeps expected checkout outcomes out of controllers.
- Allows unavailable checkout items to be returned as structured Application results.

Contracts:

- `CheckoutOutcome`
- `CheckoutSucceededOutcome`
- `CheckoutProductsUnavailableOutcome`
- `UnavailableCheckoutItemOutcome`

These are Application contracts, not API response contracts.

## Current User Decision

No current-user abstraction was added in Phase 2.

Reason:

- Authentication and Identity/Auth framework selection remain reserved/TBD.
- Phase 2 contracts should not assume claims, tokens, roles, `HttpContext`, or a specific account ID shape.

Future authenticated use cases may add a framework-neutral current-user abstraction after the Identity/Auth boundary is approved.

## Verification

`dotnet build src\Talabat\Talabat.slnx --no-restore` succeeds.

The existing API warning remains:

- `NU1903` for transitive `Microsoft.OpenApi` `2.0.0`.

No packages, migrations, repository implementations, API endpoints, or Identity/Auth code were added.
