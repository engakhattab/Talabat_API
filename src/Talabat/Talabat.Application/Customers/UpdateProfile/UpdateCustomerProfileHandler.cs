using Talabat.Application.Common.Results;
using Talabat.Application.Customers.Mapping;
using Talabat.Application.Customers.Models;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Customers.UpdateProfile;

public sealed class UpdateCustomerProfileHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerProfileHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CustomerProfile>> Handle(
        UpdateCustomerProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(
            command.CustomerId,
            cancellationToken);

        if (user is null)
        {
            return UseCaseResult<CustomerProfile>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.CustomerNotFound,
                    "Customer profile was not found."));
        }

        try
        {
            user.UpdateCustomerProfile(command.FullName, command.Age, command.PhoneNumber);
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<CustomerProfile>.Success(CustomerMapper.ToProfile(user));
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<CustomerProfile>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
