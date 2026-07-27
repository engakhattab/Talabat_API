using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetActiveDelivery;

public sealed class GetActiveDeliveryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetActiveDeliveryHandler(
        IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<UseCaseResult<ActiveDeliveryDto>> Handle(
        GetActiveDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var agentId = query.AgentId;

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
