using Talabat.Application.Common.Results;
using Talabat.Application.Customers.UpdateProfile;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Customers.UpdateProfile;

public sealed class UpdateCustomerProfileHandlerTests
{
    [Fact]
    public async Task Handle_TrimsRequiredFullNameAndAllowsOptionalPhone()
    {
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCustomerProfileHandler(users, unitOfWork);

        var result = await handler.Handle(
            new UpdateCustomerProfileCommand(1, "  New Name  ", 31, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.FullName);
        Assert.Null(result.Value.PhoneNumber);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(" ", 31)]
    [InlineData("Valid Name", 0)]
    public async Task Handle_ReturnsInvalidProfileForRequiredRuleFailures(
        string fullName,
        int age)
    {
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var handler = new UpdateCustomerProfileHandler(users, new FakeUnitOfWork());

        var result = await handler.Handle(
            new UpdateCustomerProfileCommand(1, fullName, age, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.InvalidCustomerProfile, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenCustomerMissing()
    {
        var handler = new UpdateCustomerProfileHandler(
            new FakeUserRepository(),
            new FakeUnitOfWork());

        var result = await handler.Handle(
            new UpdateCustomerProfileCommand(404, "Name", 30, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CustomerNotFound, result.Error?.Code);
    }
}
