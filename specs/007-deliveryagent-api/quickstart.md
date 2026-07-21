# Quickstart Guide: Testing DeliveryAgent API

## 1. Local Database & Seed Setup
To test the endpoints, you'll need the database seeded with:
1. At least one user who has the `DeliveryAgent` role capability and is approved (we can use the registration flow or direct DB insertion for test setup).
2. At least one order with a corresponding pending delivery.

---

## 2. API End-to-End Scenarios

### Scenario A: Agent Status & Location Update
1. **Authenticate**: Log in as a delivery agent through the Identity API to retrieve the bearer token.
2. **Go Online**:
   ```bash
   curl -X PUT https://localhost:7083/api/agent/status/online \
     -H "Authorization: Bearer <token>"
   ```
   *Expected Response*: `200 OK`.
3. **Update Location**:
   ```bash
   curl -X PUT https://localhost:7083/api/agent/location \
     -H "Authorization: Bearer <token>" \
     -H "Content-Type: application/json" \
     -d '{"latitude": 30.0596, "longitude": 31.2285}'
   ```
   *Expected Response*: `200 OK`.
4. **Verify Location In DB**: Inspect `AspNetUsers` table to verify `CurrentLocation_Latitude` and `CurrentLocation_Longitude` are updated.

### Scenario B: Manual Assignment & Lifecycle
1. **List Pending**:
   ```bash
   curl -X GET https://localhost:7083/api/agent/deliveries/pending \
     -H "Authorization: Bearer <token>"
   ```
   *Expected Response*: `200 OK` returning a list containing a delivery with ID `X` in `PendingAssignment` status.
2. **Assign Delivery**:
   ```bash
   curl -X POST https://localhost:7083/api/agent/deliveries/X/assign \
     -H "Authorization: Bearer <token>" \
     -H "Content-Type: application/json" \
     -d '{"agentId": <AgentUserId>}'
   ```
   *Expected Response*: `200 OK`. Delivery status changes to `Assigned`, and agent's `DeliveryAgentStatus` changes to `Busy`.
3. **Arrived at Restaurant**:
   ```bash
   curl -X POST https://localhost:7083/api/agent/deliveries/X/arrive \
     -H "Authorization: Bearer <token>"
   ```
   *Expected Response*: `200 OK`. Status transitions to `ArrivedAtRestaurant`.
4. **Picked Up**:
   ```bash
   curl -X POST https://localhost:7083/api/agent/deliveries/X/pickup \
     -H "Authorization: Bearer <token>"
   ```
   *Expected Response*: `200 OK`. Status transitions to `PickedUp`.
5. **Out for Delivery**:
   ```bash
   curl -X POST https://localhost:7083/api/agent/deliveries/X/out-for-delivery \
     -H "Authorization: Bearer <token>"
   ```
   *Expected Response*: `200 OK`. Status transitions to `OutForDelivery`.
6. **Delivered**:
   ```bash
   curl -X POST https://localhost:7083/api/agent/deliveries/X/deliver \
     -H "Authorization: Bearer <token>"
   ```
   *Expected Response*: `200 OK`. Status transitions to `Delivered`. The agent's `DeliveryAgentStatus` transitions back to `Available`.

---

## 3. Integration & Unit Testing
Tests will be implemented under `tests/Talabat.Application.Tests` and a new test project (or within `tests/Talabat.Infrastructure.Tests`) validating:
- Agent status checks and exception throwing (e.g. going online when busy throws `AgentNotAvailableException`).
- Delivery assignment atomic updates.
- Concurrency conflicts using `RowVersion`.
- API endpoint integration tests mock authentication and verify 401/403 controls.
