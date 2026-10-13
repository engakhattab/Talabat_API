# Application Ports: Unified User Phase 2

These signatures are binding. Implementations may add private helpers but must not expose EF Core,
ASP.NET Core Identity, claims, HTTP, or caller-provided role names through them.

## ICurrentUser v2

**Path**: `src/Talabat/Talabat.Application/Abstractions/ICurrentUser.cs`

```csharp
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    int? UserId { get; }

    bool HasCustomerCapability { get; }

    int? CustomerId { get; }
}
```

Invariants:

- `UserId` is a positive parsed `sub`/name-identifier value or null.
- `IsAuthenticated` is true only when ASP.NET authenticated the principal and the selected subject
  is a positive integer.
- `HasCustomerCapability` comes from current persisted `UserType`, never a role claim.
- `CustomerId == UserId` only when Customer capability exists; otherwise `CustomerId` is null.
- Resolution and its scalar database lookup are cached per request.

Removed members: `IdentityUserId`, `HasProfile`.

## IUserRepository

**Path**: `src/Talabat/Talabat.Domain/Interfaces/IUserRepository.cs`

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);

    Task<User?> GetByIdReadOnlyAsync(int userId, CancellationToken ct = default);

    Task<User?> GetByIdWithAddressesAsync(int userId, CancellationToken ct = default);

    Task<IReadOnlyCollection<User>> GetAvailableAgentsAsync(CancellationToken ct = default);

    void Update(User user);
}
```

Infrastructure semantics:

| Member | Required query behavior |
|---|---|
| `GetByIdAsync` | tracked `SingleOrDefaultAsync` by `Id` |
| `GetByIdReadOnlyAsync` | `AsNoTracking`, `SingleOrDefaultAsync` by `Id` |
| `GetByIdWithAddressesAsync` | tracked `Include("_addresses")`, `SingleOrDefaultAsync` by `Id` |
| `GetAvailableAgentsAsync` | `AsNoTracking`, DeliveryAgentStatus Available, `OrderBy(FullName)`, materialized read-only collection |
| `Update` | null guard, then `_dbContext.Users.Update(user)` |

All ordinary queries inherit the `!IsDeleted` global filter. There is no `AddAsync`; new accounts
must go through `UserManager<User>` inside `IUserCapabilityService`.

## IUserCapabilityService

**Paths**:

- Interface: `src/Talabat/Talabat.Application/Abstractions/IUserCapabilityService.cs`
- Implementation: `src/Talabat/Talabat.Infrastructure/Identity/UserCapabilityService.cs`

```csharp
public interface IUserCapabilityService
{
    Task<UseCaseResult<int>> RegisterCustomerAsync(
        string email,
        string password,
        string fullName,
        int age,
        string? phoneNumber,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> RegisterDeliveryAgentApplicantAsync(
        string email,
        string password,
        string fullName,
        VehicleType vehicleType,
        string? phoneNumber,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> GrantCustomerCapabilityAsync(
        int userId,
        string fullName,
        int age,
        string? phoneNumber,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> ApproveDeliveryAgentAsync(
        int userId,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> RejectDeliveryAgentAsync(
        int userId,
        CancellationToken ct = default);

    Task<UseCaseResult<int>> DeactivateUserAsync(
        int userId,
        CancellationToken ct = default);
}
```

The implementation injects only the shared scoped `TalabatDbContext` and `UserManager<User>`.
`RoleManager` is for role-definition seeding, not required for user-role deltas.

### Server-owned role projection

| Domain flag | Role name constant |
|---|---|
| `UserType.Customer` | `Customer` |
| `UserType.DeliveryAgent` | `DeliveryAgent` |
| `UserType.Admin` | `Admin` |
| `UserType.RestaurantOwner` | `RestaurantOwner` |

Phase 2 public methods grant only Customer and DeliveryAgent. Admin and RestaurantOwner definitions
are seeded but have no user grant path. No method accepts a role string or general `UserType` input.

### Result contract

| Operation/outcome | Result |
|---|---|
| Successful mutation | `UseCaseResult<int>.Success(user.Id)` after persistence assigned a positive ID |
| Duplicate/invalid account creation | `IdentityOperationFailed`, Validation |
| Missing target user | `UserNotFound`, NotFound |
| Existing Customer capability | `ProfileAlreadyExists`, Conflict, exact existing message |
| Non-pending approve/reject | mapped Domain failure, Conflict |
| Failed role or security-stamp projection, including a missing role-store definition | `IdentityOperationFailed`, Conflict |
| Stale rowversion | `ConcurrencyConflict`, Conflict |
| Invalid profile/vehicle input | existing mapped validation/domain result |

Handled failures must roll back the entire workflow. The implementation maps
`InvalidOperationException` only when it is thrown by the explicitly wrapped role/stamp step (the
EF store uses it for a missing role); unrelated unexpected exceptions and cancellation roll back,
then propagate rather than being disguised as validation results.

### Mutation ownership

Production calls that may appear only inside `UserCapabilityService`:

```text
User.InitializeCustomerProfile(...) when granting a capability
User.ApproveDeliveryAgentApplication()
User.RejectDeliveryAgentApplication()
User.Deactivate() through the account workflow
UserManager.CreateAsync(...)
UserManager.AddToRoleAsync(...)
UserManager.UpdateSecurityStampAsync(...)
```

Business handlers may call normal profile/address/agent operational methods after loading through
`IUserRepository`, but they may not directly set `UserType`, approve an applicant, or alter Identity
role membership.

## Create Customer Profile Use Case

The retained Customer API profile creation command changes only its identity input:

```csharp
public sealed record CreateCustomerProfileCommand(
    int UserId,
    string FullName,
    int Age,
    string? PhoneNumber);
```

`CreateCustomerProfileHandler` injects only `IUserCapabilityService` and returns the result of
`GrantCustomerCapabilityAsync`. It no longer creates a separate aggregate, calls a repository, or
saves a unit of work.

All other customer/cart/order/checkout commands retain the business property name `CustomerId`.

## Concurrency Translation

```text
TalabatDbContext.SaveChangesAsync
  -> DbUpdateConcurrencyException
UnitOfWork
  -> ConcurrencyConflictException
DomainExceptionMapper
  -> ApplicationError("ConcurrencyConflict", Conflict, existing exception message)
Customer API UseCaseResultExtensions
  -> HTTP 409 ProblemDetails
```

Application handlers must execute `SaveChangesAsync` inside the same try/catch that maps Domain
failures; otherwise the Domain concurrency exception would escape to the generic exception handler.
