using Microsoft.EntityFrameworkCore;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class ConstraintPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public ConstraintPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Money_and_quantity_check_constraints_reject_invalid_rows()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);
        var order = await PersistenceTestData.AddOrderAsync(dbContext, customer.Id);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlRawAsync("""
                INSERT INTO Products
                    (RestaurantId, Name, Description, IsAvailable, ImageUrl, CurrentPriceAmount, CreatedAt, IsDeleted)
                VALUES
                    (1, N'Negative Money', N'Invalid', CAST(1 AS bit), NULL, -1.00, SYSUTCDATETIME(), CAST(0 AS bit));
                """));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO OrderItems
                    (OrderId, ProductId, ProductName, Quantity, UnitPriceAmount, LineTotalAmount)
                VALUES
                    ({order.Id}, 102, N'Chicken Shawarma', 0, 95.00, 0.00);
                """));
    }
}
