namespace Talabat.Application.Fulfillment.StartPreparing;

public sealed record StartPreparingCommand(int OrderId, FulfillmentActor Actor);
