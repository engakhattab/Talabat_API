# Research: Token, Claims, and Scopes Refinement (Phase 9)

## Research 1: Duende IdentityServer Configuration in Talabat.Identity

### Decision
Configure Duende IdentityServer in `Talabat.Identity` using in-memory stores (`AddInMemoryClients`, `AddInMemoryApiScopes`, `AddInMemoryApiResources`, `AddInMemoryIdentityResources`) and register ASP.NET Core Identity integration via `AddAspNetIdentity<User>()` and a custom `AddProfileService<TalabatProfileService>()`.

### Rationale
- Retains the strict **Single DbContext** rule (`TalabatDbContext`) required by the project constitution.
- Avoids introducing Duende's secondary EF Core DbContexts (ConfigurationStore and PersistedGrantStore) into Infrastructure.
- In-memory configuration for clients and API resources is static, reproducible, clean, and easily maintainable in code (`IdentityServerConfig.cs`).

### Alternatives Considered
- *Duende EF Configuration / Operational Store*: Rejected because it requires secondary DbContexts and migrations, violating Constitution Principle 6 & Decided Standards ("one DbContext, one physical database").

---

## Research 2: Token Claims Mapping & Custom Profile Service Strategy

### Decision
Implement `TalabatProfileService` implementing `IProfileService`. It loads the `User` aggregate via `UserManager<User>` using the subject ID (`ctx.Subject`), then emits:
1. `sub`: Integer `User.Id` (standard OIDC subject claim).
2. `role`: Role strings (`Customer`, `DeliveryAgent`, `Admin`, `RestaurantOwner`) retrieved via `UserManager.GetRolesAsync(user)`.
3. `customer_id`: User's integer ID if `UserType` has `Customer` flag enabled.
4. `delivery_agent_id`: User's integer ID if `UserType` has `DeliveryAgent` flag enabled.

### Rationale
- Under the unified `User` aggregate refactor (Constitution Principle 6), `User.Id` is the single primary key for both identity accounts and domain profiles (`CustomerId` / `DeliveryAgentId` FKs point to `AspNetUsers.Id`).
- Emitting `customer_id` and `delivery_agent_id` as claims directly matching `User.Id` allows downstream business APIs (`Talabat.Customer.API` and `Talabat.Delivery.API`) to perform ownership checks using standard claims without querying Identity or touching route parameters.

### Alternatives Considered
- *Custom JWT Generator Controller*: Rejected. Custom JWT generation bypasses OIDC protocol standards and Duende discovery endpoints.
- *Separate Profile Database Lookups*: Rejected. Emitting the IDs directly into the JWT token eliminates redundant DB calls on business API hosts.

---

## Research 3: JWT Bearer Authentication & Audience Isolation in Business APIs

### Decision
Configure `Microsoft.AspNetCore.Authentication.JwtBearer` in both `Talabat.Customer.API` and `Talabat.Delivery.API` pointing to `Talabat.Identity` as `Authority`.
- `Talabat.Customer.API`: Requires `Audience = "talabat.customer.api"` and scope `customer.api`.
- `Talabat.Delivery.API`: Requires `Audience = "talabat.delivery.api"` and scope `delivery.api`.

### Rationale
- Audience isolation ensures a token minted for the Customer SPA cannot be presented to the Delivery API (and vice versa), enforcing strict boundary defense.
- `JwtBearer` middleware handles local signature validation via the Identity host's public JWKS (`/.well-known/openid-configuration/jwks`), adding under 10ms overhead per request without per-request HTTP calls to `Talabat.Identity`.

### Alternatives Considered
- *Introspection / Reference Tokens*: Rejected. Requires a network call from the API to Identity for every request, introducing latency and coupling runtime availability.

---

## Research 4: Public SPA Client Registration & CORS

### Decision
Define two public OIDC client registrations in `IdentityServerConfig.cs`:
1. `talabat-customer-spa`: `GrantTypes.Code`, `RequirePkce = true`, `RequireClientSecret = false`, `AllowedScopes = { "openid", "profile", "roles", "customer.api", "offline_access" }`.
2. `talabat-delivery-spa`: `GrantTypes.Code`, `RequirePkce = true`, `RequireClientSecret = false`, `AllowedScopes = { "openid", "profile", "roles", "delivery.api", "offline_access" }`.

CORS origins are dynamically configured per client (`AllowedCorsOrigins`) and mapped by Duende's CORS middleware.

### Rationale
- Single-Page Applications (SPAs) are public clients and cannot keep secrets safely. PKCE (Proof Key for Code Exchange) protects authorization codes from intercept attacks.
- Exact CORS origin matching prevents unauthorized origins from exchanging codes or accessing tokens.

### Alternatives Considered
- *Resource Owner Password Credentials (ROPC)*: Explicitly forbidden by architecture rules and security best practices.

---

## Research 5: Package Availability and Build Verification

### Decision
Confirm that all required NuGet packages are installed, restored, and build cleanly across all projects:
- `Talabat.Identity`: `Duende.IdentityServer` (8.0.2), `Duende.IdentityServer.AspNetIdentity` (8.0.2).
- `Talabat.Customer.API`: `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.9).
- `Talabat.Delivery.API`: `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.9).

### Rationale
- Verified via `dotnet build d:\link-dev\talabat\src\Talabat\Talabat.slnx` with **0 Errors** and **0 Warnings**.
