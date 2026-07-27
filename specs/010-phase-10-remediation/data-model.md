# Data Model: Phase 10 Remediation

## Entity Changes

### Delivery (modified)

| Property | Type | Change | Notes |
|----------|------|--------|-------|
| `RowVersion` | `byte[]` | **ADD** | Private setter. Initialized to `[]` in private constructor. EF maps as `IsRowVersion()`. Mirrors `User.RowVersion`. |

**No new entities introduced.** The `CreateDeliveryForOrderCommand` is a use case record, not an entity.

### Delivery — EF Configuration

```csharp
// In DeliveryConfiguration.cs
builder.Property(d => d.RowVersion).IsRowVersion();
```

**Migration**: `AddDeliveryRowVersion` — adds `RowVersion` column with `rowversion` SQL Server type.

## Use Case Records

### CreateDeliveryForOrderCommand (new)

```csharp
public sealed record CreateDeliveryForOrderCommand(
    int OrderId,
    int CustomerId,
    int RestaurantId,
    DeliveryAddressSnapshot DeliveryAddress);
```

### UpdateLocationRequest (new)

```csharp
public sealed record UpdateLocationRequest(decimal Latitude, decimal Longitude);
```

Separate from `UpdateLocationCommand` to prevent BOLA — `AgentId` is injected server-side.

## DTO Changes

### PendingDeliveryDto (modified)

**Before**:
```csharp
public sealed record PendingDeliveryDto(
    int Id, int OrderId, int CustomerId, int RestaurantId,
    DeliveryStatus Status, string Street, string City,
    string BuildingNumber, string? Floor, DateTime CreatedAt);
```

**After**:
```csharp
public sealed record PendingDeliveryDto(
    int Id, int OrderId, int RestaurantId,
    DeliveryStatus Status, string City, DateTime CreatedAt);
```

Removed: `CustomerId`, `Street`, `BuildingNumber`, `Floor` (PII reduction pre-assignment).

## Policy Changes

No new authorization policies. The remediation operates within Phase 10's existing policy framework (`CustomerAccess`, `CustomerScopeOnly`, `DeliveryAgentAccess`).

## Error Codes

| Code | Category | HTTP | Usage |
|------|----------|------|-------|
| `DeliveryAlreadyExists` | Conflict | 409 | Duplicate `CreateDeliveryForOrder` for same order |
| `ConcurrencyConflict` | Conflict | 409 | Concurrent delivery assignment (existing) |
| `AgentRequired` | OwnershipMismatch | 403 | Missing agent capability (existing) |
