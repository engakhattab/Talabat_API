# Implementation Plan: DeliveryAgent API

**Branch**: `feature/user-aggregate-refactor` | **Date**: 2026-07-21 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/007-deliveryagent-api/spec.md`

## Summary

The DeliveryAgent API exposes capabilities for authenticated delivery agents to manage status, update location, inspect active and pending tasks, and progress the delivery lifecycle (Assigned → Arrived at Restaurant → Picked Up → Out for Delivery → Delivered). Handlers in `Talabat.Application` will orchestrate operations on the unified `User` aggregate and `Delivery` aggregate, utilizing `DeliveryAssignmentDomainService` for atomic assignment and completion transitions.

## Technical Context

- **Language/Version**: C# / .NET 10
- **Primary Dependencies**: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.EntityFrameworkCore.SqlServer`
- **Storage**: MS SQL Server via EF Core (`TalabatDbContext`)
- **Testing**: xUnit with integration test support, utilizing EF InMemory or SQL Server LocalDB
- **Target Platform**: ASP.NET Core Web API
- **Project Type**: Web API (new HTTP host: `Talabat.Delivery.API`)
- **Performance Goals**: Under 100ms response time for typical status/location updates
- **Constraints**: No EF Core/Identity packages inside Domain/Application (except approved `Microsoft.Extensions.Identity.Stores` in Domain). Handlers must be thin, CQRS-lite. No direct `UserManager` inside handlers.
- **Scale/Scope**: Expose delivery-agent functionality safely, enforcing assigned-agent ownership.

## Constitution Check

- **Principle 1 (Domain Isolation)**: Domain remains independent of EF and Web packages. No new dependencies added to Domain.
- **Principle 4 (Repository isolation)**: Domain interfaces (`IDeliveryRepository`, `IUserRepository`) are implemented in Infrastructure. No EF types leak.
- **Principle 5 (HTTP composition root)**: `Talabat.Delivery.API` serves as composition root. No business logic in controllers.
- **Principle 6 (Unified User Model)**: Leverages the unified `User : IdentityUser<int>` aggregate. No separate delivery agent profile tables.
- **Decided Standard (Concurrency)**: `RowVersion` concurrency checks will be performed on User and Delivery entities.

## Project Structure

### Documentation

```text
specs/007-deliveryagent-api/
  spec.md
  plan.md
  research.md
  data-model.md
  quickstart.md
  contracts/
    endpoints.md
  checklists/
    requirements.md
```

### Source Code

```text
src/Talabat/Talabat.Application/
  Abstractions/
    ICurrentUser.cs
  DeliveryAgents/
    GoOnline/
    GoOffline/
    UpdateLocation/
    GetActiveDelivery/
    GetDeliveryHistory/
    GetPendingDeliveries/
    AssignDelivery/
    ProgressArrive/
    ProgressPickup/
    ProgressOutForDelivery/
    ProgressDeliver/
    ProgressCancel/
    ProgressFail/
src/Talabat/Talabat.Delivery.API/
  Controllers/
    StatusController.cs
    LocationController.cs
    DeliveriesController.cs
  Auth/
    CurrentUser.cs
  Middleware/
    ProfileEnforcementFilter.cs
```

## Phase 0: Research
- Mapped JwtBearer validation against `Talabat.Identity` with `talabat.deliveryagent-api` audience.
- Extended `ICurrentUser` to resolve `HasDeliveryAgentCapability` and `AgentId`.
- Documented domain exception mappings in `research.md`.

## Phase 1: Design And Contracts
- Created `data-model.md` defining `User` and `Delivery` state machine/constraints.
- Documented endpoint query/command contracts in `contracts/endpoints.md`.
- Wrote testing scenarios in `quickstart.md`.

## Phase 2: Planning Handoff
Implementation sequence:
1. Extend `ICurrentUser` interface and update the Customer API concrete `CurrentUser` implementation to support the new properties.
2. Implement delivery-agent status handlers: `GoOnlineHandler`, `GoOfflineHandler`, `UpdateLocationHandler` in Application.
3. Implement delivery lifecycle handlers: `AssignDeliveryAgentHandler` (using domain service), `ArriveAtRestaurantHandler`, `PickUpOrderHandler`, `OutForDeliveryHandler`, `DeliverOrderHandler`, `CancelDeliveryHandler`, and `FailDeliveryHandler` in Application.
4. Implement query handlers: `GetActiveDeliveryHandler`, `GetDeliveryHistoryHandler`, `GetPendingDeliveriesHandler` in Application.
5. Scaffold and wire `Talabat.Delivery.API` (set up authentication, DI, exception handling, custom `CurrentUser`).
6. Remove `WeatherForecast` template files in `Talabat.Delivery.API`.
7. Add API controllers exposing the endpoint routes mapped to Application handlers.
8. Implement end-to-end integration and unit tests validating status transitions, ownership checks, and concurrency handling.

## Post-Design Constitution Check
The design strictly follows all principles of the project constitution. No packages or database models are introduced that violate separation of concerns.
