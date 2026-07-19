using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public int UpdateCount { get; private set; }

    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Users.SingleOrDefault(user => user.Id == userId));
    }

    public Task<User?> GetByIdReadOnlyAsync(int userId, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(userId, cancellationToken);
    }

    public Task<User?> GetByIdWithAddressesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyCollection<User>> GetAvailableAgentsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<User> available = Users
            .Where(u => u.IsActive && u.DeliveryAgentStatus == DeliveryAgentStatus.Available)
            .OrderBy(u => u.FullName)
            .ToList();
        return Task.FromResult(available);
    }

    public void Update(User user)
    {
        UpdateCount++;
        if (!Users.Contains(user))
        {
            Users.Add(user);
        }
    }
}
