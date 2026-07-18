# Authorization Matrix: Talabat Customer API

**Phase**: 7 — Talabat.Customer.API
**Date**: 2026-07-16
**Status**: Provisional — finalized in Phase 9

## Endpoint Authorization Matrix

| Endpoint | Method | Auth Required | Profile Required | Notes |
|----------|--------|--------------|-----------------|-------|
| `/api/catalog/restaurants` | GET | No | No | Anonymous browsing |
| `/api/catalog/restaurants/{id}/menu` | GET | No | No | Anonymous browsing |
| `/api/me/profile` | POST | Yes | No | Creates profile on first use |
| `/api/me/profile` | GET | Yes | Yes | Returns 409 if no profile |
| `/api/me/profile` | PUT | Yes | Yes | Returns 409 if no profile |
| `/api/me/cart` | GET | Yes | Yes | Returns 409 if no profile |
| `/api/me/cart/items` | POST | Yes | Yes | Returns 409 if no profile |
| `/api/me/cart/items/{productId}` | PUT | Yes | Yes | Returns 409 if no profile |
| `/api/me/cart/items/{productId}` | DELETE | Yes | Yes | Returns 409 if no profile |
| `/api/me/cart` | DELETE | Yes | Yes | Returns 409 if no profile |
| `/api/me/addresses` | POST | Yes | Yes | Returns 409 if no profile |
| `/api/me/addresses/{id}` | DELETE | Yes | Yes | Returns 409 if no profile |
| `/api/me/addresses/{id}/default` | PUT | Yes | Yes | Returns 409 if no profile |
| `/api/me/checkout` | POST | Yes | Yes | Returns 409 if no profile |
| `/api/me/orders` | GET | Yes | Yes | Returns 409 if no profile |
| `/api/me/orders/{id}` | GET | Yes | Yes | Returns 409 if no profile |
| `/health` | GET | No | No | Health check endpoint |

## Status Code Legend

- **401 Unauthorized**: Missing or invalid bearer token
- **409 Conflict (`ProfileNotCreated`)**: Authenticated but no customer profile exists
- **404 Not Found**: Resource does not exist or does not belong to the customer
- **400 Bad Request**: Validation failure

## Phase 9 Refinement Notes

- Token audiences, scopes, and custom claims need finalization
- Per-endpoint authorization policies may be introduced
- Account→profile linkage strategy may change
- The `sub` claim-to-`Customer` mapping is provisional
