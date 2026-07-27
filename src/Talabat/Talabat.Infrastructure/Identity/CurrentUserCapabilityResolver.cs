using Microsoft.EntityFrameworkCore;
using Talabat.Application.Abstractions;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Infrastructure.Identity;

public sealed class CurrentUserCapabilityResolver : ICurrentUserCapabilityResolver
{
    private readonly TalabatDbContext _dbContext;

    public CurrentUserCapabilityResolver(TalabatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserType> GetUserTypeAsync(int userId, CancellationToken ct = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.UserType)
            .FirstOrDefaultAsync(ct);
    }
}
