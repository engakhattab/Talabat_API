# Research: Authorization Strategy And Quality Gates

**Feature**: Phase 10 — Authorization Strategy And Quality Gates
**Date**: 2026-07-26
**Spec**: [spec.md](spec.md)

## R1: Scope Enforcement Pattern

**Decision**: Create `ScopeRequirement` + `ScopeHandler` in each API host's `Auth/` directory (not shared).

**Rationale**: The `scope` claim may arrive as one space-delimited string (e.g., `"customer.api"`) or as multiple claim entries (e.g., two `scope` claims: `"customer.api"` and `"openid"`). The handler must call `FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))` to handle both formats. Duplicating this small class in each host avoids a shared library dependency and keeps the hosts independent.

**Alternatives considered**:
- **Shared library**: Over-engineered for two hosts; adds a project dependency that must be maintained.
- **Built-in scope check**: ASP.NET Core has no built-in "has scope" handler. `ClaimTypes.Authorization` is not standardized across token issuers.
- **Policy requirement via `RequireClaim("scope", "customer.api")`**: Does not handle space-delimited format; silently fails if the claim is split across multiple entries.

## R2: Policy Registration Pattern

**Decision**: Use `AddAuthorizationBuilder()` with named policies in each host's `Program.cs`. Policy names defined as constants in `Auth/AuthorizationPolicies.cs`.

**Rationale**: Named constants prevent magic-string errors. `AddAuthorizationBuilder()` is the modern .NET 10 API (replaces `AddAuthorization` lambda pattern). Policies combine `RequireAuthenticatedUser()`, `ScopeRequirement`, and `RequireRole()`.

**Alternatives considered**:
- **Attribute-based inline policies**: Violates DRY; magic strings in controllers.
- **Global authorization filter**: Cannot differentiate `CustomerAccess` vs `CustomerScopeOnly` per endpoint.
- **Default policy + fallback**: Would require per-endpoint overrides anyway; adds complexity.

## R3: Delivery Ownership Hardening

**Decision**: Move agent-id injection from handlers to controllers for lifecycle commands. Add `AgentId` to command records. Add `GetByIdForAgentAsync` to `IDeliveryRepository`.

**Rationale**: This matches the customer-side pattern (controllers inject `CustomerId` from `ICurrentUser`). Handlers become identity-unaware. The repository scoping ensures 404 for unauthorized access (not post-load 403).

**Current state** (verified in codebase):
- Customer-side: Controllers inject `ICurrentUser`, extract `CustomerId`, pass it to command records. Handlers receive `CustomerId` as a parameter. ✅ Correct pattern.
- Delivery-side: Controllers do NOT inject `ICurrentUser`. Handlers inject `ICurrentUser` and resolve agent identity internally. Commands carry only `DeliveryId`. ❌ Needs fixing.

**Alternatives considered**:
- **Keep `ICurrentUser` in handlers**: Inconsistent with customer-side pattern; leaks identity concerns into Application layer. The Application layer should not depend on `ICurrentUser`.
- **Post-load comparison in controller**: Returns 403 (confirms resource exists) — violates the 404-only ownership rule.

## R4: Self-Assignment for AssignDelivery

**Decision**: Agent claims pending delivery for themselves. `AgentId` from `ICurrentUser.AgentId`, not from request body.

**Rationale**: The `DeliveryOperations` role (operations-assigned model) has no approved use case. Self-assignment is the MVP. Body-supplied agent id is a security hole (any agent can assign to any other agent).

**Current state**: `AssignDeliveryCommand(int DeliveryId, int AgentId)` with `AgentId` from `AssignDeliveryBody(int AgentId)` — any authenticated agent can assign to any other agent id.

**Alternatives considered**:
- **Operations-assigned**: Requires `DeliveryOperations` role — out of scope (no approved use case).
- **Keep body-supplied agent id**: Security vulnerability; contradicts roadmap ("no endpoint derives caller identity from request fields").

## R5: Architecture Testing Approach

**Decision**: Use reflection-based assertions (no external library). Assert prohibited package references and prohibited type usages via `Assembly.GetReferencedAssemblies()` and type scanning.

**Rationale**: The existing codebase has no NetArchTest dependency. Reflection-based tests are self-contained and do not add a new package. The assertions are straightforward:
- Check `Assembly.GetReferencedAssemblies().Name` for prohibited packages.
- Scan types for prohibited base types/interfaces (e.g., `ICurrentUser` in aggregates).

**Alternatives considered**:
- **NetArchTest**: Popular library for architecture tests, but adds a dependency. The existing test projects have minimal dependencies.
- **Manual code review**: Not automated; cannot prevent regression.
- **Roslyn analyzers**: More powerful but significantly more complex to implement.

## R6: Test Auth Pattern for Delivery API Tests

**Decision**: Mirror `Talabat.Customer.API.Tests` pattern: `CustomWebApplicationFactory` with `TestAuthHandler` using `X-Test-Subject` and `X-Test-Roles` headers. Add scope claim support via `X-Test-Scopes` header.

**Rationale**: The existing customer API tests use header-based auth (not JWT minting). Adding scope support to the header-based handler is simpler than introducing JWT minting. The `ScopeHandler` reads `FindAll("scope")`, so the test handler must emit scope claims from the `X-Test-Scopes` header.

**Current state** (verified in `Talabat.Customer.API.Tests`):
- `TestAuthHandler` reads `X-Test-Subject` → `ClaimTypes.NameIdentifier` + `"sub"` claims.
- `TestAuthHandler` reads `X-Test-Roles` → comma-separated role claims.
- No scope claims are emitted — the existing tests don't test scope enforcement (it doesn't exist yet).

**Change needed**: Add `X-Test-Scopes` header support to `TestAuthHandler` in both test projects. Emit each scope as a `"scope"` claim.

**Alternatives considered**:
- **JWT minting in tests**: More realistic but adds complexity (key management, token signing). The existing pattern avoids this.
- **Mock `IAuthorizationService`**: Bypasses the real authorization pipeline; integration tests lose value.

## R7: CI Pipeline

**Decision**: GitHub Actions workflow with: checkout → setup .NET 10 → restore → build → test + coverage → vulnerability scan.

**Rationale**: Matches the plan's §6.5 specification. `XPlat Code Coverage` for coverage. `dotnet list package --vulnerable --include-transitive` for vulnerability scanning (fail on any finding).

**Current state**: `.github/workflows/` directory exists but is empty.

**Alternatives considered**:
- **Azure DevOps**: Not specified in the plan; GitHub Actions is the default for open-source.
- **Skip vulnerability scan**: Security risk; the plan explicitly requires it.
- **Separate CI jobs for each test project**: Adds parallelism complexity; a single `dotnet test` command discovers all projects.
