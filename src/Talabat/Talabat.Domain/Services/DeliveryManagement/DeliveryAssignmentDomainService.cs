using Talabat.Domain.DeliveryManagement;
using Talabat.Domain.Exceptions;

namespace Talabat.Domain.Services.DeliveryManagement;

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

        delivery.MarkDelivered(agent.Id, currentTime);
        agent.MarkAvailable();
    }
}
