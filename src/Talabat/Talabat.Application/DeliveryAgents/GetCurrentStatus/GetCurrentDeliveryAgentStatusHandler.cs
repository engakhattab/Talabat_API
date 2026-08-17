using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetCurrentStatus;

public sealed class GetCurrentDeliveryAgentStatusHandler
{
    private readonly IUserRepository _userRepository;

    public GetCurrentDeliveryAgentStatusHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UseCaseResult<DeliveryAgentStatusDto>> Handle(
        GetCurrentDeliveryAgentStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdReadOnlyAsync(query.AgentId, cancellationToken);
        if (user is null)
        {
            return UseCaseResult<DeliveryAgentStatusDto>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.UserNotFound,
                    "User was not found."));
        }

        if (!user.UserType.HasFlag(UserType.DeliveryAgent) ||
            user.DeliveryAgentStatus is not DeliveryAgentStatus status ||
            !Enum.IsDefined(status))
        {
            return UseCaseResult<DeliveryAgentStatusDto>.Failure(
                DomainExceptionMapper.Map(new DeliveryAgentNotInitializedException()));
        }

        return UseCaseResult<DeliveryAgentStatusDto>.Success(new DeliveryAgentStatusDto(status));
    }
}
