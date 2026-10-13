# Implementation Plan: Talabat Customer API

> **SUPERSEDED** — This plan describes the original Phase 7 implementation with a separate
> `Customer` aggregate, `ICustomerRepository`, and `IdentityUserId` linkage. The unified
> `User : IdentityUser<int>` aggregate and `IUserRepository` have replaced this design.
> Controller, middleware, and exception-mapping code remain valid; identity, repository, and
> data-model sections are superseded.
>
> Current authority: `user-aggregate-refactor-plan.md`,
> `specs/004-unified-user-domain-model/`, `specs/005-unified-user-identity-cutover/`,
> `specs/006-unified-user-behavior-governance/`.

**Branch**: `main` | **Date**: 2026-07-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/003-customer-api/spec.md`

## Summary

Phase 7 transforms the template `Talabat.API` project into a production-ready `Talabat.Customer.API`
host that exposes all customer-facing use cases (catalog, basket, profile, addresses, checkout,
orders) through attribute-routed controllers. The host validates bearer tokens against the
`Talabat.Identity` authority and resolves the account->`Customer` link through a read-only,
framework-neutral `ICurrentUser` abstraction; profiles are created explicitly on first use via
`POST /api/me/profile` (no empty/placeholder profiles). It maps Domain exceptions to RFC 9457
ProblemDetails and ships its own integration test project. A development CORS policy and `/health` endpoint complete the
operational baseline.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ASP.NET Core 10, Microsoft.AspNetCore.Authentication.JwtBearer,
  Microsoft.AspNetCore.Diagnostics.HealthChecks (built-in), Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
**Storage**: SQL Server (existing `TalabatDb` via `TalabatDbContext`)
**Testing**: xUnit, `WebApplicationFactory<Program>`, test-minted JWTs, SQL Server integration tests
**Target Platform**: ASP.NET Core Web API host (`net10.0`)
**Project Type**: Customer-facing HTTP composition root
**Performance Goals**: Catalog browse < 2s for 100 restaurants; auth enforcement < 1s
**Constraints**: No reference to `Talabat.Identity`; Domain/Application free of web packages; thin
  controllers only; the account->profile linkage rule is provisional (Phase 9 refinement); cart
  endpoints are authenticated and customer-scoped (guest/anonymous carts are a separate future phase)
**Scale/Scope**: 6 controllers, ~16 endpoints, 1 new Application use case (create-profile), 1 new
  Domain property + creation factory, 2 new Infrastructure service implementations (`IClock`,
  `IRestaurantLocalTimeProvider`), 1 Infrastructure migration, 1 test project

## Constitution Check

### Pre-Design Evaluation

| Principle | Status | Notes |
|-----------|--------|-------|
| P1: Domain independence | ✅ PASS | Only change is nullable `string? IdentityUserId` — a scalar, not an Identity type |
| P2: Application orchestration | ✅ PASS | New `CreateCustomerProfileHandler` follows existing handler pattern; `AddApplication()` also registers `CheckoutDomainService` and the clock/local-time implementations |
| P3: Aggregate root invariants | ✅ PASS | No child-entity repositories; `Customer` still owns addresses |
| P4: Repository contracts in Domain | ✅ PASS | New `GetByIdentityUserIdAsync` added to existing `ICustomerRepository` |
| P5: Thin HTTP host | ✅ PASS | Controllers delegate to handlers; no business logic |
| P6: Identity boundary | ✅ PASS | Constitution v2.1.0 explicitly permits nullable scalar linkage key and `ICurrentUser` |
| P7: Database-generated IDs | ✅ PASS | No changes to ID strategy |
| P8: EF encapsulation | ✅ PASS | Private constructors and backing fields preserved |
| P9: DB constraints back invariants | ✅ PASS | Unique filtered index on `IdentityUserId` |

### Phase Scope Compliance

| Allowed Item | Status |
|-------------|--------|
| Rename `Talabat.API` → `Talabat.Customer.API` | ✅ Planned |
| Remove WeatherForecast template code | ✅ Planned |
| `AddApplication()` DI extension | ✅ Planned |
| DomainException → ProblemDetails mapping | ✅ Planned |
| JwtBearer validation against Identity authority | ✅ Planned |
| `/api/me/...` owner-scoped routes | ✅ Planned |
| Explicit create-profile-on-first-use + `ICurrentUser` + `IdentityUserId` | ✅ Planned |
| OpenAPI, dev CORS, `/health` | ✅ Planned |
| `tests/Talabat.Customer.API.Tests` | ✅ Planned |
| Authorization matrix | ✅ Planned |

| Prohibited Item | Status |
|----------------|--------|
| DeliveryAgent endpoints | ✅ Not included |
| Login/register/logout endpoints | ✅ Not included |
| Reference to `Talabat.Identity` | ✅ Not included |
| Token-issuing client in Identity | ✅ Not included |
| Business logic in controllers | ✅ Not included |
| Domain entities as HTTP responses | ✅ Not included |
| Angular/frontend | ✅ Not included |

## Project Structure

### Documentation

```text
specs/003-customer-api/
├── spec.md
├── plan.md              ← this file
├── research.md
├── data-model.md
├── contracts/
│   └── api-endpoints.md
└── checklists/
    ├── requirements.md
    └── api.md
```

### Source Code

```text
src/Talabat/
├── Talabat.Customer.API/           ← renamed from Talabat.API
│   ├── Program.cs                  ← composition root
│   ├── Talabat.Customer.API.csproj
│   ├── Auth/
│   │   └── CurrentUser.cs          ← ICurrentUser implementation
│   ├── Contracts/
│   │   ├── Catalog/
│   │   ├── Cart/
│   │   ├── Customer/
│   │   ├── Checkout/
│   │   ├── Orders/
│   │   └── Common/
│   ├── Controllers/
│   │   ├── CatalogController.cs
│   │   ├── CartController.cs
│   │   ├── CustomerController.cs
│   │   ├── AddressController.cs
│   │   ├── CheckoutController.cs
│   │   └── OrderController.cs
│   ├── Extensions/
│   │   └── UseCaseResultExtensions.cs
│   ├── Middleware/
│   │   └── DomainExceptionHandler.cs
│   └── appsettings*.json
├── Talabat.Application/
│   ├── Abstractions/
│   │   ├── IClock.cs               ← existing
│   │   ├── IRestaurantLocalTimeProvider.cs  ← existing
│   │   └── ICurrentUser.cs         ← NEW
│   ├── Customers/
│   │   └── CreateProfile/          ← NEW
│   │       ├── CreateCustomerProfileCommand.cs
│   │       └── CreateCustomerProfileHandler.cs
│   └── DependencyInjection.cs      ← NEW (AddApplication; registers handlers + CheckoutDomainService)
├── Talabat.Domain/
│   ├── Aggregates/Customer/
│   │   └── Customer.cs             ← add IdentityUserId property
│   └── Interfaces/
│       └── ICustomerRepository.cs  ← add GetByIdentityUserIdAsync
├── Talabat.Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/
│   │   │   └── CustomerConfiguration.cs  ← add IdentityUserId mapping + unique filtered index
│   │   └── Repositories/
│   │       └── CustomerRepository.cs     ← add GetByIdentityUserIdAsync
│   ├── Time/                             ← NEW
│   │   ├── SystemClock.cs                ← IClock implementation (none existed before)
│   │   └── RestaurantLocalTimeProvider.cs ← IRestaurantLocalTimeProvider implementation
│   ├── DependencyInjection.cs            ← register SystemClock + RestaurantLocalTimeProvider
│   └── Migrations/                       ← new migration
tests/
├── Talabat.Customer.API.Tests/          ← NEW test project
│   ├── Talabat.Customer.API.Tests.csproj
│   ├── Infrastructure/
│   │   ├── CustomWebApplicationFactory.cs
│   │   └── TestAuthHandler.cs
│   ├── CatalogEndpointTests.cs
│   ├── CartEndpointTests.cs
│   ├── CustomerEndpointTests.cs
│   ├── AddressEndpointTests.cs
│   ├── CheckoutEndpointTests.cs
│   ├── OrderEndpointTests.cs
│   ├── AuthEnforcementTests.cs
│   └── ErrorMappingTests.cs
docs/
└── authorization-matrix.md              ← NEW
```

## Phase 0: Research

All unknowns resolved in [research.md](research.md):

| ID | Unknown | Decision |
|----|---------|----------|
| R1 | Exception mapping strategy | `IExceptionHandler` + `ApplicationErrorCategory` → HTTP status mapping |
| R2 | Bearer token validation | JwtBearer against Identity authority; no audience validation in Phase 7 |
| R3 | Account→profile strategy | Explicit create-on-first-use (`POST /api/me/profile`); `CreateCustomerProfileHandler` + `IdentityUserId` + unique index; no auto-provision |
| R4 | ICurrentUser design | Read-only framework-neutral interface (`HasProfile`, `int? CustomerId`) in Application; implementation in API host; no side-effecting write |
| R5 | Test strategy | `WebApplicationFactory` + test auth handler + SQL Server |
| R6 | CORS configuration | `localhost`-only named policy in Development |
| R7 | Health checks | Built-in ASP.NET Core health checks + EF Core DbContext check |
| R8 | AddApplication() design | Explicit per-handler registration in Application DI extension |

## Phase 1: Design And Contracts

Design artifacts completed:

- **[data-model.md](data-model.md)**: Domain `Customer.IdentityUserId`, `ICurrentUser`, repository
  method, controller layout, DTO structure, exception handler, and result-to-action-result mapping.
- **[contracts/api-endpoints.md](contracts/api-endpoints.md)**: Full endpoint catalog with route
  templates, HTTP methods, request/response JSON shapes, status codes, and error format.
- **[research.md](research.md)**: Eight research decisions covering every technical unknown.

## Phase 2: Planning Handoff

### Implementation Sequence

The implementation should follow this dependency order:

**Stage 1: Foundation (no endpoints yet)**
1. Rename `Talabat.API` → `Talabat.Customer.API` (project, solution, namespaces, docs).
2. Remove `WeatherForecast` template code.
3. Add `IdentityUserId` to `Customer` Domain entity.
4. Add `GetByIdentityUserIdAsync` to `ICustomerRepository` and implement in Infrastructure.
5. Add the `IdentityUserId` EF configuration and create the migration.
6. Create `ICurrentUser` in Application `Abstractions/` (read-only: `IdentityUserId`,
   `IsAuthenticated`, `HasProfile`, `int? CustomerId`).
7. Create `CreateCustomerProfileHandler` in Application (`Customers/CreateProfile/`).
7b. Add production `SystemClock : IClock` and `RestaurantLocalTimeProvider : IRestaurantLocalTimeProvider`
   in Infrastructure — **no implementations of these exist yet** (only test fakes), so the existing
   cart/checkout handlers cannot be resolved from DI without them.
8. Create `AddApplication()` DI extension (registers all use-case handlers **and** the concrete
   `CheckoutDomainService`); register `SystemClock` and `RestaurantLocalTimeProvider` in
   `AddInfrastructure()`.

**Stage 2: API Plumbing (before controllers)**
9. Add JwtBearer authentication configuration to `Program.cs`.
10. Add `DomainExceptionHandler` (`IExceptionHandler`).
11. Add `UseCaseResultExtensions` for `ApplicationErrorCategory` → HTTP status mapping.
12. Add `CurrentUser` implementation in API host.
13. Add CORS policy configuration.
14. Add health check configuration.
15. Update `Program.cs` composition root (wire everything together).

**Stage 3: Controllers (thin transport)**
16. Create request/response DTO contracts.
17. Create `CatalogController` (anonymous).
18. Create `CartController` (authenticated).
19. Create `CustomerController` (authenticated).
20. Create `AddressController` (authenticated).
21. Create `CheckoutController` (authenticated).
22. Create `OrderController` (authenticated).
23. Add OpenAPI configuration and descriptions.

**Stage 4: Tests and Documentation**
24. Create `tests/Talabat.Customer.API.Tests` project.
25. Create test infrastructure (`CustomWebApplicationFactory`, `TestAuthHandler`).
26. Write auth enforcement tests.
27. Write endpoint routing and response shape tests.
28. Write error mapping tests.
29. Write full-workflow integration test.
30. Create authorization matrix document.
31. Run all existing tests to confirm regression-free.
32. Run package vulnerability audit.

### Key Dependencies

```
Stage 1 (Foundation) ──→ Stage 2 (Plumbing) ──→ Stage 3 (Controllers) ──→ Stage 4 (Tests)
         │                       │
         └── Migration must run before API starts
         └── CreateCustomerProfile handler + SystemClock/RestaurantLocalTimeProvider must exist
             before controllers resolve their handlers
```

### Risk Mitigations

| Risk | Mitigation |
|------|-----------|
| Concurrent first-time profile create | Unique index on `IdentityUserId`; unique-constraint violation mapped to `409 Conflict` (`ProfileAlreadyExists`) |
| Cart/checkout handlers unresolvable | Add + register `SystemClock`/`RestaurantLocalTimeProvider` (Stage 1) before any controller uses them |
| Checkout without default address | `CheckoutCommand` requires `DeliveryAddressId`; 404 if invalid |
| Project rename breaks references | Comprehensive rename across .csproj, .slnx, namespaces, launchSettings, docs |
| Token validation without real client | Tests use self-minted JWTs with test-only signing key |
| `IdentityUserId` migration impact | Nullable column + filtered index — no existing data affected |

## Complexity Tracking

No constitution gate failures requiring justification.

## Post-Design Constitution Check

| Principle | Status | Verified Against |
|-----------|--------|-----------------|
| P1: Domain independence | ✅ PASS | `Customer.IdentityUserId` is `string?` — no framework type |
| P2: Application orchestration | ✅ PASS | `CreateCustomerProfileHandler` follows CQRS-lite pattern |
| P3: Aggregate roots | ✅ PASS | No new child-entity repositories |
| P4: Repo contracts in Domain | ✅ PASS | `GetByIdentityUserIdAsync` in `ICustomerRepository` |
| P5: Thin HTTP host | ✅ PASS | Controllers map DTOs ↔ commands/queries and call handlers |
| P6: Identity boundary | ✅ PASS | `ICurrentUser` is framework-neutral; implementation isolated to API host |
| P7: DB-generated IDs | ✅ PASS | No ID strategy changes |
| P8: EF encapsulation | ✅ PASS | Private constructor, backing fields unchanged |
| P9: DB constraints | ✅ PASS | Unique filtered index on `IdentityUserId` |
| Quality Gates | ✅ PASS | All gate items addressed in Stage 4 tasks |
