# Authorization Matrix: Talabat Customer API

**Phase**: 3 — User Aggregate Refactor
**Date**: 2026-07-19
**Status**: Current

## Identity Model

| Property | Value |
|----------|-------|
| Identity entity | `User : IdentityUser<int>` (single unified aggregate) |
| Primary key | `User.Id` (int, database-generated SQL Server IDENTITY) |
| Authenticated subject | `sub` claim = `User.Id` (int) |
| Role claim type | `role` (JWT `TokenValidationParameters.RoleClaimType = "role"`) |
| Capability source of truth | `User.UserType` flags enum (`[Flags]`) — Domain owns the value; Identity roles are an authorization projection |
| Role/flag sync | Exclusively via `IUserCapabilityService` in one transaction; no controller or handler may mutate roles or `UserType` outside that workflow |

## Roles

| Role | Granted by | Notes |
|------|-----------|-------|
| `Customer` | `IUserCapabilityService.GrantCustomerAsync` or registration | Sets `UserType \|= UserType.Customer` |
| `DeliveryAgent` | `IUserCapabilityService.ApproveAgentAsync` (admin approval) | Sets `UserType \|= UserType.Agent`; `AgentApprovalStatus = Approved` |
| `Admin` | `IUserCapabilityService` (never self-registered) | Sets `UserType \|= UserType.Admin` |
| `RestaurantOwner` | `IUserCapabilityService` (never self-registered) | Sets `UserType \|= UserType.RestaurantOwner` |

## Capability Enforcement

Customer API endpoints resolve the caller's `CustomerId` from `ICurrentUser`, which queries
`User.UserType` for the `UserType.Customer` flag using the `sub` claim's int ID. Absent this
flag, `ICurrentUser.CustomerId` is `null` and the `ProfileEnforcementFilter` blocks access.

## Endpoint Authorization Matrix

| Endpoint | Method | Auth | Customer Capability | Notes |
|----------|--------|------|---------------------|-------|
| `/api/catalog/restaurants` | GET | No | No | Anonymous browsing |
| `/api/catalog/restaurants/{id}/menu` | GET | No | No | Anonymous browsing |
| `/api/me/profile` | POST | Yes | No | Creates customer profile on first use; sets `UserType \|= UserType.Customer` via domain method |
| `/api/me/profile` | GET | Yes | Yes | `ProfileNotCreated` → 404 if no profile |
| `/api/me/profile` | PUT | Yes | Yes | `ProfileNotCreated` → 409 if no profile |
| `/api/me/addresses` | POST | Yes | Yes | `ProfileNotCreated` → 409 if no profile |
| `/api/me/addresses/{id}` | DELETE | Yes | Yes | Owner-scoped 404 if address not found or belongs to another customer |
| `/api/me/addresses/{id}/default` | PUT | Yes | Yes | Owner-scoped 404 if address not found or belongs to another customer |
| `/api/me/cart` | GET | Yes | Yes | Cart scoped to `CustomerId` from `sub` claim |
| `/api/me/cart/items` | POST | Yes | Yes | `ProfileNotCreated` → 409 if no profile |
| `/api/me/cart/items/{productId}` | PUT | Yes | Yes | Owner-scoped cart item |
| `/api/me/cart/items/{productId}` | DELETE | Yes | Yes | Owner-scoped cart item |
| `/api/me/cart` | DELETE | Yes | Yes | Owner-scoped cart clear |
| `/api/me/checkout` | POST | Yes | Yes | `ProfileNotCreated` → 409 if no profile |
| `/api/me/orders` | GET | Yes | Yes | Owner-scoped order history |
| `/api/me/orders/{id}` | GET | Yes | Yes | Owner-scoped order detail; 404 if not found or belongs to another customer |
| `/health` | GET | No | No | Health check endpoint |

## Ownership Rules

| Rule | Detail |
|------|--------|
| Identity resolution | `sub` = `User.Id` (int) from validated JWT; never from route, body, or query |
| CustomerId resolution | `ICurrentUser.CustomerId` is set equal to `User.Id` when `UserType` has `Customer` flag |
| Foreign-resource protection | Address, cart, and order lookups are scoped to `CustomerId`; absent matches return 404 (not 403) |
| Caller-supplied CustomerId | Prohibited — no `[FromRoute]` or `[FromBody]` CustomerId on any endpoint |
| Cart isolation | `GetCartHandler` uses `ICurrentUser.CustomerId`; each customer sees only their own cart |

## Status Code Legend

| Code | Meaning |
|------|---------|
| 401 Unauthorized | Missing or invalid bearer token; user not authenticated |
| 403 Forbidden | Authenticated but lacks required role/capability (not used for owner-scoped resources) |
| 404 Not Found | Resource does not exist or does not belong to the customer; also used for `GET /api/me/profile` when no profile exists (`ProfileNotCreated`) |
| 409 Conflict | `ProfileNotCreated` on `/api/me/profile PUT` and all other `/api/me/*` routes when no customer profile exists; also `ConcurrencyConflict` on rowversion stale writes |
| 400 Bad Request | Validation failure (e.g., empty name, non-positive age) |

## Concurrency

| Mechanism | Detail |
|-----------|--------|
| Token | SQL `rowversion` (`User.RowVersion`) — single EF concurrency token for business writes |
| Conflict path | `DbUpdateConcurrencyException` → `ConcurrencyConflictException` → `CustomErrorHandler` → HTTP 409 |
| Scope | All `User` aggregate writes via `IUnitOfWork.SaveChangesAsync` |

## Login Rejection

| Condition | Result |
|-----------|--------|
| `User.IsActive == false` | `SignInManager.CanSignInAsync` returns false; login rejected |
| `User.IsDeleted == true` | `SignInManager.CanSignInAsync` returns false; login rejected |
| Existing session cookie after deactivation/soft-delete | `SecurityStampValidator` detects stamp mismatch → 401 on next validated request |
