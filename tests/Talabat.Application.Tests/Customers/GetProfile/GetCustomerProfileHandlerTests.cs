using Talabat.Application.Common.Results;
using Talabat.Application.Customers.GetProfile;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Customers.GetProfile;

public sealed class GetCustomerProfileHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCustomerProfileWithAddresses()
    {
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var handler = new GetCustomerProfileHandler(users);

        var result = await handler.Handle(new GetCustomerProfileQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Equal("Customer One", result.Value.FullName);
        Assert.Equal(2, result.Value.Addresses.Count);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenCustomerMissing()
    {
        var handler = new GetCustomerProfileHandler(new FakeUserRepository());

        var result = await handler.Handle(new GetCustomerProfileQuery(404));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.CustomerNotFound, result.Error?.Code);
    }
}
