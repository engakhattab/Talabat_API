using Talabat.Application.Common.Results;
using Talabat.Application.Customers.SetDefaultAddress;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Customers.SetDefaultAddress;

public sealed class SetDefaultCustomerAddressHandlerTests
{
    [Fact]
    public async Task Handle_SetsOneDefaultAddress()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(TestData.CreateCustomer());
        var handler = new SetDefaultCustomerAddressHandler(customers, new FakeUnitOfWork());

        var result = await handler.Handle(new SetDefaultCustomerAddressCommand(1, 2));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Addresses, address => address.IsDefault);
        Assert.Contains(result.Value.Addresses, address => address.Id == 2 && address.IsDefault);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenAddressMissing()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(TestData.CreateCustomer());
        var handler = new SetDefaultCustomerAddressHandler(customers, new FakeUnitOfWork());

        var result = await handler.Handle(new SetDefaultCustomerAddressCommand(1, 999));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AddressNotFound, result.Error?.Code);
    }
}
