using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly TalabatDbContext _dbContext;

    public DeliveryRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<Delivery?> GetByIdAsync(
        int deliveryId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Deliveries
            .SingleOrDefaultAsync(delivery => delivery.Id == deliveryId, cancellationToken);
    }

    public Task<Delivery?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Deliveries
            .SingleOrDefaultAsync(delivery => delivery.OrderId == orderId, cancellationToken);
    }

    public Task<Delivery?> GetActiveByAgentIdAsync(
        int agentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Deliveries
            .SingleOrDefaultAsync(
                delivery => delivery.AssignedAgentId == agentId
                    && (delivery.Status == DeliveryStatus.Assigned
                        || delivery.Status == DeliveryStatus.ArrivedAtRestaurant
                        || delivery.Status == DeliveryStatus.PickedUp
                        || delivery.Status == DeliveryStatus.OutForDelivery),
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<Delivery>> GetPendingAssignmentAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Deliveries
            .AsNoTracking()
            .Where(delivery => delivery.Status == DeliveryStatus.PendingAssignment)
            .OrderBy(delivery => delivery.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Delivery>> GetAssignedToAgentAsync(
        int agentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Deliveries
            .AsNoTracking()
            .Where(delivery => delivery.AssignedAgentId == agentId)
            .OrderByDescending(delivery => delivery.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Delivery delivery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await _dbContext.Deliveries.AddAsync(delivery, cancellationToken);
    }

    public void Update(Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        _dbContext.Deliveries.Update(delivery);
    }
}
