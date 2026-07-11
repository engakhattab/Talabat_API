using Talabat.Application.Common.Results;
using Talabat.Application.Customers.Mapping;
using Talabat.Application.Customers.Models;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Customers.UpdateProfile;

public sealed class UpdateCustomerProfileHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerProfileHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CustomerProfile>> Handle(
        UpdateCustomerProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(
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
            customer.UpdateProfile(command.FullName, command.Age, command.PhoneNumber);
            _customerRepository.Update(customer);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<CustomerProfile>.Success(CustomerMapper.ToProfile(customer));
        }
        catch (ArgumentException exception)
        {
            return UseCaseResult<CustomerProfile>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
