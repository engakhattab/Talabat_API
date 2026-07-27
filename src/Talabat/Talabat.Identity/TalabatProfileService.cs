using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Identity;

public sealed class TalabatProfileService : IProfileService
{
    private readonly UserManager<User> _userManager;

    public TalabatProfileService(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken cancellationToken = default)
    {
        var subjectId = context.Subject.GetSubjectId();
        var user = await _userManager.FindByIdAsync(subjectId);

        if (user is null || !user.IsActive || user.IsDeleted)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, user.Id.ToString())
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(JwtClaimTypes.Role, role));
        }

        if (user.UserType.HasFlag(UserType.Customer))
        {
            claims.Add(new Claim("customer_id", user.Id.ToString()));
        }

        if (user.UserType.HasFlag(UserType.DeliveryAgent))
        {
            claims.Add(new Claim("delivery_agent_id", user.Id.ToString()));
        }

        context.AddRequestedClaims(claims);
    }

    public async Task IsActiveAsync(IsActiveContext context, CancellationToken cancellationToken = default)
    {
        var subjectId = context.Subject.GetSubjectId();
        var user = await _userManager.FindByIdAsync(subjectId);

        context.IsActive = user is { IsActive: true, IsDeleted: false };
    }
}
