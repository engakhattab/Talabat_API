using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Customer.API.Tests.Infrastructure;

public sealed class ThrowingUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _inner;

    public ThrowingUnitOfWork(IUnitOfWork inner)
    {
        _inner = inner;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new ConcurrencyConflictException();
    }
}
