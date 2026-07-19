using Talabat.Application.Common.Results;
using Talabat.Application.Customers.CreateProfile;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Tests.Customers.CreateProfile;

public sealed class CreateCustomerProfileHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesWithCorrectArguments()
    {
        var fakeCapabilityService = new FakeUserCapabilityService();
        var user = User.Register("user42", "user42@test.com", "Test User");
        TestIds.SetId(user, 42);
        fakeCapabilityService.RegisteredUsers.Add(user);

        var handler = new CreateCustomerProfileHandler(fakeCapabilityService);

        await handler.Handle(new CreateCustomerProfileCommand(
            UserId: 42, FullName: "Test User", Age: 30, PhoneNumber: "+201000000000"));

        Assert.Single(fakeCapabilityService.RegisteredUsers);
        Assert.Equal("Test User", user.FullName);
        Assert.Equal(30, user.Age);
        Assert.Equal("+201000000000", user.PhoneNumber);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithUserId()
    {
        var fakeCapabilityService = new FakeUserCapabilityService();
        var user = User.Register("user42", "user42@test.com", "Test User");
        TestIds.SetId(user, 42);
        fakeCapabilityService.RegisteredUsers.Add(user);

        var handler = new CreateCustomerProfileHandler(fakeCapabilityService);

        var result = await handler.Handle(new CreateCustomerProfileCommand(
            UserId: 42, FullName: "Test User", Age: 30, PhoneNumber: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task Handle_PropagatesProfileAlreadyExists()
    {
        var fakeCapabilityService = new FakeUserCapabilityService();
        var existingUser = User.Register("existing", "existing@test.com", "Existing");
        existingUser.InitializeCustomerProfile("Existing", 30, null);
        TestIds.SetId(existingUser, 1);
        fakeCapabilityService.RegisteredUsers.Add(existingUser);

        var handler = new CreateCustomerProfileHandler(fakeCapabilityService);

        var result = await handler.Handle(new CreateCustomerProfileCommand(
            UserId: 1, FullName: "Existing", Age: 30, PhoneNumber: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.ProfileAlreadyExists, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_PropagatesUserNotFound()
    {
        var fakeCapabilityService = new FakeUserCapabilityService();
        var handler = new CreateCustomerProfileHandler(fakeCapabilityService);

        var result = await handler.Handle(new CreateCustomerProfileCommand(
            UserId: 999, FullName: "Ghost", Age: 25, PhoneNumber: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ForwardsAllArgumentsExactly()
    {
        var fakeCapabilityService = new FakeUserCapabilityService();
        var user = User.Register("user7", "user7@test.com", "Old Name");
        TestIds.SetId(user, 7);
        fakeCapabilityService.RegisteredUsers.Add(user);

        var handler = new CreateCustomerProfileHandler(fakeCapabilityService);

        var result = await handler.Handle(new CreateCustomerProfileCommand(
            UserId: 7, FullName: "  Alice  ", Age: 22, PhoneNumber: "+966500000000"));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
        Assert.Equal("Alice", user.FullName);
        Assert.Equal(22, user.Age);
        Assert.Equal("+966500000000", user.PhoneNumber);
    }
}
