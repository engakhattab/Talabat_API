using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class DeliveryOperationalMigrationTests
{
    private const string PreviousMigration = "20260807105019_AddCartExpiredStatus";
    private readonly SqlServerDatabaseFixture _fixture;

    public DeliveryOperationalMigrationTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_BackfillsExistingDeliveryFromRepositoryOwnedRestaurant()
    {
        await using var database = await _fixture.CreateDatabaseAsync(PreviousMigration);
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);
        var order = await PersistenceTestData.AddOrderAsync(dbContext, customer.Id);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Deliveries]
                ([OrderId], [CustomerId], [RestaurantId], [Status],
                 [DeliveryStreet], [DeliveryCity], [DeliveryBuildingNumber], [DeliveryFloor],
                 [CreatedAt], [IsDeleted])
            VALUES
                ({order.Id}, {customer.Id}, 1, 1,
                 N'Old Dropoff Street', N'Giza', N'20', N'3',
                 {PersistenceTestData.Now}, CAST(0 AS bit));
            """);

        await dbContext.GetService<IMigrator>().MigrateAsync();
        dbContext.ChangeTracker.Clear();

        var delivery = await dbContext.Deliveries.SingleAsync(item => item.OrderId == order.Id);

        Assert.Equal("Cairo Grill", delivery.RestaurantName);
        Assert.Equal("1 Demo Grill Street", delivery.RestaurantPickupAddress.Street);
        Assert.Equal("Cairo", delivery.RestaurantPickupAddress.City);
        Assert.Equal("1", delivery.RestaurantPickupAddress.BuildingNumber);
        Assert.Equal("Ground Floor", delivery.RestaurantPickupAddress.Floor);
        Assert.Equal("Old Dropoff Street", delivery.DeliveryAddress.Street);
    }

    [Fact]
    public async Task Migration_BlocksUnknownRestaurantWithoutTruthfulPickupAddress()
    {
        await using var database = await _fixture.CreateDatabaseAsync(PreviousMigration);
        await using var dbContext = database.CreateContext();

        await dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO [Restaurants]
                ([Name], [Description], [OpeningStart], [OpeningEnd], [IsActive], [CreatedAt], [IsDeleted])
            VALUES
                (N'Existing Restaurant', N'Existing non-demo data', '08:00', '22:00',
                 CAST(1 AS bit), SYSUTCDATETIME(), CAST(0 AS bit));
            """);

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => dbContext.GetService<IMigrator>().MigrateAsync());

        Assert.Contains("truthful pickup address", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
