using Microsoft.EntityFrameworkCore;
using Talabat.Application.Abstractions;
using Talabat.Application.Fulfillment;
using Talabat.Application.Fulfillment.AcceptOrder;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class AcceptOrderPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public AcceptOrderPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // Design §4 test 30 (D3, D12): the fulfillment's acceptance and the new delivery commit together or not at all.
    [Fact]
    public async Task Failed_accept_rolls_back_fulfillment_and_delivery()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var setupContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(setupContext);
        var order = await PersistenceTestData.AddOrderAsync(setupContext, customer.Id);
        await setupContext.OrderFulfillments.AddAsync(
            new OrderFulfillment(order.Id, order.RestaurantId, PersistenceTestData.Now, TimeSpan.FromMinutes(10)));

        // A stray delivery already holds the order, so inserting the acceptance's delivery violates
        // UX_Deliveries_OrderId: the step after the fulfillment change fails inside the transaction.
        var stray = new Delivery(
            order.Id,
            customer.Id,
            order.RestaurantId,
            "Stray",
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now);
        await setupContext.Deliveries.AddAsync(stray);
        await setupContext.SaveChangesAsync();

        using (var acceptScope = provider.CreateScope())
        {
            var services = acceptScope.ServiceProvider;
            var handler = new AcceptOrderHandler(
                services.GetRequiredService<IOrderRepository>(),
                services.GetRequiredService<IOrderFulfillmentRepository>(),
                services.GetRequiredService<IDeliveryRepository>(),
                services.GetRequiredService<IRestaurantRepository>(),
                new FixedClock(PersistenceTestData.Now.AddMinutes(2)),
                services.GetRequiredService<IUnitOfWork>());

            await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                handler.Handle(new AcceptOrderCommand(order.Id, new FulfillmentActor.Admin())));
        }

        await using var verificationContext = database.CreateContext();
        var fulfillment = await verificationContext.OrderFulfillments.SingleAsync(item => item.OrderId == order.Id);
        var deliveries = await verificationContext.Deliveries.Where(item => item.OrderId == order.Id).ToListAsync();

        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, fulfillment.Status);
        Assert.Null(fulfillment.AcceptedAt);
        Assert.Equal(stray.Id, Assert.Single(deliveries).Id);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
