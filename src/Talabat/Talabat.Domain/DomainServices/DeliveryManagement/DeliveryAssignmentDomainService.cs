using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Exceptions;

namespace Talabat.Domain.DomainServices.DeliveryManagement;

public sealed class DeliveryAssignmentDomainService
{
    public void Assign(
        Delivery delivery,
        DeliveryAgent agent,
        DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(agent);

        if (!agent.IsAvailable())
        {
            throw new AgentNotAvailableException();
        }

        delivery.AssignAgent(agent.Id, currentTime);
        agent.MarkBusy();
    }

    public void CompleteDelivery(
        Delivery delivery,
        DeliveryAgent agent,
        DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(agent);

        EnsureAssignedBusyAgent(delivery, agent);
        delivery.MarkDelivered(agent.Id, currentTime);
        agent.MarkAvailable();
    }

    public void CancelDelivery(
        Delivery delivery,
        DeliveryAgent agent,
        DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(agent);

        EnsureAssignedBusyAgent(delivery, agent);
        delivery.CancelAssigned(agent.Id, currentTime);
        agent.MarkAvailable();
    }

    public void FailDelivery(
        Delivery delivery,
        DeliveryAgent agent,
        string reason,
        DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(agent);

        EnsureAssignedBusyAgent(delivery, agent);
        delivery.FailAssigned(agent.Id, reason, currentTime);
        agent.MarkAvailable();
    }

    private static void EnsureAssignedBusyAgent(Delivery delivery, DeliveryAgent agent)
    {
        if (!delivery.AssignedAgentId.HasValue)
        {
            throw new DeliveryNotAssignedException();
        }

        if (delivery.AssignedAgentId.Value != agent.Id)
        {
            throw new DeliveryAgentMismatchException();
        }

        if (agent.Status != DeliveryAgentStatus.Busy)
        {
            throw new AgentNotAvailableException();
        }
    }
}
