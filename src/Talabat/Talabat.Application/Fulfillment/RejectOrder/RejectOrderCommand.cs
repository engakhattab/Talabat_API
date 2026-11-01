using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Fulfillment.RejectOrder;

public sealed record RejectOrderCommand(
    int OrderId,
    FulfillmentActor Actor,
    FulfillmentReasonCode? ReasonCode,
    string? Note);
