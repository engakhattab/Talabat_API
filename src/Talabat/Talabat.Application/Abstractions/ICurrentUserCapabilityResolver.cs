using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Abstractions;

public interface ICurrentUserCapabilityResolver
{
    Task<UserType> GetUserTypeAsync(int userId, CancellationToken ct = default);
}
