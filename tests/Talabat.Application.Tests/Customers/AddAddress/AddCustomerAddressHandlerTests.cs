using Talabat.Application.Common.Results;
using Talabat.Application.Customers.AddAddress;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Customers.AddAddress;

public sealed class AddCustomerAddressHandlerTests
{
    [Fact]
    public async Task Handle_AddsAddressAndPreservesOneDefault()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(TestData.CreateCustomer());
        var handler = new AddCustomerAddressHandler(
            customers,
            new FakeApplicationIdGenerator(),
            new FakeUnitOfWork());

        var result = await handler.Handle(
            new AddCustomerAddressCommand(1, "Third", "Cairo", "12", null, true));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Addresses, address => address.IsDefault);
        Assert.Contains(result.Value.Addresses, address => address.Id == 200 && address.IsDefault);
    }

    [Fact]
    public async Task Handle_ReturnsDuplicateForNormalizedDuplicateAddress()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(TestData.CreateCustomer());
        var handler = new AddCustomerAddressHandler(
            customers,
            new FakeApplicationIdGenerator(),
            new FakeUnitOfWork());

        var result = await handler.Handle(
            new AddCustomerAddressCommand(1, " street ", "cairo", "10", "2", false));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DuplicateAddress, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_ReturnsInvalidAddressForMissingRequiredField()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(TestData.CreateCustomer());
        var handler = new AddCustomerAddressHandler(
            customers,
            new FakeApplicationIdGenerator(),
            new FakeUnitOfWork());

        var result = await handler.Handle(
            new AddCustomerAddressCommand(1, "", "Cairo", "10", null, false));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.InvalidAddress, result.Error?.Code);
    }
}
