# Data Model & Configuration Schemas: Token, Claims, and Scopes Refinement

## 1. OIDC Identity & API Boundary Architecture

```
                                  ┌───────────────────────────┐
                                  │     Talabat.Identity      │
                                  │ (Duende + ASP.NET Identity)│
                                  └─────────────┬─────────────┘
                                                │
                                    Issues JWT Access Token
                                  (aud, sub, scope, roles, claims)
                                                │
                     ┌──────────────────────────┴──────────────────────────┐
                     ▼                                                     ▼
      ┌───────────────────────────┐                         ┌───────────────────────────┐
      │   Talabat.Customer.API    │                         │   Talabat.Delivery.API    │
      │ (aud: talabat.customer.api│                         │ (aud: talabat.delivery.api│
      │  scope: customer.api)     │                         │  scope: delivery.api)     │
      └───────────────────────────┘                         └───────────────────────────┘
```

---

## 2. Token Claims Data Model

Each issued Access Token is a JSON Web Token (JWT) containing standard OIDC header & payload fields.

### JWT Header
```json
{
  "alg": "RS256",
  "typ": "at+jwt",
  "kid": "<key-identifier>"
}
```

### JWT Payload Schema

| Field / Claim | Type | Source | Description |
|---|---|---|---|
| `iss` | string | `Talabat.Identity` Authority URL | Token issuer URL (e.g. `https://localhost:7237`) |
| `aud` | string / array | Duende `ApiResource` | Intended resource server (`talabat.customer.api` or `talabat.delivery.api`) |
| `sub` | string | `User.Id` | Subject identifier (User's database integer primary key) |
| `client_id` | string | Duende `Client` | Originating OIDC client (`talabat-customer-spa` or `talabat-delivery-spa`) |
| `scope` | string | Allowed Scopes | Granted API scope (`customer.api` or `delivery.api`) |
| `role` | string / array | `UserType` / Identity Roles | User capability roles (`Customer`, `DeliveryAgent`, `Admin`, `RestaurantOwner`) |
| `customer_id` | string (int) | `TalabatProfileService` | Domain Customer Profile ID (equals `User.Id` if Customer capability enabled) |
| `delivery_agent_id` | string (int) | `TalabatProfileService` | Domain Delivery Agent Profile ID (equals `User.Id` if DeliveryAgent capability enabled) |
| `nbf` | number | System Clock | Not Before timestamp (Unix epoch) |
| `exp` | number | System Clock | Expiration timestamp (Unix epoch, 1 hour after issuance) |
| `iat` | number | System Clock | Issued At timestamp (Unix epoch) |

---

## 3. OIDC Configuration Schemas (In-Memory Configuration)

### 3.1 API Resources (Audiences)

```csharp
public static IEnumerable<ApiResource> ApiResources => new ApiResource[]
{
    new ApiResource("talabat.customer.api", "Talabat Customer Business API")
    {
        Scopes = { "customer.api" },
        UserClaims = { JwtClaimTypes.Role, "customer_id" }
    },
    new ApiResource("talabat.delivery.api", "Talabat Delivery Agent Business API")
    {
        Scopes = { "delivery.api" },
        UserClaims = { JwtClaimTypes.Role, "delivery_agent_id" }
    }
};
```

### 3.2 API Scopes

```csharp
public static IEnumerable<ApiScope> ApiScopes => new ApiScope[]
{
    new ApiScope("customer.api", "Customer API Access"),
    new ApiScope("delivery.api", "Delivery Agent API Access")
};
```

### 3.3 Clients (Public SPAs with PKCE)

```csharp
public static IEnumerable<Client> Clients => new Client[]
{
    new Client
    {
        ClientId = "talabat-customer-spa",
        ClientName = "Talabat Customer Single Page Application",
        AllowedGrantTypes = GrantTypes.Code,
        RequirePkce = true,
        RequireClientSecret = false,
        RedirectUris = { "http://localhost:4200/signin-callback" },
        PostLogoutRedirectUris = { "http://localhost:4200/signout-callback" },
        AllowedCorsOrigins = { "http://localhost:4200" },
        AllowedScopes = { IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile, "roles", "customer.api", IdentityServerConstants.StandardScopes.OfflineAccess },
        AllowOfflineAccess = true,
        AccessTokenLifetime = 3600,
        SlidingRefreshTokenLifetime = 1296000
    },
    new Client
    {
        ClientId = "talabat-delivery-spa",
        ClientName = "Talabat Delivery Agent Single Page Application",
        AllowedGrantTypes = GrantTypes.Code,
        RequirePkce = true,
        RequireClientSecret = false,
        RedirectUris = { "http://localhost:4300/signin-callback" },
        PostLogoutRedirectUris = { "http://localhost:4300/signout-callback" },
        AllowedCorsOrigins = { "http://localhost:4300" },
        AllowedScopes = { IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile, "roles", "delivery.api", IdentityServerConstants.StandardScopes.OfflineAccess },
        AllowOfflineAccess = true,
        AccessTokenLifetime = 3600,
        SlidingRefreshTokenLifetime = 1296000
    }
};
```
