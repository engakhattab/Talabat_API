using Talabat.Application.Common.Results;
using Talabat.Application.Customers.Mapping;
using Talabat.Application.Customers.Models;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Customers.GetProfile;

public sealed class GetCustomerProfileHandler
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomerProfileHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    }

    public async Task<UseCaseResult<CustomerProfile>> Handle(
        GetCustomerProfileQuery query,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdWithAddressesAsync(
            query.CustomerId,
            cancellationToken);

        if (customer is null)
        {
            return UseCaseResult<CustomerProfile>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.CustomerNotFound,
                    "Customer profile was not found."));
        }

        return UseCaseResult<CustomerProfile>.Success(CustomerMapper.ToProfile(customer));
    }
}
