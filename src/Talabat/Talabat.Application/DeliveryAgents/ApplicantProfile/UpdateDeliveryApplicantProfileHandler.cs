using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.ApplicantProfile;

public sealed class UpdateDeliveryApplicantProfileHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDeliveryApplicantProfileHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<DeliveryApplicantProfileDto>> Handle(
        int userId,
        string fullName,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return UseCaseResult<DeliveryApplicantProfileDto>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryAgentApplicationNotFound,
                    "The delivery agent application was not found."));
        }

        try
        {
            user.UpdateDeliveryApplicantProfile(fullName, phoneNumber);
        }
        catch (DomainException exception)
        {
            return UseCaseResult<DeliveryApplicantProfileDto>.Failure(
                DomainExceptionMapper.Map(exception));
        }
        catch (ArgumentException exception)
        {
            return UseCaseResult<DeliveryApplicantProfileDto>.Failure(
                DomainExceptionMapper.Map(exception));
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<DeliveryApplicantProfileDto>.Success(
            new DeliveryApplicantProfileDto(
                user.FullName,
                user.PhoneNumber,
                user.VehicleType,
                user.AgentApprovalStatus!.Value,
                user.DeliveryAgentStatus));
    }
}
