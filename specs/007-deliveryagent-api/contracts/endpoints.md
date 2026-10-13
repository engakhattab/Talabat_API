# API Endpoint Contracts: DeliveryAgent API

All endpoints require authentication (JWT Bearer token) and the `DeliveryAgent` role/scope (except operations/admin assignment endpoints).

---

## 1. Status Management

### Go Online
- **HTTP Method**: `PUT`
- **Route**: `/api/agent/status/online`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (if status is `Busy` or `Suspended`)
  - `401 Unauthorized` / `403 Forbidden`

### Go Offline
- **HTTP Method**: `PUT`
- **Route**: `/api/agent/status/offline`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (if status is `Busy` or `Suspended`)
  - `401 Unauthorized` / `403 Forbidden`

---

## 2. Location Tracking

### Update Location
- **HTTP Method**: `PUT`
- **Route**: `/api/agent/location`
- **Headers**:
  - `Authorization: Bearer <token>`
  - `Content-Type: application/json`
- **Request Body**:
  ```json
  {
    "latitude": 30.0444,
    "longitude": 31.2357
  }
  ```
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (invalid coordinates: latitude must be [-90, 90], longitude must be [-180, 180])
  - `401 Unauthorized` / `403 Forbidden`

---

## 3. Assignment & Lifecycle

### Assign Delivery Agent (Operations Endpoint)
- **HTTP Method**: `POST`
- **Route**: `/api/agent/deliveries/{deliveryId}/assign`
- **Headers**:
  - `Authorization: Bearer <token>`
  - `Content-Type: application/json`
- **Request Body**:
  ```json
  {
    "agentId": 123
  }
  ```
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (if agent is not available, delivery is not pending, or input is invalid)
  - `401 Unauthorized` / `403 Forbidden`

### Arrived at Restaurant
- **HTTP Method**: `POST`
- **Route**: `/api/agent/deliveries/{deliveryId}/arrive`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (invalid state transition)
  - `403 Forbidden` (if not the assigned agent)
  - `404 Not Found` (delivery does not exist)

### Picked Up Order
- **HTTP Method**: `POST`
- **Route**: `/api/agent/deliveries/{deliveryId}/pickup`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (invalid state transition)
  - `403 Forbidden` (if not the assigned agent)
  - `404 Not Found` (delivery does not exist)

### Out For Delivery
- **HTTP Method**: `POST`
- **Route**: `/api/agent/deliveries/{deliveryId}/out-for-delivery`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (invalid state transition)
  - `403 Forbidden` (if not the assigned agent)
  - `404 Not Found` (delivery does not exist)

### Mark Delivered
- **HTTP Method**: `POST`
- **Route**: `/api/agent/deliveries/{deliveryId}/deliver`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (invalid state transition)
  - `403 Forbidden` (if not the assigned agent)
  - `404 Not Found` (delivery does not exist)

### Cancel Delivery (Before Picked Up)
- **HTTP Method**: `POST`
- **Route**: `/api/agent/deliveries/{deliveryId}/cancel`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (invalid transition, e.g. already picked up)
  - `403 Forbidden` (if not the assigned agent)
  - `404 Not Found` (delivery does not exist)

### Fail Delivery
- **HTTP Method**: `POST`
- **Route**: `/api/agent/deliveries/{deliveryId}/fail`
- **Headers**:
  - `Authorization: Bearer <token>`
  - `Content-Type: application/json`
- **Request Body**:
  ```json
  {
    "reason": "Vehicle broke down"
  }
  ```
- **Response**: `200 OK`
- **Error Responses**:
  - `400 Bad Request` (invalid transition, missing reason)
  - `403 Forbidden` (if not the assigned agent)
  - `404 Not Found` (delivery does not exist)

---

## 4. Queries

### Get Active Delivery
- **HTTP Method**: `GET`
- **Route**: `/api/agent/deliveries/active`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
  ```json
  {
    "id": 45,
    "orderId": 789,
    "customerId": 12,
    "restaurantId": 3,
    "status": "Assigned",
    "deliveryAddress": {
      "street": "9 El-Tahrir St",
      "city": "Cairo",
      "buildingNumber": "12",
      "floor": "3"
    },
    "assignedAt": "2026-07-21T11:00:00Z"
  }
  ```
- **Error Responses**:
  - `404 Not Found` (no active delivery assigned to current agent)
  - `401 Unauthorized` / `403 Forbidden`

### Get Delivery History
- **HTTP Method**: `GET`
- **Route**: `/api/agent/deliveries/history`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
  ```json
  [
    {
      "id": 42,
      "orderId": 780,
      "customerId": 12,
      "restaurantId": 3,
      "status": "Delivered",
      "deliveryAddress": {
        "street": "9 El-Tahrir St",
        "city": "Cairo",
        "buildingNumber": "12",
        "floor": "3"
      },
      "assignedAt": "2026-07-20T10:00:00Z",
      "deliveredAt": "2026-07-20T10:30:00Z"
    }
  ]
  ```
- **Error Responses**:
  - `401 Unauthorized` / `403 Forbidden`

### Get Pending Deliveries
- **HTTP Method**: `GET`
- **Route**: `/api/agent/deliveries/pending`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response**: `200 OK`
  ```json
  [
    {
      "id": 46,
      "orderId": 790,
      "customerId": 14,
      "restaurantId": 5,
      "status": "PendingAssignment",
      "deliveryAddress": {
        "street": "15 University St",
        "city": "Giza",
        "buildingNumber": "4B",
        "floor": "1"
      },
      "createdAt": "2026-07-21T13:45:00Z"
    }
  ]
  ```
- **Error Responses**:
  - `401 Unauthorized` / `403 Forbidden`
