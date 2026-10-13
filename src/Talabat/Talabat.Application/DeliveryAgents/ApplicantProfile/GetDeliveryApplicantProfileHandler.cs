using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.ApplicantProfile;

public sealed class GetDeliveryApplicantProfileHandler
{
    private readonly IUserRepository _userRepository;

    public GetDeliveryApplicantProfileHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UseCaseResult<DeliveryApplicantProfileDto>> Handle(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdReadOnlyAsync(userId, cancellationToken);
        if (user?.AgentApprovalStatus is null)
        {
            return UseCaseResult<DeliveryApplicantProfileDto>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryAgentApplicationNotFound,
                    "The delivery agent application was not found."));
        }

        return UseCaseResult<DeliveryApplicantProfileDto>.Success(
            new DeliveryApplicantProfileDto(
                user.FullName,
                user.PhoneNumber,
                user.VehicleType,
                user.AgentApprovalStatus.Value,
                user.DeliveryAgentStatus));
    }
}
