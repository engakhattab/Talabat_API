namespace Talabat.Admin.API.Contracts.Orders;

public sealed record AcceptOrderResponse(int OrderId, string FulfillmentStatus, int DeliveryId, DateTime AcceptedAt);
