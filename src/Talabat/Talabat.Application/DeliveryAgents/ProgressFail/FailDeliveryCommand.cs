namespace Talabat.Application.DeliveryAgents.ProgressFail;

public sealed record FailDeliveryCommand(int DeliveryId, int AgentId, string Reason);
