# Data Model: Unified User Domain Model (Phase 1)

**Date**: 2026-07-18  
**Spec**: [spec.md](spec.md)  
**Persistence status**: Domain-only in Phase 1; no mapping, schema, or migration changes

## Aggregate Boundary

```text
User (aggregate root, IdentityUser<int>)
├── identity/account fields inherited from IdentityUser<int>
├── customer profile and UserType capabilities
├── delivery-agent application and operational state
├── audit, soft-deletion, activation, and concurrency state
└── _addresses: List<UserAddress>
    └── Details: Address value object
```

`UserAddress` is a child entity. It is created, removed, and defaulted only through `User`; it has no
repository. `Address`, `GeoLocation`, and `DeliveryAddressSnapshot` remain existing value objects.

## User Aggregate

**Location**: `src/Talabat/Talabat.Domain/Aggregates/Users/User.cs`  
**Base type**: `IdentityUser<int>`  
**Interfaces**: `IAuditable`, `ISoftDeletable`

| Field/property | Type | Initial state | Validation / meaning |
|----------------|------|---------------|----------------------|
| `Id` | `int` | `0` | Inherited; database-generated only in Phase 2 |
| `UserName` | `string?` | registration input | Inherited account name; Identity policy validation is deferred |
| `Email` | `string?` | registration input | Inherited account email; Identity policy validation is deferred |
| `PhoneNumber` | `string?` | `null` | Inherited; normalized with `Guard.OptionalText` on profile changes |
| `FullName` | `string` | registration input | Required non-blank text |
| `Age` | `int?` | `null` | Positive when customer profile is initialized |
| `UserType` | `UserType` | `None` | Combinable Domain capability source of truth |
| `IsActive` | `bool` | `true` | Business activation; independent of soft deletion and lockout |
| `VehicleType` | `VehicleType?` | `null` | Bike, Motorcycle, or Car once an application is submitted |
| `DeliveryAgentStatus` | `DeliveryAgentStatus?` | `null` | Remains null until approval; then starts Offline |
| `CurrentLocation` | `GeoLocation?` | `null` | May be updated only by an initialized agent |
| `AgentApprovalStatus` | `AgentApprovalStatus?` | `null` | PendingApproval, Approved, or Rejected |
| `RowVersion` | `byte[]` | `[]` | Placeholder for Phase 2 SQL rowversion mapping |
| `CreatedAt` | `DateTime` | default | UTC audit creation time; stamped later by persistence |
| `CreatedBy` | `string?` | `null` | Normalized audit actor |
| `ModifiedAt` | `DateTime?` | `null` | UTC last-modified time |
| `ModifiedBy` | `string?` | `null` | Normalized modifying actor |
| `IsDeleted` | `bool` | `false` | Retained soft-deletion marker |
| `DeletedAt` | `DateTime?` | `null` | UTC deletion time when deleted |
| `DeletedBy` | `string?` | `null` | Normalized deleting actor |
| `_addresses` | `List<UserAddress>` | empty | Private mutable backing list |
| `Addresses` | `IReadOnlyCollection<UserAddress>` | empty read-only view | No public mutation |

### Construction and Customer Behavior

| Operation | Preconditions | State change / result |
|-----------|---------------|-----------------------|
| `Register(userName, email, fullName)` | `fullName` required | Sets inherited username/email, normalized full name, active true, `UserType.None` |
| `InitializeCustomerProfile(fullName, age, phoneNumber)` | required name; positive age | Replaces profile fields and ORs Customer flag |
| `UpdateCustomerProfile(...)` | Customer flag; valid inputs | Replaces profile fields without changing other flags |
| `AddAddress(address, makeDefault)` | Customer flag; non-null; no equal address | Adds child; when default, clears prior defaults first |
| `RemoveAddress(addressId)` | Customer flag; positive existing ID | Removes exactly the selected child; no replacement default |
| `SetDefaultAddress(addressId)` | Customer flag; positive existing ID | Clears every default, then selects exactly one |
| `CreateDeliveryAddressSnapshot(addressId)` | Customer flag; positive existing ID | Returns Street, City, BuildingNumber, Floor snapshot |
| `Activate()` / `Deactivate()` | none | Toggles only `IsActive` |

Calling a customer operation without the Customer flag throws
`CustomerProfileNotInitializedException`. Direct repeated initialization is permitted to replace
valid profile fields; Phase 2's capability workflow prevents duplicate onboarding before calling it.
For address operations, a non-positive ID is an argument-range failure; an unknown positive ID is
an `AddressNotFoundException`.

### Address Equality

Two addresses are duplicates when Street, City, BuildingNumber, and Floor all compare equal using
case-insensitive ordinal comparison. A null Floor equals only a null Floor after optional-text
normalization.

## UserAddress Child Entity

**Location**: `src/Talabat/Talabat.Domain/Aggregates/Users/UserAddress.cs`

| Field/property | Type | Rules |
|----------------|------|-------|
| `Id` | `int` | Starts `0`; later database-generated |
| `Details` | `Address` | Required, immutable value object |
| `IsDefault` | `bool` | Mutated only by internal methods called by User |

The private parameterless constructor exists only for later materialization. The functional
constructor and `MarkAsDefault` / `MarkAsNonDefault` remain internal.

## Capability Model

### UserType

| Name | Value | Meaning in Phase 1 |
|------|-------|--------------------|
| `None` | `0` | Registered account with no initialized business capability |
| `Customer` | `1` | Customer profile and address behaviors are available |
| `DeliveryAgent` | `2` | Approved delivery-agent behaviors are available |
| `Admin` | `4` | Reserved combinable classification; no Phase 1 grant behavior |
| `RestaurantOwner` | `8` | Reserved combinable classification; no Phase 1 grant behavior |

Capability changes preserve existing bits. Domain never reads or mutates Identity roles.

### AgentApprovalStatus

| Name | Value | Meaning |
|------|-------|---------|
| `PendingApproval` | `1` | Application awaits an admin decision; no agent flag/status |
| `Approved` | `2` | DeliveryAgent flag is present; operational status initialized |
| `Rejected` | `3` | Application rejected; may be resubmitted |

`VehicleType` and `DeliveryAgentStatus` remain in
`Talabat.Domain.Aggregates.DeliveryManagement` during Phase 1 and move only during Phase 2.

## Delivery-Agent Application State Transitions

| Current approval state | Submit application | Approve | Reject |
|------------------------|-------------------|---------|--------|
| `null` | Pending; store vehicle | Not-pending error | Not-pending error |
| PendingApproval | Remain Pending; refresh vehicle | Approved; add flag; status Offline | Rejected; flag absent; status null |
| Rejected | Pending; store vehicle | Not-pending error | Not-pending error |
| Approved | Reject as already approved | Not-pending error | Not-pending error |

Every approval/rejection failure leaves capability and operational state unchanged.

## Delivery-Agent Operational State Transitions

All operations require the DeliveryAgent flag and non-null status. Otherwise they throw
`DeliveryAgentNotInitializedException`.

| Current status | `GoOnline` | `GoOffline` | `Suspend` | internal `MarkBusy` | internal `MarkAvailable` |
|----------------|------------|-------------|-----------|---------------------|--------------------------|
| Offline | Available | Offline | Suspended | Agent-not-available error | Invalid-transition error |
| Available | Available | Offline | Suspended | Busy | Invalid-transition error |
| Busy | Agent-not-available error | Invalid-transition error | Invalid-transition error | Agent-not-available error | Available |
| Suspended | Agent-not-available error | Invalid-transition error | Suspended | Agent-not-available error | Invalid-transition error |

`IsAvailable()` is true only in Available. `UpdateLocation(nonNullLocation)` is allowed in any
initialized agent status and retains the `GeoLocation` latitude/longitude range rules.

## Audit and Soft-Deletion Contracts

### IAuditable

- Readable `CreatedAt`, `CreatedBy`, `ModifiedAt`, and `ModifiedBy`.
- `SetCreatedAudit` sets CreatedAt only when it is still default, always normalizes CreatedBy.
- `SetModifiedAudit` requires UTC and replaces modified metadata.

### ISoftDeletable

- Readable `IsDeleted`, `DeletedAt`, and `DeletedBy`.
- `SoftDelete` is idempotent; first call stores UTC delete metadata and modified metadata.
- `Restore` is idempotent; restoration clears delete metadata and updates modified metadata.

`AuditableEntity` and `User` share these contracts. Phase 1 generalizes audit discovery to
`IAuditable`; it does not persist User or introduce a User query filter.

## Domain Failures Added

| Failure | Trigger in Phase 1 |
|---------|--------------------|
| `CustomerProfileNotInitializedException` | Customer-only behavior without Customer flag |
| `DeliveryAgentNotInitializedException` | Agent-only behavior without both flag and status |
| `AgentApplicationNotPendingException` | Approve/reject when application is not pending |
| `ConcurrencyConflictException` | Defined for Phase 2; not thrown or mapped in Phase 1 |

Existing `DuplicateAddressException`, `AddressNotFoundException`, `AgentNotAvailableException`, and
`InvalidDeliveryAgentStatusTransitionException` retain their bodies and messages.

## Persistence Boundary

Phase 1 creates no database model. There is no User configuration, owned-address mapping, query
filter, concurrency mapping, migration, role table interaction, or repository implementation. Those
belong to Phase 2. The only Infrastructure change generalizes the existing audit interceptor's
entity discovery from a base class to `IAuditable`.
