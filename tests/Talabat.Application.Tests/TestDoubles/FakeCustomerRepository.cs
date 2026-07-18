using Talabat.Domain.Aggregates.Customer;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeCustomerRepository : ICustomerRepository
{
    public List<Customer> Customers { get; } = [];

    public int AddCount { get; private set; }

    public int UpdateCount { get; private set; }

    public Task<Customer?> GetByIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Customers.SingleOrDefault(customer => customer.Id == customerId));
    }

    public Task<Customer?> GetByIdWithAddressesAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(customerId, cancellationToken);
    }

    public Task<Customer?> GetByIdentityUserIdAsync(
        string identityUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Customers.SingleOrDefault(customer => customer.IdentityUserId == identityUserId));
    }

    public Task AddAsync(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        AddCount++;
        Customers.Add(customer);
        return Task.CompletedTask;
    }

    public void Update(Customer customer)
    {
        UpdateCount++;

        if (!Customers.Contains(customer))
        {
            Customers.Add(customer);
        }
    }
}
