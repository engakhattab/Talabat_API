# Domain and Application Contracts: Unified User Phase 1

**Date**: 2026-07-18  
**Spec**: [spec.md](../spec.md)  
**Scope**: Compile-time contracts only; implementations and host exposure begin in Phase 2

Phase 1 exposes no HTTP endpoint, message, or persistence contract. It freezes two internal
cross-layer interfaces so later phases cannot invent competing account and capability boundaries.

## IUserRepository

**Location**: `src/Talabat/Talabat.Domain/Interfaces/IUserRepository.cs`  
**Owner**: Domain  
**Consumer/implementation**: Application/Infrastructure beginning in Phase 2

```csharp
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(
        int userId,
        CancellationToken ct = default);

    Task<User?> GetByIdReadOnlyAsync(
        int userId,
        CancellationToken ct = default);

    Task<User?> GetByIdWithAddressesAsync(
        int userId,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<User>> GetAvailableAgentsAsync(
        CancellationToken ct = default);

    void Update(User user);
}
```

### Behavioral Contract

| Member | Contract |
|--------|----------|
| `GetByIdAsync` | Returns a tracked aggregate suitable for business mutation, or null |
| `GetByIdReadOnlyAsync` | Returns a read-only/non-tracked aggregate, or null |
| `GetByIdWithAddressesAsync` | Returns a tracked aggregate with its private address collection loaded, or null |
| `GetAvailableAgentsAsync` | Returns users whose operational status is Available in stable full-name order; callers do not mutate the returned instances |
| `Update` | Marks an existing aggregate for update; it does not save or create |

There is deliberately no `AddAsync`. Account creation is owned by the Identity-backed capability
workflow in Phase 2. No method exposes query, context, or persistence-provider types.

## IUserCapabilityService

**Location**: `src/Talabat/Talabat.Application/Abstractions/IUserCapabilityService.cs`  
**Owner**: Application abstraction  
**Implementation**: Infrastructure beginning in Phase 2

```csharp
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Application.Abstractions;

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

### Behavioral Contract

| Member | Phase 2 responsibility fixed by this Phase 1 contract |
|--------|--------------------------------------------------------|
| `RegisterCustomerAsync` | Create one account, initialize Customer capability, and return its generated integer ID |
| `RegisterDeliveryAgentApplicantAsync` | Create one account and a pending vehicle application without granting DeliveryAgent |
| `GrantCustomerCapabilityAsync` | Add Customer capability to the identified existing user without removing other capabilities |
| `ApproveDeliveryAgentAsync` | Approve a pending application and synchronize the DeliveryAgent capability projection |
| `RejectDeliveryAgentAsync` | Reject a pending application without granting DeliveryAgent |
| `DeactivateUserAsync` | Deactivate the identified account without revoking capabilities or deleting profile state |

Every method returns a transport-neutral `UseCaseResult<int>`. Registration receives credentials and
profile data; existing-account operations receive an integer user ID. None accepts a role name,
authorization principal, request context, user manager, database context, or transaction type.

Phase 1 adds only the interface. Transactionality, Identity error mapping, role synchronization,
security-stamp changes, and dependency registration are explicitly Phase 2 work.

## Domain Failure Contracts

The following Phase 1 failures are stable Domain types:

| Failure | Contract |
|---------|----------|
| `CustomerProfileNotInitializedException` | Customer-only behavior was attempted without Customer capability |
| `DeliveryAgentNotInitializedException` | Agent-only behavior was attempted without an approved/initialized agent capability |
| `AgentApplicationNotPendingException` | An application decision is invalid because the application is not pending; also used when an approved user submits again |
| `ConcurrencyConflictException` | Reserved Domain failure for Phase 2 persistence conflicts; never raised by Phase 1 behavior |

Existing address and delivery-agent exceptions retain their current types and messages.

## Compatibility Boundary

- Existing `ICustomerRepository`, `IDeliveryAgentRepository`, and `ICurrentUser` remain unchanged.
- Existing Customer and DeliveryAgent runtime flows keep using their current contracts.
- No Phase 1 service implementation, role mutation, DI registration, host endpoint, or database
  mapping consumes these new contracts.
