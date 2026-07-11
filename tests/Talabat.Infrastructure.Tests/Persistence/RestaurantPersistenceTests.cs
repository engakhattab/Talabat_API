using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class RestaurantPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public RestaurantPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Seeded_catalog_round_trips_through_repository()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var repository = provider.GetRequiredService<IRestaurantRepository>();

        var restaurants = await repository.GetActiveRestaurantsAsync();
        var restaurant = await repository.GetByIdWithProductsAsync(1);

        Assert.Contains(restaurants, item => item.Id == 1 && item.Name == "Cairo Grill");
        Assert.NotNull(restaurant);
        Assert.Contains(restaurant.Products, product => product.Id == 101 && product.CurrentPrice.Amount == 185m);
        Assert.Equal(new TimeOnly(10, 0), restaurant.OpeningHours.Start);
    }

    [Fact]
    public async Task New_restaurant_and_product_receive_identity_ids()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var restaurant = new Restaurant(
            "Identity Kitchen",
            "Identity test restaurant.",
            imageUrl: null,
            new TimeRange(new TimeOnly(8, 0), new TimeOnly(20, 0)));

        await dbContext.Restaurants.AddAsync(restaurant);
        await dbContext.SaveChangesAsync();

        var product = restaurant.AddProduct(
            "Identity Meal",
            "Identity test product.",
            new Money(42m),
            imageUrl: null);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var saved = await dbContext.Restaurants
            .Include("_products")
            .SingleAsync(item => item.Id == restaurant.Id);

        Assert.True(restaurant.Id > 0);
        Assert.True(product.Id > 0);
        Assert.Contains(saved.Products, item => item.Name == "Identity Meal");
    }

    [Fact]
    public async Task Duplicate_product_name_per_restaurant_is_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlRawAsync("""
                INSERT INTO Products
                    (RestaurantId, Name, Description, IsAvailable, ImageUrl, CurrentPriceAmount, CreatedAt, IsDeleted)
                VALUES
                    (1, N'Mixed Grill Plate', N'Duplicate', CAST(1 AS bit), NULL, 10.00, SYSUTCDATETIME(), CAST(0 AS bit));
                """));

        Assert.NotNull(exception);
    }
}
