# T4 technical walkthrough: release replaces delivery agent cancel

> Ticket: Phase 2 · T4 ([engakhattab/Talabat_API#13](https://github.com/engakhattab/Talabat_API/issues/13)) ·
> Design: [`specs/011-order-lifecycle/design.md`](../../specs/011-order-lifecycle/design.md) D6, D7, D11 (Q3, Q4) ·
> Branch: `feat/t4-release-replaces-cancel` → `feature/phase-2-order-lifecycle`

This document explains what T4 built, where each piece lives, and why it lives there. It assumes you have read
[`CONTEXT.md`](../../CONTEXT.md) and the [T3 walkthrough](T3-technical-walkthrough.md). The terms below are the
glossary's terms.

---

## 1. Domain and business intent

### The story

Before T4, a Delivery Agent who could not take an order they had claimed pressed **cancel**. That *terminated* the
Delivery (`Cancelled`). Because `Deliveries.OrderId` is unique, the order could never get another delivery: one
agent's bad day killed a customer's order.

T4 replaces that with **Release**:

> The agent gives the claimed delivery back **before pickup**. The same Delivery row returns to
> `PendingAssignment` with its assignment cleared, so another agent can claim it, and the agent becomes `Available`.
> The order and its Order Fulfillment are untouched. If the agent is unresponsive, an **Admin releases on their
> behalf**. The old `POST /api/agent/deliveries/{id}/cancel` is removed.

### Ubiquitous language used

| Term | Meaning in T4 |
|---|---|
| **Release** | A Delivery Agent (or an Admin on their behalf) gives a claimed delivery back before pickup. Not a cancellation: nothing ends, the delivery is re-queued. |
| **Delivery** | The delivery-side lifecycle. Release is the one transition that goes *backwards* (`Assigned`/`ArrivedAtRestaurant` → `PendingAssignment`). |
| **Delivery Agent status** | `Available` / `Busy` / `Offline` / `Suspended`. Release moves the agent from `Busy` to `Available`. |
| **Acting on behalf (D11)** | The Admin API runs the same use case as the agent, with the Admin as the caller. |

### Business rules fulfilled or revised

| Rule | Source | Where it is enforced |
|---|---|---|
| Release only from `Assigned` or `ArrivedAtRestaurant` | D6, test 12/13 | `Delivery.Release` (aggregate invariant) |
| Release clears the assignment and reuses the same row | D6, test 12 | `Delivery.Release` nulls `AssignedAgentId`, `AssignedAt`, `ArrivedAtRestaurantAt` |
| Only the assigned agent can release | D6, test 13 | `Delivery.Release` → `DeliveryAgentMismatchException`; over HTTP another agent's delivery is `404` (ownership rule) |
| The agent becomes `Available` | D6, test 20 | `DeliveryAssignmentDomainService.ReleaseDelivery` |
| An Admin can release on behalf of the agent | D6, Q3, D11, tests 20/34 | `ReleaseDeliveryCommand.ByAdmin` + `POST /api/orders/{orderId}/release-agent` under `AdminAccess` |
| Agent cancel no longer exists | D6, D7 ("Delivery agent → Release only"), test 33 | `/cancel` action, handler and domain path deleted |
| Release history is **not** stored | Q4 | Deliberately nothing (arrives with the M4 audit log) |

New business rule ID: **BR-DEL-017** (release instead of cancel; Admin release on behalf). Writing it into
`docs/delivery/delivery-business-rules.md` is T9's job (business-rule docs), as the roadmap assigns it.

**Retired tests (§4.1), named in the red commits:** `CancelDeliveryHandlerTests.*`,
`DeliveryAssignmentDomainServiceTests.CancelDelivery_Cancels_AgentAvailable`,
`AgentAssignmentAuthorizationTests.CancelDelivery_BusyAgent_ReleasesToAvailable` (replaced by tests 12, 13 and 20 in
`467e9da`), and `DeliveryOwnershipTests.AgentB_Cancel_OnAgentADelivery_Returns404` (replaced in place by
`AgentB_Release_OnAgentADelivery_Returns404` in `f469eaf`).

---

## 2. Layer-by-layer flow

### Write side: the agent releases (Delivery API)

```text
POST /api/agent/deliveries/{deliveryId}/release            (OperationalDeliveryAgentAccess)
  DeliveriesController.ReleaseDelivery
    agentId ← token sub (ICurrentUser); never from the URL or body
    ReleaseDeliveryHandler.Handle(ReleaseDeliveryCommand.ByAgent(deliveryId, agentId))
      IDeliveryRepository.GetByIdForAgentAsync(deliveryId, agentId)   → null ⇒ 404 DeliveryNotFound
      IUserRepository.GetByIdAsync(delivery.AssignedAgentId)
      DeliveryAssignmentDomainService.ReleaseDelivery(delivery, agent, now)
        EnsureAssignedBusyAgent        (assignee? Busy?)
        Delivery.Release(agent.Id, now) (state + ownership + time invariants)
        User.MarkAvailable()
      IUnitOfWork.SaveChangesAsync     (delivery + agent in one SaveChanges)
  200 DeliveryIdResponse { id }
```

### Write side: an Admin releases on the agent's behalf (Admin API)

```text
POST /api/orders/{orderId}/release-agent                    (AdminAccess)
  OrdersController.ReleaseAgent
    ReleaseDeliveryHandler.Handle(ReleaseDeliveryCommand.ByAdmin(orderId))
      IDeliveryRepository.GetByOrderIdAsync(orderId)   → null ⇒ 404 DeliveryNotFound
      no AssignedAgentId                               ⇒ 409 DeliveryNotAssignedException
      … then exactly the same steps as the agent path
  200 ReleaseAgentResponse { orderId, deliveryId, releasedAgentId, deliveryStatus: "PendingAssignment" }
```

### Read side

No query changed, but the read models see the effect immediately:

- `GET /api/agent/deliveries/pending` lists the released delivery again (it is `PendingAssignment` with an accepted
  fulfillment).
- `GET /api/agent/deliveries/active` for the releasing agent returns `404` (they have no active delivery).
- `GET /api/agent/status` returns `Available`.
- The customer's **Customer Order Status** is derived from the fulfillment (still `Accepted`, `Preparing` or
  `ReadyForPickup`) plus the delivery, so a release is invisible to the customer except that
  `deliveryAgentAssigned` turns false.
- The releasing agent's **history** no longer shows the delivery, because history is "deliveries assigned to me" and
  the assignment is cleared. That is the accepted consequence of Q4 (no release history yet).

---

## 3. File-by-file breakdown

### Domain

**`Aggregates/DeliveryManagement/Delivery.cs`** (modified)
- **Added** `public void Release(int agentId, DateTime currentTime)`. It checks, in order: not terminal → assigned to
  *this* agent → status is `Assigned` or `ArrivedAtRestaurant` → time is not before the last transition. Then it
  clears `AssignedAgentId`, `AssignedAt`, `ArrivedAtRestaurantAt` and sets `PendingAssignment`.
- **Removed** `internal void CancelAssigned(...)`: its only caller was agent cancel.
- *Why here:* "which states may release" and "clear the assignment" are invariants of one aggregate. Putting them on
  `Delivery` means no caller (agent API, Admin API, a future job) can release a picked-up delivery by mistake.
- *Why clear `ArrivedAtRestaurantAt`:* the next agent has not arrived. Leaving the old timestamp would make
  `GetLastTransitionTime()` and any "arrived at" display lie about the new assignment.
- `Delivery.Cancel` (no agent assigned) stays. T6 (restaurant/Admin cancel after acceptance) will design how a
  cancellation with an agent assigned works.

**`DomainServices/DeliveryManagement/DeliveryAssignmentDomainService.cs`** (modified)
- **Added** `ReleaseDelivery(delivery, agent, now)`; **removed** `CancelDelivery`.
- *Why a domain service:* release changes **two aggregates**: the `Delivery` and the agent `User` (`Busy →
  Available`). CLAUDE.md's rule is that cross-aggregate work goes through a domain service, never a controller. It
  reuses `EnsureAssignedBusyAgent` exactly as `CompleteDelivery` and `FailDelivery` do.

### Application

**`DeliveryAgents/ProgressRelease/ReleaseDeliveryCommand.cs`** (new)
- A closed hierarchy: `ByAgent(DeliveryId, AgentId)` and `ByAdmin(OrderId)`, plus the result `ReleasedDelivery
  (DeliveryId, OrderId, ReleasedAgentId)`.
- *Why two shapes, one command:* design D11 says the use case takes its actor explicitly so it is written once. The
  two callers differ only in **how they find the delivery**: the agent by delivery id *scoped to themselves*, the
  Admin by order id (what an operator looks at). A private constructor keeps the hierarchy closed, like
  `FulfillmentActor`.

**`DeliveryAgents/ProgressRelease/ReleaseDeliveryHandler.cs`** (new)
- Finds the delivery (`switch` on the command), reads the assigned agent, calls the domain service, saves once.
- Returns a `UseCaseResult`; domain exceptions are mapped by `DomainExceptionMapper` (`InvalidDeliveryStatusTransition`
  and `DeliveryNotAssigned` → 409, not found → 404).
- *Why `GetByIdForAgentAsync` for the agent:* agent B asking about agent A's delivery gets **404**, not 403. A 403
  would confirm that the delivery exists (an information leak). Every other agent endpoint already behaves this way.
- *Why one `SaveChangesAsync` and no explicit transaction:* both changed entities are tracked by the same
  `DbContext`; EF Core wraps a single `SaveChanges` in one database transaction. `ExecuteInTransactionAsync` is for
  workflows that need several saves (checkout, accept, pickup).

**`DeliveryAgents/ProgressCancel/*`** (deleted): `CancelDeliveryCommand`, `CancelDeliveryHandler`.

**`DependencyInjection.cs`** (modified): registers `ReleaseDeliveryHandler` and drops `CancelDeliveryHandler`.

### API

**`Talabat.Delivery.API/Controllers/DeliveriesController.cs`** (modified)
- `POST {deliveryId}/release` (`ReleaseDelivery`) replaces `POST {deliveryId}/cancel` (`CancelDelivery`). Same shape
  (no body, `DeliveryIdResponse`), so the contract diff is two lines.

**`Talabat.Admin.API/Controllers/OrdersController.cs`** (modified) and
**`Contracts/Orders/ReleaseAgentResponse.cs`** (new)
- `POST /api/orders/{orderId}/release-agent` (`ReleaseOrderAgent`) next to accept, start-preparing and mark-ready,
  under the controller-wide `AdminAccess` policy (scope `admin.api` + role `Admin` + persisted capability).
- The response names the released agent so the operator can follow up (for example, suspend them with the existing
  endpoint, as D6 suggests).

**`openapi/delivery-api.json`, `openapi/admin-api.json`** (regenerated, own commits). Delivery: the `/cancel` path is
renamed to `/release` (breaking, but no Delivery frontend consumes it yet). Admin: additions only.

### Tests

| File | Seam | What it proves |
|---|---|---|
| `Talabat.Domain.Tests/DeliveryManagement/DeliveryReleaseTests.cs` (new) | Domain | Tests 12 and 13: release from both pre-pickup states, the row can be claimed again, after pickup / other agent / unassigned are rejected. |
| `Talabat.Application.Tests/Domain/DeliveryAgents/ReleaseDeliveryHandlerTests.cs` (new) | Application (fakes) | Test 20: agent and Admin release free the agent with one save; 404 for another agent's delivery or an order without one; 409 after pickup or when unassigned, with nothing saved. |
| `Talabat.Delivery.API.Tests/ReleaseEndpointTests.cs` (new) | Delivery API | Test 33: claim → release over HTTP puts the delivery back in the queue and the agent back to `Available`; `/cancel` is `404`. |
| `Talabat.Delivery.API.Tests/DeliveryOwnershipTests.cs` (modified) | Delivery API | `AgentB_Release_OnAgentADelivery_Returns404` replaces the cancel variant. |
| `Talabat.Admin.API.Tests/AgentReleaseTests.cs` (new) | Admin API | Test 34: 401/403/403 for anonymous, non-admin and missing scope; Admin release frees the agent and leaves the fulfillment `Accepted`; 409 after pickup or when unclaimed; 404 for an unknown order. |
| Three retired cancel tests (deleted/edited) | — | See §1 "Retired tests". |

---

## 4. Tricky concepts and decisions

- **Release is not a cancellation.** A cancel ends a lifecycle; a release *rewinds* one. That is why it lives as its
  own transition with its own name instead of a flag on `Cancel`. It is also why the fulfillment is not touched: the
  kitchen keeps cooking, only the Delivery Agent changes.
- **Why only before pickup?** After `PickedUp` the food is physically with the agent (and the fulfillment is
  `HandedOver`). Handing it "back to the queue" would leave food with nobody responsible for it. After pickup the way
  out is **delivery failure** (D8, ticket T7).
- **404 vs 403 for another agent.** The domain throws `DeliveryAgentMismatchException` (mapped to 403) when asked
  directly. The HTTP path never gets that far for an agent because the repository query is scoped to the caller, so the
  answer is 404. Both behaviours are tested at their own seam.
- **The Admin path has no "is this my delivery" check.** The Admin acts on *whichever* agent holds it, which is the
  point of acting on behalf. The handler reads `AssignedAgentId` and loads that user.
- **Agent must be `Busy`.** `EnsureAssignedBusyAgent` refuses to release if the assignee is not `Busy`
  (`409 AgentNotAvailableException`). A Busy agent cannot be suspended or go offline, so in practice the assignee is
  always Busy; the guard protects against corrupt data instead of silently "freeing" an agent who was never busy.
- **Timestamps after a release.** `GetLastTransitionTime()` falls back to `CreatedAt` once `AssignedAt` and
  `ArrivedAtRestaurantAt` are cleared, so the next `AssignAgent` only has to be after creation. The release itself must
  not be earlier than the last transition (monotonic UTC, like every other transition).
- **Concurrency.** `Delivery` has a `RowVersion`. If the agent releases while, for example, a pickup is saved for the
  same row, one `SaveChanges` loses with a concurrency error instead of both "winning". Release adds no new rule here;
  it inherits the aggregate's existing optimistic concurrency.
- **A test-arrangement gotcha.** `CustomWebApplicationFactory` seeds its data when the *first client* is created.
  Reading `_factory.AgentBUserId` before any `CreateClient()` returns `0`, and the request is forbidden. The new
  endpoint test creates a client in its constructor, like the other test classes.
- **A folder name that git silently ignored.** The use case first lived in `DeliveryAgents/Release/`. The Visual
  Studio `.gitignore` entry `[Rr]elease/` (meant for `bin/Release` build output) matches *any* folder called
  `Release`, so `git add` skipped both files without a word. Everything passed locally because the files were on disk;
  a clean checkout would not have compiled. The code review caught it, and `dfc0c5a` moved the files to
  `ProgressRelease/` (the `Progress*` naming of its siblings) instead of weakening the ignore rule. Lesson: after
  adding a new folder, check `git status` lists the new files, or run `git check-ignore -v <path>`.

---

## 5. Commit trail

| Commit | Kind | What it proves |
|---|---|---|
| `c83ad60` | test (red) | Test 12: `Delivery.Release` does not exist yet. |
| `5852650` | feat (green) | `Delivery.Release` with its invariants. |
| `c2b21e2` | test | Test 13: after pickup / other agent / unassigned are rejected. They passed at once because the guards came with `Release`; removing the status guard was checked to turn them red. |
| `467e9da` | test (red) | Test 20 at the handler; retires the three agent-cancel tests (§4.1). |
| `bced674` | feat (green) | `DeliveryAssignmentDomainService.ReleaseDelivery` + DI. The handler files were meant to be here but were ignored by git (see §4); `dfc0c5a` adds them. |
| `f469eaf` | test (red) | Test 33: `/release` over HTTP, `/cancel` gone; ownership test renamed (§4.1). |
| `429bd8c` | feat (green) | Endpoint swapped; agent-cancel code deleted end to end. It also corrects two wrong assumptions in the red test (the response property is `id`, and `/active` with no delivery is `404`), matching the existing contract; the commit body says so. |
| `bc43afc` | chore | Delivery contract regenerated (`/cancel` → `/release`). |
| `0415716` | test (red) | Test 34 + Admin path of test 20 over HTTP. |
| `0622329` | feat (green) | Admin `release-agent` endpoint. |
| `3449d41` | chore | Admin contract regenerated (additions only). |
| `7fc1e63` | docs | This walkthrough. |
| `dfc0c5a` | fix | Review fix: the release use case committed under `ProgressRelease/` (it had been git-ignored). |

---

## 6. Self-check questions

1. Why does release reuse the same Delivery row instead of cancelling it and creating a new one? What database
   constraint forces the choice?
2. Release changes two aggregates. Which class coordinates them, and why is that not the controller or the handler?
3. Agent B posts `/release` on agent A's delivery. Trace which component produces the `404`, and explain why the API
   prefers 404 over 403 here.
4. Why is release refused after pickup, and what will an agent use instead once T7 lands?
5. After an Admin releases, what does the customer see, and why does their order status not change?
