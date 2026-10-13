using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.DeliveryManagement;
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

    [Fact]
    public async Task Transaction_rolls_back_order_and_cart_when_a_later_step_fails()
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

        // Mirrors checkout: the first save generates the order id, then the delivery step fails.
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await orderRepository.AddAsync(
                Order.CreateFromCheckout(
                    customer.Id,
                    activeCart.RestaurantId,
                    [PersistenceTestData.SeedCheckoutItem101],
                    PersistenceTestData.DeliveryAddress,
                    PersistenceTestData.Now),
                ct);
            activeCart.MarkCheckedOut(PersistenceTestData.Now);
            cartRepository.Update(activeCart);
            await unitOfWork.SaveChangesAsync(ct);

            throw new InvalidOperationException("Simulated delivery creation failure.");
        }));

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        Assert.Equal(0, await verificationContext.Orders.CountAsync(item => item.CustomerId == customer.Id));
        var savedCart = await verificationContext.Carts.AsNoTracking().SingleAsync(item => item.Id == cart.Id);
        Assert.Equal(CartStatus.Active, savedCart.Status);
    }

    [Fact]
    public async Task Transaction_commits_order_and_its_delivery_together()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var setupContext = provider.GetRequiredService<TalabatDbContext>();
        var customer = await PersistenceTestData.AddCustomerAsync(setupContext);
        var cart = await PersistenceTestData.AddActiveCartAsync(setupContext, customer.Id);

        var orderRepository = provider.GetRequiredService<IOrderRepository>();
        var deliveryRepository = provider.GetRequiredService<IDeliveryRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var restaurant = await setupContext.Restaurants.AsNoTracking().SingleAsync(item => item.Id == cart.RestaurantId);

        var order = Order.CreateFromCheckout(
            customer.Id,
            cart.RestaurantId,
            [PersistenceTestData.SeedCheckoutItem101],
            PersistenceTestData.DeliveryAddress,
            PersistenceTestData.Now);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await orderRepository.AddAsync(order, ct);
            await unitOfWork.SaveChangesAsync(ct);

            await deliveryRepository.AddAsync(
                new Delivery(
                    order.Id,
                    customer.Id,
                    restaurant.Id,
                    restaurant.Name,
                    PersistenceTestData.DeliveryAddress,
                    PersistenceTestData.DeliveryAddress,
                    PersistenceTestData.Now),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        });

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var delivery = await verificationContext.Deliveries.AsNoTracking().SingleAsync(item => item.OrderId == order.Id);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
    }
}
