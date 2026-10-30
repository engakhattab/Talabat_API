namespace Talabat.Admin.API.Contracts.Orders;

public sealed record MarkReadyResponse(int OrderId, string FulfillmentStatus, DateTime ReadyAt);
