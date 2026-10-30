namespace Talabat.Application.Fulfillment.MarkReady;

public sealed record MarkReadyCommand(int OrderId, FulfillmentActor Actor);
