using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetPendingDeliveries;

public sealed class GetPendingDeliveriesHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;

    public GetPendingDeliveriesHandler(
        IDeliveryRepository deliveryRepository,
        IOrderFulfillmentRepository fulfillmentRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
    }

    public async Task<UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>> Handle(
        GetPendingDeliveriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var deliveries = await _deliveryRepository.GetPendingAssignmentAsync(cancellationToken);

        // Delivery agents see the order fulfillment status before they claim (D5); one read for the whole page.
        var fulfillments = await _fulfillmentRepository.GetByOrderIdsAsync(
            deliveries.Select(d => d.OrderId).ToList(),
            cancellationToken);
        IReadOnlyDictionary<int, OrderFulfillmentStatus> fulfillmentStatusByOrder =
            fulfillments.ToDictionary(f => f.OrderId, f => f.Status);

        var dtos = deliveries
            .Select(d => new PendingDeliveryDto(
                d.Id,
                d.OrderId,
                d.RestaurantId,
                d.Status,
                d.RestaurantName,
                d.RestaurantPickupAddress.Street,
                d.RestaurantPickupAddress.City,
                d.CreatedAt,
                fulfillmentStatusByOrder.RequiredStatusFor(d)))
            .ToList()
            .AsReadOnly();

        return UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>.Success(dtos);
    }
}
