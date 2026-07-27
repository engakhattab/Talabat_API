using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetDeliveryHistory;

public sealed class GetDeliveryHistoryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetDeliveryHistoryHandler(
        IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<UseCaseResult<IReadOnlyCollection<DeliveryHistoryDto>>> Handle(
        GetDeliveryHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var agentId = query.AgentId;

        var deliveries = await _deliveryRepository.GetAssignedToAgentAsync(agentId, cancellationToken);

        var dtos = deliveries
            .Select(d => new DeliveryHistoryDto(
                d.Id,
                d.OrderId,
                d.CustomerId,
                d.RestaurantId,
                d.Status,
                d.DeliveryAddress.Street,
                d.DeliveryAddress.City,
                d.DeliveryAddress.BuildingNumber,
                d.DeliveryAddress.Floor,
                d.AssignedAt,
                d.DeliveredAt))
            .ToList()
            .AsReadOnly();

        return UseCaseResult<IReadOnlyCollection<DeliveryHistoryDto>>.Success(dtos);
    }
}
