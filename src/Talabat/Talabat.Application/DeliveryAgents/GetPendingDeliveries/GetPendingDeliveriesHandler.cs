using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetPendingDeliveries;

public sealed class GetPendingDeliveriesHandler
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetPendingDeliveriesHandler(
        IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>> Handle(
        GetPendingDeliveriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var deliveries = await _deliveryRepository.GetPendingAssignmentAsync(cancellationToken);

        var dtos = deliveries
            .Select(d => new PendingDeliveryDto(
                d.Id,
                d.OrderId,
                d.RestaurantId,
                d.Status,
                d.DeliveryAddress.City,
                d.CreatedAt))
            .ToList()
            .AsReadOnly();

        return UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>.Success(dtos);
    }
}
