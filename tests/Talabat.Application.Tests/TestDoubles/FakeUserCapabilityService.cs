using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeUserCapabilityService : IUserCapabilityService
{
    public int NextId { get; set; } = 1;
    public List<User> RegisteredUsers { get; } = [];

    public Task<UseCaseResult<int>> RegisterCustomerAsync(
        string email, string password, string fullName, int age, string? phoneNumber,
        CancellationToken ct = default)
    {
        var user = User.Register(email, email, fullName);
        user.InitializeCustomerProfile(fullName, age, phoneNumber);
        RegisteredUsers.Add(user);
        return Task.FromResult(UseCaseResult<int>.Success(NextId++));
    }

    public Task<UseCaseResult<int>> RegisterDeliveryAgentApplicantAsync(
        string email, string password, string fullName, VehicleType vehicleType, string? phoneNumber,
        CancellationToken ct = default)
    {
        var user = User.Register(email, email, fullName);
        user.SubmitDeliveryAgentApplication(vehicleType);
        RegisteredUsers.Add(user);
        return Task.FromResult(UseCaseResult<int>.Success(NextId++));
    }

    public Task<UseCaseResult<int>> GrantCustomerCapabilityAsync(
        int userId, string fullName, int age, string? phoneNumber,
        CancellationToken ct = default)
    {
        var user = RegisteredUsers.FirstOrDefault(u => u.Id == userId);
        if (user is null)
            return Task.FromResult(UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.UserNotFound, "User not found.")));
        if (user.UserType.HasFlag(UserType.Customer))
            return Task.FromResult(UseCaseResult<int>.Failure(
                DomainExceptionMapper.Conflict(ApplicationErrorCodes.ProfileAlreadyExists, "A profile already exists for this account.")));
        user.InitializeCustomerProfile(fullName, age, phoneNumber);
        return Task.FromResult(UseCaseResult<int>.Success(userId));
    }

    public Task<UseCaseResult<int>> ApproveDeliveryAgentAsync(int userId, CancellationToken ct = default)
    {
        var user = RegisteredUsers.FirstOrDefault(u => u.Id == userId);
        if (user is null)
            return Task.FromResult(UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.UserNotFound, "User not found.")));
        user.ApproveDeliveryAgentApplication();
        return Task.FromResult(UseCaseResult<int>.Success(userId));
    }

    public Task<UseCaseResult<int>> RejectDeliveryAgentAsync(int userId, CancellationToken ct = default)
    {
        var user = RegisteredUsers.FirstOrDefault(u => u.Id == userId);
        if (user is null)
            return Task.FromResult(UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.UserNotFound, "User not found.")));
        user.RejectDeliveryAgentApplication();
        return Task.FromResult(UseCaseResult<int>.Success(userId));
    }

    public Task<UseCaseResult<int>> DeactivateUserAsync(int userId, CancellationToken ct = default)
    {
        var user = RegisteredUsers.FirstOrDefault(u => u.Id == userId);
        if (user is null)
            return Task.FromResult(UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.UserNotFound, "User not found.")));
        user.Deactivate();
        return Task.FromResult(UseCaseResult<int>.Success(userId));
    }
}
