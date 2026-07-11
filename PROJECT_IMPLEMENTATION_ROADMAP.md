# Project Implementation Roadmap

## 1. Current Repository Status

### 1.1 Implemented In Code

- The solution file is `src/Talabat/Talabat.slnx`.
- The solution currently contains four projects:
  - `Talabat.Domain`
  - `Talabat.Application`
  - `Talabat.Infrastructure`
  - `Talabat.API`
- All projects currently target `net10.0`.
- The project dependency direction is mostly clean:
  - `Talabat.Application` references `Talabat.Domain`.
  - `Talabat.Infrastructure` references `Talabat.Application`.
  - `Talabat.API` references `Talabat.Application`.
  - `Talabat.Domain` has no project references and no package references.
- The Domain layer contains the main implemented business model:
  - Catalog: `Restaurant`, `Product`.
  - Basket: `Cart`, `CartItem`, `CartStatus`.
  - Ordering: `Order`, `OrderItem`.
  - Customer: `Customer`, `CustomerAddress`.
  - Delivery Management: `Delivery`, `DeliveryAgent`, `DeliveryStatus`, `DeliveryAgentStatus`, `VehicleType`.
- The Domain layer contains value objects and snapshots:
  - `Money`
  - `TimeRange`
  - `Address`
  - `DeliveryAddressSnapshot`
  - `CatalogProductSnapshot`
  - `CheckoutItemSnapshot`
  - `GeoLocation`
- The Domain layer contains domain services:
  - `CheckoutDomainService`
  - `DeliveryAssignmentDomainService`
- The Domain layer contains focused domain exceptions under `Talabat.Domain.Exceptions`.
- The Domain layer currently has no EF Core, ASP.NET Core, Identity, JWT, controller, HTTP, repository implementation, or database dependency.
- `AuditableEntity` exists in Domain and includes audit/soft-delete fields such as `CreatedBy`, `ModifiedBy`, and `DeletedBy`.
- `Talabat.API` is still a template-style ASP.NET Core API:
  - `Program.cs` registers controllers and OpenAPI.
  - `UseAuthorization()` is present.
  - There is no `AddAuthentication()`.
  - There is no JWT bearer setup.
  - There are no business API endpoints yet.
  - `WeatherForecastController` and `WeatherForecast` still exist as template code.
- `Talabat.Application` exists as a project but has no production source files beyond the project file.
- `Talabat.Infrastructure` exists as a project but has no production source files beyond the project file.
- No IdentityServer, ASP.NET Core Identity, JWT bearer, EF Core, MediatR, or validation packages are installed in the production projects.

### 1.2 Designed In Documentation Only

- Strategic and tactical DDD documentation exists under `docs/`.
- Existing documentation covers:
  - Bounded contexts.
  - Business rules.
  - Business rule classification.
  - Aggregates and invariants.
  - Entity design.
  - Value object design.
  - Domain services.
  - Domain failures.
  - Repository interface design.
  - Delivery extension design.
  - Domain review findings.
  - IdentityServer4 readiness analysis.
- The existing documentation describes repository interfaces, but the interfaces are not implemented in code.
- The existing documentation describes Application use cases, but no Application handlers, commands, queries, DTOs, validators, or orchestration services exist yet.
- The existing documentation describes Infrastructure and EF Core mapping concerns, but no DbContext, EF configurations, repositories, migrations, seed data, or connection strings exist yet.
- The existing documentation describes Delivery database design, but no delivery persistence exists yet.
- Root planning files exist:
  - `PLAN.md`
  - `Talabat_Implementation_Roadmap.md`
  - `Talabat_DDD_Project_Architecture_Prompt.md`
- These root planning files still contain old MVP assumptions and should not be treated as the final implementation plan without revision.

### 1.3 Partially Implemented

- Clean Architecture project structure is started but not complete.
- Domain modeling is significantly ahead of the other layers.
- Delivery is no longer documentation-only: Delivery domain classes and the delivery assignment domain service exist in code.
- Checkout has domain validation logic, but the full checkout Application use case does not exist.
- Cart, Customer, Catalog, Ordering, and Delivery aggregates protect many invariants, but persistence and transaction boundaries are not implemented.
- `Talabat.Domain.csproj` still contains a folder include for `Interfaces\`, but no repository interfaces exist.
- Authorization is only present as a middleware call in API. Authentication and policies are not configured.
- Audit fields exist in Domain, but there is no current-user abstraction or infrastructure mechanism that supplies audit identity values.
- The historical domain review file contains findings that appear to have been partly remediated in code. It should be refreshed before being used as current truth.

### 1.4 Missing

- No test projects exist for Domain, Application, Infrastructure, or API.
- No Application layer use cases exist.
- No commands, queries, DTOs, result models, validators, or use-case handlers exist.
- No repository interfaces exist in code.
- No unit-of-work abstraction exists in code.
- No current-user abstraction exists in code.
- No clock/time-provider abstraction exists in code.
- No ID-generation strategy is documented as an implementation decision.
- No Infrastructure persistence exists:
  - No EF Core packages.
  - No DbContext.
  - No entity configurations.
  - No repository implementations.
  - No migrations.
  - No database schema.
  - No seed data.
- No business API endpoints exist.
- No API exception mapping middleware exists.
- No request/response contracts exist.
- No authentication implementation exists.
- No authorization policies exist.
- No IdentityServer/Auth Portal project exists.
- No Customer Website frontend exists.
- No Delivery Website frontend exists.
- No IdentityServer Website/Auth Portal exists.
- No payment, notifications, coupons, reviews, restaurant-owner workflows, or admin workflows exist.
- No CI workflow files were found under `.github/workflows`.

### 1.5 Needs Refactoring Or Scope Update

- Old MVP v1 assumptions must be revised. The system is no longer strictly a single unauthenticated customer MVP.
- Documentation that says authentication, authorization, Identity, JWT, delivery drivers, admins, or restaurant owners are fully out of scope must be updated to say:
  - Not implemented now.
  - Reserved for later.
  - Architecture must not block future integration.
- Documentation that says the system assumes one normal customer profile must be updated. That assumption may still be useful for early local testing, but it is not the final system direction.
- Any documentation recommending immediate Identity implementation should be changed. IdentityServer/Auth Portal must remain a reserved/TBD phase until the framework decision is made.
- `Talabat_DDD_Project_Architecture_Prompt.md` mentions ASP.NET Core Identity as the authentication approach. That is no longer decided and must be revised.
- `PLAN.md` and `Talabat_Implementation_Roadmap.md` mention Identity in the planned stack/implementation sequence. These should be updated or superseded by this roadmap.
- `docs/identityserver4-readiness-report.md` is useful research, but it is too specific to IdentityServer4 for the new strategy. It should be reframed as historical research, not a framework decision.
- API template code should eventually be removed or replaced with real health/version endpoints, but not during this roadmap-only step.
- The audit base entity should be reviewed before persistence. Domain should not depend on claims or Identity types, and audit user values should be supplied from outer layers through abstractions later.

## 2. Updated Project Direction

### 2.1 What Changed From The Old MVP v1

- The old MVP v1 deliberately excluded authentication, authorization, Identity, login/register, JWT, admins, restaurant owners, delivery drivers, payment, notifications, coupons, and reviews.
- The new direction keeps the Domain-first and Clean Architecture approach, but the system must now be prepared for a future authenticated multi-user platform.
- Delivery is no longer merely a far-future idea. Delivery domain code already exists and must be included in future repository, Application, Infrastructure, API, and website planning.
- The final system must account for three websites/web apps:
  - Customer Website.
  - Delivery Website.
  - IdentityServer Website/Auth Portal.
- Identity/Auth is now a planned cross-cutting capability, but the concrete framework is intentionally undecided.
- The project should no longer design around a permanent single-customer assumption.
- Customer-specific reads such as order history and cart access must eventually be scoped to the authenticated customer profile.
- Delivery operations must eventually be scoped to authenticated delivery agents or delivery operations roles.

### 2.2 What Still Remains Valid

- Domain layer must remain independent from frameworks and infrastructure.
- Aggregate roots should protect invariants.
- Child entities should be modified through aggregate roots only.
- Repositories should exist only for aggregate roots.
- Domain services should be stateless and should not load data or save changes.
- Application layer should orchestrate use cases.
- Infrastructure should implement persistence, repositories, external integrations, and future identity integration details.
- API should handle HTTP request/response mapping, authentication middleware, authorization policies, and endpoint wiring.
- Cart should not store product prices.
- Orders should store immutable price and delivery address snapshots.
- Checkout should use current Catalog prices.
- Delivery should remain separate from Ordering:
  - `Order` is a historical purchase record.
  - `Delivery` is an operational task.
  - `DeliveryAgent` is a courier/agent profile and availability aggregate.

### 2.3 What Must Be Deferred

- Do not implement IdentityServer now.
- Do not choose IdentityServer4, Duende IdentityServer, OpenIddict, ASP.NET Core Identity only, or any other identity framework now.
- Do not install identity packages now.
- Do not write login/register/token implementation now.
- Do not add Identity-specific types to Domain entities now.
- Do not build the three websites now.
- Do not implement payment, notifications, coupons, reviews, admin management, or restaurant-owner workflows now.

### 2.4 IdentityServer Strategy Placeholder

Identity/Auth must be treated as a reserved architectural phase.

The current recommendation is:

- Keep the Domain independent from Identity.
- Keep all auth framework decisions outside the Domain model.
- Use scalar domain identifiers such as `CustomerId` and `DeliveryAgentId` inside core aggregates.
- Later, after selecting an identity framework, decide how account identities map to domain profiles.
- A future Application abstraction such as `ICurrentUserContext` or `ICurrentUser` is likely needed, but it should be introduced only when authenticated use cases are being designed.
- Any future identity linkage should be scalar and framework-neutral from the Domain perspective. Do not put `ApplicationUser`, `ClaimsPrincipal`, `IdentityUser`, `HttpContext`, JWT claims, or IdentityServer-specific types in Domain.

## 3. Target Architecture

### 3.1 Backend Layers

Recommended backend structure:

```text
Talabat.Domain
  Enterprise/business rules, aggregates, value objects, domain services, domain exceptions.
  No EF Core, ASP.NET Core, Identity, HTTP, repositories implementations, or database concerns.

Talabat.Application
  Use-case orchestration, commands, queries, DTOs/results, application contracts, transaction intent.
  Depends on Domain.
  Does not depend on Infrastructure or API.

Talabat.Infrastructure
  EF Core, DbContext, repository implementations, UnitOfWork implementation, external services,
  seed data, future identity integration adapters.
  Depends inward on Application/Domain contracts.

Talabat.API
  HTTP endpoints, controllers or minimal APIs, request/response mapping, authentication middleware,
  authorization policies, exception mapping, OpenAPI, DI composition.
  Depends on Application and eventually Infrastructure for DI registration.

Talabat.Identity/Auth Portal
  Reserved/TBD future host for login/register/authentication/authorization/token issuing/user management.
  Framework not selected yet.
```

### 3.2 Bounded Contexts

Current and target bounded contexts:

- Catalog:
  - Owns restaurants, products, active state, opening hours, availability, and current prices.
- Basket:
  - Owns active cart, cart items, quantity rules, one-restaurant-per-cart rule, and cart expiry.
- Ordering:
  - Owns checkout result, orders, immutable order item snapshots, order totals, and order history.
- Customer:
  - Owns customer profile and addresses.
  - Must remain separate from authentication account implementation.
- Delivery Management:
  - Owns delivery tasks, delivery lifecycle, delivery agents, agent availability, and assignment coordination.
- Identity/Auth:
  - Future cross-cutting context/host.
  - Owns accounts, credentials, tokens, roles, claims, policies, and user management depending on framework choice.
  - Must not own core business rules for carts, orders, customers, restaurants, or deliveries.

### 3.3 Aggregate Boundaries

Current aggregate roots:

- `Restaurant`
  - Child: `Product`
  - Repository later: `IRestaurantRepository`
- `Cart`
  - Child: `CartItem`
  - Repository later: `ICartRepository`
- `Order`
  - Child: `OrderItem`
  - Repository later: `IOrderRepository`
- `Customer`
  - Child: `CustomerAddress`
  - Repository later: `ICustomerRepository`
- `Delivery`
  - Repository later: `IDeliveryRepository`
- `DeliveryAgent`
  - Repository later: `IDeliveryAgentRepository`

Do not create repositories for:

- `Product`
- `CartItem`
- `OrderItem`
- `CustomerAddress`

Important aggregate rules:

- `Product` is modified through `Restaurant`.
- `CartItem` is modified through `Cart`.
- `OrderItem` is created through `Order`.
- `CustomerAddress` is modified through `Customer`.
- `Delivery` and `DeliveryAgent` are separate aggregate roots coordinated by an Application use case and the `DeliveryAssignmentDomainService`.

### 3.4 Identity/Auth Boundary

The Domain model should not know the identity framework.

Allowed now:

- Continue using domain IDs such as `CustomerId`, `RestaurantId`, `OrderId`, `DeliveryAgentId`, and `DeliveryId`.
- Keep Customer and DeliveryAgent as business profiles.
- Document that future auth will resolve a current authenticated account to the correct domain profile.

Avoid now:

- `IdentityUser`
- `ApplicationUser`
- `ClaimsPrincipal`
- `HttpContext`
- JWT claims in Domain
- IdentityServer client/resource/scope types in Domain
- Direct role checks inside aggregates
- Domain dependencies on ASP.NET Core packages

Likely future boundary:

- API authenticates the caller.
- API or Application reads the current user through an abstraction.
- Application resolves the domain profile:
  - Customer Website flow: authenticated account -> `Customer`.
  - Delivery Website flow: authenticated account -> `DeliveryAgent` or delivery operations user.
- Domain methods receive domain IDs and values, not tokens or claims.

### 3.5 Three Website Model

#### Customer Website

Purpose:

- Customer-facing web app for browsing restaurants/products, managing basket/cart, checkout, profile, addresses, and order history.

Backend contexts used:

- Catalog for restaurants and menus.
- Basket for cart operations.
- Customer for profile and addresses.
- Ordering for checkout and order history.
- Delivery Management later for order delivery status display.

APIs/use cases needed before building:

- Browse restaurants.
- Get restaurant menu.
- Get cart.
- Add item to cart.
- Update cart item quantity.
- Remove cart item.
- Clear cart.
- Get customer profile.
- Update customer profile.
- Add/remove/set default address.
- Checkout.
- Get order history.
- Get order details.
- Later: get delivery status for an order.

Future identity/auth concerns:

- Customer endpoints will require authenticated Customer role/policy later.
- Current customer profile should be resolved from the authenticated account, not from a hardcoded MVP customer.
- The frontend should not know database customer IDs as an authorization mechanism.

What must be implemented first:

- Domain cleanup.
- Repository contracts.
- Application use cases.
- Persistence.
- API endpoints.
- Reserved current-user boundary design before auth is enabled.

#### Delivery Website

Purpose:

- Delivery-agent or delivery-operations web app for delivery task assignment, agent status, delivery progress, and delivery lifecycle.

Backend contexts used:

- Delivery Management for delivery tasks and agent lifecycle.
- Ordering for order reference data needed to create or inspect delivery tasks.
- Customer/Catalog only through snapshots or read models where needed for delivery display.

APIs/use cases needed before building:

- Get delivery-agent profile.
- Set agent online/offline.
- Update agent location.
- Get available/pending delivery tasks, depending on assignment model.
- Assign delivery agent.
- Mark arrived at restaurant.
- Mark picked up.
- Mark out for delivery.
- Mark delivered.
- Cancel/fail delivery through coordinated use cases.
- Get delivery status.

Future identity/auth concerns:

- Delivery endpoints will require authenticated DeliveryAgent or delivery-operations policies later.
- Delivery lifecycle actions must verify the authenticated agent maps to the `DeliveryAgentId` used in the domain operation.
- Operations/admin assignment may require separate policy design later.

What must be implemented first:

- Delivery repository contracts.
- Delivery Application use cases.
- Delivery persistence and constraints.
- Delivery API endpoints.
- Clear authorization policy candidates before auth framework implementation.

#### IdentityServer Website / Auth Portal

Purpose:

- Future dedicated authentication/authorization portal for login, registration, token issuing, account management, roles, and user management depending on the selected framework.

Backend contexts used:

- Identity/Auth context.
- It may coordinate account creation with Customer or DeliveryAgent profile creation through Application services after the framework decision.

APIs/use cases needed before building:

- Not defined yet.
- Registration/login/token/user-management flows must wait for framework selection.

Future identity/auth concerns:

- Framework choice is TBD.
- Token shape, roles, scopes, claims, refresh-token behavior, account lifecycle, and user management are TBD.

What must be implemented first:

- Stable core Domain.
- Application and persistence foundations.
- Clear boundaries that keep Domain independent.
- A later architecture decision record comparing identity options.

## 4. Recommended Implementation Order

Recommended order:

1. Repository and documentation audit.
2. Domain cleanup and invariant stabilization.
3. Domain and Application contracts.
4. Application layer use cases.
5. Persistence and Infrastructure.
6. API layer.
7. Customer Website backend support.
8. Delivery Website backend support.
9. Reserved IdentityServer/Auth Portal decision and implementation phase.
10. Authorization strategy.
11. Testing and quality gates.
12. Delivery extensions and advanced features.

This order is safer than starting directly with IdentityServer because the current repository is Domain-heavy and has no Application or Infrastructure foundation yet. Authentication depends on stable profile boundaries, repository contracts, persistence, transactions, and API policies. Starting IdentityServer now would force identity decisions into a system that has not yet defined its Application use cases or persistence model.

The immediate priority is to make the core business model stable, then define use-case contracts, then implement persistence and API boundaries. Identity should be delayed until the system has clear domain profile concepts, use cases, repositories, and transaction boundaries that Identity can integrate with cleanly.

## 5. Phased Roadmap

### Phase 0: Repository And Documentation Audit

- Goal
  - Establish a current, trusted baseline before adding more code.
- Strategy
  - Treat this phase as read-only except for documentation corrections.
  - Reconcile the old MVP v1 scope with the new long-term three-website direction.
  - Separate current facts from historical review notes.
- Main decisions
  - Confirm this roadmap supersedes the old MVP-only roadmaps for sequencing.
  - Confirm Identity/Auth remains reserved/TBD.
  - Confirm Delivery domain is part of the current codebase, while Delivery outer layers remain deferred.
- Actions
  - Re-read `src/Talabat/Talabat.slnx` and all project files.
  - Confirm which Domain review findings are already fixed in current code.
  - Confirm there are no production Identity/Auth packages or source files.
  - Confirm Application and Infrastructure are still empty.
  - Confirm API is template-only.
  - Mark old MVP assumptions that need revision.
  - Decide whether `docs/identityserver4-readiness-report.md` stays as historical research or is renamed/reframed.
- Files/Folders To Inspect Or Update
  - `PROJECT_IMPLEMENTATION_ROADMAP.md`
  - `PLAN.md`
  - `Talabat_Implementation_Roadmap.md`
  - `Talabat_DDD_Project_Architecture_Prompt.md`
  - `docs/`
  - `docs/reviews/domain-review-report.md`
  - `docs/identityserver4-readiness-report.md`
  - `src/Talabat/`
- What Should Not Be Done
  - Do not implement repositories, use cases, infrastructure, API endpoints, Identity, or frontend code.
  - Do not install packages.
  - Do not run migrations.
  - Do not refactor source files as part of the audit.
- Acceptance Criteria
  - Current implementation status is documented accurately.
  - Old MVP v1 exclusions are explicitly updated to deferred/reserved where appropriate.
  - Identity framework remains undecided.
  - No source code, packages, migrations, or frontend work are introduced.
- Risks / Mistakes To Avoid
  - Do not treat historical review findings as current without verifying code.
  - Do not use the IdentityServer4 readiness report as a decision to adopt IdentityServer4.
  - Do not begin repository or Identity implementation during audit.

### Phase 1: Domain Cleanup And Invariant Stabilization

- Goal
  - Make the Domain layer stable enough for repository contracts and persistence design.
- Strategy
  - Fix only Domain correctness, invariant, and documentation mismatches.
  - Keep Domain independent from frameworks.
  - Add or update focused Domain tests if test projects are introduced in this phase.
- Main decisions
  - Confirm cart creation semantics: cart is created with the first item and empty active carts are not persisted.
  - Confirm child identity strategy for `CartItem`, `OrderItem`, and `CustomerAddress`.
  - Confirm UTC timestamp policy and restaurant-local time boundary.
  - Confirm delivery terminal coordination and agent release behavior.
  - Confirm audit fields remain framework-neutral.
- Actions
  - Review all aggregate methods for atomic failure behavior.
  - Review child identity and EF mapping implications before repositories.
  - Review `AuditableEntity` and decide whether user audit values remain plain strings or move to an outer-layer concern later.
  - Ensure domain exceptions are business-language and framework-neutral.
  - Ensure Delivery domain behavior is aligned with delivery documentation.
  - Update docs where code is already correct but docs are stale.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Domain/`
  - `docs/aggregates-and-invariants.md`
  - `docs/entity-design.md`
  - `docs/domain-invariants.md`
  - `docs/domain-failures-design.md`
  - `docs/delivery/`
  - Optional later if approved: `tests/Talabat.Domain.Tests/`
- What Should Not Be Done
  - Do not add repository interfaces yet if aggregate semantics are still unresolved.
  - Do not add EF Core.
  - Do not add Identity packages.
  - Do not add API endpoints.
  - Do not add frontend code.
- Acceptance Criteria
  - Domain remains dependency-free.
  - Aggregate invariants are consistent in code and docs.
  - Child entities are still modified only through aggregate roots.
  - Delivery and checkout coordination rules are explicit.
  - Repository contracts can be designed without ambiguity.
- Risks / Mistakes To Avoid
  - Do not add persistence attributes to Domain entities.
  - Do not add `IdentityUserId` or framework-specific user concepts prematurely.
  - Do not create services that duplicate aggregate behavior.
  - Do not add repositories for child entities.

### Phase 2: Domain And Application Contracts

- Goal
  - Define the contracts Application needs to orchestrate use cases without binding to Infrastructure.
- Strategy
  - Add repository interfaces for aggregate roots only.
  - Add application-level abstractions only when they are needed by use cases.
  - Keep contracts framework-neutral.
- Main decisions
  - Repository interfaces live either in Domain or Application based on the final team convention. The existing docs currently place them in Domain.
  - `IUnitOfWork` should represent commit boundary only.
  - Current-user abstraction should be reserved unless an unauthenticated placeholder is needed for future-proof signatures.
  - Clock/time abstraction should be considered because Domain methods require current UTC time.
- Actions
  - Create repository interfaces for:
    - `IRestaurantRepository`
    - `ICartRepository`
    - `ICustomerRepository`
    - `IOrderRepository`
    - `IDeliveryRepository`
    - `IDeliveryAgentRepository`
  - Create `IUnitOfWork`.
  - Consider an `IClock` or `IDateTimeProvider` contract for Application use cases.
  - Define application result types for expected outcomes such as unavailable checkout items.
  - Define contracts without EF Core, `IQueryable`, `DbContext`, HTTP, or API DTOs.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Domain/Interfaces/` or `src/Talabat/Talabat.Application/Abstractions/`
  - `src/Talabat/Talabat.Application/`
  - `docs/repository-interfaces-design.md`
- What Should Not Be Done
  - Do not implement repositories.
  - Do not install EF Core.
  - Do not create migrations.
  - Do not add Identity contracts that assume a framework.
  - Do not expose `IQueryable`.
- Acceptance Criteria
  - Every repository contract maps to an aggregate root and at least one planned use case.
  - No repository exists for child entities.
  - `IUnitOfWork` has no business logic.
  - Contracts compile without Infrastructure references.
  - Contracts do not leak API or persistence details.
- Risks / Mistakes To Avoid
  - Do not create a generic repository as the main abstraction.
  - Do not add methods that are not tied to a real use case.
  - Do not let Domain services call repositories.
  - Do not design auth-specific repository methods until Identity strategy is decided.

### Phase 3: Application Layer Use Cases

- Goal
  - Implement use-case orchestration without persistence details or HTTP concerns.
- Strategy
  - Application handlers load aggregate roots through interfaces, call Domain behavior, coordinate results, and commit through UnitOfWork.
  - Start with Customer Website backend use cases because they exercise Catalog, Basket, Customer, and Ordering.
  - Add Delivery use cases after core checkout and order flow are stable.
- Main decisions
  - Decide command/query style: simple services, handlers, or CQRS-lite.
  - Decide whether to introduce MediatR later. Do not install it unless approved.
  - Decide validation placement for request shape versus domain invariants.
- Actions
  - Implement Catalog read use cases:
    - Browse restaurants.
    - Get restaurant menu.
  - Implement Basket use cases:
    - Get cart.
    - Add item.
    - Update quantity.
    - Remove item.
    - Clear cart.
  - Implement Customer use cases:
    - Get profile.
    - Update profile.
    - Add address.
    - Remove address.
    - Set default address.
  - Implement Ordering use cases:
    - Checkout.
    - Get order history.
    - Get order details.
  - Later in this phase or the next delivery-focused phase, implement Delivery use cases:
    - Create delivery for order.
    - Assign delivery agent.
    - Delivery lifecycle transitions.
    - Get delivery status.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Application/`
  - Possible folders:
    - `Catalog/`
    - `Basket/`
    - `Customers/`
    - `Orders/`
    - `Delivery/`
    - `Abstractions/`
    - `Common/`
- What Should Not Be Done
  - Do not add EF Core implementation.
  - Do not put business logic in handlers when the Domain already owns it.
  - Do not reference ASP.NET Core types.
  - Do not add Identity framework types.
- Acceptance Criteria
  - Use cases depend only on Application/Domain abstractions.
  - Checkout loads Cart, Restaurant, Customer address data, validates through Domain, creates Order, marks Cart checked out, and commits once.
  - Delivery assignment and lifecycle use cases coordinate `Delivery` and `DeliveryAgent` through the domain service.
  - Application results are suitable for API mapping but do not contain HTTP response types.
- Risks / Mistakes To Avoid
  - Do not let controllers become the real Application layer.
  - Do not duplicate aggregate invariant logic in handlers.
  - Do not leak persistence concerns into use-case APIs.
  - Do not implement authenticated profile resolution until auth strategy is approved.

### Phase 4: Persistence And Infrastructure

- Goal
  - Persist the core business model behind the repository contracts.
- Strategy
  - Add EF Core only after Domain and Application contracts are stable.
  - Implement persistence in Infrastructure with explicit mappings.
  - Keep database constraints aligned with domain invariants.
- Main decisions
  - Choose ID generation approach for aggregate roots.
  - Choose owned type mappings for `Money`, `TimeRange`, `Address`, `DeliveryAddressSnapshot`, and `GeoLocation`.
  - Choose child key strategy for `CartItem`, `OrderItem`, and `CustomerAddress`.
  - Choose whether Catalog seed data is static seed data or managed through internal tooling later.
  - Choose transaction boundary for checkout and delivery coordination.
- Actions
  - Add EF Core packages only after approval.
  - Create application DbContext.
  - Create entity configurations for each aggregate.
  - Implement repository classes.
  - Implement UnitOfWork.
  - Add database constraints:
    - Quantity greater than zero.
    - Money amount greater than or equal to zero.
    - One active cart per customer if status-based carts are persisted.
    - One default address per customer where supported.
    - Product belongs to Restaurant.
    - Order belongs to Customer and Restaurant.
    - Unique Delivery per Order.
    - One active delivery per assigned agent where supported.
  - Add seed data for restaurants/products and optionally delivery agents for local testing.
  - Create migrations after mappings are reviewed.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Infrastructure/Persistence/`
  - `src/Talabat/Talabat.Infrastructure/Persistence/Configurations/`
  - `src/Talabat/Talabat.Infrastructure/Persistence/Repositories/`
  - `src/Talabat/Talabat.Infrastructure/DependencyInjection.cs`
  - `src/Talabat/Talabat.API/appsettings*.json`
- What Should Not Be Done
  - Do not implement Identity tables or IdentityServer stores in this phase.
  - Do not add auth-related migrations.
  - Do not expose DbContext to API controllers.
  - Do not create repositories for child entities.
- Acceptance Criteria
  - Infrastructure implements Application/Domain contracts.
  - Domain remains framework-independent.
  - Application does not reference Infrastructure.
  - Migrations reflect aggregate boundaries and value object mappings.
  - Checkout and delivery coordination can commit atomically.
- Risks / Mistakes To Avoid
  - Do not let EF mapping requirements weaken aggregate encapsulation.
  - Do not map temporary snapshots as independent tables.
  - Do not use `IQueryable` outside Infrastructure.
  - Do not start with migrations before reviewing model configuration.

### Phase 5: API Layer

- Goal
  - Expose backend use cases through HTTP without moving business logic into controllers.
- Strategy
  - API should be a thin delivery mechanism over Application.
  - Implement request/response mapping, exception mapping, endpoint grouping, OpenAPI, and dependency injection.
  - Keep authentication off or optional until Identity phase, but structure endpoints so policies can be added later.
- Main decisions
  - Controllers vs minimal APIs.
  - API versioning approach.
  - Error response format.
  - Endpoint grouping for Customer-facing and Delivery-facing APIs.
  - Whether to keep one API host initially or split delivery into a separate API host later.
- Actions
  - Remove or isolate template WeatherForecast endpoints.
  - Add health/version endpoint if useful.
  - Add DI registration for Application and Infrastructure.
  - Add API exception mapping for `DomainException`.
  - Add Customer-facing endpoints for Catalog, Basket, Customer, and Ordering.
  - Add Delivery endpoints only after Delivery Application use cases exist.
  - Add OpenAPI descriptions.
  - Add authorization placeholders as comments/docs only, not active policy implementation unless auth is approved.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.API/Program.cs`
  - `src/Talabat/Talabat.API/Controllers/` or endpoint folders
  - `src/Talabat/Talabat.API/Contracts/`
  - `src/Talabat/Talabat.API/Middleware/`
  - `src/Talabat/Talabat.API/DependencyInjection.cs` if needed
- What Should Not Be Done
  - Do not implement IdentityServer.
  - Do not add login/register endpoints.
  - Do not add frontend code.
  - Do not put domain decisions in controllers.
  - Do not authorize by trusting user-submitted customer or agent IDs.
- Acceptance Criteria
  - Controllers/endpoints contain request mapping and call Application use cases.
  - Domain exceptions map consistently to API errors.
  - No business rules are implemented in controllers.
  - API can support Customer Website use cases.
  - Delivery endpoints are either deferred or mapped to Delivery Application use cases.
- Risks / Mistakes To Avoid
  - Do not build endpoints before Application handlers exist.
  - Do not expose EF entities or Domain entities directly as API contracts.
  - Do not hardcode future identity assumptions into route design.

### Phase 6: Customer Website Backend Support

- Goal
  - Ensure the backend supports the future Customer Website workflows.
- Strategy
  - Complete customer-facing API capabilities before building the frontend.
  - Keep auth as a future integration concern, but avoid designs that assume anonymous permanent access.
- Main decisions
  - Define customer-facing API contracts and response shapes.
  - Define how temporary unauthenticated development flows map to future authenticated flows.
  - Decide whether Delivery status is visible in the customer order details at this phase or later.
- Actions
  - Validate end-to-end customer workflow:
    - Browse.
    - View menu.
    - Manage cart.
    - Manage addresses.
    - Checkout.
    - View order history.
  - Add customer-oriented read models.
  - Add pagination/filtering where needed.
  - Document future auth policy per endpoint.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Application/`
  - `src/Talabat/Talabat.API/`
  - `src/Talabat/Talabat.Infrastructure/` if read models or query persistence are needed.
  - API contract folders created in Phase 5.
- API/Use Case Areas Required
  - Catalog browse/menu.
  - Basket/cart.
  - Customer profile.
  - Address management.
  - Checkout.
  - Order history/details.
  - Later delivery status.
- Acceptance Criteria
  - Customer Website can be built against stable backend contracts.
  - APIs do not expose internal aggregate mutation details.
  - Future authenticated customer scoping is documented for each endpoint.
- What Should Not Be Done
  - Do not build the Customer Website frontend in this phase.
  - Do not implement login/register.
  - Do not select an identity framework.
  - Do not rely on hardcoded customer IDs as final authorization design.
- Risks / Mistakes To Avoid
  - Do not build the frontend before backend contracts are stable.
  - Do not bake the old single-customer assumption into public API contracts.
  - Do not require Identity framework-specific claims in Application results.

### Phase 7: Delivery Website Backend Support

- Goal
  - Ensure the backend supports the future Delivery Website workflows.
- Strategy
  - Build delivery backend capabilities after core checkout/order persistence exists.
  - Keep Delivery separate from Ordering while allowing Application workflows to create and update delivery tasks.
- Main decisions
  - Decide assignment model:
    - Manual operations assignment.
    - Agent self-acceptance.
    - Automatic nearest-agent assignment later.
  - Decide what delivery agents can see before assignment.
  - Decide which delivery actions belong to agents versus operations/admin users.
- Actions
  - Implement delivery task creation after successful checkout, if approved for this phase.
  - Implement delivery-agent availability flows.
  - Implement delivery lifecycle flows.
  - Add delivery read models for agent dashboard and delivery status.
  - Add constraints preventing one agent from holding multiple active deliveries.
  - Document future authorization policy per endpoint.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Application/`
  - `src/Talabat/Talabat.Domain/` only if approved domain gaps are discovered.
  - `src/Talabat/Talabat.Infrastructure/`
  - `src/Talabat/Talabat.API/` or a future delivery API host if that split is approved.
  - `docs/delivery/`
- API/Use Case Areas Required
  - Delivery agent profile.
  - Agent status online/offline.
  - Agent location update.
  - Pending/assigned delivery tasks.
  - Assignment.
  - Arrived at restaurant.
  - Picked up.
  - Out for delivery.
  - Delivered.
  - Cancelled/failed with coordination.
- Acceptance Criteria
  - Delivery Website can be built against stable backend contracts.
  - Delivery actions are backed by Application use cases and Domain invariants.
  - Delivery does not mutate Order directly.
  - Future DeliveryAgent authorization scoping is documented.
- What Should Not Be Done
  - Do not build the Delivery Website frontend in this phase.
  - Do not add real-time GPS/maps.
  - Do not implement Identity/Auth.
  - Do not merge Delivery lifecycle into Ordering.
- Risks / Mistakes To Avoid
  - Do not put delivery lifecycle rules in controllers.
  - Do not model `Delivery` as a child of `Order`.
  - Do not let a delivery agent act on another agent's assigned delivery once auth is introduced.
  - Do not implement real-time GPS/maps before basic lifecycle works.

### Phase 8: IdentityServer Website / Auth Portal

Reserved / TBD.

Do not choose the framework yet.
Do not add implementation tasks yet.
Do not install packages yet.
Do not write identity code yet.

- Why this phase exists
  - The final system needs authentication, authorization, token issuing, roles/policies, user management, and login/register flows.
  - The three-website model requires a dedicated identity/auth experience or host.
  - Customer Website and Delivery Website must eventually authenticate users and call protected APIs.
- Decisions that must be made later
  - Identity framework:
    - IdentityServer4.
    - Duende IdentityServer.
    - OpenIddict.
    - ASP.NET Core Identity only.
    - Other.
  - Whether Identity/Auth is a separate project/host.
  - Token format and claims.
  - Roles and policies.
  - Account registration flows.
  - Whether registration creates Customer and DeliveryAgent profiles.
  - How account IDs map to domain profiles.
  - Database/schema layout.
  - Migration strategy.
  - Signing credentials and production secrets.
  - Password policy, lockout, email/phone confirmation, password reset, refresh tokens, external login.
- What the rest of the system must avoid now
  - Do not add Identity framework types to Domain.
  - Do not store `ClaimsPrincipal` or JWT claims in aggregates.
  - Do not use auth roles to enforce domain invariants.
  - Do not assume a specific token claim shape.
  - Do not design Application use cases that require IdentityServer-specific services.
  - Do not treat `docs/identityserver4-readiness-report.md` as a framework decision.

### Phase 9: Authorization Strategy

- Goal
  - Define role/policy boundaries after use cases are clear and before enabling authentication on production endpoints.
- Strategy
  - Authorization belongs in API/Application boundary decisions, not Domain entities.
  - Model domain ownership separately from auth roles.
  - Keep policy names framework-neutral until the identity framework is selected.
- Main decisions
  - Which operations are customer-only.
  - Which operations are delivery-agent-only.
  - Which operations require admin/delivery-operations permissions.
  - Whether restaurant-owner workflows are in scope and what domain model they require.
- Actions
  - Create a policy matrix for endpoints/use cases.
  - Define future role candidates.
  - Define ownership checks:
    - Customer can access only own cart/orders/profile.
    - DeliveryAgent can access only assigned delivery tasks where applicable.
    - Operations/Admin can assign or inspect broader delivery data if later approved.
  - Add current-user abstraction only when implementation begins.
- Files/Folders To Create Or Modify
  - `docs/authorization-strategy.md` if a separate authorization document is approved.
  - `src/Talabat/Talabat.Application/Abstractions/` later for current-user contracts.
  - `src/Talabat/Talabat.API/` later for policies after framework selection.
- Future Role/Policy Candidates
  - `Customer`
  - `DeliveryAgent`
  - `DeliveryOperations`
  - `Admin`
  - `RestaurantOwner`
- Acceptance Criteria
  - Every planned endpoint has a documented future auth policy.
  - Domain model remains independent from roles and claims.
  - Application use cases know when ownership checks are needed without depending on a concrete identity framework.
- What Should Not Be Done
  - Do not implement JWT validation.
  - Do not add authorization middleware/policies until an identity framework is chosen.
  - Do not add role checks inside aggregates.
  - Do not add `IdentityUserId` blindly before the profile-linking decision.
- Risks / Mistakes To Avoid
  - Do not confuse authentication with domain profile existence.
  - Do not trust route IDs for ownership.
  - Do not add restaurant-owner role without deciding the restaurant ownership domain model.
  - Do not implement policies before the identity framework decision.

### Phase 10: Testing And Quality Gates

- Goal
  - Make each phase reviewable and prevent architecture regressions.
- Strategy
  - Start with Domain unit tests, then Application use-case tests, then Infrastructure integration tests, then API contract tests.
  - Add tests as phases introduce code.
- Main decisions
  - Choose test framework.
  - Choose integration database strategy.
  - Choose CI workflow.
  - Decide coverage expectations for critical workflows.
- Actions
  - Add Domain tests for value objects, aggregates, domain services, and exceptions.
  - Add Application tests with fake repositories for use-case orchestration.
  - Add Infrastructure tests for EF mappings and constraints.
  - Add API tests for endpoint contracts and error mapping.
  - Add architecture tests to protect dependency direction if desired.
  - Add CI build/test workflow.
- Files/Folders To Create Or Modify
  - `tests/Talabat.Domain.Tests/`
  - `tests/Talabat.Application.Tests/`
  - `tests/Talabat.Infrastructure.Tests/`
  - `tests/Talabat.API.Tests/`
  - `.github/workflows/`
- Test Types Required
  - Domain unit tests.
  - Application unit tests.
  - Infrastructure integration tests.
  - API integration/contract tests.
  - Architecture/dependency tests.
- Acceptance Criteria
  - Domain invariants have focused tests.
  - Checkout and delivery lifecycle have regression tests.
  - Full solution build is green.
  - Tests run from a documented command.
  - CI runs build and tests.
- What Should Not Be Done
  - Do not write broad end-to-end tests before lower-level behavior is covered.
  - Do not use test setup to bypass aggregate invariants.
  - Do not require Identity/Auth test infrastructure before the Identity phase exists.
- Risks / Mistakes To Avoid
  - Do not rely only on API tests for domain rules.
  - Do not test EF behavior with mocks.
  - Do not add brittle tests against private implementation details.
  - Do not delay all testing until the end.

### Phase 11: Delivery Extension And Advanced Features

- Goal
  - Add advanced marketplace features only after core ordering, delivery, API, persistence, and auth foundations are stable.
- Strategy
  - Treat these as separate feature increments, not part of core foundation work.
  - Each feature should get its own Domain/Application/Infrastructure/API plan.
- Main decisions
  - Payment provider and payment flow.
  - Notifications channel strategy.
  - Coupon/discount domain model.
  - Reviews and rating moderation rules.
  - Restaurant-owner/admin portal scope.
  - Real-time delivery tracking approach.
- Actions
  - Payment.
  - Notifications.
  - Coupons/discounts.
  - Reviews/ratings.
  - Restaurant-owner management.
  - Admin operations.
  - Real-time delivery tracking.
  - Nearest-agent selection/route optimization.
- Files/Folders To Create Or Modify
  - New or existing Domain/Application/Infrastructure/API folders depending on each approved feature.
  - Dedicated design docs under `docs/` for each advanced feature before implementation.
- What Should Not Be Done
  - Do not bundle all advanced features into one large implementation.
  - Do not add payment, notifications, coupons, reviews, admin, and restaurant-owner code in the same phase.
  - Do not introduce new bounded contexts without documenting ownership and integration points.
- Acceptance Criteria
  - Each advanced feature has a clear bounded context or module decision.
  - No feature weakens the core aggregate boundaries.
  - No feature introduces Identity coupling into Domain.
- Risks / Mistakes To Avoid
  - Do not add payment before checkout/order persistence is reliable.
  - Do not add notifications before domain events or Application event strategy is clear.
  - Do not add coupons by scattering discount logic across controllers.
  - Do not add restaurant-owner workflows before restaurant ownership is modeled.

## 6. Documentation Update Plan

| File | Current Issue | Required Change | Priority |
|---|---|---|---|
| `PROJECT_IMPLEMENTATION_ROADMAP.md` | New roadmap file. | Use as the current high-level implementation sequence after approval. | High |
| `Talabat_DDD_Project_Architecture_Prompt.md` | Mentions ASP.NET Core Identity and old MVP exclusions. | Update to say Identity/Auth is reserved/TBD and remove the framework decision. | High |
| `Talabat_Implementation_Roadmap.md` | Old roadmap recommends implementing Identity in Infrastructure and says auth is out of scope for MVP v1. | Mark as superseded or revise to align with this phase order and reserved Identity phase. | High |
| `PLAN.md` | Old plan includes Identity in stack/context and suggests auth service work before the new strategy is ready. | Mark as historical or update with new sequencing. | High |
| `docs/bounded-contexts.md` | Says MVP v1 excludes auth, delivery drivers, roles, and assumes one normal customer. | Update to distinguish initial unauthenticated development from final Customer/Delivery/Auth direction. Add Identity/Auth as future boundary. | High |
| `docs/business-rules.md` | Title and intro are MVP v1-only and assume no auth/single customer. | Update rule framing to include future authenticated ownership while keeping current domain rules. | High |
| `docs/business-rule-classification.md` | Classifies auth, delivery, roles, and restaurant owners as out of scope. | Reclassify as deferred/reserved; add future Application/API authorization ownership notes. | High |
| `docs/aggregates-and-invariants.md` | Contains old MVP v1 no-Identity boundary and repository list omits Delivery roots at the end. | Update with future identity boundary, Delivery roots, and three-website implications. | High |
| `docs/entity-design.md` | Customer notes say no IdentityUserId in MVP v1 and final direction is not reflected. | Update to say Customer remains a domain profile independent from identity framework; future linkage TBD. | High |
| `docs/value-object-design.md` | MVP v1 scope excludes auth and delivery drivers, while Delivery value objects now exist. | Update scope language and include Delivery value object status consistently. | Medium |
| `docs/domain-services-design.md` | MVP v1-only language and only Checkout service as MVP v1 service. | Update to include DeliveryAssignmentDomainService as implemented and Identity/Auth as external boundary. | High |
| `docs/domain-failures-design.md` | Auth failures are described as out of scope for MVP v1 only. | Clarify Domain still should not model auth failures; API/Auth boundary handles them later. | Medium |
| `docs/domain-invariants.md` | No mention of Identity boundary, Delivery role scoping, or three websites. | Add concise notes that auth ownership checks are Application/API concerns, not Domain invariants. | Medium |
| `docs/repository-interfaces-design.md` | Mostly updated for Delivery, but old MVP v1 scope and no-auth assumptions remain. | Update repository methods for future multi-customer/multi-agent support; remove permanent `GetMvpCustomer` assumption. | High |
| `docs/delivery/README.md` | Says no Delivery implementation exists during documentation step, but implementation now exists. | Update to reflect current Domain implementation and deferred outer layers. | High |
| `docs/delivery/delivery-implementation-roadmap.md` | Largely updated, but still excludes auth/courier login without referencing reserved Auth Portal. | Keep repository/Application/Infrastructure/API as deferred and add future Delivery Website/Auth Portal relationship. | Medium |
| `docs/delivery/delivery-bounded-context.md` | Delivery context likely lacks three-website and future auth boundary. | Add Delivery Website/API ownership and note auth remains external. | Medium |
| `docs/delivery/delivery-business-rules.md` | Delivery rules do not distinguish domain lifecycle from future authorization/agent ownership checks. | Add notes for Application/API ownership checks after auth is introduced. | Medium |
| `docs/delivery/delivery-aggregates-and-invariants.md` | May not reflect current code and future auth scoping. | Verify against code and add note that DeliveryAgent identity linkage is future/TBD. | Medium |
| `docs/delivery/delivery-entity-design.md` | DeliveryAgent is a courier profile but not linked to future auth direction. | Add framework-neutral future identity linkage note. | Medium |
| `docs/delivery/delivery-domain-services-design.md` | Should be checked against current implemented cancellation/failure coordination. | Ensure service behavior and docs match current code. | Medium |
| `docs/delivery/delivery-database-design.md` | Database design predates final persistence and auth decisions. | Revisit after Phase 4 ID/key decisions and future DeliveryAgent linkage decisions. | Medium |
| `docs/reviews/domain-review-report.md` | Historical review says it predates remediation and should not be treated as current status. | Either refresh the review after Phase 1 or keep it explicitly historical. | High |
| `docs/identityserver4-readiness-report.md` | Focuses on IdentityServer4 and contains implementation phase recommendations that conflict with the new TBD strategy. | Reframe as historical compatibility research only; do not use it as the identity roadmap. | High |
| `docs/glossary.md` | Deleted in the current working tree. | Decide whether to restore, remove from references, or recreate later as part of documentation cleanup. | Low |

## 7. First 10 Concrete Tasks

1. Approve this roadmap as the current sequencing document.
2. Update `Talabat_DDD_Project_Architecture_Prompt.md` to remove the fixed ASP.NET Core Identity decision and state Identity/Auth is reserved/TBD.
3. Mark `PLAN.md` and `Talabat_Implementation_Roadmap.md` as historical or revise them to point to this roadmap.
4. Refresh `docs/reviews/domain-review-report.md` against current code so already-fixed findings are not treated as blockers.
5. Update `docs/bounded-contexts.md` for the new direction: Customer Website, Delivery Website, and reserved Identity/Auth boundary.
6. Update `docs/business-rules.md` and `docs/business-rule-classification.md` so auth/roles/delivery are deferred/reserved instead of permanently out of scope.
7. Update `docs/repository-interfaces-design.md` to remove the permanent `GetMvpCustomer` assumption and support future multi-customer/multi-agent flows.
8. Confirm Phase 1 Domain decisions: cart persistence semantics, child identity strategy, UTC/timezone boundary, audit-user boundary, and Delivery terminal coordination.
9. Implement Phase 1 Domain cleanup only after the documentation baseline is approved.
10. After Phase 1 passes review, implement Phase 2 repository/Application contracts without EF Core, API endpoints, or Identity packages.

## 8. Final Notes

The recommended strategy is Domain-first, then contracts, then Application, then Infrastructure, then API, then website-specific backend support, and only then Identity/Auth implementation.

IdentityServer/Auth Portal should not be started now. The project is not ready for that decision because the Application layer, repositories, persistence model, transaction boundaries, API contracts, and authorization matrix are not yet stable.

The Domain should remain independent from IdentityServer, ASP.NET Core Identity, EF Core, controllers, HTTP, JWT, and claims. Future authentication should resolve users to domain profiles at the API/Application boundary and pass domain identifiers into use cases.

Do not implement phases, install packages, create migrations, refactor source code, or build websites until the relevant phase is approved.
