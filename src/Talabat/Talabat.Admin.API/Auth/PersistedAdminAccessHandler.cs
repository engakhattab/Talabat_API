using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Admin.API.Auth;

public sealed class PersistedAdminAccessHandler : AuthorizationHandler<AdminAccessRequirement>
{
    private readonly TalabatDbContext _dbContext;

    public PersistedAdminAccessHandler(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminAccessRequirement requirement)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!int.TryParse(subject, out var userId) || userId <= 0)
        {
            return;
        }

        var userType = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && !user.IsDeleted)
            .Select(user => (UserType?)user.UserType)
            .SingleOrDefaultAsync();

        if (userType?.HasFlag(UserType.Admin) == true)
        {
            context.Succeed(requirement);
        }
    }
}
