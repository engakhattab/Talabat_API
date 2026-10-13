using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.ApplicationReview;

public sealed class ListDeliveryAgentApplicationsHandler
{
    private readonly IUserRepository _userRepository;

    public ListDeliveryAgentApplicationsHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<IReadOnlyCollection<DeliveryAgentApplicationReviewDto>> Handle(
        AgentApprovalStatus status,
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetDeliveryAgentApplicationsReadOnlyAsync(status, cancellationToken);
        return users.Select(Map).ToArray();
    }

    internal static DeliveryAgentApplicationReviewDto Map(User user) =>
        new(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.VehicleType,
            user.AgentApprovalStatus!.Value,
            user.CreatedAt);
}
