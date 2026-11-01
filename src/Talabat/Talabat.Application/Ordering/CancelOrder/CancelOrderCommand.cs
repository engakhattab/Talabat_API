using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Ordering.CancelOrder;

public sealed record CancelOrderCommand(
    int CustomerId,
    int OrderId,
    FulfillmentReasonCode? ReasonCode,
    string? Note);
