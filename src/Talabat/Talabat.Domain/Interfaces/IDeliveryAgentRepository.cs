using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Domain.Interfaces;

public interface IDeliveryAgentRepository
{
    Task<DeliveryAgent?> GetByIdAsync(
        int agentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DeliveryAgent>> GetAvailableAgentsAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        DeliveryAgent deliveryAgent,
        CancellationToken cancellationToken = default);

    void Update(DeliveryAgent deliveryAgent);
}
