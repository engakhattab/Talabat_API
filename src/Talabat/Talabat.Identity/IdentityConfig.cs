using Duende.IdentityServer.Models;

namespace Talabat.Identity;

internal static class IdentityConfig
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        [new IdentityResources.OpenId(), new IdentityResources.Profile()];

    public static IEnumerable<ApiScope> ApiScopes =>
        [new ApiScope("talabat.customer-api"), new ApiScope("talabat.deliveryagent-api")];

    public static IEnumerable<Client> Clients =>
        [];
}
