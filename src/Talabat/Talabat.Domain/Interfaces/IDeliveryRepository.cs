using Talabat.Domain.Aggregates.DeliveryManagement;

namespace Talabat.Domain.Interfaces;

public interface IDeliveryRepository
{
    Task<Delivery?> GetByIdAsync(
        int deliveryId,
        CancellationToken cancellationToken = default);

    Task<Delivery?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<Delivery?> GetActiveByAgentIdAsync(
        int agentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Delivery>> GetPendingAssignmentAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Delivery>> GetAssignedToAgentAsync(
        int agentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Delivery delivery,
        CancellationToken cancellationToken = default);

    void Update(Delivery delivery);
}
