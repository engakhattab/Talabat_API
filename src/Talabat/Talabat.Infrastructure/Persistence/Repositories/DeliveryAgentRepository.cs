using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class DeliveryAgentRepository : IDeliveryAgentRepository
{
    private readonly TalabatDbContext _dbContext;

    public DeliveryAgentRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<DeliveryAgent?> GetByIdAsync(
        int agentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DeliveryAgents
            .SingleOrDefaultAsync(agent => agent.Id == agentId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeliveryAgent>> GetAvailableAgentsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeliveryAgents
            .AsNoTracking()
            .Where(agent => agent.Status == DeliveryAgentStatus.Available)
            .OrderBy(agent => agent.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        DeliveryAgent deliveryAgent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deliveryAgent);
        await _dbContext.DeliveryAgents.AddAsync(deliveryAgent, cancellationToken);
    }

    public void Update(DeliveryAgent deliveryAgent)
    {
        ArgumentNullException.ThrowIfNull(deliveryAgent);
        _dbContext.DeliveryAgents.Update(deliveryAgent);
    }
}
