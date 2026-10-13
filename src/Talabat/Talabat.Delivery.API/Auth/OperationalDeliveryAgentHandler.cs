using Microsoft.AspNetCore.Authorization;
using Talabat.Application.Abstractions;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Interfaces;

namespace Talabat.Delivery.API.Auth;

public sealed class OperationalDeliveryAgentHandler : AuthorizationHandler<OperationalDeliveryAgentRequirement>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;

    public OperationalDeliveryAgentHandler(ICurrentUser currentUser, IUserRepository userRepository)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationalDeliveryAgentRequirement requirement)
    {
        if (_currentUser.AgentId is not int agentId)
        {
            return;
        }

        var user = await _userRepository.GetByIdReadOnlyAsync(agentId);
        if (user?.DeliveryAgentStatus is DeliveryAgentStatus status &&
            Enum.IsDefined(status) &&
            status != DeliveryAgentStatus.Suspended)
        {
            context.Succeed(requirement);
        }
    }
}
