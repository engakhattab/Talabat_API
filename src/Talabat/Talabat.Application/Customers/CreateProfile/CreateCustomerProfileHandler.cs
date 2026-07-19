using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;

namespace Talabat.Application.Customers.CreateProfile;

public sealed class CreateCustomerProfileHandler
{
    private readonly IUserCapabilityService _capabilityService;

    public CreateCustomerProfileHandler(IUserCapabilityService capabilityService)
    {
        _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
    }

    public async Task<UseCaseResult<int>> Handle(
        CreateCustomerProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        return await _capabilityService.GrantCustomerCapabilityAsync(
            command.UserId,
            command.FullName,
            command.Age,
            command.PhoneNumber,
            cancellationToken);
    }
}
