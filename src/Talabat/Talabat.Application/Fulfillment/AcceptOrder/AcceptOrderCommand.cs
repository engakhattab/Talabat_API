namespace Talabat.Application.Fulfillment.AcceptOrder;

public sealed record AcceptOrderCommand(int OrderId, FulfillmentActor Actor);
