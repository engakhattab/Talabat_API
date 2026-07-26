namespace Talabat.Customer.API.Auth;

public static class AuthorizationPolicies
{
    public const string CustomerAccess = nameof(CustomerAccess);
    public const string CustomerScopeOnly = nameof(CustomerScopeOnly);
}
