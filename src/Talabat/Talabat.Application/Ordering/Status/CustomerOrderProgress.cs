namespace Talabat.Application.Ordering.Status;

public sealed record CustomerOrderProgress(
    CustomerOrderStatus Status,
    bool DeliveryAgentAssigned);
