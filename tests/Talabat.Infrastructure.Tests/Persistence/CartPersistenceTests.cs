using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class CartPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public CartPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Active_cart_round_trips_with_composite_items()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext, withAddress: false);
        var repository = provider.GetRequiredService<ICartRepository>();

        var cart = Cart.Create(
            customer.Id,
            PersistenceTestData.SeedProduct101,
            quantity: 2,
            PersistenceTestData.Now);

        await repository.AddAsync(cart);
        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var saved = await repository.GetActiveCartByCustomerIdAsync(customer.Id);

        Assert.True(cart.Id > 0);
        Assert.NotNull(saved);
        Assert.Contains(saved.Items, item => item.ProductId == 101 && item.Quantity == 2);
    }

    [Fact]
    public async Task Duplicate_active_cart_for_customer_is_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext, withAddress: false);

        await PersistenceTestData.AddActiveCartAsync(dbContext, customer.Id, PersistenceTestData.SeedProduct101);

        var duplicate = Cart.Create(
            customer.Id,
            PersistenceTestData.SeedProduct102,
            quantity: 1,
            PersistenceTestData.Now);

        await dbContext.Carts.AddAsync(duplicate);

        await Assert.ThrowsAnyAsync<Exception>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Invalid_cart_item_quantity_is_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext, withAddress: false);
        var cart = await PersistenceTestData.AddActiveCartAsync(dbContext, customer.Id);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO CartItems (CartId, ProductId, ProductName, Quantity)
                VALUES ({cart.Id}, 102, N'Chicken Shawarma', 0);
                """));

        Assert.NotNull(exception);
    }
}
