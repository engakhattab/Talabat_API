using Duende.IdentityServer.Models;

namespace Talabat.Identity;

internal static class IdentityConfig
{
    public static IEnumerable<IdentityResource> IdentityResources => IdentityServerConfig.IdentityResources;
    public static IEnumerable<ApiScope> ApiScopes => IdentityServerConfig.ApiScopes;
    public static IEnumerable<ApiResource> ApiResources => IdentityServerConfig.ApiResources;
    public static IEnumerable<Client> Clients => IdentityServerConfig.Clients;
}
