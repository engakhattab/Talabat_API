# Unified User Identity Refactor Guide

This guide explains the refactor from the old split-account model to the unified `User : IdentityUser<int>` model, end to end.

It follows the actual Phase 2 implementation in this repo and is written to help someone understand what changed, why it changed, and how the pieces fit together.

## 1. What We Started With

The old design had separate account and business concepts:

- `ApplicationUser` for Identity
- `Customer` plus `CustomerAddress`
- `DeliveryAgent`
- `ICustomerRepository` and `IDeliveryAgentRepository`
- string-key Identity state in parts of the app

That created duplication and made it hard to keep identity, profile state, and authorization state aligned.

The main problems were:

- two sources of truth for the same human account;
- role and business state drifting apart;
- awkward account-to-profile linkage;
- fragile login and profile enforcement behavior;
- more complex test fixtures and migrations than necessary.

## 2. What We Moved To

The new design uses one persisted user row:

- `User : IdentityUser<int>`
- all customer and delivery-agent state lives on `User`
- `CustomerId` and `AssignedAgentId` still exist as business names, but they now point at `AspNetUsers.Id`
- `UserType` is the business capability flag
- Identity roles are the authorization projection

The rule is simple:

- `UserType` says what the account can do
- roles mirror that state for Identity
- only `IUserCapabilityService` is allowed to change either of them

## 3. Layer-by-Layer Shape

### Domain

The Domain now owns:

- `User`
- `UserAddress`
- `UserType`
- `VehicleType`
- `DeliveryAgentStatus`
- `AgentApprovalStatus`
- delivery assignment behavior
- user-level customer and agent state transitions

Domain does not know about:

- `UserManager`
- `DbContext`
- `HttpContext`
- `ClaimsPrincipal`

The only Identity package allowed in Domain is `Microsoft.Extensions.Identity.Stores`.

### Application

Application now talks to:

- `IUserRepository`
- `IUserCapabilityService`
- `ICurrentUser`

Application no longer depends on:

- `ApplicationUser`
- `ICustomerRepository`
- `IDeliveryAgentRepository`

### Infrastructure

Infrastructure now owns:

- `TalabatDbContext : IdentityDbContext<User, IdentityRole<int>, int>`
- `UserRepository`
- `UserCapabilityService`
- `IdentityDataSeeder`
- `TalabatSignInManager`
- entity configuration for `User`

### Hosts

There are three host patterns:

- Identity host uses the full `AddIdentity<User, IdentityRole<int>>()` chain.
- Customer API and Delivery API use the shared `AddUnifiedUserIdentityCore()` helper.
- Customer API also resolves current user state with one cached scalar query.

## 4. The Main File Changes

### Removed

The old account and repository split was deleted:

- `ApplicationUser`
- `Customer`
- `CustomerAddress`
- `DeliveryAgent`
- `ICustomerRepository`
- `IDeliveryAgentRepository`
- `CustomerRepository`
- `DeliveryAgentRepository`
- their EF configurations

### Added

The new core pieces are:

- `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs`
- `src/Talabat/Talabat.Domain/Aggregates/Users/UserAddress.cs`
- `src/Talabat/Talabat.Domain/Aggregates/Users/UserType.cs`
- `src/Talabat/Talabat.Domain/Aggregates/Users/VehicleType.cs`
- `src/Talabat/Talabat.Domain/Aggregates/Users/DeliveryAgentStatus.cs`
- `src/Talabat/Talabat.Domain/Aggregates/Users/AgentApprovalStatus.cs`
- `src/Talabat/Talabat.Domain/Interfaces/IUserRepository.cs`
- `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/UserRepository.cs`
- `src/Talabat/Talabat.Infrastructure/Identity/UserCapabilityService.cs`
- `src/Talabat/Talabat.Infrastructure/Identity/IdentityDataSeeder.cs`
- `src/Talabat/Talabat.Infrastructure/Identity/TalabatSignInManager.cs`
- `src/Talabat/Talabat.Infrastructure/Identity/UnifiedUserIdentityServiceCollectionExtensions.cs`

## 5. The New User Aggregate

`User` now holds:

- Identity fields from `IdentityUser<int>`
- `FullName`
- `Age`
- `PhoneNumber`
- `UserType`
- `IsActive`
- `VehicleType`
- `DeliveryAgentStatus`
- `AgentApprovalStatus`
- `CurrentLocation`
- `RowVersion`
- audit fields
- soft-delete fields
- owned `_addresses`

The important behavior lives here:

- `InitializeCustomerProfile`
- `UpdateCustomerProfile`
- `AddAddress`
- `RemoveAddress`
- `SetDefaultAddress`
- `CreateDeliveryAddressSnapshot`
- `SubmitDeliveryAgentApplication`
- `ApproveDeliveryAgentApplication`
- `RejectDeliveryAgentApplication`
- `GoOnline`
- `GoOffline`
- `Suspend`
- `MarkBusy`
- `MarkAvailable`
- `Deactivate`

This is the key design move: business rules moved into the aggregate instead of being split across multiple entities.

## 6. Persistence Design

`TalabatDbContext` now derives from:

```csharp
IdentityDbContext<User, IdentityRole<int>, int>
```

That gives us:

- integer Identity keys
- inherited `Users`
- normal Identity tables
- one unified user table mapped to `AspNetUsers`

### `UserConfiguration`

The user mapping includes:

- `AspNetUsers`
- `RowVersion`
- nullable enum conversions
- audit and soft-delete mapping
- `CurrentLocation`
- owned `UserAddresses`
- the filtered unique default-address index
- eight `CK_Users_*` checks

### Why this matters

The model contract is now explicit. EF and SQL both know:

- what a user row contains;
- what values are legal;
- how default addresses work;
- how deleted rows stay hidden while still being audited.

## 7. Capability Workflow

`UserCapabilityService` is the only place that mutates:

- `UserType`
- Identity role membership

It owns the customer and delivery-agent workflows:

- register customer
- register delivery-agent applicant
- grant customer capability
- approve delivery-agent applicant
- reject delivery-agent applicant
- deactivate user

### Transaction rule

Each call must be atomic.

It either:

- starts its own transaction, or
- creates a savepoint when it joins an existing one

If anything fails:

- rollback happens
- the change tracker is cleared
- the call returns a mapped application error

That is how the code avoids partial capability state.

### Why the service matters

Before this refactor, account state could be changed in too many places.
Now the mutation boundary is narrow and testable.

## 8. Host Authentication Setup

### Identity host

The Identity host uses the full chain:

```csharp
AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<TalabatDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager<TalabatSignInManager>()
```

It also seeds roles at startup.

### Customer API and Delivery API

Those hosts use:

```csharp
AddUnifiedUserIdentityCore()
```

That helper returns:

```csharp
AddIdentityCore<User>()
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<TalabatDbContext>()
```

This split matters because the app needs two different bootstrap styles:

- full Identity in the dedicated Identity host
- core identity services in the API hosts

## 9. Current User Resolution

`CurrentUser` in the Customer API now:

- caches resolution once per request
- prefers `NameIdentifier`, then `sub`
- rejects malformed, zero, or negative IDs
- does one no-tracking scalar `UserType` query
- derives `HasCustomerCapability`
- sets `CustomerId == UserId` only when the user has customer capability

This is important because the Customer API should not trust the token role claim alone.

It trusts persisted account state.

## 10. Profile Enforcement

The `ProfileEnforcementFilter` does three things:

- lets the POST profile route through for authenticated positive-int users without customer capability
- returns empty 401 when authentication succeeded but no valid positive subject can be derived
- returns the frozen `ProfileNotCreated` 404/409 bodies for the existing customer routes

That preserves the contract while tightening malformed-subject handling.

## 11. Identity Endpoints

The Identity host now exposes:

- `POST /account/register/customer`
- `POST /account/register/delivery-agent`
- `POST /account/login`
- `POST /account/logout`
- `GET /account/me`

Important details:

- no generic `POST /account/register`
- no caller-supplied role
- `GET /account/me` returns numeric `id`
- login still returns the same cookie-based behavior
- inactive and deleted users are denied

## 12. Delivery-Agent Flow

Delivery-agent handling is now a lifecycle on `User`:

1. applicant registers
2. applicant is stored with `PendingApproval`
3. approval flips the capability flag and role
4. agent becomes `Offline`
5. delivery assignment uses `User` instead of `DeliveryAgent`

The domain still preserves the old business names where they matter:

- `CustomerId`
- `AssignedAgentId`
- `DeliveryAgentStatus`

## 13. Migration and Schema

The new migration is `InitialUnifiedUser`.

It creates:

- `AspNetUsers`
- `AspNetRoles`
- the Identity link tables
- `UserAddresses`
- `Carts`
- `Orders`
- `Deliveries`
- `Restaurants`
- `Products`
- `CartItems`
- `OrderItems`

It removes the old account split and keeps the business foreign keys pointed at `AspNetUsers.Id`.

The schema contract also keeps:

- the eight checks
- the filtered default-address index
- the restricted business FKs
- the rowversion token

## 14. Test Strategy

The refactor is covered by four main test groups:

- Application tests
- Infrastructure SQL tests
- Identity host tests
- Customer API tests

The important scenarios are:

- customer registration
- delivery-agent applicant registration
- approval and rejection
- inactive/deleted login denial
- profile enforcement
- current-user resolution
- cart/order/checkout compatibility
- concurrency conflict mapping
- migration/persistence shape

## 15. What We Fixed After Review

During review we fixed three runtime issues:

- added the shared identity bootstrap to the Customer and Delivery hosts
- moved malformed-subject rejection ahead of the profile-route exemption
- removed transaction-state leakage from `UserCapabilityService`

Those fixes were needed because the code compiled but did not fully match the runtime contract.

## 16. What Stayed Intentionally Unchanged

We kept these names on purpose:

- `CustomerId`
- `AssignedAgentId`
- `DeliveryAgentStatus`
- `CustomerProfile`
- `CustomerAddressDetails`
- exact `ProfileNotCreated` response bodies

We also did not add:

- a separate delivery API implementation
- admin approval endpoints
- caller-supplied roles
- capability revocation workflows
- data-preserving migrations

## 17. Mental Model for the Whole Refactor

Think of the new design this way:

- Identity stores who the user is
- `UserType` stores what the user can do
- roles mirror that capability for authorization
- `UserRepository` reads user business state
- `UserCapabilityService` changes user capability state
- the API hosts read `CurrentUser` from persisted state, not from token claims alone

That is the core of the refactor.

If you understand those six ideas, the rest of the code falls into place.

