using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents;

/// <summary>
/// Reads the Order Fulfillment behind a delivery. A delivery is created only by acceptance (design D3), so one
/// without a fulfillment is corrupt data: it is reported loudly, naming the order, never as "not found".
/// </summary>
internal static class DeliveryFulfillmentLookup
{
    public static async Task<OrderFulfillment> GetRequiredForDeliveryAsync(
        this IOrderFulfillmentRepository repository,
        Delivery delivery,
        CancellationToken cancellationToken)
    {
        return await repository.GetByOrderIdAsync(delivery.OrderId, cancellationToken)
            ?? throw MissingFulfillment(delivery);
    }

    public static OrderFulfillmentStatus RequiredStatusFor(
        this IReadOnlyDictionary<int, OrderFulfillmentStatus> statusByOrder,
        Delivery delivery)
    {
        return statusByOrder.TryGetValue(delivery.OrderId, out var status)
            ? status
            : throw MissingFulfillment(delivery);
    }

    private static InvalidOperationException MissingFulfillment(Delivery delivery) =>
        new($"Delivery {delivery.Id} has no fulfillment for order {delivery.OrderId}.");
}
