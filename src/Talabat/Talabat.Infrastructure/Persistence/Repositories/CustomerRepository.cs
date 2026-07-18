using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Customer;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly TalabatDbContext _dbContext;

    public CustomerRepository(TalabatDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<Customer?> GetByIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);
    }

    public Task<Customer?> GetByIdWithAddressesAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers
            .Include("_addresses")
            .SingleOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);
    }

    public Task<Customer?> GetByIdentityUserIdAsync(
        string identityUserId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(customer => customer.IdentityUserId == identityUserId, cancellationToken);
    }

    public async Task AddAsync(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public void Update(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        _dbContext.Customers.Update(customer);
    }
}
