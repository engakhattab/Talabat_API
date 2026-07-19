using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Infrastructure.Identity;

public sealed class TalabatSignInManager : SignInManager<User>
{
    public TalabatSignInManager(
        UserManager<User> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<User> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<User>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<User> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<bool> CanSignInAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsActive || user.IsDeleted)
        {
            return false;
        }

        return await base.CanSignInAsync(user);
    }
}
