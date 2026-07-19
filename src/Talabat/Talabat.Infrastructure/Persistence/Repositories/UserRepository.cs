using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly TalabatDbContext _dbContext;

    public UserRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<User?> GetByIdReadOnlyAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<User?> GetByIdWithAddressesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include("_addresses")
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> GetAvailableAgentsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.DeliveryAgentStatus == DeliveryAgentStatus.Available)
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);
    }

    public void Update(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _dbContext.Users.Update(user);
    }
}
