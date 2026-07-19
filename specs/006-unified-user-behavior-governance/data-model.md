# Data Model: Unified User Behavior and Governance

**Date**: 2026-07-19  
**Spec**: [spec.md](spec.md)  
**Persisted model delta**: None

Phase 3 does not add a table, column, relationship, index, constraint, seed record, or migration.
This document freezes the existing Phase 2 model portions exercised by the behavior proof.

## Unified User

Persisted in `AspNetUsers`, with database-generated positive integer `Id`.

| Concern | Existing fields | Phase 3 rule |
|---|---|---|
| Customer profile | `FullName`, nullable `Age`, `PhoneNumber`, owned addresses | Customer behavior requires `UserType.Customer`; `CustomerId == User.Id`. |
| Capabilities | `UserType` flags | Domain source of truth; values may be combined. |
| Agent application | `VehicleType`, nullable `AgentApprovalStatus` | Pending/rejected users have no DeliveryAgent flag or role. |
| Agent operation | nullable `DeliveryAgentStatus`, `CurrentLocation` | Assignment requires initialized DeliveryAgent capability and Available state. |
| Account state | `IsActive`, `IsDeleted` | Either blocked state denies new login and invalidates existing sessions at validation. |
| Concurrency | `RowVersion` | Every user-row update changes the token; stale writers conflict. |
| Audit/deletion | created/modified/deleted fields | Blocking does not erase business history or capabilities. |

## Capability and Role Projection

`UserType` is persisted business state. Identity roles are an authorization projection in
`AspNetUserRoles`.

| UserType flag | Required projected role |
|---|---|
| `Customer` | `Customer` |
| `DeliveryAgent` | `DeliveryAgent` |
| `Admin` | `Admin` |
| `RestaurantOwner` | `RestaurantOwner` |

Projection rules:

1. After a successful workflow, the role set equals all and only mapped flags.
2. An applicant in PendingApproval or Rejected has no DeliveryAgent flag, role, or operational
   status.
3. Customer grant adds Customer without removing DeliveryAgent or any other existing flag/role.
4. A failed role/stamp step rolls back flag, role, application, operational, and stamp changes.
5. Deactivation changes account activity and security stamp but preserves the capability/role set.

## Delivery-Agent State Transitions

```text
No application
  -> PendingApproval (vehicle selected; no flag/role/status)
  -> Rejected        (no flag/role/status)
  -> PendingApproval (re-apply allowed)

PendingApproval
  -> Approved + DeliveryAgent flag/role + Offline

Offline -> Available -> Busy -> Available
Available -> Offline
Offline/Available -> Suspended
Busy -X-> Offline or Suspended
```

Assignment interpretation:

- no DeliveryAgent capability/status: `DeliveryAgentNotInitializedException`;
- initialized but not Available: `AgentNotAvailableException`;
- Available: assignment stores `AssignedAgentId == User.Id`, then agent becomes Busy;
- completion/cancellation/failure for the matching Busy agent returns it to Available.

## Owner-Scoped Relationships

Existing foreign keys remain unchanged:

| Dependent | Business key | Principal |
|---|---|---|
| `UserAddresses` | `UserId` | `AspNetUsers.Id` |
| `Carts` | `CustomerId` | `AspNetUsers.Id` |
| `Orders` | `CustomerId` | `AspNetUsers.Id` |
| `Deliveries` | `CustomerId` | `AspNetUsers.Id` |
| `Deliveries` | `AssignedAgentId` | `AspNetUsers.Id` |

Transport ownership rules do not alter these relationships:

- authenticated positive int `sub` resolves `User.Id`;
- `CustomerId` is exposed only when current stored `UserType` contains Customer;
- controllers pass that derived ID to handlers;
- an address/order ID is queried within the current CustomerId boundary and returns 404 when not
  owned;
- `/api/me/cart` accepts no cart/customer ID and can return only the current customer's cart or its
  empty representation.

## Concurrency Lifecycle

```text
Writer A loads User(RowVersion = V1)
Writer B loads User(RowVersion = V1)
Writer A saves -> database stores V2
Writer B saves with V1 -> stale-write conflict
UnitOfWork -> ConcurrencyConflictException
Application -> Conflict / ConcurrencyConflict
Customer API -> HTTP 409 ProblemDetails
```

Different address-row updates remain independent when they do not update the `AspNetUsers` row.

## Session Validity Projection

The authentication cookie is not Domain data. Its validity is projected from the user's current
Identity security stamp and resolvable account state.

| Event | Persistent outcome | Existing-session outcome |
|---|---|---|
| Customer/DeliveryAgent role grant | capability/role + refreshed security stamp | old cookie rejected at next validation |
| Deactivate | `IsActive = false` + refreshed security stamp | new login denied; old cookie rejected at next validation |
| Soft delete | `IsDeleted = true`; ordinary user query hides row | new login denied; validator cannot retain old principal |

Production validation interval remains five minutes; tests may set zero only in test DI.

## Schema Acceptance

- Migration source remains exactly the timestamped `InitialUnifiedUser` pair plus model snapshot.
- Applied development history remains one `InitialUnifiedUser` row.
- Phase 3 must produce no pending model change.
