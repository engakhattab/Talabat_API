using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class OrderFulfillmentPersistenceTests
{
    private static readonly TimeSpan AcceptanceWindow = TimeSpan.FromMinutes(10);
    private readonly SqlServerDatabaseFixture _fixture;

    public OrderFulfillmentPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // Design §4 test 27.
    [Fact]
    public async Task Fulfillment_round_trips_and_enforces_unique_order_and_check_constraints()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var setupContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(setupContext);
        var firstOrder = await PersistenceTestData.AddOrderAsync(
            setupContext,
            customer.Id,
            [PersistenceTestData.SeedCheckoutItem101]);
        var secondOrder = await PersistenceTestData.AddOrderAsync(
            setupContext,
            customer.Id,
            [PersistenceTestData.SeedCheckoutItem102]);

        using (var writeScope = provider.CreateScope())
        {
            var repository = writeScope.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>();
            var unitOfWork = writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repository.AddAsync(NewFulfillment(firstOrder.Id));
            await repository.AddAsync(NewFulfillment(secondOrder.Id));
            await unitOfWork.SaveChangesAsync();
        }

        using (var readScope = provider.CreateScope())
        {
            var repository = readScope.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>();

            var saved = await repository.GetByOrderIdAsync(firstOrder.Id);
            var both = await repository.GetByOrderIdsAsync([firstOrder.Id, secondOrder.Id]);

            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(firstOrder.Id, saved.OrderId);
            Assert.Equal(1, saved.RestaurantId);
            Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, saved.Status);
            Assert.Equal(PersistenceTestData.Now, saved.PlacedAt);
            Assert.Equal(PersistenceTestData.Now.Add(AcceptanceWindow), saved.AcceptanceDeadline);
            Assert.Null(saved.AcceptedAt);
            Assert.NotEmpty(saved.RowVersion);
            Assert.Equal(
                new[] { firstOrder.Id, secondOrder.Id },
                both.Select(fulfillment => fulfillment.OrderId).Order());
        }

        // T5 (D13): a rejection and a customer cancellation keep their actor, reason code, note and time.
        var rejectedAt = PersistenceTestData.Now.AddMinutes(2);
        var cancelledAt = PersistenceTestData.Now.AddMinutes(3);
        using (var endScope = provider.CreateScope())
        {
            var repository = endScope.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>();
            var rejecting = await repository.GetByOrderIdAsync(firstOrder.Id);
            var cancelling = await repository.GetByOrderIdAsync(secondOrder.Id);

            rejecting!.Reject(1, FulfillmentReasonCode.Other, "Oven broke", rejectedAt);
            cancelling!.CancelByCustomer(FulfillmentReasonCode.ChangedMind, note: null, cancelledAt);
            await endScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        using (var endReadScope = provider.CreateScope())
        {
            var repository = endReadScope.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>();
            var rejected = await repository.GetByOrderIdAsync(firstOrder.Id);
            var cancelled = await repository.GetByOrderIdAsync(secondOrder.Id);

            Assert.Equal(OrderFulfillmentStatus.Rejected, rejected!.Status);
            Assert.Equal(FulfillmentReasonCode.Other, rejected.ReasonCode);
            Assert.Equal("Oven broke", rejected.ReasonNote);
            Assert.Equal(rejectedAt, DateTime.SpecifyKind(rejected.RejectedAt!.Value, DateTimeKind.Utc));
            Assert.Null(rejected.CancelledBy);

            Assert.Equal(OrderFulfillmentStatus.Cancelled, cancelled!.Status);
            Assert.Equal(CancellationActor.Customer, cancelled.CancelledBy);
            Assert.Equal(FulfillmentReasonCode.ChangedMind, cancelled.ReasonCode);
            Assert.Null(cancelled.ReasonNote);
            Assert.Equal(cancelledAt, DateTime.SpecifyKind(cancelled.CancelledAt!.Value, DateTimeKind.Utc));
        }

        using (var duplicateScope = provider.CreateScope())
        {
            var repository = duplicateScope.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>();
            var unitOfWork = duplicateScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repository.AddAsync(NewFulfillment(firstOrder.Id));

            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => unitOfWork.SaveChangesAsync());
        }

        await using var sqlContext = database.CreateContext();
        var checkViolation = await Assert.ThrowsAsync<SqlException>(() =>
            sqlContext.Database.ExecuteSqlAsync(
                $"UPDATE [OrderFulfillments] SET [Status] = 99 WHERE [OrderId] = {firstOrder.Id}"));
        Assert.Equal(547, checkViolation.Number);

        var actorViolation = await Assert.ThrowsAsync<SqlException>(() =>
            sqlContext.Database.ExecuteSqlAsync(
                $"UPDATE [OrderFulfillments] SET [CancelledBy] = 99 WHERE [OrderId] = {secondOrder.Id}"));
        Assert.Equal(547, actorViolation.Number);

        var reasonViolation = await Assert.ThrowsAsync<SqlException>(() =>
            sqlContext.Database.ExecuteSqlAsync(
                $"UPDATE [OrderFulfillments] SET [ReasonCode] = 99 WHERE [OrderId] = {secondOrder.Id}"));
        Assert.Equal(547, reasonViolation.Number);

        var tooLongNote = new string('x', 501);
        await Assert.ThrowsAsync<SqlException>(() =>
            sqlContext.Database.ExecuteSqlAsync(
                $"UPDATE [OrderFulfillments] SET [ReasonNote] = {tooLongNote} WHERE [OrderId] = {firstOrder.Id}"));
    }

    // Design §4 test 28.
    [Fact]
    public async Task Fulfillment_rowversion_rejects_stale_update()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var setupContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(setupContext);
        var order = await PersistenceTestData.AddOrderAsync(setupContext, customer.Id);

        using (var setupScope = provider.CreateScope())
        {
            await setupScope.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>()
                .AddAsync(NewFulfillment(order.Id));
            await setupScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        using var firstWriter = provider.CreateScope();
        using var staleWriter = provider.CreateScope();
        var firstCopy = await firstWriter.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>()
            .GetByOrderIdAsync(order.Id);
        var staleCopy = await staleWriter.ServiceProvider.GetRequiredService<IOrderFulfillmentRepository>()
            .GetByOrderIdAsync(order.Id);
        Assert.NotNull(firstCopy);
        Assert.NotNull(staleCopy);

        firstCopy.Accept(1, PersistenceTestData.Now.AddMinutes(1));
        await firstWriter.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        staleCopy.Accept(1, PersistenceTestData.Now.AddMinutes(2));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            staleWriter.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
    }

    // Design §4 test 29 (D12): added on top of the existing migrations, no backfill.
    [Fact]
    public async Task Add_order_fulfillment_migration_applies_on_an_empty_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = await dbContext.Database.GetPendingMigrationsAsync();

        Assert.EndsWith("_AddOrderFulfillment", applied.Last());
        Assert.Empty(pending);
        Assert.False(dbContext.Database.HasPendingModelChanges());
        Assert.Equal(0, await dbContext.Set<OrderFulfillment>().CountAsync());
    }

    private static OrderFulfillment NewFulfillment(int orderId)
    {
        return new OrderFulfillment(orderId, restaurantId: 1, PersistenceTestData.Now, AcceptanceWindow);
    }
}
