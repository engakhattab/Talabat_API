using Talabat.Domain.Common.Abstractions;

namespace Talabat.Domain.Aggregates.RestaurantOperations;

/// <summary>
/// The restaurant's handling of one order (ADR-0001): from awaiting the restaurant's decision through
/// acceptance, preparation and readiness to hand-over, rejection or cancellation.
/// </summary>
public sealed class OrderFulfillment : AuditableEntity
{
    public int Id { get; private set; }

    public int OrderId { get; private set; }

    public int RestaurantId { get; private set; }

    public OrderFulfillmentStatus Status { get; private set; }

    public DateTime PlacedAt { get; private set; }

    public DateTime AcceptanceDeadline { get; private set; }

    public DateTime? AcceptedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    private OrderFulfillment()
    {
    }

    public OrderFulfillment(
        int orderId,
        int restaurantId,
        DateTime placedAt,
        TimeSpan acceptanceWindow)
    {
        throw new NotImplementedException();
    }

    public void Accept(int restaurantId, DateTime currentTime)
    {
        throw new NotImplementedException();
    }
}
