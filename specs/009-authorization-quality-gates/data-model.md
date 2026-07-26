# Data Model: Authorization Strategy And Quality Gates

**Feature**: Phase 10 — Authorization Strategy And Quality Gates
**Date**: 2026-07-26

## Summary

Phase 10 introduces **no new entities**. Changes are to existing command records, repository interfaces, and authorization infrastructure.

## Modified Command Records

### Delivery Lifecycle Commands (add `AgentId`)

| Command | Current Signature | New Signature |
|---------|-------------------|---------------|
| `OutForDeliveryCommand` | `(int DeliveryId)` | `(int DeliveryId, int AgentId)` |
| `ArrivedAtRestaurantCommand` | `(int DeliveryId)` | `(int DeliveryId, int AgentId)` |
| `PickUpOrderCommand` | `(int DeliveryId)` | `(int DeliveryId, int AgentId)` |
| `DeliverOrderCommand` | `(int DeliveryId)` | `(int DeliveryId, int AgentId)` |
| `CancelDeliveryCommand` | `(int DeliveryId)` | `(int DeliveryId, int AgentId)` |
| `FailDeliveryCommand` | `(int DeliveryId, string Reason)` | `(int DeliveryId, int AgentId, string Reason)` |

**Pattern change**: Controllers populate `AgentId` from `ICurrentUser.AgentId`. Handlers use `command.AgentId` instead of injecting `ICurrentUser`.

### AssignDeliveryCommand (remove `AgentId`)

| Command | Current Signature | New Signature |
|---------|-------------------|---------------|
| `AssignDeliveryCommand` | `(int DeliveryId, int AgentId)` | `(int DeliveryId)` |

**Pattern change**: Controller populates from `ICurrentUser.AgentId`. `AssignDeliveryBody` record is removed (or reduced to empty).

## Modified Repository Interface

### IDeliveryRepository (add agent-scoped read)

```csharp
// NEW — agent-scoped delivery lookup for ownership enforcement
Task<Delivery?> GetByIdForAgentAsync(int deliveryId, int agentId, CancellationToken cancellationToken = default);
```

**Implementation**: Returns `null` when the delivery is not assigned to the specified agent. Used by lifecycle handlers to enforce ownership (returns 404 for unauthorized access).

## Authorization Infrastructure (new files in each API host)

### Auth/AuthorizationPolicies.cs

```csharp
public static class AuthorizationPolicies
{
    public const string CustomerAccess = nameof(CustomerAccess);
    public const string CustomerScopeOnly = nameof(CustomerScopeOnly);
    public const string DeliveryAgentAccess = nameof(DeliveryAgentAccess);
}
```

### Auth/ScopeRequirement.cs

```csharp
public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
```

### Auth/ScopeHandler.cs

```csharp
public sealed class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var scopes = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (scopes.Contains(requirement.Scope, StringComparer.Ordinal))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

## Policy Definitions

| Policy | Scope | Role | Used By |
|--------|-------|------|---------|
| `CustomerAccess` | `customer.api` | `Customer` | All Customer API controllers except `CatalogController` and `POST /api/me/profile` |
| `CustomerScopeOnly` | `customer.api` | (none) | `POST /api/me/profile` only |
| `DeliveryAgentAccess` | `delivery.api` | `DeliveryAgent` | All Delivery API controllers |

## No Changes To

- `User` aggregate
- `Delivery` aggregate
- `Cart`, `Order`, `Address` entities
- `TalabatDbContext` schema
- `TalabatProfileService` claims
- `IdentityServerConfig` clients/scopes/resources
- `ICurrentUser` interface
- `CurrentUser` implementations
- `ProfileEnforcementFilter`
