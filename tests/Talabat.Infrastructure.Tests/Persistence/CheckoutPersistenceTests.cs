using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class CheckoutPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public CheckoutPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UnitOfWork_commits_one_order_and_one_checked_out_cart_atomically()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var setupContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(setupContext);
        var cart = await PersistenceTestData.AddActiveCartAsync(setupContext, customer.Id);

        var orderRepository = provider.GetRequiredService<IOrderRepository>();
        var cartRepository = provider.GetRequiredService<ICartRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var activeCart = await cartRepository.GetActiveCartByCustomerIdAsync(customer.Id);
        Assert.NotNull(activeCart);

        var order = Order.CreateFromCheckout(
            customer.Id,
            activeCart.RestaurantId,
            [PersistenceTestData.SeedCheckoutItem101],
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now);

        await orderRepository.AddAsync(order);
        activeCart.MarkCheckedOut(PersistenceTestData.Now);
        cartRepository.Update(activeCart);

        await unitOfWork.SaveChangesAsync();

        var verificationContext = provider.GetRequiredService<TalabatDbContext>();
        var orders = await verificationContext.Orders.CountAsync(item => item.CustomerId == customer.Id);
        var savedCart = await verificationContext.Carts.SingleAsync(item => item.Id == cart.Id);

        Assert.True(order.Id > 0);
        Assert.Equal(1, orders);
        Assert.Equal(CartStatus.CheckedOut, savedCart.Status);
    }
}
