namespace Talabat.Admin.API.Contracts.Orders;

public sealed record StartPreparingResponse(int OrderId, string FulfillmentStatus, DateTime PreparingAt);
