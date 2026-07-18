using Talabat.Domain.Aggregates.Users;

namespace Talabat.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(
        int userId,
        CancellationToken ct = default);

    Task<User?> GetByIdReadOnlyAsync(
        int userId,
        CancellationToken ct = default);

    Task<User?> GetByIdWithAddressesAsync(
        int userId,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<User>> GetAvailableAgentsAsync(
        CancellationToken ct = default);

    void Update(User user);
}
