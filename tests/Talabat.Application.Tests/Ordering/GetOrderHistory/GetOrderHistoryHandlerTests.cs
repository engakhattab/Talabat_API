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
        var handler = new GetOrderHistoryHandler(orders, TestData.CreateFulfillmentsFor(orders), new FakeDeliveryRepository());

        var result = await handler.Handle(new GetOrderHistoryQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 3, 1 }, result.Value.Items.Select(order => order.Id));
    }

    [Fact]
    public async Task Handle_ReportsItemCountAsTotalQuantity()
    {
        var orders = new FakeOrderRepository();
        orders.Orders.Add(TestData.CreateOrder(1, customerId: 1)); // one line, quantity 2
        var handler = new GetOrderHistoryHandler(orders, TestData.CreateFulfillmentsFor(orders), new FakeDeliveryRepository());

        var result = await handler.Handle(new GetOrderHistoryQuery(1));

        Assert.Equal(2, Assert.Single(result.Value.Items).ItemCount);
    }

    [Fact]
    public async Task Handle_PagesNewestFirstAndReportsTotalCount()
    {
        var orders = new FakeOrderRepository();
        for (var id = 1; id <= 5; id++)
        {
            orders.Orders.Add(TestData.CreateOrder(id, customerId: 1, createdAt: TestData.UtcNow.AddMinutes(id)));
        }

        var handler = new GetOrderHistoryHandler(orders, TestData.CreateFulfillmentsFor(orders), new FakeDeliveryRepository());

        var result = await handler.Handle(new GetOrderHistoryQuery(1, Page: 2, PageSize: 2));

        Assert.Equal(new[] { 3, 2 }, result.Value.Items.Select(order => order.Id));
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(2, result.Value.PageSize);
        Assert.Equal(5, result.Value.TotalCount);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, 500, 1, 50)]
    public async Task Handle_ClampsOutOfRangePaging(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var handler = new GetOrderHistoryHandler(new FakeOrderRepository(), new FakeOrderFulfillmentRepository(), new FakeDeliveryRepository());

        var result = await handler.Handle(new GetOrderHistoryQuery(1, page, pageSize));

        Assert.Equal(expectedPage, result.Value.Page);
        Assert.Equal(expectedPageSize, result.Value.PageSize);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyCollectionWhenNoOrdersExist()
    {
        var handler = new GetOrderHistoryHandler(new FakeOrderRepository(), new FakeOrderFulfillmentRepository(), new FakeDeliveryRepository());

        var result = await handler.Handle(new GetOrderHistoryQuery(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }
}
