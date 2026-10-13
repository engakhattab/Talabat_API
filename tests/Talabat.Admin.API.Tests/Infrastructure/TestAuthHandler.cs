using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Talabat.Admin.API.Tests.Infrastructure;

#pragma warning disable CS0618
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "Test";
    public const string SubjectHeader = "X-Test-Subject";
    public const string RolesHeader = "X-Test-Roles";
    public const string ScopeHeader = "X-Test-Scope";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subject = Request.Headers[SubjectHeader].FirstOrDefault() ?? "0";
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject), new("sub", subject) };
        foreach (var role in (Request.Headers[RolesHeader].FirstOrDefault() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim("role", role));
        var scope = Request.Headers[ScopeHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(scope)) claims.Add(new Claim("scope", scope));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme, null, "role")), AuthenticationScheme)));
    }
}
