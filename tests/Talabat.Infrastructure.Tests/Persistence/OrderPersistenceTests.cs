using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class OrderPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public OrderPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Order_snapshot_round_trips_for_customer_history_and_details()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);
        var repository = provider.GetRequiredService<IOrderRepository>();

        var order = Order.CreateFromCheckout(
            customer.Id,
            restaurantId: 1,
            [PersistenceTestData.SeedCheckoutItem101, PersistenceTestData.SeedCheckoutItem102],
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now);

        await repository.AddAsync(order);
        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var saved = await repository.GetByIdForCustomerAsync(order.Id, customer.Id);
        var history = await repository.GetByCustomerIdAsync(customer.Id);

        Assert.True(order.Id > 0);
        Assert.NotNull(saved);
        Assert.Equal(465m, saved.TotalAmount.Amount);
        Assert.Equal("Tahrir Street", saved.DeliveryAddress.Street);
        Assert.Contains(saved.Items, item =>
            item.ProductId == 101
            && item.ProductName == "Mixed Grill Plate"
            && item.UnitPrice.Amount == 185m
            && item.LineTotal.Amount == 370m);
        Assert.Contains(history, item => item.Id == order.Id);
    }
}
