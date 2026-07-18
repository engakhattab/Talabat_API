using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Customer;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Customers.CreateProfile;

public sealed class CreateCustomerProfileHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerProfileHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<int>> Handle(
        CreateCustomerProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var existing = await _customerRepository.GetByIdentityUserIdAsync(
            command.IdentityUserId,
            cancellationToken);

        if (existing is not null)
        {
            return UseCaseResult<int>.Failure(
                DomainExceptionMapper.Conflict(
                    ApplicationErrorCodes.ProfileAlreadyExists,
                    "A profile already exists for this account."));
        }

        var customer = Customer.CreateForAccount(
            command.IdentityUserId,
            command.FullName,
            command.Age,
            command.PhoneNumber);

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<int>.Success(customer.Id);
    }
}
