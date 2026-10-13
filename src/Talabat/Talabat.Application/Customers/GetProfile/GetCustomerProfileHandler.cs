using Talabat.Application.Common.Results;
using Talabat.Application.Customers.Mapping;
using Talabat.Application.Customers.Models;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Customers.GetProfile;

public sealed class GetCustomerProfileHandler
{
    private readonly IUserRepository _userRepository;

    public GetCustomerProfileHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UseCaseResult<CustomerProfile>> Handle(
        GetCustomerProfileQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithAddressesAsync(
            query.CustomerId,
            cancellationToken);

        if (user is null)
        {
            return UseCaseResult<CustomerProfile>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.CustomerNotFound,
                    "Customer profile was not found."));
        }

        return UseCaseResult<CustomerProfile>.Success(CustomerMapper.ToProfile(user));
    }
}
