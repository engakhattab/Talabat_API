# Authorization Endpoint Matrix

**Date**: 2026-07-26
**Status**: Current
**Supersedes**: `authorization-matrix.md` (Customer API only — this document covers both hosts)

## Customer API

**Host**: `talabat.customer.api`
**Auth scheme**: JWT Bearer
**Scope enforcement**: `customer.api`

### Catalog (Anonymous)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/catalog/restaurants` | GET | None | — | — | — | — |
| `/api/catalog/restaurants/{restaurantId}/menu` | GET | None | — | — | — | — |

### Profile (CustomerScopeOnly)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/me/profile` | POST | `CustomerScopeOnly` | — | `customer.api` | N/A (bootstrap) | 401, 403 |
| `/api/me/profile` | GET | `CustomerScopeOnly` | — | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 404 |
| `/api/me/profile` | PUT | `CustomerScopeOnly` | — | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 404 |

> **Design note**: Profile endpoints use `CustomerScopeOnly` (no role) because profile creation is the bootstrap step that grants the `Customer` role. Requiring the role here would create a deadlock. The `ProfileEnforcementFilter` returns 404/409 for missing profiles after authorization succeeds.

### Addresses (CustomerAccess)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/me/addresses` | POST | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 409 |
| `/api/me/addresses/{addressId}` | DELETE | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 404 |
| `/api/me/addresses/{addressId}/default` | PUT | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 404 |

### Cart (CustomerAccess)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/me/cart` | GET | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403 |
| `/api/me/cart/items` | POST | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 409 |
| `/api/me/cart/items/{productId}` | PUT | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 404 |
| `/api/me/cart/items/{productId}` | DELETE | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 404 |
| `/api/me/cart` | DELETE | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403 |

### Checkout (CustomerAccess)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/me/checkout` | POST | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 409 |

### Orders (CustomerAccess)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/me/orders` | GET | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403 |
| `/api/me/orders/{orderId}` | GET | `CustomerAccess` | `Customer` | `customer.api` | `CustomerId` from `ICurrentUser` | 401, 403, 404 |

### Health

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/health` | GET | None | — | — | — | — |

---

## Delivery API

**Host**: `talabat.delivery.api`
**Auth scheme**: JWT Bearer
**Scope enforcement**: `delivery.api`

### Agent Status (DeliveryAgentAccess)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/agent/status/online` | PUT | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | N/A | 401, 403 |
| `/api/agent/status/offline` | PUT | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | N/A | 401, 403 |

### Agent Location (DeliveryAgentAccess)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/agent/location` | PUT | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | N/A | 401, 403 |

### Deliveries (DeliveryAgentAccess)

| Endpoint | Method | Policy | Role | Scope | Ownership | Failure Codes |
|----------|--------|--------|------|-------|-----------|---------------|
| `/api/agent/deliveries/active` | GET | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `AgentId` from `ICurrentUser` | 401, 403 |
| `/api/agent/deliveries/pending` | GET | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | N/A (pool query) | 401, 403 |
| `/api/agent/deliveries/history` | GET | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `AgentId` from `ICurrentUser` | 401, 403 |
| `/api/agent/deliveries/{deliveryId}/assign` | POST | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | Self-assignment (controller populates `AgentId`) | 401, 403, 404 |
| `/api/agent/deliveries/{deliveryId}/out-for-delivery` | POST | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `GetByIdForAgentAsync` | 401, 403, 404 |
| `/api/agent/deliveries/{deliveryId}/arrived-at-restaurant` | POST | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `GetByIdForAgentAsync` | 401, 403, 404 |
| `/api/agent/deliveries/{deliveryId}/picked-up` | POST | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `GetByIdForAgentAsync` | 401, 403, 404 |
| `/api/agent/deliveries/{deliveryId}/delivered` | POST | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `GetByIdForAgentAsync` | 401, 403, 404 |
| `/api/agent/deliveries/{deliveryId}/cancel` | POST | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `GetByIdForAgentAsync` | 401, 403, 404 |
| `/api/agent/deliveries/{deliveryId}/fail` | POST | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | `GetByIdForAgentAsync` | 401, 403, 404 |

---

## Policy Comparison

| Policy | Host | Auth | Scope | Role | Use Case |
|--------|------|------|-------|------|----------|
| `CustomerScopeOnly` | Customer API | Yes | `customer.api` | — | Profile bootstrap (create, read, update) |
| `CustomerAccess` | Customer API | Yes | `customer.api` | `Customer` | All customer-scoped resources (address, cart, checkout, orders) |
| `DeliveryAgentAccess` | Delivery API | Yes | `delivery.api` | `DeliveryAgent` | All delivery agent operations |
