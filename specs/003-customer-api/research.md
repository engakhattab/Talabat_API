# Research: Talabat Customer API (Phase 7)

**Date**: 2026-07-16  
**Spec**: [spec.md](spec.md)

## R1: DomainException → ProblemDetails Mapping Strategy

**Decision**: Use an ASP.NET Core `IExceptionHandler` implementation (available since .NET 8) that
catches exceptions during request processing, maps them to `ProblemDetails` responses using the
built-in `IProblemDetailsService`, and returns the appropriate HTTP status code.

**Rationale**: The Application layer already has `DomainExceptionMapper` which maps exceptions to
`ApplicationError` with categories (`Validation`, `NotFound`, `Conflict`, `Unavailable`,
`OwnershipMismatch`). However, the current handlers *catch* domain exceptions and return
`UseCaseResult.Failure(...)`. The API exception handler serves as a safety net for any uncaught
domain exceptions that propagate past the handler (e.g., exceptions thrown during model-binding
validation). The primary mapping from `UseCaseResult.IsFailure` to HTTP status codes happens in a
shared controller-base method or extension.

**Status code mapping** (ApplicationErrorCategory → HTTP):

| Category | HTTP Status |
|----------|------------|
| `Validation` | 400 Bad Request |
| `NotFound` | 404 Not Found |
| `Conflict` | 409 Conflict |
| `Unavailable` | 422 Unprocessable Entity |
| `OwnershipMismatch` | 403 Forbidden |

For uncaught `DomainException` subtypes (safety net):

| Exception Type | HTTP Status |
|---------------|------------|
| Any `DomainException` | 400 Bad Request (default) |
| Unhandled non-domain exceptions | 500 (framework default) |

**Alternatives considered**: Middleware-based exception handler — rejected because `IExceptionHandler`
is the idiomatic .NET 10 approach, integrates with `ProblemDetails` natively, and avoids
middleware ordering concerns.

## R2: Bearer Token Validation Configuration

**Decision**: Add `Microsoft.AspNetCore.Authentication.JwtBearer` to the Customer API project and
configure it against the `Talabat.Identity` authority URL (`https://localhost:7237` in development).
The authority's JWKS endpoint provides the signing keys. No explicit audience or scope validation in
Phase 7 — the token must be issued by the trusted authority and not expired; fine-grained audience
and scope validation are deferred to Phase 9.

**Rationale**: The Identity host uses `AddDeveloperSigningCredential()` and has
`EmitStaticAudienceClaim = true`. The pre-configured API scopes are `talabat.customer-api` and
`talabat.deliveryagent-api`, but no clients are registered yet to actually issue tokens with these
scopes. Phase 7 tests will use self-minted JWTs (test-only signing key) that match the authority's
issuer. Real token acquisition is Phase 9 work (requires an interactive client).

**Configuration**:
- Authority: configurable via `appsettings.json` (`Identity:Authority`)
- Audience: not validated in Phase 7 (set to `false` — `ValidateAudience = false`)
- RequireHttpsMetadata: `false` in development only
- Tests: use `WebApplicationFactory` with a test authentication handler that reads test-minted JWTs

**Alternatives considered**: Cookie-based session with the Identity host — rejected because the
Customer API is a resource server, not an interactive app; bearer tokens are the correct pattern.

## R3: Account → Customer Profile Strategy (Explicit Profile Creation on First Use)

**Decision**: Account registration (in `Talabat.Identity`) stays account-only. The `Customer` profile
is created explicitly on first use via `POST /api/me/profile`, which supplies the domain-required
fields (full name, positive age, optional phone) and sets the `IdentityUserId` linkage from the token
`sub` claim. No empty/placeholder `Customer` is ever created.

**Why not auto-provision**: The `Customer` aggregate requires a non-empty `FullName`
(`Guard.RequiredText`) and a positive `Age` (`Guard.Positive`). Auto-creating a "minimal" `Customer`
from only a `sub` claim would either throw or force fabricated placeholder data, violating the
aggregate's invariants. Profile fields are domain data that must be supplied by the customer — only
the *linkage rule* is provisional (Phase 9), not the profile contents.

**Required Domain change**: Add a nullable `string? IdentityUserId` scalar to the `Customer`
aggregate (framework-neutral; permitted by Principle 6, v2.1.x) plus a factory
`Customer.CreateForAccount(string identityUserId, string fullName, int age, string? phoneNumber)`
that keeps the existing name/age guards. Add `ICustomerRepository.GetByIdentityUserIdAsync(...)`.

**Required Application changes**:
1. `ICurrentUser` interface in `Talabat.Application/Abstractions/` (see R4) — read-only resolution.
2. `CreateCustomerProfileHandler` in `Talabat.Application/Customers/CreateProfile/`: if an
   `IdentityUserId` match exists → `Conflict` (`ProfileAlreadyExists`); otherwise create via the
   factory and return the new `Id`.

**Required Infrastructure change**: `IdentityUserId` column with a unique filtered index; the
`GetByIdentityUserIdAsync` implementation.

**Required API change**: Owner-scoped endpoints (except `POST /api/me/profile`) return `409 Conflict`
(`ProfileNotCreated`) when the authenticated account has no profile yet. `POST /api/me/profile` is the
one endpoint reachable without a pre-existing profile.

**Create race condition**: The unique index on `IdentityUserId` prevents duplicate profiles; a
concurrent second create hits a unique-constraint violation, mapped to `Conflict`
(`ProfileAlreadyExists`).

**Alternatives considered**:
1. Auto-provision an empty profile on first request — rejected: violates `Customer` invariants and
   fabricates placeholder data (see "Why not auto-provision").
2. Collect profile fields during account registration in `Talabat.Identity` — rejected: couples the
   Identity host to the `Customer` domain and doesn't generalize to `DeliveryAgent`.

## R4: ICurrentUser Abstraction Design

**Decision**: `ICurrentUser` lives in `Talabat.Application/Abstractions/` as a framework-neutral
interface. Its implementation lives in the API host and reads claims from `HttpContext.User`.

```
// In Talabat.Application/Abstractions/
public interface ICurrentUser
{
    string IdentityUserId { get; }
    bool IsAuthenticated { get; }
    bool HasProfile { get; }
    int? CustomerId { get; }   // null until the profile is created
}
```

Resolution is **read-only** and has no side effects. Owner-scoped endpoints check `HasProfile` and
return `409 Conflict` (`ProfileNotCreated`) when it is `false`; profile creation happens only through
the explicit `POST /api/me/profile` endpoint (R3).

**Implementation**: A scoped `CurrentUser` class in the API host that:
1. Reads the `sub` claim from `HttpContext.User`.
2. Calls `ICustomerRepository.GetByIdentityUserIdAsync(sub)` once per request to populate
   `HasProfile` and `CustomerId` (no create).
3. Caches the result for the request lifetime.

This keeps the Application layer free from `ClaimsPrincipal` and HTTP types, and avoids a
side-effecting write during identity resolution.

**Alternatives considered**: Claims transformer that injects `customerId` as a claim — rejected
because it couples identity resolution to ASP.NET Core claims, and the Application layer
shouldn't know about claims.

## R5: Test Strategy for API Integration Tests

**Decision**: Use `WebApplicationFactory<Program>` with an in-memory test authentication handler
and the existing SQL Server test database (same approach as `Talabat.Infrastructure.Tests`). Tests
mint JWTs with known `sub` claims using a test-only symmetric key.

**Test categories**:
1. **Endpoint routing**: verify correct HTTP methods and route templates.
2. **Auth enforcement**: verify anonymous catalog access, 401 for unauthenticated protected
   endpoints, `409 ProfileNotCreated` before a profile is created, and successful
   create-profile-then-use.
3. **Error mapping**: verify `DomainException` → ProblemDetails with correct status codes.
4. **Response shapes**: verify DTO structure matches expected contracts.
5. **Full workflow**: browse → add to cart → checkout → view order.

**Alternatives considered**: Using SQLite in-memory database — rejected because the project uses
filtered unique indexes that SQLite doesn't support (same decision as Infrastructure tests).

## R6: CORS Policy Configuration

**Decision**: Use `AddCors()` with a named policy that allows any `localhost` origin (any port) and
standard headers/methods. Applied globally in `Program.cs` via `UseCors()`.

**Configuration**:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.SetIsOriginAllowed(origin =>
            new Uri(origin).Host == "localhost")
        .AllowAnyHeader()
        .AllowAnyMethod());
});
```

Only applied in the Development environment.

## R7: Health Check Configuration

**Decision**: Use the built-in ASP.NET Core health checks with EF Core's
`AddDbContextCheck<TalabatDbContext>()` to verify database connectivity. Do **not** add a third-party
health-check package — `AddDbContextCheck` ships with `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
and reuses the existing `TalabatDbContext`.

**Endpoint**: `GET /health` — anonymous, returns `Healthy`/`Unhealthy` status (`200`/`503`).

**Alternatives considered**: `AspNetCore.HealthChecks.SqlServer` — rejected to avoid a new dependency;
a custom health endpoint — rejected because the built-in framework is standard and monitoring-friendly.

## R8: AddApplication() DI Extension Design

**Decision**: Create a static `DependencyInjection` class in `Talabat.Application` with an
`AddApplication()` extension method on `IServiceCollection`. It registers all 15 use-case handlers
as scoped services, plus the `CheckoutDomainService` and `IRestaurantLocalTimeProvider` /`IClock`
implementations.

**Handler registration**: Explicit per-handler registration (not reflection-based scanning),
matching the project's CQRS-lite pattern. This keeps the dependency graph transparent and
avoids assembly-scanning packages.

**Note**: `IClock` and `IRestaurantLocalTimeProvider` implementations live in Infrastructure
(they're infrastructure concerns — system clock, time zone lookup). They're already registered by
`AddInfrastructure()` if the Infrastructure DI extension handles them, or they need to be added.
Review during implementation.
