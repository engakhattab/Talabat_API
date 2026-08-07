using Microsoft.EntityFrameworkCore;
using Talabat.Application.Basket.AddItem;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Interfaces;
using Talabat.Infrastructure.Time;

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
    public async Task Expired_active_cart_is_retired_and_new_active_cart_is_created()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext, withAddress: false);

        var expiredCart = Cart.Create(
            customer.Id,
            PersistenceTestData.SeedProduct101,
            quantity: 1,
            PersistenceTestData.Now.AddHours(-2));

        await dbContext.Carts.AddAsync(expiredCart);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(provider);
        var result = await handler.Handle(new AddCartItemCommand(customer.Id, 1, 101, 2));

        Assert.True(result.IsSuccess);

        var verificationContext = database.CreateContext();
        var carts = await verificationContext.Carts
            .Where(cart => cart.CustomerId == customer.Id)
            .ToListAsync();

        Assert.Equal(2, carts.Count);
        var active = Assert.Single(carts, cart => cart.Status == CartStatus.Active);
        var expired = Assert.Single(carts, cart => cart.Status == CartStatus.Expired);
        Assert.Equal(expiredCart.Id, expired.Id);
        Assert.NotEqual(expiredCart.Id, active.Id);
        Assert.Contains(active.Items, item => item.ProductId == 101 && item.Quantity == 2);
    }

    [Fact]
    public async Task Two_concurrent_first_adds_leave_exactly_one_active_cart()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var seedProvider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var seedContext = seedProvider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(seedContext, withAddress: false);

        var expiredCart = Cart.Create(
            customer.Id,
            PersistenceTestData.SeedProduct101,
            quantity: 1,
            PersistenceTestData.Now.AddHours(-2));

        await seedContext.Carts.AddAsync(expiredCart);
        await seedContext.SaveChangesAsync();

        await using var provider1 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        await using var provider2 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);

        var command = new AddCartItemCommand(customer.Id, 1, 101, 1);
        var results = await Task.WhenAll(
            CreateHandler(provider1).Handle(command),
            CreateHandler(provider2).Handle(command));

        Assert.Equal(2, results.Length);
        Assert.Contains(results, result => result.IsSuccess);
        foreach (var result in results)
        {
            Assert.True(
                result.IsSuccess ||
                result.Error?.Code == ApplicationErrorCodes.ConcurrencyConflict);
        }

        var verificationContext = database.CreateContext();
        var activeCartCount = await verificationContext.Carts
            .CountAsync(cart =>
                cart.CustomerId == customer.Id &&
                cart.Status == CartStatus.Active);

        Assert.Equal(1, activeCartCount);
    }

    private static AddCartItemHandler CreateHandler(IServiceProvider provider)
    {
        return new AddCartItemHandler(
            provider.GetRequiredService<ICartRepository>(),
            provider.GetRequiredService<IRestaurantRepository>(),
            new SystemClock(),
            provider.GetRequiredService<IUnitOfWork>());
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
