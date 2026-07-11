using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.Customers.Mapping;
using Talabat.Application.Customers.Models;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Customers.AddAddress;

public sealed class AddCustomerAddressHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IApplicationIdGenerator _idGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public AddCustomerAddressHandler(
        ICustomerRepository customerRepository,
        IApplicationIdGenerator idGenerator,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CustomerProfile>> Handle(
        AddCustomerAddressCommand command,
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
            var address = new Address(
                command.Street,
                command.City,
                command.BuildingNumber,
                command.Floor);

            customer.AddAddress(
                _idGenerator.NewCustomerAddressId(),
                address,
                command.MakeDefault);

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
