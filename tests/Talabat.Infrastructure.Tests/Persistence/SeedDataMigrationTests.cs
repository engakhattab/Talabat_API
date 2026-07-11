using Microsoft.EntityFrameworkCore;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class SeedDataMigrationTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public SeedDataMigrationTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Catalog_seed_data_is_applied_by_migrations()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var restaurant = await dbContext.Restaurants
            .Include("_products")
            .SingleAsync(item => item.Id == 1);

        Assert.Equal("Cairo Grill", restaurant.Name);
        Assert.Contains(restaurant.Products, product => product.Id == 101);
    }
}
