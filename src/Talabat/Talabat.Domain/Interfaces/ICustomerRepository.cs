using Talabat.Domain.Aggregates.Customer;

namespace Talabat.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task<Customer?> GetByIdWithAddressesAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task<Customer?> GetByIdentityUserIdAsync(
        string identityUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Customer customer,
        CancellationToken cancellationToken = default);

    void Update(Customer customer);
}
