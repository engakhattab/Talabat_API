using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Ordering.CancelOrder;

public sealed record CancelledOrder(int OrderId, FulfillmentReasonCode? ReasonCode, DateTime CancelledAt);
