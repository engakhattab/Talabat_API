namespace Talabat.Admin.API.Contracts.Orders;

public sealed record ReleaseAgentResponse(int OrderId, int DeliveryId, int ReleasedAgentId, string DeliveryStatus);
