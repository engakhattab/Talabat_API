using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GoOnline;

public sealed class GoOnlineHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public GoOnlineHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<UseCaseResult<bool>> Handle(
        GoOnlineCommand command,
        CancellationToken cancellationToken = default)
    {
        var agentId = command.AgentId;

        var user = await _userRepository.GetByIdAsync(agentId, cancellationToken);

        if (user is null)
        {
            return UseCaseResult<bool>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.UserNotFound,
                    "User was not found."));
        }

        try
        {
            user.GoOnline();
        }
        catch (DomainException ex)
        {
            return UseCaseResult<bool>.Failure(DomainExceptionMapper.Map(ex));
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<bool>.Success(true);
    }
}
