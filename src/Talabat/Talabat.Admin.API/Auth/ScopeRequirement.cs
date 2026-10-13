using Microsoft.AspNetCore.Authorization;

namespace Talabat.Admin.API.Auth;

public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
