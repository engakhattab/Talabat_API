using Talabat.Application.Common.Results;
using Talabat.Application.Customers.RemoveAddress;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Customers.RemoveAddress;

public sealed class RemoveCustomerAddressHandlerTests
{
    [Fact]
    public async Task Handle_RemovingDefaultDoesNotSelectNewDefault()
    {
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var handler = new RemoveCustomerAddressHandler(users, new FakeUnitOfWork());

        var result = await handler.Handle(new RemoveCustomerAddressCommand(1, 1));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Addresses, address => address.IsDefault);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenAddressMissing()
    {
        var users = new FakeUserRepository();
        users.Users.Add(TestData.CreateCustomer());
        var handler = new RemoveCustomerAddressHandler(users, new FakeUnitOfWork());

        var result = await handler.Handle(new RemoveCustomerAddressCommand(1, 999));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AddressNotFound, result.Error?.Code);
    }
}
