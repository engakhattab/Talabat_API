using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.ApplicationReview;

public sealed class GetDeliveryAgentApplicationHandler
{
    private readonly IUserRepository _userRepository;

    public GetDeliveryAgentApplicationHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UseCaseResult<DeliveryAgentApplicationReviewDto>> Handle(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdReadOnlyAsync(userId, cancellationToken);
        if (user?.AgentApprovalStatus is null)
        {
            return UseCaseResult<DeliveryAgentApplicationReviewDto>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryAgentApplicationNotFound,
                    "The delivery agent application was not found."));
        }

        return UseCaseResult<DeliveryAgentApplicationReviewDto>.Success(
            ListDeliveryAgentApplicationsHandler.Map(user));
    }
}
