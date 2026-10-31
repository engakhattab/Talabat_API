using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Admin.API.Contracts.Orders;

/// <summary>A reject reason code (design D13) and an optional note, required when the code is Other.</summary>
public sealed record RejectOrderRequest(FulfillmentReasonCode? ReasonCode, string? Note);
