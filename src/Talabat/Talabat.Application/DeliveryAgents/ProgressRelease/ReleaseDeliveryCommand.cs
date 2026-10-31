namespace Talabat.Application.DeliveryAgents.ProgressRelease;

/// <summary>
/// Who releases a claimed delivery (design D6): the assigned agent, or an Admin on behalf of an unresponsive agent.
/// </summary>
public abstract record ReleaseDeliveryCommand
{
    private ReleaseDeliveryCommand()
    {
    }

    /// <summary>The agent releases one of their own deliveries; anyone else's is reported as not found.</summary>
    public sealed record ByAgent(int DeliveryId, int AgentId) : ReleaseDeliveryCommand;

    /// <summary>An Admin releases the delivery of an order, whichever agent holds it.</summary>
    public sealed record ByAdmin(int OrderId) : ReleaseDeliveryCommand;
}

public sealed record ReleasedDelivery(int DeliveryId, int OrderId, int ReleasedAgentId);
