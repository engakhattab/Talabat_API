# Research & Design Decisions: DeliveryAgent API

## 1. Authentication & API Scopes

### Decision
The `Talabat.Delivery.API` will use ASP.NET Core's `Microsoft.AspNetCore.Authentication.JwtBearer` package to validate JWT access tokens minted by `Talabat.Identity`.

- **Authority**: Dynamically configured via `Configuration["Identity:Authority"]` (defaults to `https://localhost:7237`).
- **Audience**: Configured as `talabat.deliveryagent-api`.
- **Scope Verification**: Incoming requests must possess the `talabat.deliveryagent-api` scope in their claims.
- **Role Claim Type**: Configured to map the `"role"` claim to ensure `[Authorize(Roles = "DeliveryAgent")]` policy works out of the box.

### Rationale
This aligns with the Customer API pattern (`Talabat.Customer.API`), maintaining consistency in authentication setup and middleware pipeline layout while strictly isolating the audience and scopes.

---

## 2. Extending Current User Abstraction (`ICurrentUser`)

### Decision
We will extend `ICurrentUser` in `Talabat.Application` to support DeliveryAgent-specific operations:

```csharp
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    bool HasCustomerCapability { get; }
    int? CustomerId { get; }
    bool HasDeliveryAgentCapability { get; }
    int? AgentId { get; }
}
```

The concrete `CurrentUser` implementation in both `Talabat.Customer.API` and `Talabat.Delivery.API` will be updated to:
1. Read the `sub` (or `NameIdentifier`) claim to parse `UserId`.
2. Inspect the user's `UserType` flag inside `TalabatDbContext` (loaded via `AsNoTracking()` to avoid EF tracking overhead).
3. Populate `HasDeliveryAgentCapability = userType.HasFlag(UserType.DeliveryAgent)`.
4. Set `AgentId = UserId` if the capability is present.

### Rationale
Using a shared application contract `ICurrentUser` ensures that use-case handlers remain transport-neutral and decoupled from HTTP-specific namespaces (`HttpContext`, `ClaimsPrincipal`). Resolving `UserType` flags directly from the DB ensures security tokens don't drift from state changes, keeping the single-source-of-truth invariant in tact.

---

## 3. CQRS-lite Handler Design

### Decision
Each endpoint will invoke a dedicated use-case handler in `Talabat.Application`.

- **Online/Offline Status**: `GoOnlineHandler` and `GoOfflineHandler` load the `User` aggregate via `IUserRepository.GetByIdAsync`, call `GoOnline()` / `GoOffline()`, and commit via `IUnitOfWork`.
- **Location Updates**: `UpdateLocationHandler` calls `UpdateLocation()` on the `User` aggregate and commits.
- **Assignment**: `AssignDeliveryAgentHandler` coordinates the assignment using `DeliveryAssignmentDomainService.AssignAgentAsync` to ensure both delivery and agent states transition atomically in a single transaction.
- **Lifecycle Progression**: Handlers for arrived at restaurant, picked up, out for delivery, and delivered will load the `Delivery` aggregate, execute the respective progression method, and commit via `IUnitOfWork`.
- **Delivery history and active delivery queries**:
  - `GetActiveDeliveryHandler`: Calls `IDeliveryRepository.GetActiveByAgentIdAsync`.
  - `GetDeliveryHistoryHandler`: Calls `IDeliveryRepository.GetAssignedToAgentAsync`.
  - `GetPendingDeliveriesHandler`: Calls `IDeliveryRepository.GetPendingAssignmentAsync`.

---

## 4. Exception Mapping

### Decision
We will extend `DomainExceptionHandler` to map delivery-specific domain exceptions:

| Domain Exception | HTTP Status | Problem Detail Title |
|---|---|---|
| `AgentNotAvailableException` | 400 Bad Request | Agent Not Available |
| `DeliveryNotAssignedException` | 400 Bad Request | Delivery Not Assigned |
| `DeliveryAgentMismatchException` | 403 Forbidden | Access Denied |
| `DeliveryTerminalStateException` | 400 Bad Request | Invalid Lifecycle Operation |
| `InvalidDeliveryStatusTransitionException` | 400 Bad Request | Invalid Status Transition |
| `InvalidDeliveryTimestampException` | 400 Bad Request | Invalid Timestamp |
| `DeliveryAlreadyAssignedException` | 409 Conflict | Concurrency Conflict |

### Rationale
Keeps API controllers clean and ensures clients receive compliant Problem Details (RFC 7807) with detailed error types.
