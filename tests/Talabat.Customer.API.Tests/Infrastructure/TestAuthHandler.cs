using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace Talabat.Customer.API.Tests.Infrastructure;

#pragma warning disable CS0618 // ISystemClock is obsolete in newer versions but required by AuthenticationHandler base

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "Test";
    public const string SubjectHeader = "X-Test-Subject";
    public const string RolesHeader = "X-Test-Roles";
    public const int TestUserId = 1;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subjectValue = Request.Headers[SubjectHeader].FirstOrDefault()
            ?? TestUserId.ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subjectValue),
            new("sub", subjectValue)
        };

        var rolesValue = Request.Headers[RolesHeader].FirstOrDefault();
        if (!string.IsNullOrEmpty(rolesValue))
        {
            foreach (var role in rolesValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim("role", role));
            }
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme, nameType: null, roleType: "role");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
