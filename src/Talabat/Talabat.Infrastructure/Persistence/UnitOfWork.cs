using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly TalabatDbContext _dbContext;

    public UnitOfWork(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
