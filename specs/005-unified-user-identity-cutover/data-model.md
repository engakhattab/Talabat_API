# Data Model: Unified User Identity and Persistence Cutover

**Feature**: Phase 2 unified-user cutover  
**Persistence target**: SQL Server through `TalabatDbContext`  
**Identity target**: `IdentityDbContext<User, IdentityRole<int>, int>`

## Aggregate: User

`Talabat.Domain.Aggregates.Users.User` is both the ASP.NET Core Identity account and the business
aggregate root. It is stored in `AspNetUsers`; there is no separate account, customer profile, or
delivery-agent table.

### Identity-owned fields

These are inherited from `IdentityUser<int>` and retain ASP.NET Core Identity's model/validation
behavior:

| Field | Type | Phase 2 meaning |
|---|---|---|
| `Id` | `int` | SQL-generated positive account and business identifier; emitted as `sub` |
| `UserName` / `NormalizedUserName` | `string?` | Email is supplied as sign-in name; Identity normalizes and enforces uniqueness |
| `Email` / `NormalizedEmail` | `string?` | Account email; duplicate validation remains Identity-owned |
| `EmailConfirmed` | `bool` | Existing Identity behavior; Phase 2 adds no confirmation flow |
| `PasswordHash` | `string?` | Credential hash; never returned by an endpoint |
| `SecurityStamp` | `string?` | Refreshed after role deltas and deactivation to invalidate cookies |
| `ConcurrencyStamp` | `string?` | Identity internal concurrency metadata; not the business rowversion |
| `PhoneNumber` | `string?` | Customer/applicant phone normalized through the aggregate/workflow |
| Remaining Identity fields | existing Identity types | Existing account policies remain unchanged |

### Business-owned fields

| Field | CLR type | Database rule | Meaning |
|---|---|---|---|
| `FullName` | `string` | required, max 200 | Shared person name for all capabilities |
| `Age` | `int?` | null or `> 0` | Customer profile age; initialized with Customer capability |
| `UserType` | flags enum | int 0–15 | Business capability source of truth |
| `IsActive` | `bool` | required, default true | Business account activation; false blocks sign-in |
| `VehicleType` | `VehicleType?` | null or 1–3 | Agent application vehicle |
| `AgentApprovalStatus` | `AgentApprovalStatus?` | null or 1–3 | Pending/approved/rejected application state |
| `DeliveryAgentStatus` | `DeliveryAgentStatus?` | null or 1–4 | Offline/Available/Busy/Suspended operational state |
| `CurrentLocation` | `GeoLocation?` | paired nullable/ranged coordinates | Current approved-agent location |
| `RowVersion` | `byte[]` | SQL `rowversion` | Business optimistic concurrency token for User-row writes |
| `CreatedAt` / `CreatedBy` | audit fields | shared audit mapping | Creation audit stamped even through UserManager |
| `ModifiedAt` / `ModifiedBy` | audit fields | shared audit mapping | Last principal-row modification audit |
| `IsDeleted` / deletion fields | soft-delete fields | query filter `!IsDeleted` | Ordinary loads and UserManager paths hide deleted accounts |
| `_addresses` | `List<UserAddress>` | owned field navigation | Customer addresses modified only through User |

### Capability flags and authorization projection

| Capability | Flag | Identity role | Grant path in Phase 2 |
|---|---:|---|---|
| None | 0 | none | New base account/applicant before approval |
| Customer | 1 | `Customer` | Customer registration or existing-account onboarding |
| DeliveryAgent | 2 | `DeliveryAgent` | Pending application approval only |
| Admin | 4 | `Admin` | Definition seeded; no user grant workflow this phase |
| RestaurantOwner | 8 | `RestaurantOwner` | Definition seeded; no user grant workflow this phase |

Flags may combine by bitwise OR. Workflows add only their capability/role and preserve unrelated
flags, role memberships, application data, operational state, and business history. Capability
revocation is not modeled in Phase 2.

### Customer lifecycle

```text
UserType lacks Customer
  -- InitializeCustomerProfile(fullName, positive age, phone) -->
UserType includes Customer + customer fields initialized
  -- UpdateCustomerProfile(...) --> same capability, changed profile
  -- Add/Remove/SetDefaultAddress --> same capability, changed owned rows
```

Calling the public onboarding workflow when Customer is already present returns
`ProfileAlreadyExists` before invoking initialization. Customer registration creates the user and
Customer projection atomically. A delivery agent can become a Customer on the same `Id` without
losing agent state.

### Delivery-agent application and operational lifecycle

```text
No application
  -- SubmitDeliveryAgentApplication(vehicle) --> PendingApproval

PendingApproval
  -- RejectDeliveryAgentApplication() --> Rejected
  -- ApproveDeliveryAgentApplication() --> Approved
                                           + DeliveryAgent flag
                                           + DeliveryAgent role (workflow)
                                           + Offline status

Approved + Offline
  -- GoOnline() --> Available

Approved + Available
  -- MarkBusy() [Domain service only] --> Busy
  -- GoOffline() --> Offline
  -- Suspend() --> Suspended

Approved + Busy
  -- MarkAvailable() [Domain service only] --> Available
  -- GoOffline/Suspend --> rejected transition
```

Rejected applications may be resubmitted through the existing Domain submission behavior and
return to PendingApproval. Applicants and rejected users have null operational status and cannot
appear in the available-agent query. Agent approval/rejection remains a service-level operation;
there is no Phase 2 admin controller.

### Activation and deletion

| State | Ordinary query | Password sign-in | Business history/FKs |
|---|---|---|---|
| Active, not deleted | visible | follows base Identity eligibility | retained |
| Inactive, not deleted | visible | explicitly rejected | retained |
| Soft-deleted | hidden by global filter | explicitly forbidden by rule; ordinary name lookup also hides it | retained |

`Deactivate()` is idempotent and does not remove flags, roles, carts, orders, deliveries, or agent
history. The capability workflow updates `SecurityStamp` after deactivation. `Restore`/`Activate`
exist on the Domain model but no Phase 2 public workflow is added for them.

## Owned Entity: UserAddress

`UserAddress` is owned by one User and stored in `UserAddresses`.

| Field | Type | Mapping |
|---|---|---|
| `Id` | `int` | generated key within the owned table |
| `UserId` | shadow `int` | required FK to `AspNetUsers.Id` |
| `Street` | `string` | required, max 300 |
| `City` | `string` | required, max 120 |
| `BuildingNumber` | `string` | required, max 50 |
| `Floor` | `string?` | max 50 |
| `IsDefault` | `bool` | required |
| `IsDeleted` | shadow `bool` | required, default false; used by the filtered index |

Index `UX_UserAddresses_UserId_Default` is unique on `UserId` with filter:

```sql
[IsDefault] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit)
```

This guarantees at most one active default address. The Domain also clears existing default flags
before selecting a new default.

Phase 2 preserves current removal behavior: removing an owned address deletes its row. The shadow
soft-delete column is mapped because the target schema/index requires it, but no new production
workflow sets it to true.

## Value Object: GeoLocation

`CurrentLocation` is an optional owned value mapped into the `AspNetUsers` row:

| Column | SQL shape | Rule |
|---|---|---|
| `CurrentLatitude` | decimal(9,6), nullable | null or -90 through 90 |
| `CurrentLongitude` | decimal(9,6), nullable | null or -180 through 180 |

Both columns must be null or both present.

## Retained Aggregate Relationships

Business property names stay unchanged even though principals change:

```text
AspNetUsers.Id
├── Carts.CustomerId          (required, Restrict)
├── Orders.CustomerId         (required, Restrict)
├── Deliveries.CustomerId     (required, Restrict)
├── Deliveries.AssignedAgentId(optional, Restrict)
└── UserAddresses.UserId      (owned relationship)
```

Retained relationship/index behavior:

| Dependent | Existing name retained | Important existing index/rule retained |
|---|---|---|
| Cart | `CustomerId` | one active non-deleted cart per customer |
| Order | `CustomerId` | customer/creation-time history index |
| Delivery | `CustomerId` | order and customer relationships remain restricted |
| Delivery | `AssignedAgentId` | one active delivery per assigned agent filtered by delivery status |

No `UserId` replacement property and no `IdentityUserId` linkage property is introduced.

## Database Checks on AspNetUsers

| Name | SQL predicate |
|---|---|
| `CK_Users_Age` | `([Age] IS NULL OR [Age] > 0)` |
| `CK_Users_VehicleType` | `([VehicleType] IS NULL OR [VehicleType] IN (1, 2, 3))` |
| `CK_Users_DeliveryAgentStatus` | `([DeliveryAgentStatus] IS NULL OR [DeliveryAgentStatus] IN (1, 2, 3, 4))` |
| `CK_Users_AgentApprovalStatus` | `([AgentApprovalStatus] IS NULL OR [AgentApprovalStatus] IN (1, 2, 3))` |
| `CK_Users_UserType_Range` | `([UserType] >= 0 AND [UserType] <= 15)` |
| `CK_Users_CurrentLocation_PairedNull` | `(([CurrentLatitude] IS NULL AND [CurrentLongitude] IS NULL) OR ([CurrentLatitude] IS NOT NULL AND [CurrentLongitude] IS NOT NULL))` |
| `CK_Users_CurrentLatitude_Range` | `([CurrentLatitude] IS NULL OR ([CurrentLatitude] >= -90 AND [CurrentLatitude] <= 90))` |
| `CK_Users_CurrentLongitude_Range` | `([CurrentLongitude] IS NULL OR ([CurrentLongitude] >= -180 AND [CurrentLongitude] <= 180))` |

The database deliberately does not enforce “Age required for Customer” or role-dependent field
rules because SQL CHECK constraints cannot join Identity role tables and `UserType` lifecycle rules
belong in the Domain/workflow.

## Concurrency Model

`RowVersion` protects changes that update the `AspNetUsers` principal row:

```text
load User + RowVersion A
  -> writer 1 updates User -> database RowVersion B
  -> writer 2 updates with original A -> DbUpdateConcurrencyException
  -> ConcurrencyConflictException
  -> ApplicationError(ConcurrencyConflict, Conflict)
  -> HTTP 409 at web result boundary
```

Address-only changes affect `UserAddresses` and need not bump the principal rowversion. Competing
default-address changes are governed by the unique filtered index.

## Audit and Query Model

`ConfigureAuditing<TEntity>()` maps the same audit/deletion fields for every type implementing both
interfaces. The save interceptor discovers `IAuditable`, so Identity-store saves stamp User rows.
The global `!IsDeleted` filter affects repository and UserManager queries unless explicitly ignored;
Phase 2 adds no `IgnoreQueryFilters` production path.

## Migration Target

The final migration source contains exactly:

```text
<timestamp>_InitialUnifiedUser.cs
<timestamp>_InitialUnifiedUser.Designer.cs
TalabatDbContextModelSnapshot.cs
```

The applied development database has one `__EFMigrationsHistory` row whose ID ends in
`_InitialUnifiedUser`. There are no `Customers`, `CustomerAddresses`, or `DeliveryAgents` tables,
and no string Identity user/role keys.
