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

    public DateTime? HandedOverAt { get; private set; }

    public DateTime? RejectedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public CancellationActor? CancelledBy { get; private set; }

    /// <summary>Why the order was rejected or cancelled (design D13); customers see it, never the note.</summary>
    public FulfillmentReasonCode? ReasonCode { get; private set; }

    public string? ReasonNote { get; private set; }

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

    /// <summary>Allowed from <c>Accepted</c> too: instant items skip <c>Preparing</c> (design D2).</summary>
    public void MarkReady(int restaurantId, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        if (Status is not (OrderFulfillmentStatus.Accepted or OrderFulfillmentStatus.Preparing))
        {
            throw new InvalidOrderFulfillmentStatusTransitionException();
        }

        EnsureTimeNotBefore(currentTime, PreparingAt ?? AcceptedAt!.Value);

        Status = OrderFulfillmentStatus.ReadyForPickup;
        ReadyAt = currentTime;
    }

    /// <summary>Entered only by the pickup workflow, never by an actor directly (design D2, D5).</summary>
    public void HandOver(int restaurantId, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        EnsureStatus(OrderFulfillmentStatus.ReadyForPickup);
        EnsureTimeNotBefore(currentTime, ReadyAt!.Value);

        Status = OrderFulfillmentStatus.HandedOver;
        HandedOverAt = currentTime;
    }

    /// <summary>The restaurant declines the order before accepting it (design D2, D13).</summary>
    public void Reject(int restaurantId, FulfillmentReasonCode reasonCode, string? note, DateTime currentTime)
    {
        EnsureRestaurant(restaurantId);
        EnsureStatus(OrderFulfillmentStatus.AwaitingRestaurant);
        EnsureTimeNotBefore(currentTime, PlacedAt);
        var validNote = FulfillmentReasons.Validate(reasonCode, note, FulfillmentReasons.Reject);

        Status = OrderFulfillmentStatus.Rejected;
        RejectedAt = currentTime;
        ReasonCode = reasonCode;
        ReasonNote = validNote;
    }

    /// <summary>
    /// The customer cancels their order; allowed only until the restaurant accepts it, and the reason is optional
    /// (design D7, D13).
    /// </summary>
    public void CancelByCustomer(FulfillmentReasonCode? reasonCode, string? note, DateTime currentTime)
    {
        if (Status != OrderFulfillmentStatus.AwaitingRestaurant)
        {
            throw new OrderNoLongerCancellableException();
        }

        EnsureTimeNotBefore(currentTime, PlacedAt);
        var validNote = FulfillmentReasons.Validate(reasonCode, note, FulfillmentReasons.CustomerCancel);

        Status = OrderFulfillmentStatus.Cancelled;
        CancelledAt = currentTime;
        CancelledBy = CancellationActor.Customer;
        ReasonCode = reasonCode;
        ReasonNote = validNote;
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
