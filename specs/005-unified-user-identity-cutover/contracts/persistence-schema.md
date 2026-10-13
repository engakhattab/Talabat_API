# Persistence Contract: InitialUnifiedUser

This contract is the review oracle for `UserConfiguration`, the generated migration/model snapshot,
SQL integration tests, and post-rebuild inspection.

## Required tables

The final database contains the existing catalog/cart/order/delivery tables plus standard ASP.NET
Core Identity tables using integer keys. Unified-user-specific tables are:

```text
AspNetUsers
UserAddresses
AspNetRoles
AspNetUserRoles
AspNetUserClaims
AspNetUserLogins
AspNetUserTokens
AspNetRoleClaims
```

Forbidden legacy tables:

```text
Customers
CustomerAddresses
DeliveryAgents
```

## AspNetUsers additions

| Column | Required shape |
|---|---|
| `Id` | `int IDENTITY`, primary key |
| `FullName` | required, max length 200 |
| `Age` | nullable int |
| `UserType` | required int |
| `IsActive` | required bit, default true |
| `VehicleType` | nullable int |
| `DeliveryAgentStatus` | nullable int |
| `AgentApprovalStatus` | nullable int |
| `CurrentLatitude` | nullable decimal(9,6) |
| `CurrentLongitude` | nullable decimal(9,6) |
| `RowVersion` | SQL rowversion/timestamp, concurrency token |
| `CreatedAt` | required datetime2 |
| `CreatedBy` | nullable, max 200 |
| `ModifiedAt` | nullable datetime2 |
| `ModifiedBy` | nullable, max 200 |
| `IsDeleted` | required bit, default false |
| `DeletedAt` | nullable datetime2 |
| `DeletedBy` | nullable, max 200 |

All standard Identity columns/indexes remain mapped for `IdentityUser<int>`.

## Required checks

The migration must create exactly these eight unified-user checks with equivalent SQL:

```sql
CK_Users_Age
([Age] IS NULL OR [Age] > 0)

CK_Users_VehicleType
([VehicleType] IS NULL OR [VehicleType] IN (1, 2, 3))

CK_Users_DeliveryAgentStatus
([DeliveryAgentStatus] IS NULL OR [DeliveryAgentStatus] IN (1, 2, 3, 4))

CK_Users_AgentApprovalStatus
([AgentApprovalStatus] IS NULL OR [AgentApprovalStatus] IN (1, 2, 3))

CK_Users_UserType_Range
([UserType] >= 0 AND [UserType] <= 15)

CK_Users_CurrentLocation_PairedNull
(([CurrentLatitude] IS NULL AND [CurrentLongitude] IS NULL) OR
 ([CurrentLatitude] IS NOT NULL AND [CurrentLongitude] IS NOT NULL))

CK_Users_CurrentLatitude_Range
([CurrentLatitude] IS NULL OR
 ([CurrentLatitude] >= -90 AND [CurrentLatitude] <= 90))

CK_Users_CurrentLongitude_Range
([CurrentLongitude] IS NULL OR
 ([CurrentLongitude] >= -180 AND [CurrentLongitude] <= 180))
```

No check may query/join `AspNetRoles` or `AspNetUserRoles`.

## UserAddresses

| Column | Required shape |
|---|---|
| `Id` | generated int key |
| `UserId` | required int FK to `AspNetUsers.Id` |
| `Street` | required, max 300 |
| `City` | required, max 120 |
| `BuildingNumber` | required, max 50 |
| `Floor` | nullable, max 50 |
| `IsDefault` | required bit |
| `IsDeleted` | required bit, default false |

Required unique filtered index:

```text
Name: UX_UserAddresses_UserId_Default
Key: UserId
Filter: [IsDefault] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit)
```

## Required retained foreign keys

| Dependent column | Principal | Delete behavior |
|---|---|---|
| `Carts.CustomerId` | `AspNetUsers.Id` | Restrict/NoAction |
| `Orders.CustomerId` | `AspNetUsers.Id` | Restrict/NoAction |
| `Deliveries.CustomerId` | `AspNetUsers.Id` | Restrict/NoAction |
| `Deliveries.AssignedAgentId` | `AspNetUsers.Id` | Restrict/NoAction |
| `UserAddresses.UserId` | `AspNetUsers.Id` | owned relationship |

The column/property names `CustomerId` and `AssignedAgentId` must not change.

## Roles

Identity-host startup converges role definitions to:

```text
Admin
Customer
DeliveryAgent
RestaurantOwner
```

The order shown is inspection order, not role priority. No users, passwords, claims, or user-role
memberships are seed data.

## Migration history

Source directory after regeneration:

```text
<timestamp>_InitialUnifiedUser.cs
<timestamp>_InitialUnifiedUser.Designer.cs
TalabatDbContextModelSnapshot.cs
```

Applied database:

- exactly one row in `__EFMigrationsHistory`;
- migration ID ends in `_InitialUnifiedUser`;
- no pending EF model changes.

The old `InitialPersistence`, `AddIdentitySchema`, `AddCustomerIdentityUserId`, old snapshot, and
`InitialPersistence.idempotent.sql` artifacts are absent.

## Schema inspection assertions

Post-rebuild inspection must prove:

1. forbidden legacy tables count is zero;
2. all eight check names exist on `AspNetUsers`;
3. the filtered address index exists, is unique, and has the exact filter;
4. the four retained business FKs reference `AspNetUsers`;
5. all four and only the expected role names exist;
6. no user is seeded solely by startup seeding;
7. one migration-history row exists; and
8. `RowVersion` is generated as SQL rowversion/timestamp.
