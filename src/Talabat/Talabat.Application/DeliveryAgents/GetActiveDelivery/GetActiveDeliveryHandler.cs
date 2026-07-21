using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetActiveDelivery;

public sealed class GetActiveDeliveryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ICurrentUser _currentUser;

    public GetActiveDeliveryHandler(
        IDeliveryRepository deliveryRepository,
        ICurrentUser currentUser)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<UseCaseResult<ActiveDeliveryDto>> Handle(
        GetActiveDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.HasDeliveryAgentCapability || _currentUser.AgentId is null)
        {
            return UseCaseResult<ActiveDeliveryDto>.Failure(
                DomainExceptionMapper.OwnershipMismatch(
                    ApplicationErrorCodes.AgentRequired,
                    "Authenticated delivery agent required."));
        }

        var agentId = _currentUser.AgentId.Value;

        var delivery = await _deliveryRepository.GetActiveByAgentIdAsync(agentId, cancellationToken);

        if (delivery is null)
        {
            return UseCaseResult<ActiveDeliveryDto>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryNotFound,
                    "No active delivery found for this agent."));
        }

        var dto = new ActiveDeliveryDto(
            delivery.Id,
            delivery.OrderId,
            delivery.CustomerId,
            delivery.RestaurantId,
            delivery.Status,
            delivery.DeliveryAddress.Street,
            delivery.DeliveryAddress.City,
            delivery.DeliveryAddress.BuildingNumber,
            delivery.DeliveryAddress.Floor,
            delivery.AssignedAt);

        return UseCaseResult<ActiveDeliveryDto>.Success(dto);
    }
}
