using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Ordering.Status;

/// <summary>
/// What a customer sees of an order's progress (design D9). <see cref="CancelledBy"/> and <see cref="ReasonCode"/>
/// are set only when the order ended early; the free-text note is never part of it.
/// </summary>
public sealed record CustomerOrderProgress(
    CustomerOrderStatus Status,
    bool DeliveryAgentAssigned,
    CancellationActor? CancelledBy = null,
    FulfillmentReasonCode? ReasonCode = null);
