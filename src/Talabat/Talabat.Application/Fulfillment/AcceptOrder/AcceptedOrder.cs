namespace Talabat.Application.Fulfillment.AcceptOrder;

public sealed record AcceptedOrder(int OrderId, int DeliveryId, DateTime AcceptedAt);
