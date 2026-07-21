using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeDeliveryRepository : IDeliveryRepository
{
    public List<Delivery> Deliveries { get; } = [];

    public Task<Delivery?> GetByIdAsync(int deliveryId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Deliveries.SingleOrDefault(d => d.Id == deliveryId));
    }

    public Task<Delivery?> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Deliveries.SingleOrDefault(d => d.OrderId == orderId));
    }

    public Task<Delivery?> GetActiveByAgentIdAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var active = Deliveries.FirstOrDefault(d =>
            d.AssignedAgentId == agentId && d.IsActive());
        return Task.FromResult(active);
    }

    public Task<IReadOnlyCollection<Delivery>> GetPendingAssignmentAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Delivery> pending = Deliveries
            .Where(d => d.Status == DeliveryStatus.PendingAssignment)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task<IReadOnlyCollection<Delivery>> GetAssignedToAgentAsync(int agentId, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Delivery> assigned = Deliveries
            .Where(d => d.AssignedAgentId == agentId)
            .ToList();
        return Task.FromResult(assigned);
    }

    public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        Deliveries.Add(delivery);
        return Task.CompletedTask;
    }

    public void Update(Delivery delivery)
    {
        // No-op for in-memory testing
    }
}
