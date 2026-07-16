# Project Implementation Roadmap

## 0. Status Snapshot (2026-07-15)

| Phase | Status | Record |
|---|---|---|
| Phase 1: Repository And Documentation Audit | Completed | Historical record: `docs/phase-0-repository-and-documentation-audit.md` |
| Phase 2: Domain Cleanup And Invariant Stabilization | Completed | Historical record: `docs/phase-1-domain-cleanup-and-invariant-stabilization.md` |
| Phase 3: Domain And Application Contracts | Completed | Historical record: `docs/phase-2-domain-and-application-contracts.md` |
| Phase 4: Application Layer Use Cases | Completed | `docs/phase-3-application-use-cases.md` + `specs/001-application-use-cases/`; includes the completed ID-strategy supporting milestone |
| Phase 5: Infrastructure And Persistence Foundation | Completed | `docs/phase-4-persistence-and-infrastructure.md` + `specs/002-persistence-infrastructure/` |
| Phase 6: Minimal Identity/Auth Setup Before Business APIs | Completed | `docs/phase-6-identity.md` |
| Phase 7: `Talabat.Customer.API` | Not started | — |
| Phase 8: `Talabat.DeliveryAgent.API` | Not started | — |
| Phase 9: Token, Claims, And Scopes Refinement | Not started | — |
| Phase 10: Authorization Strategy And Quality Gates | Not started | — |
| Phase 11: Advanced Features | Not started | — |

- The phase numbers were normalized on 2026-07-14. Existing completion documents retain their historical filenames so links and audit history remain stable.
- Historical Phases 0–2 were implemented in `91dc805`; historical Phase 3 in `2e7f148` (both 2026-07-11).
- The historical Phase 3.5 supporting milestone completed on 2026-07-11 after the ID-strategy impact review: the project now uses SQL Server IDENTITY-compatible keys, the application-side ID generator is removed, and the earlier sequence-based persistence recommendation is superseded.
- The historical Phase 4, now normalized as Phase 5, completed on 2026-07-11 with SQL Server-backed Infrastructure persistence, one reviewed migration, deterministic catalog seed data, and SQL Server integration tests.
- Application use cases have a full spec-kit under `specs/001-application-use-cases/` (spec, plan, research, data model, contracts, tasks). Where that historical spec-kit is more specific than the Application-use-case section in this file, the spec-kit wins. Delivery Application use cases remain deferred to the new Phase 8, and the handler style remains CQRS-lite without MediatR.
- Section 1 below describes the repository as of commit `91dc805`. Statements that predated Phases 0–2 have been corrected in place.

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
- Repository and Unit of Work contracts now exist in code (added in Phase 2) under `src/Talabat/Talabat.Domain/Interfaces/`:
  - `IRestaurantRepository`
  - `ICartRepository`
  - `ICustomerRepository`
  - `IOrderRepository`
  - `IDeliveryRepository`
  - `IDeliveryAgentRepository`
  - `IUnitOfWork`
  - All contracts target aggregate roots only and expose no EF Core, `IQueryable`, HTTP, or identity-framework types.
- `Talabat.Application` now contains its first contracts (added in Phase 2):
  - `Abstractions/IClock.cs` (UTC time source for use cases).
  - `Ordering/Checkout/CheckoutOutcome.cs` (transport-neutral checkout result hierarchy).
  - No use-case handlers exist yet; orchestration is Phase 3 work.
- `Talabat.Infrastructure` exists as a project but has no production source files beyond the project file.
- Phase 1 renamed `DeliveryAlreadyCompletedException` to `DeliveryTerminalStateException` so the invariant clearly covers all terminal delivery states (`Delivered`, `Cancelled`, `Failed`).
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
- The existing documentation describes repository interfaces; these are now implemented in code (see 1.1), so `docs/repository-interfaces-design.md` is a design record rather than pending work.
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
- `Talabat.API` does not reference `Talabat.Infrastructure`. The composition root cannot load future persistence implementations until that project reference and an `AddInfrastructure()` registration are added in Phase 4/5.
- Authorization is only present as a middleware call in API. Authentication and policies are not configured.
- Audit fields exist in Domain, but there is no current-user abstraction or infrastructure mechanism that supplies audit identity values.
- The historical domain review file contains findings that appear to have been partly remediated in code. It should be refreshed before being used as current truth.

### 1.4 Missing

- No test projects exist yet for Domain, Application, Infrastructure, or API. The first test project (`tests/Talabat.Application.Tests`, xUnit) is a required deliverable of Phase 3, not optional.
- No Application layer use-case handlers exist. Commands, queries, read models, and result contracts beyond `CheckoutOutcome` arrive in Phase 3 (specced in `specs/001-application-use-cases/`).
- No current-user abstraction exists in code. This is deliberate: it is deferred until the Identity/Auth boundary is designed (see Phases 8–9); Phase 3 uses explicit `customerId` request data instead.
- No ID-generation or restaurant-local-time abstractions exist yet; both are specced for Phase 3 (application-side ID generator, `IRestaurantLocalTimeProvider`). The database-level ID strategy remains a Phase 4 decision.
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
- Documentation must reflect the approved minimal-Identity-before-APIs sequence while keeping advanced auth, profile linkage, and final token design deferred.
- `Talabat_DDD_Project_Architecture_Prompt.md` should be aligned with the approved Duende IdentityServer + ASP.NET Core Identity combination and the separate `Talabat.Identity` host.
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
- Identity/Auth is now an approved pre-API capability: Duende IdentityServer integrated with ASP.NET Core Identity in a separate `Talabat.Identity` host.
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
- `Talabat.Infrastructure` owns the single EF Core persistence model, including business mappings and the ASP.NET Core Identity EF store inside `TalabatDbContext`. Duende protocol/UI interaction remains owned by the separate `Talabat.Identity` host.
- API should handle HTTP request/response mapping, authentication middleware, authorization policies, and endpoint wiring.
- Cart should not store product prices.
- Orders should store immutable price and delivery address snapshots.
- Checkout should use current Catalog prices.
- Delivery should remain separate from Ordering:
  - `Order` is a historical purchase record.
  - `Delivery` is an operational task.
  - `DeliveryAgent` is a courier/agent profile and availability aggregate.

### 2.3 What Must Be Deferred

- Do not build business API endpoints before the approved minimal Identity/Auth setup is complete.
- Do not add Identity-specific types to Domain entities at any phase.
- Do not make `Customer` or `DeliveryAgent` inherit from `IdentityUser` or `ApplicationUser`.
- Do not decide the account-to-profile linkage until the Customer and DeliveryAgent API requirements make the mapping concrete.
- Do not finalize token audiences, scopes, custom claims, or endpoint authorization policies during the minimal Identity phase.
- Do not build the Customer or Delivery websites now.
- Do not implement refresh-token tuning, external login, password reset, email confirmation, 2FA, admin UI, advanced consent/custom grants, or production signing/secrets hardening during the minimal Identity phase.
- Do not implement payment, notifications, coupons, reviews, admin management, or restaurant-owner workflows now.

### 2.4 Approved Identity/Auth Direction

Identity/Auth is an approved, separate host that now begins before the business APIs.

The approved direction is:

- Keep the Domain independent from Identity.
- Create `Talabat.Identity` as a separate ASP.NET Core Web API host.
- Use Duende IdentityServer as the OIDC/OAuth2 server and ASP.NET Core Identity as the account/user store beneath it.
- Start with simple JSON register/login/logout account endpoints for manual testing; login establishes the Identity authentication cookie and never acts as a custom token issuer.
- Add the Angular interaction UI later; it will use Authorization Code with PKCE against the Duende protocol endpoints.
- Keep all framework decisions and types outside the Domain model.
- Use scalar domain identifiers such as `CustomerId` and `DeliveryAgentId` inside core aggregates.
- Defer the account-to-profile linkage strategy until the business APIs clarify the required customer and delivery-agent flows.
- Refine tokens, audiences, scopes, and custom claims after the API surfaces become clear.
- Keep any future profile linkage scalar and framework-neutral from the Domain perspective.
- Never put `ApplicationUser`, `ClaimsPrincipal`, `IdentityUser`, `HttpContext`, JWT claims, Duende types, or ASP.NET Core Identity types in Domain.

Framework decision (approved 2026-07-14): **Duende IdentityServer + ASP.NET Core Identity**, hosted in `Talabat.Identity` during Phase 6. Duende owns standards-based protocol and token-server behavior. ASP.NET Core Identity owns accounts, password hashes, login state, roles, lockout, and related user-store behavior. They are complementary components, not alternatives.

Boundary rules:

- `Customer` and `DeliveryAgent` remain pure domain profiles and never inherit from Identity classes.
- An authentication account is not automatically a Customer or DeliveryAgent profile.
- Minimal registration creates an Identity account only unless a later approved profile-provisioning design says otherwise.
- No `IdentityUserId` column, repository lookup, cross-database transaction, or profile-provisioning workflow is introduced in Phase 6.
- API token validation, policies, and ownership checks belong to the later API/refinement phases, not the Domain model.

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
  One EF Core TalabatDbContext for business aggregates plus ASP.NET Core Identity tables, repository
  implementations, UnitOfWork, external services, and seed data. ApplicationUser is an Infrastructure
  persistence model, never a Domain entity.
  Depends inward on Application/Domain contracts.

Talabat.Customer.API   (currently named Talabat.API; renamed at Phase 7 start)
  Customer-facing HTTP host: restaurants, menu, cart, profile, addresses, checkout, orders.
  Request/response mapping, authentication middleware, authorization policies, exception mapping,
  OpenAPI, DI composition. Depends on Application and Infrastructure for DI registration.

Talabat.DeliveryAgent.API   (new host, introduced in Phase 8)
  Delivery-agent-facing HTTP host: agent profile, online/offline, location, assigned deliveries,
  delivery lifecycle actions. Reuses Application/Infrastructure via shared DI extensions and
  shared API plumbing (DomainException->ProblemDetails mapping).

Talabat.Identity   (new ASP.NET Core Web API host, introduced in Phase 6 before the business APIs)
  Duende IdentityServer (OIDC/OAuth2 protocol endpoints) + ASP.NET Core Identity (user/account store).
  References Talabat.Infrastructure to reuse TalabatDbContext and its Identity EF store; Infrastructure
  never references this host.
  Phase 6 starts with JSON register/login/logout endpoints for manual cookie-session testing and the
  minimum IdentityServer configuration. A future Angular SPA supplies the interactive UI and uses
  Authorization Code with PKCE. Advanced account management and production hardening remain deferred.
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
  - Separate cross-cutting host introduced minimally before the business APIs.
  - Uses Duende IdentityServer with ASP.NET Core Identity and initially owns accounts, credentials, register/login/logout, authentication cookies, and basic protocol behavior.
  - Token resources/scopes/custom claims, profile linkage, and advanced account management are refined later.
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
- Use-case request shapes stay unchanged; only the source of `customerId`/`agentId` moves from request/route data to token-resolved values.

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

#### Identity Web API And Future Angular Auth UI

Purpose:

- `Talabat.Identity` is the centralized ASP.NET Core Web API authority for account interaction and Duende protocol/token endpoints.
- A future Angular SPA supplies the login/register/logout user interface and uses Authorization Code with PKCE.

Backend contexts used:

- Identity/Auth context.
- Account-to-Customer/DeliveryAgent profile coordination remains deferred until the business API requirements are defined.

Phase 6 account endpoints:

- JSON register/login/logout endpoints for manual Identity-cookie testing.
- Duende discovery and protocol endpoints.
- No custom password-to-token endpoint.

Deferred identity/auth concerns:

- Framework is decided: Duende IdentityServer integrated with ASP.NET Core Identity.
- Angular UI, final client registrations, token shape, roles, scopes, claims, refresh-token behavior, profile linkage, account lifecycle, and advanced user management remain deferred.

What must be complete before Phase 6 implementation:

- Stable core Domain, Application, and persistence foundations (completed in Phases 1–5).
- A Phase 6 specification and constitution scope that permit only the minimal Web API Identity host.
- Clear boundaries that keep Domain and Application independent from Identity frameworks.

## 4. Recommended Implementation Order

Approved order:

1. Repository and documentation audit.
2. Domain cleanup and invariant stabilization.
3. Domain and Application contracts.
4. Application layer use cases, including the completed database-generated ID strategy milestone.
5. Infrastructure and persistence foundation.
6. Minimal Identity/Auth setup before business APIs.
7. `Talabat.Customer.API`.
8. `Talabat.DeliveryAgent.API`.
9. Token, claims, and scopes refinement after both API surfaces are clearer.
10. Authorization strategy and quality gates.
11. Advanced features.

Identity/Auth is no longer fully deferred until after the business APIs. The approved direction creates a simple, separate ASP.NET Core Web API host named `Talabat.Identity` first, using Duende IdentityServer integrated with ASP.NET Core Identity. Phase 6 exposes minimal JSON register/login/logout account endpoints for manual testing. Login creates the Identity authentication session cookie; it does not mint or return a custom JWT.

This is intentionally not the final security design. The initial Identity host establishes account storage, cookie-session behavior, Duende protocol endpoints, and clean separation from Domain. The future Angular SPA will provide the interactive user experience and use Authorization Code with PKCE. Precise access-token audiences, scopes, custom claims, profile linkage, and per-endpoint authorization policies will be refined after `Talabat.Customer.API` and `Talabat.DeliveryAgent.API` make those requirements concrete.

The three planned hosts are:

- `Talabat.Identity` — ASP.NET Core Web API host for accounts, credentials, JSON register/login/logout interaction endpoints, and Duende protocol/token-server responsibilities.
- `Talabat.Customer.API` — customer-facing business HTTP endpoints added in Phase 7.
- `Talabat.DeliveryAgent.API` — delivery-agent-facing business HTTP endpoints added in Phase 8.

Advanced Identity/Auth remains deferred: refresh-token tuning, external login, password reset, email confirmation, 2FA, admin UI, advanced consent or custom grants, and production signing/secrets hardening.

## 5. Phased Roadmap

### Phase 1: Repository And Documentation Audit

> **Status: Completed (2026-07-11).** Implemented as scope banners/notices across the root planning docs and the `docs/` design set, plus the audit record `docs/phase-0-repository-and-documentation-audit.md`. Note: the `docs/glossary.md` deletion and the `.codex-scratch/` IdentityServer4 spike were committed as-is; their follow-ups are tracked in Section 6.

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

### Phase 2: Domain Cleanup And Invariant Stabilization

> **Status: Completed (2026-07-11).** See `docs/phase-1-domain-cleanup-and-invariant-stabilization.md`. Outcome: the Domain review found the aggregates already sound; the phase recorded the binding decisions (cart created with first item, child-identity strategy, UTC policy, checkout/delivery coordination ownership, framework-neutral audit) and made two small changes (renamed `DeliveryAlreadyCompletedException` to `DeliveryTerminalStateException`; removed the stale empty `Interfaces\` folder include). Domain tests were not created in this phase; the first tests land in Phase 3, and a dedicated Domain test project is backfilled in Phase 10.

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

### Phase 3: Domain And Application Contracts

> **Status: Completed (2026-07-11).** See `docs/phase-2-domain-and-application-contracts.md`. Outcome: six aggregate-root repository contracts plus `IUnitOfWork` in `Talabat.Domain/Interfaces/`; `IClock` and the `CheckoutOutcome` result hierarchy in `Talabat.Application`. No current-user abstraction was added (deliberate deferral). Contracts verified clean: no EF Core, `IQueryable`, HTTP, or identity-framework leakage; no child-entity repositories; no `GetMvpCustomer`.

- Goal
  - Define the contracts Application needs to orchestrate use cases without binding to Infrastructure.
- Strategy
  - Add repository interfaces for aggregate roots only.
  - Add application-level abstractions only when they are needed by use cases.
  - Keep contracts framework-neutral.
- Main decisions
  - Decided (as implemented): repository and `IUnitOfWork` contracts live in `Talabat.Domain/Interfaces/`, matching `docs/repository-interfaces-design.md`. Application-level abstractions (`IClock`, and in Phase 3 application-side ID generator / `IRestaurantLocalTimeProvider`) live in `Talabat.Application/Abstractions/`.
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

### Phase 4: Application Layer Use Cases

> **Status: Completed (2026-07-11, commit `2e7f148`).** See `docs/phase-3-application-use-cases.md`. Implemented per the spec-kit at `specs/001-application-use-cases/` (where this section and the spec-kit differ, the spec-kit wins). Note: the temporary application-side ID generator introduced here was removed in Phase 3.5.

- Goal
  - Implement use-case orchestration without persistence details or HTTP concerns.
- Strategy
  - Application handlers load aggregate roots through interfaces, call Domain behavior, coordinate results, and commit through UnitOfWork.
  - Start with Customer Website backend use cases because they exercise Catalog, Basket, Customer, and Ordering.
  - Add Delivery use cases after core checkout and order flow are stable.
- Main decisions (resolved in `specs/001-application-use-cases/research.md`)
  - Decided: CQRS-lite — one explicit handler per use case with request/response models. No MediatR and no new production packages in this phase.
  - Decided: use cases receive explicit `customerId` request data; no current-user abstraction yet.
  - Decided: expected business outcomes are returned as transport-neutral `UseCaseResult`/error contracts; domain invariants stay in Domain, request-shape validation stays in handlers/request models.
  - Decided: handlers return Application read models, never Domain aggregates.
  - Decided: two new Application abstractions bridge deferred infrastructure — an application-side ID generator (Domain factories then required positive int IDs while persistence was deferred; superseded by Phase 3.5) and `IRestaurantLocalTimeProvider` (checkout needs restaurant-local time).
  - Decided: xUnit is the test framework; `tests/Talabat.Application.Tests` is created in this phase with fake repositories, clock, ID generator, and local-time provider.
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
  - Delivery use cases are explicitly deferred to Phase 7 (spec clarification, 2026-07-11). Do not implement delivery task creation, assignment, lifecycle transitions, agent workflows, or delivery status use cases in Phase 3.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Application/` with folders per the spec-kit:
    - `Common/Results/`
    - `Abstractions/`
    - `Catalog/`
    - `Basket/`
    - `Customers/`
    - `Ordering/`
  - `tests/Talabat.Application.Tests/` (new xUnit project, added to `src/Talabat/Talabat.slnx`)
- What Should Not Be Done
  - Do not add EF Core implementation.
  - Do not put business logic in handlers when the Domain already owns it.
  - Do not reference ASP.NET Core types.
  - Do not add Identity framework types.
- Acceptance Criteria
  - Use cases depend only on Application/Domain abstractions.
  - Checkout loads Cart, Restaurant, Customer address data, validates through Domain, creates Order, marks Cart checked out, and commits once.
  - Application results are suitable for API mapping but do not contain HTTP response types.
  - `tests/Talabat.Application.Tests` exists, runs green, and covers checkout success/unavailable/failure paths, cart conflict outcomes, ownership-scoped order reads, and profile/address rules (spec tasks T020–T083).
  - The guardrail check passes: no MediatR, EF Core, Identity/Auth, or ASP.NET Core packages were added to `Talabat.Application` (spec task T084).
  - `docs/phase-3-application-use-cases.md` records the phase outcome, and the Status Snapshot in this file is updated (spec task T088).
- Risks / Mistakes To Avoid
  - Do not let controllers become the real Application layer.
  - Do not duplicate aggregate invariant logic in handlers.
  - Do not leak persistence concerns into use-case APIs.
  - Do not implement authenticated profile resolution until auth strategy is approved.

#### Phase 4 Supporting Milestone: ID Strategy Refactor Before Persistence

> **Status: Completed (2026-07-11).** See `docs/phase-3.5-id-strategy-refactor.md`. Added after the ID-strategy impact review. Supersedes the earlier sequence-based recommendation that Phase 4 previously carried.

- Goal
  - Make Domain, Application, and test code compatible with SQL Server database-generated IDENTITY keys before any EF Core mapping or migration exists, and remove the temporary application-side ID generator.
- Why It Must Happen Before Phase 4
  - The first EF Core configuration written in Phase 4 must already know who generates keys. Refactoring now is compile-driven with no schema or data to migrate; after migrations exist, the same change becomes a schema-and-data change.
  - The sequence alternative is not churn-free anyway: application-side ID generator is synchronous, so a real database-backed implementation would force blocking calls or an async signature change that touches the same handlers and tests.
- Scope (Likely Affected Areas)
  - Domain members that accept self IDs: `Cart.Create` (+ private ctor), `Order.CreateFromCheckout` (+ private ctor), `Customer` ctor, `Customer.AddAddress`, `CustomerAddress` ctor, `Restaurant` ctor, `Restaurant.AddProduct`, `Product` ctor, `Delivery` ctor, `DeliveryAgent` ctor.
  - Application: delete the application-side ID generator abstraction; update its three consumers — `AddCartItemHandler`, `AddCustomerAddressHandler`, `CheckoutHandler`.
  - Tests: the fake ID generator (delete), `TestData`, the six test files that wire the fake, fake repositories/unit of work, and ID-literal assertions.
  - Documentation: this file (Phase 4 notes updated below), a short decision record, and a superseded-decision note in `specs/001-application-use-cases/research.md`.
- What Should Change (High Level)
  - Remove self-ID parameters and their `Guard.Positive` checks; keep guards on cross-aggregate reference IDs. Unify `Id` as `{ get; private set; }` (`Cart.Id` and `Customer.Id` are get-only today); `Id == 0` means not yet persisted.
  - Handlers build response models that carry generated IDs (`CartDetails.Id`, `CustomerAddressDetails.Id`, `CheckoutSucceededOutcome.OrderId`) only after `IUnitOfWork.SaveChangesAsync`: move the `CartMapper.ToDetails` call after the save in `AddCartItemHandler`; build the checkout outcome from `order.Id` after the save in `CheckoutHandler`.
  - Test doubles simulate identity assignment for newly added aggregates (small reflection helper); `TestData` keeps deterministic IDs for pre-seeded aggregates so existing literal command IDs keep working.
- What Should Not Be Done
  - No EF Core packages, DbContext, mappings, or migrations. No API endpoints. No Identity/Auth work. No repository interface changes (`Talabat.Domain/Interfaces/` stays as-is). No business-rule changes.
- Main Risk Points
  - `Restaurant.AddProduct` currently dedupes by caller-supplied `productId`; new products all have `Id == 0`, so the duplicate check must move to normalized product name (keep `DuplicateProductException`; a DB unique index backstop lands in Phase 4).
  - `Customer.AddAddress` drops its duplicate-ID branch; the value-equality duplicate check must remain so `DuplicateAddressException` behavior is preserved.
  - Any result mapping that runs before `SaveChangesAsync` silently returns `Id = 0` — review every create-path handler.
  - A future flow that creates one aggregate referencing another unsaved aggregate in the same transaction (for example order + delivery in one use case) would need a save-first step; current phased use cases do not do this — note it for Phase 7.
  - `CheckoutHandlerSuccessTests` asserts the literal fake order ID (`300`); ID literals in tests will change.
- Acceptance Criteria
  - Solution builds and all Application tests pass; a solution-wide search for the removed generator interface name returns nothing.
  - No aggregate factory or constructor accepts its own ID; every entity `Id` is `{ get; private set; }`.
  - Create-path handlers return generated IDs only after the save; business behavior assertions unchanged except ID literals.
  - Domain and Application still reference no EF Core, ASP.NET Core, or Identity types.
  - Decision recorded: Phase 4 notes in this file updated, short record at `docs/phase-3.5-id-strategy-refactor.md`, spec research ID decision annotated as superseded.

### Phase 5: Infrastructure And Persistence Foundation

> **Status: Next.** To be specced with spec-kit at `specs/002-persistence-infrastructure/` and executed from its `tasks.md`. Where the spec-kit is more specific than this section, the spec-kit wins. The phase scope guard lives in `.specify/memory/constitution.md` (updated 2026-07-11 for Phase 4). Inputs for the spec: this section, `docs/phase-3.5-id-strategy-refactor.md`, `docs/phase-1-domain-cleanup-and-invariant-stabilization.md` (child keys, UTC policy), and `docs/delivery/delivery-database-design.md` (delivery tables).

- Goal
  - Persist the core business model behind the repository contracts.
- Strategy
  - Add EF Core only after Domain and Application contracts are stable.
  - Implement persistence in Infrastructure with explicit mappings.
  - Keep database constraints aligned with domain invariants.
- Main decisions
  - ID strategy: decided and executed in Phase 3.5 — SQL Server IDENTITY for integer keys. Phase 4 assumes that refactor is complete: map keys with the EF default (`ValueGeneratedOnAdd`); do not create sequences, do not configure `ValueGeneratedNever`, and do not reintroduce an application-side ID generator. If Phase 3.5 is not complete, stop and complete it first. Integration tests must assert IDs are populated after `SaveChangesAsync`.
  - Choose owned type mappings for `Money`, `TimeRange`, `Address`, `DeliveryAddressSnapshot`, and `GeoLocation`.
  - Child key strategy: `CustomerAddress` uses IDENTITY (per Phase 3.5); `CartItem` and `OrderItem` keep the Phase 1 composite-key decision (`CartId + ProductId`, `OrderId + ProductId`).
  - Choose whether Catalog seed data is static seed data or managed through internal tooling later (note: Catalog has no API creation path, so without seed data Phase 5 endpoints return nothing).
  - Choose transaction boundary for checkout and delivery coordination.
  - Choose the integration-test database strategy: SQL Server via Testcontainers (preferred when Docker is available) or LocalDB. SQLite is disqualified — the filtered unique indexes are load-bearing.
  - Decide audit stamping: a `SaveChanges` interceptor fills `AuditableEntity` timestamps (user values stay null until Identity exists); decide whether `IsDeleted` gets a global soft-delete query filter now.
  - Decide EF materialization mechanics that preserve encapsulation: private parameterless constructors and backing-field mapping for `_items`, `_addresses`, `_products` and get-only properties; no public setters added for EF.
  - Decide connection-string handling for local development (`appsettings.Development.json` vs user secrets).
- Actions
  - Add EF Core packages only after approval.
  - Add the composition-root wiring: `Talabat.API` project reference to `Talabat.Infrastructure` plus an `AddInfrastructure()` DI extension (required for migrations tooling and runtime registration).
  - Fix the standing `NU1903` warning (vulnerable transitive `Microsoft.OpenApi` 2.0.0) by updating `Microsoft.AspNetCore.OpenApi` in the same package-change batch.
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
    - Unique product name per restaurant (database backstop for the Phase 3.5 name-based duplicate check).
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
  - `tests/Talabat.Infrastructure.Tests/` (new integration test project)
  - `specs/002-persistence-infrastructure/` (spec-kit artifacts)
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
  - Integration tests prove: IDs are populated after `SaveChangesAsync`; checkout persists one order and closes one cart atomically; the one-active-cart-per-customer and unique-delivery-per-order constraints reject violations; owned value objects round-trip.
  - `Talabat.Domain` and `Talabat.Application` still contain zero package references.
  - The `NU1903` vulnerability warning is resolved (`dotnet list package --vulnerable` is clean).
- Risks / Mistakes To Avoid
  - Do not let EF mapping requirements weaken aggregate encapsulation.
  - Do not map temporary snapshots as independent tables.
  - Do not use `IQueryable` outside Infrastructure.
  - Do not start with migrations before reviewing model configuration.

### Phase 6: Minimal Identity/Auth Setup Before Business APIs

> **Status: Next.** This is the first incomplete phase after the completed Infrastructure and persistence foundation.

- Goal
  - Establish a simple, separate ASP.NET Core Web API Identity/Auth host before either business API is created.
  - Learn and validate account persistence and cookie-session behavior with minimal JSON register, login, and logout endpoints.
- Strategy
  - Create `Talabat.Identity` from the ASP.NET Core Web API template, not the Web App/Razor Pages template.
  - Integrate Duende IdentityServer with ASP.NET Core Identity.
  - Keep account storage and authentication behavior completely separate from `Talabat.Domain`.
  - Use the existing Infrastructure `TalabatDbContext` as the one EF Core context for both business and ASP.NET Core Identity tables.
  - Add a project reference from `Talabat.Identity` to `Talabat.Infrastructure`; never add the reverse reference.
  - Keep the host headless/API-first in Phase 6. A future Angular SPA will provide the interactive login/register/logout experience.
  - Configure the future Angular client as a public OIDC client using Authorization Code with PKCE and no client secret.
  - Use simple, reviewed development configuration first; refine clients, token audiences, scopes, custom claims, and API authorization after the business API surfaces exist.
- Initial capabilities
  - `POST /api/account/register` creates an account through `UserManager`.
  - `POST /api/account/login` validates credentials through `SignInManager` and establishes the Identity authentication cookie; it does not return a custom access token.
  - `POST /api/account/logout` ends the local Identity session through `SignInManager`.
  - ASP.NET Core Identity-backed user storage.
  - Duende discovery and protocol endpoints with the minimum safe configuration.
  - OpenAPI/manual HTTP testing for the account endpoints, including cookie preservation between login and logout calls.
  - Full OIDC interactive login and logout are deferred until an Angular interaction UI or an explicitly approved temporary test client exists.
- Architecture rules
  - `ApplicationUser` lives in an Identity-specific Infrastructure namespace because `TalabatDbContext` must use it as an Identity EF model.
  - `TalabatDbContext` derives from the appropriate `IdentityDbContext<ApplicationUser, IdentityRole, string>` base and continues to configure the existing business aggregates.
  - `Talabat.Identity` obtains `TalabatDbContext` and the Identity EF stores through its Infrastructure reference.
  - `Talabat.Infrastructure` must never reference the `Talabat.Identity` host.
  - `Customer` and `DeliveryAgent` remain separate Domain profiles.
  - Neither profile inherits from `IdentityUser` or `ApplicationUser`.
  - No `IdentityUser`, `ApplicationUser`, `ClaimsPrincipal`, `HttpContext`, JWT, Duende, or ASP.NET Core Identity type enters `Talabat.Domain`.
  - Account-to-profile linkage is explicitly deferred.
  - Minimal registration creates an account, not a Customer or DeliveryAgent aggregate.
  - The account login endpoint never mints a JWT and never implements the resource-owner-password grant.
  - Angular never receives, stores, or submits a client secret.
- Planning and manual setup
  - Follow `docs/identity/duende-aspnet-identity-setup-guide.md` only after the Phase 6 scope/spec is approved.
  - Verify the supported Duende/.NET package line and licensing immediately before installation.
  - Use the existing `TalabatDb` connection and Infrastructure migration history. Generate the reviewed Identity schema migration from Infrastructure with `Talabat.Identity` as the startup project.
- What Should Not Be Done
  - Do not create `Talabat.Customer.API` or `Talabat.DeliveryAgent.API` in this phase.
  - Do not add authentication abstractions or framework types to Domain.
  - Do not add `IdentityUserId` to Customer or DeliveryAgent.
  - Do not finalize API resources, audiences, scopes, roles, policies, or custom claims before the APIs clarify them.
  - Do not build Angular in this phase.
  - Do not create a second Identity DbContext or second Identity database.
  - Do not add a reference from Infrastructure to the Identity Web API host.
  - Do not create EF relationships or navigation properties between `ApplicationUser` and Domain profiles.
  - Do not create a custom login endpoint that returns a hand-built JWT.
  - Do not use Resource Owner Password Credentials to avoid the interactive OIDC flow.
  - Do not add refresh-token tuning, external login, password reset, email confirmation, 2FA, admin UI, advanced consent/custom grants, or production signing/secrets hardening.
- Acceptance Criteria
  - `Talabat.Identity` is the only project containing Duende host/protocol implementation; ASP.NET Core Identity EF types are limited to Infrastructure and the Identity host.
  - A development user can register through JSON, log in to establish the Identity cookie, and log out while preserving the same test-client cookie jar.
  - Login does not return a custom JWT or expose password/password-hash data.
  - Duende discovery is available, while end-to-end Angular OIDC remains explicitly deferred.
  - `Talabat.Identity` references Infrastructure and uses the single registered `TalabatDbContext` Identity store.
  - One reviewed Infrastructure migration adds only the expected ASP.NET Core Identity schema changes without damaging existing business tables/data.
  - Domain and Application remain free of Identity/Auth framework packages and types.
  - Customer and DeliveryAgent are unchanged and no account-to-profile linkage exists.
  - The solution's existing business tests remain green.
- Risks / Mistakes To Avoid
  - Do not treat successful account creation as successful Customer or DeliveryAgent profile creation.
  - Do not over-design tokens before there are API consumers.
  - Do not confuse the Identity cookie returned by the login endpoint with an API access token.
  - Do not let the single-DbContext choice create Domain-to-Identity inheritance, navigation properties, or repository coupling.
  - Do not let a tutorial's sample users, credentials, signing keys, redirect URIs, CORS policy, or in-memory stores become production configuration.
  - Do not implement advanced account-management features merely because the framework exposes them.

### Phase 7: `Talabat.Customer.API`

- Goal
  - Expose customer-facing use cases through a dedicated HTTP host without moving business logic into controllers.
- Strategy
  - Rename the existing empty `Talabat.API` host to `Talabat.Customer.API` before adding business endpoints.
  - Keep the API as a thin transport and composition root over Application and Infrastructure.
  - Integrate with the already-running `Talabat.Identity` authority, beginning with the simplest viable token validation and refining scopes/claims in Phase 9.
- Main decisions
  - Controllers versus minimal APIs.
  - API versioning approach.
  - Error response format (`DomainException` to Problem Details).
  - Anonymous catalog endpoints versus authenticated owner-scoped endpoints.
  - How a validated account is temporarily represented before the account-to-Customer profile linkage is approved.
  - Add an `AddApplication()` DI extension for handler registration.
- Actions
  - Rename `Talabat.API` to `Talabat.Customer.API` across project metadata and documentation.
  - Remove the template WeatherForecast endpoint.
  - Add Application and Infrastructure composition-root registration.
  - Add exception mapping and OpenAPI descriptions.
  - Configure token validation against `Talabat.Identity` using the smallest approved audience/scope contract.
  - Add customer-facing Catalog, Basket, Customer, Checkout, and Ordering endpoints.
  - Use `/api/me/...` for owner-scoped routes and never trust a caller-supplied `customerId` as authorization.
  - Record every token/claim/scope limitation that must be resolved in Phase 9.
- What Should Not Be Done
  - Do not add login/register/logout endpoints to this API.
  - Do not access `TalabatDbContext` or Identity stores directly from customer controllers; persistence remains behind registered services even though the approved single context lives in Infrastructure.
  - Do not add DeliveryAgent endpoints.
  - Do not put Domain rules in controllers.
  - Do not expose EF or Domain entities as HTTP contracts.
- Acceptance Criteria
  - Public catalog endpoints behave as explicitly documented.
  - Protected customer endpoints reject missing or invalid credentials.
  - Controllers/endpoints only map requests and invoke Application use cases.
  - Ownership-sensitive operations do not trust public customer IDs.
  - Identity-specific framework types do not leak into Domain.

#### Phase 7 Completion: Customer Workflow Contract Stabilization

- Goal
  - Ensure the backend supports the future Customer Website workflows.
- Strategy
  - Complete customer-facing API capabilities before building the frontend.
  - Use the minimal Identity host from Phase 6 while recording token/claim refinements for Phase 9.
- Main decisions
  - Define customer-facing API contracts and response shapes.
  - Define how authenticated accounts will later map to Customer profiles without coupling Domain to Identity.
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
  - Document the current access rule and Phase 9 refinement need per endpoint.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Application/`
  - `src/Talabat/Talabat.Customer.API/`
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
  - Do not duplicate login/register/logout behavior from `Talabat.Identity`.
  - Do not implement advanced Identity/Auth features in the Customer API.
  - Do not rely on hardcoded customer IDs as final authorization design.
- Risks / Mistakes To Avoid
  - Do not build the frontend before backend contracts are stable.
  - Do not bake the old single-customer assumption into public API contracts.
  - Do not require Identity framework-specific claims in Application results.

### Phase 8: `Talabat.DeliveryAgent.API`

- Goal
  - Implement Delivery Application workflows and expose them through a dedicated delivery-agent HTTP host.
- Strategy
  - Build delivery backend capabilities after core checkout/order persistence exists.
  - Keep Delivery separate from Ordering while allowing Application workflows to create and update delivery tasks.
  - Integrate with the minimal `Talabat.Identity` host without finalizing complex token or claim design until Phase 9.
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
  - Document current access behavior and the token/claim/policy refinements required in Phase 9.
- Files/Folders To Create Or Modify
  - `src/Talabat/Talabat.Application/`
  - `src/Talabat/Talabat.Domain/` only if approved domain gaps are discovered.
  - `src/Talabat/Talabat.Infrastructure/`
  - `src/Talabat/Talabat.DeliveryAgent.API/` (new host; reuses `AddApplication()`/`AddInfrastructure()` and shared API plumbing established by the Customer API).
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
  - DeliveryAgent authorization and ownership assumptions are documented for Phase 9 refinement.
- What Should Not Be Done
  - Do not build the Delivery Website frontend in this phase.
  - Do not add real-time GPS/maps.
  - Do not duplicate Identity/Auth account endpoints or user storage.
  - Do not implement deferred advanced Identity/Auth features.
  - Do not merge Delivery lifecycle into Ordering.
- Risks / Mistakes To Avoid
  - Do not put delivery lifecycle rules in controllers.
  - Do not model `Delivery` as a child of `Order`.
  - Do not let a delivery agent act on another agent's assigned delivery once auth is introduced.
  - Do not implement real-time GPS/maps before basic lifecycle works.

### Phase 9: Token, Claims, And Scopes Refinement

- Goal
  - Refine the minimal Identity setup using concrete requirements from both business API hosts.
- Inputs
  - Implemented `Talabat.Customer.API` endpoints and ownership requirements.
  - Implemented `Talabat.DeliveryAgent.API` endpoints and assignment requirements.
  - The simple register/login/logout behavior from Phase 6.
- Decisions
  - Define separate API resources and audiences for Customer and DeliveryAgent APIs.
  - Define the minimum scopes required by real API operations.
  - Define which standard and custom claims are actually needed.
  - Decide whether roles, permissions, or both are needed for Delivery operations.
  - Decide the account-to-Customer and account-to-DeliveryAgent profile-link strategy.
  - Decide where profile resolution occurs and how stale links/claims are handled.
  - Decide client registrations and redirect/logout URIs for the future websites.
- Actions
  - Replace temporary/minimal token settings with reviewed API-specific resource and scope configuration.
  - Add only claims used by a documented API policy or profile-resolution flow.
  - Add audience and scope validation to each API host.
  - Document token lifetimes and refresh behavior, but defer advanced tuning unless required for a working client.
  - Extend the authorization endpoint matrix with implemented routes.
- What Should Not Be Done
  - Do not place claims or Identity framework types in Domain.
  - Do not make profile IDs caller-controlled.
  - Do not add speculative roles, permissions, scopes, or claims.
  - Do not implement external login, 2FA, admin UI, advanced consent/custom grants, or production key hardening here.
- Acceptance Criteria
  - Each API accepts only tokens intended for its audience and required scope.
  - Every custom claim has a documented consumer.
  - Account-to-profile linkage, if approved, remains framework-neutral at the Domain boundary.
  - Customer tokens cannot call DeliveryAgent-only operations and vice versa.

### Phase 10: Authorization Strategy And Quality Gates

- Goal
  - Finalize endpoint authorization and prove the complete system meets architecture, security, persistence, and behavior quality gates.
- Strategy
  - Authorization belongs in API/Application boundary decisions, not Domain entities.
  - Model domain ownership separately from auth roles.
  - Use the refined Phase 9 token contract without making Domain depend on claims or Identity types.
  - Ownership scoping is already pre-staged in code: repository contracts expose customer-scoped reads (`IOrderRepository.GetByIdForCustomerAsync`, `GetByCustomerIdAsync`, `ICartRepository.GetActiveCartByCustomerIdAsync`), and Application use cases carry explicit `customerId`. Phase 10 binds those inputs to trusted profile resolution instead of redesigning business use cases.
- Main decisions
  - Which operations are customer-only.
  - Which operations are delivery-agent-only.
  - Which operations require admin/delivery-operations permissions.
  - Whether restaurant-owner workflows are in scope and what domain model they require.
- Actions
  - Create or finalize the policy matrix for every implemented endpoint/use case.
  - Define future role candidates.
  - Define ownership checks:
    - Customer can access only own cart/orders/profile.
    - DeliveryAgent can access only assigned delivery tasks where applicable.
    - Operations/Admin can assign or inspect broader delivery data if later approved.
  - Add a framework-neutral current-user/profile-resolution abstraction only if the implemented APIs require it.
  - Run architecture, unit, integration, API authorization, migration, and package-vulnerability gates.
- Files/Folders To Create Or Modify
  - `docs/authorization-strategy.md`.
  - `docs/authorization-endpoint-matrix.md`.
  - `src/Talabat/Talabat.Application/Abstractions/` only for framework-neutral contracts that are proven necessary.
  - `src/Talabat/Talabat.Customer.API/` and `src/Talabat/Talabat.DeliveryAgent.API/` for host-specific policies and token validation.
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
  - Do not add role checks inside aggregates.
  - Do not add account/profile linkage blindly; implement only the Phase 9 decision.
  - Do not weaken quality gates to make authentication tests pass.
- Risks / Mistakes To Avoid
  - Do not confuse authentication with domain profile existence.
  - Do not trust route IDs for ownership.
  - Do not add restaurant-owner role without deciding the restaurant ownership domain model.
  - Do not implement policies before Phase 9 has concrete API requirements and a reviewed token/claim design.

#### Phase 10 Quality Gate Details

- Goal
  - Make each phase reviewable and prevent architecture regressions.
- Strategy
  - Testing starts before this phase: `tests/Talabat.Application.Tests` is a Phase 3 deliverable, and Phases 4–7 should add their own integration/API tests as part of their acceptance criteria. Phase 10 consolidates and hardens; it does not introduce testing.
  - Phase 10 backfills the Domain unit test project (aggregate state machines, value objects, domain services, exceptions), adds Infrastructure and API test layers, adds architecture/dependency tests, and wires CI with coverage and vulnerability gates (for example `dotnet list package --vulnerable`).
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

Status update (2026-07-11): Phase 0 already applied the scope banners/notices to the root planning docs (`PLAN.md`, `Talabat_Implementation_Roadmap.md`, `Talabat_DDD_Project_Architecture_Prompt.md`) and the `docs/` design set, and `docs/reviews/domain-review-report.md` is marked historical. Rows that only required those notices are done; content-level rewrites remain open and should be folded into the phase that changes the related behavior.

| File | Current Issue | Required Change | Priority |
|---|---|---|---|
| `PROJECT_IMPLEMENTATION_ROADMAP.md` | New roadmap file. | Use as the current high-level implementation sequence after approval. | High |
| `Talabat_DDD_Project_Architecture_Prompt.md` | Mentions ASP.NET Core Identity and old MVP exclusions. | Align it with the approved separate `Talabat.Identity` Web API host, Duende + ASP.NET Core Identity, and future Angular Authorization Code + PKCE client. | High |
| `Talabat_Implementation_Roadmap.md` | Old roadmap mixes the Identity host boundary with Infrastructure and says auth is out of scope for MVP v1. | Mark as superseded or revise it to show the approved split: Identity EF storage in the single Infrastructure DbContext, Duende/account HTTP behavior in the separate Web API host. | High |
| `PLAN.md` | Old plan includes Identity in stack/context and does not reflect the approved host boundaries. | Mark as historical or align it with the new Web API + future Angular sequence. | High |
| `docs/bounded-contexts.md` | Says MVP v1 excludes auth, delivery drivers, roles, and assumes one normal customer. | Update to distinguish initial unauthenticated development from final Customer/Delivery/Auth direction. Add Identity/Auth as future boundary. | High |
| `docs/business-rules.md` | Title and intro are MVP v1-only and assume no auth/single customer. | Update rule framing to include future authenticated ownership while keeping current domain rules. | High |
| `docs/business-rule-classification.md` | Classifies auth, delivery, roles, and restaurant owners as out of scope. | Reclassify as deferred/reserved; add future Application/API authorization ownership notes. | High |
| `docs/aggregates-and-invariants.md` | Contains old MVP v1 no-Identity boundary and repository list omits Delivery roots at the end. | Update with future identity boundary, Delivery roots, and three-website implications. | High |
| `docs/entity-design.md` | Customer notes say no IdentityUserId in MVP v1 and final direction is not reflected. | Update to say Customer remains a domain profile independent from identity framework; future linkage TBD. | High |
| `docs/value-object-design.md` | MVP v1 scope excludes auth and delivery drivers, while Delivery value objects now exist. | Update scope language and include Delivery value object status consistently. | Medium |
| `docs/domain-services-design.md` | MVP v1-only language and only Checkout service as MVP v1 service. | Update to include DeliveryAssignmentDomainService as implemented and Identity/Auth as external boundary. | High |
| `docs/domain-failures-design.md` | Auth failures are described as out of scope for MVP v1 only. | Clarify Domain still should not model auth failures; API/Auth boundary handles them later. | Medium |
| `docs/domain-invariants.md` | No mention of Identity boundary, Delivery role scoping, or three websites. | Add concise notes that auth ownership checks are Application/API concerns, not Domain invariants. | Medium |
| `docs/repository-interfaces-design.md` | Updated in Phases 0/2: contracts are implemented and `GetMvpCustomer` is explicitly ruled out. | Keep in sync as contracts evolve (Phase 4 persistence semantics, any future ownership-scoped methods). | Low |
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
| `docs/glossary.md` | Deletion was committed in `91dc805`; the project no longer has a ubiquitous-language glossary. | Recreate an updated glossary aligned with current terms (Cart vs Basket naming, Delivery lifecycle terms, terminal states, future account-vs-profile distinction) once Phase 3 stabilizes wording. | Medium |
| `docs/phase-0-repository-and-documentation-audit.md`, `docs/phase-1-domain-cleanup-and-invariant-stabilization.md`, `docs/phase-2-domain-and-application-contracts.md` | New phase records (added 2026-07-11). | Keep as immutable phase records; add `docs/phase-3-application-use-cases.md` when Phase 3 completes. | Low |
| `AGENTS.md`, `.specify/`, `specs/001-application-use-cases/` | Active spec-kit governing Phase 3. | Keep in sync with this roadmap; where they conflict on Phase 3 scope, the spec-kit wins. Archive or mark the spec complete after Phase 3 ships. | Medium |
| `.codex-scratch/ids4-net10-compat/` | Throwaway IdentityServer4 compatibility spike is now committed; it references an archived framework with vulnerable-era packages (excluded from the solution, so it does not affect builds). | Remove it (its findings are preserved in `docs/identityserver4-readiness-report.md`) or add a README disclaimer marking it as disposable research; consider gitignoring `.codex-scratch/`. | Low |

## 7. Next 10 Concrete Tasks

These tasks describe the next approved work. They are planning instructions only until Phase 6 is formally approved:

1. Create a dedicated Phase 6 specification and update the constitution/current agent scope from completed persistence work to Minimal Identity/Auth.
2. Confirm the supported Duende IdentityServer line for the repository's .NET target and verify licensing eligibility.
3. Plan the new `Talabat.Identity` ASP.NET Core Web API host with a one-way project reference to Infrastructure and no references from Domain or Application.
4. Define `ApplicationUser` in an Identity-specific Infrastructure namespace and extend the existing `TalabatDbContext` from the appropriate IdentityDbContext base.
5. Plan ASP.NET Core Identity registration against `TalabatDbContext`, password policy, and development seed strategy without using sample production credentials.
6. Plan Duende integration through ASP.NET Core Identity and the minimum discovery/interactive-client configuration.
7. Plan JSON register/login/logout account endpoints for manual cookie-session testing; keep registration account-only and never return a custom JWT from login.
8. Plan one reviewed Infrastructure migration that adds Identity tables to the existing database without unexpected business-schema changes.
9. Define tests for register/login/logout, Domain isolation, and existing business regression behavior.
10. Complete the Phase 6 quality review before creating or renaming either business API host.

## 8. Final Notes

The approved strategy is Domain-first, then contracts, Application, Infrastructure/persistence, a minimal separate Identity/Auth host, the two business API hosts, and finally token/authorization refinement and advanced features.

Current position (2026-07-14): the historical work through Persistence and Infrastructure is complete and is normalized as Phases 1–5 in this roadmap. The next step is Phase 6 — Minimal Identity/Auth Setup Before Business APIs. Its detailed learner procedure is documented in `docs/identity/duende-aspnet-identity-setup-guide.md`.

Phase 6 creates `Talabat.Identity` as an ASP.NET Core Web API host with Duende IdentityServer integrated with ASP.NET Core Identity. It starts with JSON register/login/logout endpoints for manual Identity-cookie testing and does not return custom tokens from the login endpoint. A future Angular SPA supplies the interactive UI and uses Authorization Code with PKCE. `Talabat.Customer.API` and `Talabat.DeliveryAgent.API` follow in Phases 7 and 8. Token audiences, scopes, custom claims, profile linkage, and detailed authorization are deliberately refined in Phases 9 and 10 after the API requirements are concrete.

The Domain remains independent from Duende, ASP.NET Core Identity, `IdentityUser`, `ApplicationUser`, `ClaimsPrincipal`, `HttpContext`, JWT, controllers, and HTTP. `Customer` and `DeliveryAgent` remain domain profiles and never inherit from Identity account classes.

Do not install packages, create projects or migrations, refactor source code, or begin a phase until its scope and quality gates are approved.
