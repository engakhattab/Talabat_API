# Authorization Endpoint Matrix

**Feature**: Phase 10 — Authorization Strategy And Quality Gates
**Date**: 2026-07-26
**Supersedes**: `docs/authorization-matrix.md` (Phase 3)

## Customer API (`Talabat.Customer.API`)

| Endpoint | Method | Anonymous | Policy | Role | Scope | Ownership Rule | Failure Codes |
|----------|--------|-----------|--------|------|-------|----------------|---------------|
| `/api/catalog/*` | GET | Yes | (none) | (none) | (none) | (none) | (none) |
| `/health` | GET | Yes | (none) | (none) | (none) | (none) | (none) |
| `/api/me/profile` | POST | No | `CustomerScopeOnly` | (none) | `customer.api` | Profile creation — no capability required | 401, 403 |
| `/api/me/profile` | GET | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |
| `/api/me/profile` | PUT | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404, 409 |
| `/api/me/addresses` | GET | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |
| `/api/me/addresses` | POST | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 409 |
| `/api/me/addresses/{id}` | DELETE | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |
| `/api/me/addresses/{id}/default` | PUT | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |
| `/api/me/cart` | GET | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 409 |
| `/api/me/cart/items` | POST | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 409 |
| `/api/me/cart/items/{id}` | DELETE | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |
| `/api/me/cart/items/{id}` | PUT | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |
| `/api/me/cart/clear` | POST | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 409 |
| `/api/me/checkout` | POST | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 409 |
| `/api/me/orders` | GET | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |
| `/api/me/orders/{id}` | GET | No | `CustomerAccess` | `Customer` | `customer.api` | `ICurrentUser.CustomerId` | 401, 403, 404 |

## Delivery API (`Talabat.Delivery.API`)

| Endpoint | Method | Anonymous | Policy | Role | Scope | Ownership Rule | Failure Codes |
|----------|--------|-----------|--------|------|-------|----------------|---------------|
| `/api/agent/status/online` | PUT | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | self (`ICurrentUser.AgentId`) | 401, 403 |
| `/api/agent/status/offline` | PUT | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | self (`ICurrentUser.AgentId`) | 401, 403 |
| `/api/agent/location` | PUT | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | self (`ICurrentUser.AgentId`) | 401, 403 |
| `/api/agent/deliveries/active` | GET | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | agent-scoped read | 401, 403, 404 |
| `/api/agent/deliveries/pending` | GET | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | unassigned pool (all agents) | 401, 403 |
| `/api/agent/deliveries/history` | GET | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | agent-scoped read | 401, 403, 404 |
| `/api/agent/deliveries/{id}/assign` | POST | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | self-assign; agent id from token | 401, 403, 404 |
| `/api/agent/deliveries/{id}/out-for-delivery` | POST | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | must be assigned to caller → else 404 | 401, 403, 404 |
| `/api/agent/deliveries/{id}/arrived-at-restaurant` | POST | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | must be assigned to caller → else 404 | 401, 403, 404 |
| `/api/agent/deliveries/{id}/picked-up` | POST | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | must be assigned to caller → else 404 | 401, 403, 404 |
| `/api/agent/deliveries/{id}/delivered` | POST | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | must be assigned to caller → else 404 | 401, 403, 404 |
| `/api/agent/deliveries/{id}/cancel` | POST | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | must be assigned to caller → else 404 | 401, 403, 404 |
| `/api/agent/deliveries/{id}/fail` | POST | No | `DeliveryAgentAccess` | `DeliveryAgent` | `delivery.api` | must be assigned to caller → else 404 | 401, 403, 404 |

## Status Code Summary

| Code | Meaning | When |
|------|---------|------|
| 401 | Unauthorized | No token, invalid token, wrong audience, expired token |
| 403 | Forbidden | Authenticated but missing required scope or role |
| 404 | Not Found | Authenticated + authorized by role, but resource not found or not owned by caller |
| 409 | Conflict | Profile not created (for `/api/me/*` endpoints requiring customer capability) |
| 400 | Bad Request | Invalid request body (validation errors) |
