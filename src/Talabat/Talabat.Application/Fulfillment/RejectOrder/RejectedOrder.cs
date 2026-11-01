using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Fulfillment.RejectOrder;

public sealed record RejectedOrder(int OrderId, FulfillmentReasonCode ReasonCode, DateTime RejectedAt);
