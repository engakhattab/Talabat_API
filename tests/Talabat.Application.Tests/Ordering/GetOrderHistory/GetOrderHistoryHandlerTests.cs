using Talabat.Application.Ordering.GetOrderHistory;
using Talabat.Application.Tests.TestDoubles;

namespace Talabat.Application.Tests.Ordering.GetOrderHistory;

public sealed class GetOrderHistoryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCustomerOrdersNewestFirst()
    {
        var orders = new FakeOrderRepository();
        orders.Orders.Add(TestData.CreateOrder(1, customerId: 1, createdAt: TestData.UtcNow.AddDays(-1)));
        orders.Orders.Add(TestData.CreateOrder(2, customerId: 2, createdAt: TestData.UtcNow));
        orders.Orders.Add(TestData.CreateOrder(3, customerId: 1, createdAt: TestData.UtcNow));
        var handler = new GetOrderHistoryHandler(orders);

        var result = await handler.Handle(new GetOrderHistoryQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 3, 1 }, result.Value.Select(order => order.Id));
    }

    [Fact]
    public async Task Handle_ReturnsEmptyCollectionWhenNoOrdersExist()
    {
        var handler = new GetOrderHistoryHandler(new FakeOrderRepository());

        var result = await handler.Handle(new GetOrderHistoryQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
