using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityModel;

namespace Talabat.Identity;

public static class IdentityServerConfig
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    new IdentityResource[]
    {
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResource("roles", "User capability roles", new[] { JwtClaimTypes.Role })
    };

    public static IEnumerable<ApiScope> ApiScopes =>
    new ApiScope[]
    {
        new ApiScope("customer.api", "Customer API Access"),
        new ApiScope("delivery.api", "Delivery Agent API Access")
    };

    public static IEnumerable<ApiResource> ApiResources =>
    new ApiResource[]
    {
        new ApiResource("talabat.customer.api", "Talabat Customer Business API")
        {
            Scopes = { "customer.api" },
            UserClaims = { "role", "customer_id" }
        },
        new ApiResource("talabat.delivery.api", "Talabat Delivery Agent Business API")
        {
            Scopes = { "delivery.api" },
            UserClaims = { "role", "delivery_agent_id" }
        }
    };

    public static IEnumerable<Client> Clients =>
    new Client[]
    {
        new Client
        {
            ClientId = "talabat-customer-spa",
            ClientName = "Talabat Customer Single Page Application",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "http://localhost:4200/signin-callback", "https://oauth.pstmn.io/v1/browser-callback" },
            PostLogoutRedirectUris = { "http://localhost:4200/signout-callback" },
            AllowedCorsOrigins = { "http://localhost:4200" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "roles",
                "customer.api",
                IdentityServerConstants.StandardScopes.OfflineAccess
            },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 900,
            UpdateAccessTokenClaimsOnRefresh = true,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 1296000
        },
        new Client
        {
            ClientId = "talabat-delivery-spa",
            ClientName = "Talabat Delivery Agent Single Page Application",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "http://localhost:4300/signin-callback", "https://oauth.pstmn.io/v1/browser-callback" },
            PostLogoutRedirectUris = { "http://localhost:4300/signout-callback" },
            AllowedCorsOrigins = { "http://localhost:4300" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "roles",
                "delivery.api",
                IdentityServerConstants.StandardScopes.OfflineAccess
            },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 900,
            UpdateAccessTokenClaimsOnRefresh = true,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 1296000
        }
    };
}
