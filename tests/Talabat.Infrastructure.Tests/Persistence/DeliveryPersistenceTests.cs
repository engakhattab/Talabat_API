using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class DeliveryPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public DeliveryPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Delivery_round_trips_through_repository_methods()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);
        var order = await PersistenceTestData.AddOrderAsync(dbContext, customer.Id);
        var repository = provider.GetRequiredService<IDeliveryRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var delivery = new Delivery(
            order.Id,
            customer.Id,
            restaurantId: 1,
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now);

        await repository.AddAsync(delivery);
        await unitOfWork.SaveChangesAsync();

        var byId = await repository.GetByIdAsync(delivery.Id);
        var byOrder = await repository.GetByOrderIdAsync(order.Id);
        var pending = await repository.GetPendingAssignmentAsync();

        Assert.True(delivery.Id > 0);
        Assert.NotNull(byId);
        Assert.Equal("Tahrir Street", byId.DeliveryAddress.Street);
        Assert.Equal(delivery.Id, byOrder?.Id);
        Assert.Contains(pending, item => item.Id == delivery.Id);
    }

    [Fact]
    public async Task Duplicate_delivery_for_one_order_is_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);
        var order = await PersistenceTestData.AddOrderAsync(dbContext, customer.Id);

        await dbContext.Deliveries.AddAsync(new Delivery(
            order.Id,
            customer.Id,
            1,
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now));
        await dbContext.SaveChangesAsync();

        await dbContext.Deliveries.AddAsync(new Delivery(
            order.Id,
            customer.Id,
            1,
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now.AddMinutes(1)));

        await Assert.ThrowsAnyAsync<Exception>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_active_deliveries_for_one_agent_are_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);
        var firstOrder = await PersistenceTestData.AddOrderAsync(
            dbContext,
            customer.Id,
            [PersistenceTestData.SeedCheckoutItem101]);
        var secondOrder = await PersistenceTestData.AddOrderAsync(
            dbContext,
            customer.Id,
            [PersistenceTestData.SeedCheckoutItem102]);
        var agent = await PersistenceTestData.AddAvailableAgentAsync(dbContext);

        var firstDelivery = new Delivery(
            firstOrder.Id,
            customer.Id,
            1,
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now);
        firstDelivery.AssignAgent(agent.Id, PersistenceTestData.Now.AddMinutes(1));

        await dbContext.Deliveries.AddAsync(firstDelivery);
        await dbContext.SaveChangesAsync();

        var secondDelivery = new Delivery(
            secondOrder.Id,
            customer.Id,
            1,
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now);
        secondDelivery.AssignAgent(agent.Id, PersistenceTestData.Now.AddMinutes(1));

        await dbContext.Deliveries.AddAsync(secondDelivery);

        await Assert.ThrowsAnyAsync<Exception>(() => dbContext.SaveChangesAsync());
    }
}
