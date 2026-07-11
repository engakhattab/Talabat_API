using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.GetOrderDetails;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Ordering.GetOrderDetails;

public sealed class GetOrderDetailsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsHistoricalSnapshotFieldsForCustomerOrder()
    {
        var orders = new FakeOrderRepository();
        orders.Orders.Add(TestData.CreateOrder(1, customerId: 1));
        var handler = new GetOrderDetailsHandler(orders);

        var result = await handler.Handle(new GetOrderDetailsQuery(1, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal("Street", result.Value.DeliveryAddress.Street);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Koshary", item.ProductName);
        Assert.Equal(50m, item.UnitPrice.Amount);
        Assert.Equal(100m, item.LineTotal.Amount);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundForMissingOrOtherCustomerOrder()
    {
        var orders = new FakeOrderRepository();
        orders.Orders.Add(TestData.CreateOrder(1, customerId: 2));
        var handler = new GetOrderDetailsHandler(orders);

        var otherCustomerResult = await handler.Handle(new GetOrderDetailsQuery(1, 1));
        var missingResult = await handler.Handle(new GetOrderDetailsQuery(1, 999));

        Assert.True(otherCustomerResult.IsFailure);
        Assert.True(missingResult.IsFailure);
        Assert.Equal(ApplicationErrorCodes.OrderNotFound, otherCustomerResult.Error?.Code);
        Assert.Equal(ApplicationErrorCodes.OrderNotFound, missingResult.Error?.Code);
    }
}
