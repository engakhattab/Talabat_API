# Agent Context

<!-- SPECKIT START -->
Current roadmap increment:

- Phase: **DeliveryAgent API** (Phase 8)
- Branch: `feature/user-aggregate-refactor`
- Technical plan: `specs/007-deliveryagent-api/plan.md`
- Governing scope: `.specify/memory/constitution.md` v3.0.1 -> "Current Phase Scope: User Aggregate Refactor"

Scope guard for the current increment: execute the plan's three phases in order. Phase 1 (additive):
`Microsoft.Extensions.Identity.Stores` 10.0.9 into `Talabat.Domain`; `IAuditable`/`ISoftDeletable`
(implemented by `AuditableEntity`); `UserType` flags enum + `AgentApprovalStatus`; the `User`
aggregate (`Aggregates/Users/`) with customer profile, `UserAddress` collection, and the ported
delivery-agent state machine (`MarkBusy`/`MarkAvailable` stay internal); `IUserRepository`;
`IUserCapabilityService`; Domain behavior unit tests (in `Talabat.Application.Tests/Domain/Users/`).
Phase 2 (cutover): `TalabatDbContext` ->
`IdentityDbContext<User, IdentityRole<int>, int>`; delete `ApplicationUser`, `Customer`,
`CustomerAddress`, `DeliveryAgent`, `ICustomerRepository`/`IDeliveryAgentRepository` and their
implementations/configurations; `UserConfiguration` (NULL-tolerant `CK_Users_*` checks,
`UserAddresses` owned table with the filtered default index, `RowVersion` rowversion token);
`UserCapabilityService` (the ONLY place that mutates `UserType` flags or Identity roles, one
transaction, rollback on any failure); `TalabatSignInManager` (reject `!IsActive || IsDeleted`);
`IdentityDataSeeder` (idempotent roles: Customer, DeliveryAgent, Admin, RestaurantOwner);
registration endpoints (`/account/register/customer`, `/account/register/delivery-agent` — agent
role only after service-level admin approval); Application handlers to `IUserRepository` +
`ICurrentUser` v2 (`sub` = `User.Id` int); full test-suite migration kept green (the
`ProfileNotCreated` 404/409 contract preserved byte-identical); destructive dev-DB rebuild -> single
`InitialUnifiedUser` migration (run the plan's safety checks first). Phase 3: Customer API
role-claim wiring + ownership hardening, the plan's business-behavior tests (role/UserType drift,
multi-role journey, agent-assignment authorization, login rejection, concurrency 409, ownership,
session invalidation), superseded-notes on `specs/003-customer-api/*`,
`docs/authorization-matrix.md`, `phase-7-architecture-guide.md`, symbol sweeps, final gates.

Keep business names `CustomerId` and `Delivery.AssignedAgentId` (FKs now reference `AspNetUsers.Id`);
do not rename them to `UserId`. Keep `DeliveryAgentStatus` (not `UserStatus`). Keep DTO names
(`CustomerProfile`, etc.). The solution must build at the end of every phase; commit the uncommitted
working tree as a checkpoint before starting.

Do not: reintroduce separate `Customer`/`DeliveryAgent` aggregates, `ApplicationUser`, or an
`IdentityUserId` linkage key; mutate `UserType` or Identity roles outside `IUserCapabilityService`;
accept caller-supplied role names; grant DeliveryAgent at registration (approval only); use
`UserManager` as a business repository or mix it with `IUserRepository` outside the workflow
transaction; add Identity/EF/web packages to Domain beyond `Microsoft.Extensions.Identity.Stores`,
or any web/EF/Identity package to Application; put `UserManager`/`ClaimsPrincipal`/`HttpContext` in
Domain; implement the full Delivery API, admin controllers, Duende interactive clients, discounts,
frontend, or data-preserving migrations; trust route/body `customerId` for owner-scoped operations;
create role-conditional CHECK constraints that join Identity role tables.
<!-- SPECKIT END -->
