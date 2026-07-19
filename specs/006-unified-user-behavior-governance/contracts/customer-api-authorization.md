# Contract: Customer API Authorization and Ownership

**Feature**: Unified User Behavior and Governance  
**Applies to**: `Talabat.Customer.API`

## Authenticated Principal

JWT bearer validation uses:

```text
subject identifier: positive integer User.Id
role claim type: role
```

All role claims may materialize in `ClaimsPrincipal`. They are authorization-host information, not
the source of truth for current Customer business capability.

`ICurrentUser` resolution remains:

1. Require an authenticated principal.
2. Prefer `ClaimTypes.NameIdentifier`; otherwise use `sub`.
3. Reject missing, malformed, zero, negative, or out-of-range selected values as 401.
4. Query the persisted `UserType` once per request.
5. Set `CustomerId = UserId` only when current stored Customer capability is present.

A stale Customer role without stored Customer capability receives `ProfileNotCreated`; a stored
Customer capability remains the business gate even if a stale token omits the role.

## Owner-Scoped Targeting

| Route family | Caller-selectable resource ID | Ownership behavior |
|---|---|---|
| `/api/me/profile` | none | Always current `UserId`/`CustomerId`. |
| `/api/me/addresses/{addressId}` | address only | Address is resolved inside current user's owned collection; foreign ID is 404. |
| `/api/me/cart` | product IDs only on item operations | Cart owner is always current `CustomerId`; another user's cart is never selectable or returned. |
| `/api/me/checkout` | delivery address only | Address snapshot must belong to current `CustomerId`; foreign ID is 404. |
| `/api/me/orders/{orderId}` | order only | Repository query includes current `CustomerId`; foreign ID is 404. |

No route or request body may accept `customerId` as an ownership authority. Response DTO fields may
retain the business name `CustomerId`.

The current-cart route has no cross-user identifier. When the current customer has no active cart,
the established empty-cart result remains valid; it must not return another user's cart.

## Missing Customer Capability

The exact Phase 2 serialized fixtures in
[identity-api.md](../../005-unified-user-identity-cutover/contracts/identity-api.md) remain frozen:

- `GET /api/me/profile`: 404 `ProfileNotCreated`;
- other protected `/api/me/*`: 409 `ProfileNotCreated`;
- `POST /api/me/profile`: authenticated positive-int subject is exempt so Customer capability can
  be granted to the same user;
- malformed subject: empty 401 before controller execution.

## Concurrency Conflict

A stale user-row save through a Customer API operation returns:

| Field | Value |
|---|---|
| HTTP status | 409 |
| title | `Conflict` |
| error code | `ConcurrencyConflict` |
| detail | `The record has been modified by another process. Please retry.` |
| type | RFC 9110 conflict section URL used by existing result mapping |

No stack trace, rowversion bytes, competing user data, or persistence exception is exposed.

## Explicit Non-Contracts

Phase 3 adds no:

- new Customer API route;
- caller-supplied role or capability;
- caller-supplied CustomerId authority;
- Delivery API route;
- admin approval endpoint; or
- token revocation service.
