You are an expert .NET backend developer working on the Talabat clean architecture API. Your task is to complete the implementation of Phase 8 (DeliveryAgent API) by developing the remaining 8 user stories. 

### 1. Context & Reference Files
Please refer to the following design specifications in the workspace:
- Specification: `specs/007-deliveryagent-api/spec.md`
- Data Model & State Transitions: `specs/007-deliveryagent-api/data-model.md`
- API Endpoint Contracts: `specs/007-deliveryagent-api/contracts/endpoints.md`
- Actionable Tasks List: `specs/007-deliveryagent-api/tasks.md`

### 2. Current Implementation State
The following components are already implemented:
- `ICurrentUser` interface extended with `HasDeliveryAgentCapability` and `AgentId`.
- Concrete `CurrentUser` implementations in `Talabat.API` and `Talabat.Delivery.API` configured.
- Authentication pipeline (JwtBearer, audience: `talabat.deliveryagent-api`) in `Talabat.Delivery.API/Program.cs`.
- Domain exception mappings in `DomainExceptionMapper.cs` and `UseCaseResultExtensions.cs`.
- **User Story 6 (Out For Delivery)** is complete (`OutForDeliveryHandler`, endpoint, and tests).

---

### 3. Strict Rules & Architectural Constraints
- **CQRS-lite**: Follow the handler-per-use-case pattern in `Talabat.Application/DeliveryAgents/`.
- **No Domain Contamination**: Do not reference Identity packages, `UserManager`, or `HttpContext` inside `Talabat.Domain` or `Talabat.Application`.
- **Atomicity**: Ensure that all status and lifecycle updates persist atomically via `IUnitOfWork.SaveChangesAsync`.
- **Assignment**: Use the existing `DeliveryAssignmentDomainService` to assign deliveries to agents.
- **Authorization**: All endpoints in `DeliveriesController`, `StatusController`, and `LocationController` must enforce `[Authorize(Roles = "DeliveryAgent")]`.

---

### 4. Implementation Steps Required

#### Step 1: Template Cleanup
- Delete `src/Talabat/Talabat.Delivery.API/WeatherForecast.cs` and `src/Talabat/Talabat.Delivery.API/Controllers/WeatherForecastController.cs`.

#### Step 2: Implement Availability Status (US1)
- Create `GoOnlineHandler` and `GoOfflineHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/GoOnline/` and `GoOffline/`.
- Add `StatusController.cs` in `src/Talabat/Talabat.Delivery.API/Controllers/` with `PUT /api/agent/status/online` and `PUT /api/agent/status/offline`.
- Add tests in `tests/Talabat.Application.Tests/Domain/Users/UserAgentLifecycleTests.cs` (or similar).

#### Step 3: Implement Location Tracking (US2)
- Create `UpdateLocationHandler` and request DTO in `src/Talabat/Talabat.Application/DeliveryAgents/UpdateLocation/`.
- Add `LocationController.cs` in `src/Talabat/Talabat.Delivery.API/Controllers/` with `PUT /api/agent/location`.
- Add coordinate validation matching domain constraints (Latitude [-90, 90], Longitude [-180, 180]).

#### Step 4: Implement Delivery Assignment (US3)
- Create `AssignDeliveryAgentHandler` in `src/Talabat/Talabat.Application/DeliveryAgents/AssignDelivery/`. Use `DeliveryAssignmentDomainService` to handle coordination.
- Add `POST /api/agent/deliveries/{deliveryId}/assign` in `DeliveriesController.cs`.

#### Step 5: Implement Remaining Lifecycle Transitions (US4, US5, US7, US8)
- Create handlers in `src/Talabat/Talabat.Application/DeliveryAgents/`:
  - `ArriveAtRestaurantHandler` (`ProgressArrive/`)
  - `PickUpOrderHandler` (`ProgressPickup/`)
  - `DeliverOrderHandler` (`ProgressDeliver/`)
  - `CancelDeliveryHandler` (`ProgressCancel/`)
  - `FailDeliveryHandler` (`ProgressFail/`) - must capture `FailureReason`.
- Add the corresponding endpoints in `DeliveriesController.cs`:
  - `POST /api/agent/deliveries/{deliveryId}/arrive`
  - `POST /api/agent/deliveries/{deliveryId}/pickup`
  - `POST /api/agent/deliveries/{deliveryId}/deliver`
  - `POST /api/agent/deliveries/{deliveryId}/cancel`
  - `POST /api/agent/deliveries/{deliveryId}/fail`

#### Step 6: Implement Queries (US9)
- Create query handlers in `src/Talabat/Talabat.Application/DeliveryAgents/`:
  - `GetActiveDeliveryHandler` (fetching current active delivery for current agent).
  - `GetDeliveryHistoryHandler` (fetching historical deliveries assigned to current agent).
  - `GetPendingDeliveriesHandler` (fetching unassigned deliveries pending assignment).
- Add the corresponding endpoints in `DeliveriesController.cs`:
  - `GET /api/agent/deliveries/active`
  - `GET /api/agent/deliveries/history`
  - `GET /api/agent/deliveries/pending`

#### Step 7: Registration & Verification
- Register all new handlers in `src/Talabat/Talabat.Application/DependencyInjection.cs`.
- Write/update tests in `tests/Talabat.Application.Tests/Domain/Deliveries/DeliveryLifecycleTests.cs` to cover all transitions, validation rules, ownership enforcement, and failure states.
- Run `dotnet build` and `dotnet test` to verify everything compiles and passes cleanly.
