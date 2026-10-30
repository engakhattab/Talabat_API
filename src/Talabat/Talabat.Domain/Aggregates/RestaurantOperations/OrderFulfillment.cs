using Talabat.Domain.Common;
using Talabat.Domain.Common.Abstractions;
using Talabat.Domain.Exceptions;

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

    public DateTime? PreparingAt { get; private set; }

    public DateTime? ReadyAt { get; private set; }

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
        if (acceptanceWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acceptanceWindow),
                acceptanceWindow,
                $"{nameof(acceptanceWindow)} must be greater than zero.");
        }

        OrderId = Guard.Positive(orderId, nameof(orderId));
        RestaurantId = Guard.Positive(restaurantId, nameof(restaurantId));
        PlacedAt = Guard.Utc(placedAt, nameof(placedAt));
        AcceptanceDeadline = placedAt.Add(acceptanceWindow);
        CreatedAt = placedAt;
        Status = OrderFulfillmentStatus.AwaitingRestaurant;
    }

    public void Accept(int restaurantId, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        EnsureStatus(OrderFulfillmentStatus.AwaitingRestaurant);
        EnsureTimeNotBefore(currentTime, PlacedAt);

        Status = OrderFulfillmentStatus.Accepted;
        AcceptedAt = currentTime;
    }

    public void StartPreparing(int restaurantId, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        EnsureStatus(OrderFulfillmentStatus.Accepted);
        EnsureTimeNotBefore(currentTime, AcceptedAt!.Value);

        Status = OrderFulfillmentStatus.Preparing;
        PreparingAt = currentTime;
    }

    public void MarkReady(int restaurantId, DateTime currentTime)
    {
        throw new NotImplementedException();
    }

    private void EnsureRestaurant(int restaurantId)
    {
        restaurantId = Guard.Positive(restaurantId, nameof(restaurantId));

        if (restaurantId != RestaurantId)
        {
            throw new OrderFulfillmentRestaurantMismatchException();
        }
    }

    private void EnsureStatus(OrderFulfillmentStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOrderFulfillmentStatusTransitionException();
        }
    }

    private static void EnsureTimeNotBefore(DateTime currentTime, DateTime previousTime)
    {
        Guard.Utc(currentTime, nameof(currentTime));

        // SQL Server datetime2 does not retain DateTimeKind: persisted UTC values materialize as
        // Unspecified and are restored to UTC before applying the ordering invariant.
        if (previousTime.Kind == DateTimeKind.Unspecified)
        {
            previousTime = DateTime.SpecifyKind(previousTime, DateTimeKind.Utc);
        }

        if (currentTime < previousTime)
        {
            throw new InvalidOrderFulfillmentTimestampException();
        }
    }
}
