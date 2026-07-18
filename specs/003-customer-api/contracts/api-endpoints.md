# API Contracts: Talabat Customer API (Phase 7)

**Date**: 2026-07-16  
**Spec**: [spec.md](spec.md)

## Base URL

`https://localhost:{port}/api`

## Authentication

Bearer token validated against `Talabat.Identity` authority. Endpoints marked `[Auth]` require a
valid token with a `sub` claim. The `sub` claim is resolved server-side to a `Customer` profile via
`Customer.IdentityUserId` (read-only lookup). A profile is created only by the explicit
`POST /api/me/profile` endpoint. Every other owner-scoped endpoint returns `409 Conflict` with
`errorCode: "ProfileNotCreated"` when the authenticated account has no profile yet.

---

## Money Representation

Money is serialized as `{ "amount": <decimal>, "currency": "EGP" }`. The domain `Money` value object
is **single-currency and has no currency field**, so `currency` is a fixed API-layer constant
(`"EGP"`) for client convenience — it is not read from the domain and must not be treated as a
per-value field.

---

## Catalog (Anonymous)

### GET /api/catalog/restaurants

Browse restaurants with optional filtering.

**Auth**: None (anonymous)

| Parameter | In | Type | Required | Description |
|-----------|-----|------|----------|-------------|
| `page` | query | int | No | Page number (default: 1) |
| `pageSize` | query | int | No | Items per page (default: 20, max: 50) |

**200 OK**:
```json
{
  "items": [
    {
      "id": 1,
      "name": "Restaurant Name",
      "description": "Short description",
      "imageUrl": null,
      "isActive": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42
}
```

### GET /api/catalog/restaurants/{restaurantId}/menu

Get a restaurant's product catalog.

**Auth**: None (anonymous)

| Parameter | In | Type | Required | Description |
|-----------|-----|------|----------|-------------|
| `restaurantId` | path | int | Yes | Restaurant ID |

**200 OK**:
```json
{
  "restaurantId": 1,
  "restaurantName": "Restaurant Name",
  "products": [
    {
      "id": 1,
      "name": "Product Name",
      "description": "Description",
      "price": { "amount": 12.99, "currency": "EGP" },
      "isAvailable": true
    }
  ]
}
```

**404 Not Found** (ProblemDetails): Restaurant not found.

---

## Cart (Authenticated — `/api/me/cart`)

### GET /api/me/cart

Get the authenticated customer's active cart.

**Auth**: Bearer token

**200 OK**:
```json
{
  "id": 1,
  "customerId": 1,
  "restaurantId": 1,
  "restaurantName": "Restaurant Name",
  "items": [
    {
      "productId": 1,
      "productName": "Product Name",
      "unitPrice": { "amount": 12.99, "currency": "EGP" },
      "quantity": 2,
      "lineTotal": { "amount": 25.98, "currency": "EGP" }
    }
  ],
  "total": { "amount": 25.98, "currency": "EGP" },
  "status": "Active"
}
```

**404 Not Found** (ProblemDetails): No active cart exists.

**401 Unauthorized**: Missing or invalid token.

### POST /api/me/cart/items

Add an item to the cart (creates a new cart if none exists).

**Auth**: Bearer token

**Request Body**:
```json
{
  "restaurantId": 1,
  "productId": 1,
  "quantity": 2
}
```

**200 OK**: Returns updated cart (same shape as GET).

**400 Bad Request** (ProblemDetails): Invalid quantity.  
**404 Not Found** (ProblemDetails): Restaurant or product not found.  
**409 Conflict** (ProblemDetails): Cross-restaurant cart conflict.

### PUT /api/me/cart/items/{productId}

Update a cart item's quantity.

**Auth**: Bearer token

**Request Body**:
```json
{
  "quantity": 3
}
```

**200 OK**: Returns updated cart.  
**404 Not Found** (ProblemDetails): Cart item not found.  
**400 Bad Request** (ProblemDetails): Invalid quantity.

### DELETE /api/me/cart/items/{productId}

Remove a cart item.

**Auth**: Bearer token

**200 OK**: Returns updated cart.  
**404 Not Found** (ProblemDetails): Cart item not found.

### DELETE /api/me/cart

Clear the entire cart.

**Auth**: Bearer token

**204 No Content**: Cart cleared successfully.  
**404 Not Found** (ProblemDetails): No active cart exists.

---

## Customer Profile (Authenticated — `/api/me/profile`)

### POST /api/me/profile

Create the authenticated account's `Customer` profile on first use. This is the only owner-scoped
endpoint reachable before a profile exists. The server sets `IdentityUserId` from the token `sub`
claim; the caller never supplies it.

**Auth**: Bearer token

**Request Body**:
```json
{
  "fullName": "John Doe",
  "age": 30,
  "phoneNumber": "+20123456789"
}
```

**201 Created**: Returns the created profile (same shape as `GET /api/me/profile`).
**400 Bad Request** (ProblemDetails): Missing/invalid full name or non-positive age.
**409 Conflict** (ProblemDetails, `ProfileAlreadyExists`): A profile already exists for this account.

### GET /api/me/profile

Get the authenticated customer's profile.

**Auth**: Bearer token

**404 Not Found** (ProblemDetails, `ProfileNotCreated`): No profile has been created for this account.

**200 OK**:
```json
{
  "id": 1,
  "fullName": "John Doe",
  "age": 30,
  "phoneNumber": "+20123456789",
  "addresses": [
    {
      "id": 1,
      "street": "123 Main St",
      "city": "Cairo",
      "buildingNumber": "42",
      "floor": "3",
      "isDefault": true
    }
  ]
}
```

### PUT /api/me/profile

Update the authenticated customer's profile.

**Auth**: Bearer token

**Request Body**:
```json
{
  "fullName": "John Doe",
  "age": 30,
  "phoneNumber": "+20123456789"
}
```

**200 OK**: Returns updated profile.  
**400 Bad Request** (ProblemDetails): Invalid profile data.

---

## Addresses (Authenticated — `/api/me/addresses`)

### POST /api/me/addresses

Add a new address.

**Auth**: Bearer token

**Request Body**:
```json
{
  "street": "123 Main St",
  "city": "Cairo",
  "buildingNumber": "42",
  "floor": "3",
  "makeDefault": false
}
```

**201 Created**: Returns the new address with its ID.  
**409 Conflict** (ProblemDetails): Duplicate address.

### DELETE /api/me/addresses/{addressId}

Remove an address.

**Auth**: Bearer token

**204 No Content**: Address removed.  
**404 Not Found** (ProblemDetails): Address not found.

### PUT /api/me/addresses/{addressId}/default

Set an address as the default.

**Auth**: Bearer token

**200 OK**: Returns updated profile with addresses.  
**404 Not Found** (ProblemDetails): Address not found.

---

## Checkout (Authenticated — `/api/me/checkout`)

### POST /api/me/checkout

Submit checkout for the active cart.

**Auth**: Bearer token

**Request Body**:
```json
{
  "deliveryAddressId": 1
}
```

**201 Created** (Success):
```json
{
  "orderId": 1,
  "status": "success"
}
```

**422 Unprocessable Entity** (Unavailable items):
```json
{
  "orderId": null,
  "status": "unavailable",
  "unavailableItems": [
    {
      "productId": 1,
      "productName": "Product Name",
      "reason": "ProductNoLongerAvailable"
    }
  ]
}
```

**400/404/409** (ProblemDetails): Domain validation failures (empty cart, address not found,
restaurant closed, etc.)

---

## Orders (Authenticated — `/api/me/orders`)

### GET /api/me/orders

List the authenticated customer's orders.

**Auth**: Bearer token

| Parameter | In | Type | Required | Description |
|-----------|-----|------|----------|-------------|
| `page` | query | int | No | Page number (default: 1) |
| `pageSize` | query | int | No | Items per page (default: 20, max: 50) |

**200 OK**:
```json
{
  "items": [
    {
      "id": 1,
      "restaurantId": 1,
      "total": { "amount": 25.98, "currency": "EGP" },
      "placedAt": "2026-07-16T10:00:00Z",
      "itemCount": 2
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 5
}
```

### GET /api/me/orders/{orderId}

Get details of a specific order (must belong to the authenticated customer).

**Auth**: Bearer token

**200 OK**:
```json
{
  "id": 1,
  "customerId": 1,
  "restaurantId": 1,
  "deliveryAddress": {
    "street": "123 Main St",
    "city": "Cairo",
    "buildingNumber": "42",
    "floor": "3"
  },
  "items": [
    {
      "productName": "Product Name",
      "unitPrice": { "amount": 12.99, "currency": "EGP" },
      "quantity": 2,
      "lineTotal": { "amount": 25.98, "currency": "EGP" }
    }
  ],
  "total": { "amount": 25.98, "currency": "EGP" },
  "placedAt": "2026-07-16T10:00:00Z"
}
```

**404 Not Found** (ProblemDetails): Order not found or does not belong to customer.

---

## Health

### GET /health

**Auth**: None (anonymous)

**200 OK**: `Healthy`  
**503 Service Unavailable**: `Unhealthy` (database unreachable)

---

## Error Response Format (ProblemDetails — RFC 9457)

All error responses use ProblemDetails:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Restaurant was not found.",
  "extensions": {
    "errorCode": "RestaurantNotFound"
  }
}
```

The `errorCode` extension carries the `ApplicationErrorCodes` constant for programmatic handling.
