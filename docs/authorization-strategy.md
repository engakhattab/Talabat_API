# Authorization Strategy

**Date**: 2026-07-26
**Status**: Current

## Overview

The Talabat platform uses a four-gate authorization model that layers authentication, scope enforcement, role-based access control, and ownership verification. Each gate returns a specific HTTP status code on failure, providing clear diagnostic signals to clients.

## Four-Gate Model

Every protected endpoint request passes through four sequential gates. A failure at any gate short-circuits the remaining gates.

```
Request
  |
  v
[Gate 1: Authentication]  -- 401 if missing/invalid token
  |
  v
[Gate 2: Scope Enforcement]  -- 403 if token lacks required scope
  |
  v
[Gate 3: Role-Based Access]  -- 403 if token lacks required role
  |
  v
[Gate 4: Ownership Verification]  -- 404 if resource doesn't belong to caller
  |
  v
Controller Action
```

### Gate 1: Authentication (401)

The ASP.NET Core authentication middleware validates the JWT bearer token. If the token is missing, expired, malformed, or fails signature validation, the request is rejected with 401 before reaching any controller.

**Implementation**: `JwtBearerDefaults.AuthenticationScheme` in both API hosts.

### Gate 2: Scope Enforcement (403)

The `ScopeHandler` (`IAuthorizationHandler`) inspects the `scope` claim for a space-delimited or multi-entry scope string. If the required scope is absent, the handler does not call `context.Succeed()`, and ASP.NET Core returns 403.

**Implementation**: `ScopeRequirement` + `ScopeHandler` in each API host.

| Host | Required Scope |
|------|---------------|
| Customer API | `customer.api` |
| Delivery API | `delivery.api` |

**Scope claim format**: The `scope` claim supports space-separated values (e.g., `"customer.api orders.read"`) or multiple `scope` claims. The handler splits by space and performs ordinal comparison.

### Gate 3: Role-Based Access (403)

ASP.NET Core's built-in role policy (`policy.RequireRole(...)`) checks the `role` claim. The role claim type is configured as `"role"` on `TokenValidationParameters`.

**Implementation**: Role requirements are part of named policies (see Policy Definitions below).

### Gate 4: Ownership Verification (404)

After authorization succeeds, the controller or handler resolves the caller's identity from `ICurrentUser` and verifies ownership of the target resource. Foreign-resource access returns 404 (not 403) to avoid leaking resource existence.

**Implementation**:
- **Customer API**: `ICurrentUser.CustomerId` resolves from `sub` claim + `UserType.Customer` flag. All `/api/me/*` handlers receive `CustomerId` from `ICurrentUser`, never from the request.
- **Delivery API**: `ICurrentUser.AgentId` resolves from `sub` claim + `UserType.DeliveryAgent` flag. Lifecycle commands carry `AgentId` populated by the controller. `GetByIdForAgentAsync` enforces ownership at the repository level.

## Status-Code Policy

| Code | Meaning | When Used |
|------|---------|-----------|
| **401** | Unauthorized | Missing or invalid bearer token |
| **403** | Forbidden | Authenticated but lacks required scope or role |
| **404** | Not Found | Resource does not exist or does not belong to the caller; also used for missing customer profile on `GET /api/me/profile` |
| **409** | Conflict | `ProfileNotCreated` on `PUT /api/me/profile` and all other `/api/me/*` routes when no customer profile exists |
| **400** | Bad Request | Validation failure (empty name, non-positive age, etc.) |

### Why 404 for Ownership Failures

Ownership failures return 404 rather than 403 to prevent resource enumeration attacks. A 403 response would confirm the resource exists but is forbidden, whereas 404 is indistinguishable from "resource doesn't exist."

## Policy Definitions

### Customer API Policies

**`CustomerAccess`** — Full access to customer-scoped resources.
- Requirements: `RequireAuthenticatedUser()` + `ScopeRequirement("customer.api")` + `RequireRole("Customer")`
- Used by: `AddressController`, `CartController`, `CheckoutController`, `OrderController`

**`CustomerScopeOnly`** — Authenticated + scope, no role required.
- Requirements: `RequireAuthenticatedUser()` + `ScopeRequirement("customer.api")`
- Used by: `CustomerController` (all actions: POST/GET/PUT profile)
- Rationale: Profile creation is the bootstrap endpoint. A new user has no `Customer` role yet (the role is granted by `InitializeCustomerProfile`). Requiring the role on POST would create a deadlock.

> **ASP.NET Core policy merging caveat**: When both class-level and action-level `[Authorize]` attributes exist, ASP.NET Core merges all requirements (AND logic). The `CustomerController` does NOT have a class-level `[Authorize]` — each action carries its own `CustomerScopeOnly` attribute to avoid accidentally inheriting `CustomerAccess` from a parent.

### Delivery API Policies

**`DeliveryAgentAccess`** — Full access to delivery agent endpoints.
- Requirements: `RequireAuthenticatedUser()` + `ScopeRequirement("delivery.api")` + `RequireRole("DeliveryAgent")`
- Used by: `DeliveriesController`, `StatusController`, `LocationController`

## Naming Reconciliation Table

| Concept | Customer API | Delivery API |
|---------|-------------|-------------|
| JWT audience | `talabat.customer.api` | `talabat.delivery.api` |
| Scope | `customer.api` | `delivery.api` |
| Named policy | `CustomerAccess`, `CustomerScopeOnly` | `DeliveryAgentAccess` |
| Identity role | `Customer` | `DeliveryAgent` |
| `ICurrentUser` property | `CustomerId` | `AgentId` |
| Ownership query | Scoped to `CustomerId` in handler | `GetByIdForAgentAsync(deliveryId, agentId)` |

## Role Inventory

| Role | Granted By | Sync Mechanism |
|------|-----------|---------------|
| `Customer` | `IUserCapabilityService.InitializeCustomerProfile` or self-registration | `User.UserType \|= UserType.Customer` + `UserManager.AddToRoleAsync` in same transaction |
| `DeliveryAgent` | `IUserCapabilityService.ApproveAgentAsync` (admin approval) | `User.UserType \|= UserType.DeliveryAgent` + `UserManager.AddToRoleAsync` in same transaction |
| `Admin` | `IUserCapabilityService` (never self-registered) | `User.UserType \|= UserType.Admin` + role assignment |
| `RestaurantOwner` | `IUserCapabilityService` (never self-registered) | `User.UserType \|= UserType.RestaurantOwner` + role assignment |

**Source of truth**: `User.UserType` (Domain-owned flags enum). Identity roles are an authorization projection maintained by `IUserCapabilityService`.

## Claim-Freshness Caveat

The `role` and `scope` claims in a JWT are point-in-time snapshots captured at token issuance. Between token issuance and token expiry, a user's roles may change (e.g., admin approves a delivery agent application). The old token still carries the old claims.

**Impact**: A user who is newly approved as a delivery agent must wait for their current token to expire (or refresh it) before the new role takes effect in JWT-based authorization.

**Mitigation**: Short token lifetimes (e.g., 15 minutes) limit the staleness window. For domain-level capability checks (e.g., `ICurrentUser.HasCustomerCapability`), the `CurrentUser` service queries the database on each request, providing a fresh view of `UserType`. The `ProfileEnforcementFilter` uses this fresh check.

## Self-Assignment Decision

`AssignDelivery` (delivery agent picks up a pending delivery) is self-assignment: the controller populates `AgentId` from `ICurrentUser.AgentId`. The handler does not accept a caller-supplied `AgentId`.

**Rationale**: Self-assignment eliminates the possibility of one agent assigning work to another. The ownership gate (`GetByIdForAgentAsync`) then ensures all subsequent lifecycle operations on that delivery are performed by the same agent.

## Domain Cleanliness Statement

The Domain layer (`Talabat.Domain`) must remain free of:
- Entity Framework Core dependencies (except documented `Microsoft.Extensions.Identity.Stores` for `User : IdentityUser<int>`)
- ASP.NET Core dependencies (no `HttpContext`, `ClaimsPrincipal`, etc.)
- Duende IdentityServer / JWT dependencies

Authorization is an infrastructure concern. The Domain layer defines the rules (via `User.UserType`, `Delivery.AssignedAgentId`), but the enforcement happens in the Application/Infrastructure layers via `ICurrentUser`, `ScopeHandler`, and `ProfileEnforcementFilter`.

Architecture tests in `Talabat.ArchitectureTests` enforce these boundaries automatically.

## ScopeHandler Implementation

```csharp
// Both API hosts implement identical ScopeHandlers:
protected override Task HandleRequirementAsync(
    AuthorizationHandlerContext context, ScopeRequirement requirement)
{
    var scopes = context.User.FindAll("scope")
        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    if (scopes.Contains(requirement.Scope, StringComparer.Ordinal))
        context.Succeed(requirement);

    return Task.CompletedTask;
}
```

The handler does NOT call `context.Fail()` when the scope is missing — it simply doesn't call `context.Succeed()`. This allows other authorization handlers to evaluate their requirements. If no handler succeeds, ASP.NET Core returns 403.
