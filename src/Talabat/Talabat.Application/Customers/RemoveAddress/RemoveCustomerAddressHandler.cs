using Talabat.Application.Common.Results;
using Talabat.Application.Customers.Mapping;
using Talabat.Application.Customers.Models;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Customers.RemoveAddress;

public sealed class RemoveCustomerAddressHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveCustomerAddressHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CustomerProfile>> Handle(
        RemoveCustomerAddressCommand command,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdWithAddressesAsync(
            command.CustomerId,
            cancellationToken);

        if (customer is null)
        {
            return UseCaseResult<CustomerProfile>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.CustomerNotFound,
                    "Customer profile was not found."));
        }

        try
        {
            customer.RemoveAddress(command.AddressId);
            _customerRepository.Update(customer);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<CustomerProfile>.Success(CustomerMapper.ToProfile(customer));
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<CustomerProfile>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
