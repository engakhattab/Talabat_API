namespace Talabat.Application.DeliveryAgents.ProgressFail;

public sealed record FailDeliveryCommand(int DeliveryId, string Reason);
