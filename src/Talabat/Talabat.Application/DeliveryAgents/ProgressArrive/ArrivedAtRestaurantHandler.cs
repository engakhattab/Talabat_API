using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.ProgressArrive;

public sealed class ArrivedAtRestaurantHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public ArrivedAtRestaurantHandler(
        IDeliveryRepository deliveryRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<UseCaseResult<int>> Handle(
        ArrivedAtRestaurantCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.HasDeliveryAgentCapability || _currentUser.AgentId is null)
        {
            return UseCaseResult<int>.Failure(
                DomainExceptionMapper.OwnershipMismatch(
                    ApplicationErrorCodes.AgentRequired,
                    "Authenticated delivery agent required."));
        }

        var agentId = _currentUser.AgentId.Value;

        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken);

        if (delivery is null)
        {
            return UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryNotFound,
                    "Delivery was not found."));
        }

        try
        {
            delivery.MarkArrivedAtRestaurant(agentId, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<int>.Success(delivery.Id);
    }
}
