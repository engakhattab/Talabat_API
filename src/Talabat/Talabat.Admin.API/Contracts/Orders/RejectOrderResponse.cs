using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Admin.API.Contracts.Orders;

public sealed record RejectOrderResponse(
    int OrderId,
    string FulfillmentStatus,
    FulfillmentReasonCode ReasonCode,
    DateTime RejectedAt);
