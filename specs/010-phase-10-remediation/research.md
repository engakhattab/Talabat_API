# Research: Phase 10 Remediation

## R1: MapInboundClaims Fix

**Decision**: Add `options.MapInboundClaims = false;` as the first line inside each `.AddJwtBearer` block.

**Rationale**: The default `MapInboundClaims = true` rewrites `"role"` to `ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`). When `RoleClaimType = "role"` is also set, `RequireRole("Customer")` looks for `ClaimTypes.Role` but finds the raw `"role"` claim type. Setting `MapInboundClaims = false` preserves the raw claim type from the token, making `RequireRole` work correctly.

**Alternatives considered**:
- Change `RoleClaimType` to `ClaimTypes.Role`: Would work but contradicts the token design where claims are emitted as `"role"`.
- Custom claim mapping: Over-engineered for this fix.

**Verification**: `RealTokenPipelineTests` must fail before the fix (RequireRole returns 403) and pass after.

## R2: Delivery Creation After Checkout

**Decision**: Create a separate `CreateDeliveryForOrder` use case invoked by `CheckoutController` after `CheckoutHandler` succeeds. Not inside `CheckoutHandler`.

**Rationale**: Ordering must not depend on Delivery (constitution layering). The controller invokes the delivery creation as a fire-and-forget step — if it fails, the checkout still returns 200 (BR-DEL-016). The `CheckoutOutcome` exposes `OrderId` but needs `RestaurantId` and `DeliveryAddress` added.

**Data flow**: `CheckoutHandler` returns `CheckoutSucceededOutcome` → `CheckoutController` extracts `OrderId`, `RestaurantId`, `DeliveryAddress` → creates `CreateDeliveryForOrderCommand` → invokes handler → on failure, logs and returns checkout success.

**Alternatives considered**:
- Domain event: Requires transactional outbox (deferred). Not feasible today.
- Inside `CheckoutHandler`: Couples ordering to delivery. Violates constitution.
- Background job: Over-engineered for MVP; adds infrastructure dependency.

## R3: Agent Approval Endpoints

**Decision**: Add `POST /account/delivery-agents/{userId:int}/approve` and `/reject` to `AccountController`, gated behind `IHostEnvironment.IsDevelopment()`.

**Rationale**: `IUserCapabilityService.ApproveDeliveryAgentAsync` is implemented but unreachable. No `Admin` policy exists yet. Gating behind `IsDevelopment()` prevents accidental production exposure. TODO comment references Phase 9 `AdminAccess` policy.

**Alternatives considered**:
- Create an `Admin` policy: Explicitly out of scope (no approved use cases).
- Separate controller: `AccountController` already handles registration; approval belongs here.

## R4: Concurrency Token for Delivery

**Decision**: Add `byte[] RowVersion` with `IsRowVersion()` mapping, mirroring `User.RowVersion`.

**Rationale**: Under READ COMMITTED, two agents can both read `PendingAssignment`, both pass `AssignAgent`'s status check, and both write. `RowVersion` ensures the second `SaveChanges` throws `DbUpdateConcurrencyException`, which `UnitOfWork` translates to `ConcurrencyConflictException` → 409.

**Alternatives considered**:
- Pessimistic locking: Requires `SELECT FOR UPDATE` — not idiomatic with EF Core.
- Application-level lock: Would drag locking infrastructure into Application layer.

## R5: ProfileEnforcementFilter Replacement

**Decision**: Replace path-matching filter with `[RequireCustomerProfile]` attribute applied per action.

**Rationale**: The filter's `StartsWith("/api/me/")` matching is fragile — a trailing slash turns 404 into 409. An attribute is explicit, discoverable, and cannot accidentally affect new routes.

**Status code preservation**:
- `GET /api/me/profile` without profile → 404 `ProfileNotCreated`
- Other `/api/me/*` without profile → 409 `ProfileNotCreated`
- `POST /api/me/profile` always allowed through

**Alternatives considered**:
- Keep the filter: Continues to accumulate accidental behaviors.
- Global authorization filter: Cannot differentiate per-endpoint status codes.

## R6: CurrentUserCapabilityResolver

**Decision**: Add `ICurrentUserCapabilityResolver` to `Talabat.Application/Abstractions/`, implement in `Talabat.Infrastructure/Identity/`, reducing each host's `CurrentUser` to claims + one resolver call, cached per request.

**Rationale**: `CurrentUser` is duplicated in both hosts and injects `TalabatDbContext` directly, issuing a synchronous DB query inside a property getter. Centralizing the capability resolution eliminates duplication and enables async resolution.

**Alternatives considered**:
- Keep current pattern: Duplicated code, sync DB query in property getter.
- Read from JWT claims: Loses real-time capability check (stale token problem).

## R7: Architecture Test Strengthening

**Decision**: Add assertions on `.csproj` `PackageReference` items. Add: no type in either API assembly may depend on `TalabatDbContext`.

**Rationale**: `Assembly.GetReferencedAssemblies()` prunes unused references — an unused forbidden package passes silently. `.csproj` checks catch the reference at the source. The `NetArchTest.Rules` package (1.3.2) is already referenced.

**Alternatives considered**:
- Keep reflection-only: Insufficient — compiler prunes unused refs.
- Manual code review: Not automated; cannot prevent regression.

## R8: CI Pipeline

**Decision**: GitHub Actions with SQL Server 2022 container, connection string via environment variable, `dotnet test` with code coverage, vulnerability check that fails on findings.

**Rationale**: The current CI has no SQL Server, so four test suites cannot run. The vulnerability check always exits 0. The container approach is standard for GitHub Actions.

**Connection string**: `Server=localhost,1433;Database=Talabat;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True`

**Alternatives considered**:
- SQLite: Different SQL dialect; `EnsureCreated` vs `Migrate` semantics differ.
- Azure SQL: Requires credentials; not suitable for open-source CI.
