using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Infrastructure.Tests.Persistence;

internal static class PersistenceTestData
{
    public static readonly DateTime Now =
        new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    public static CatalogProductSnapshot SeedProduct101 { get; } =
        new(101, 1, "Mixed Grill Plate", isAvailable: true);

    public static CatalogProductSnapshot SeedProduct102 { get; } =
        new(102, 1, "Chicken Shawarma", isAvailable: true);

    public static CheckoutItemSnapshot SeedCheckoutItem101 =>
        new(101, "Mixed Grill Plate", new Money(185m), quantity: 2);

    public static CheckoutItemSnapshot SeedCheckoutItem102 =>
        new(102, "Chicken Shawarma", new Money(95m), quantity: 1);

    public static Address Address =>
        new("Tahrir Street", "Cairo", "10", "3");

    public static DeliveryAddressSnapshot DeliveryAddress =>
        new("Tahrir Street", "Cairo", "10", "3");

    public static async Task<User> AddCustomerAsync(
        TalabatDbContext dbContext,
        bool withAddress = true)
    {
        var user = User.Register("testcustomer@test.com", "testcustomer@test.com", "Test Customer");
        user.InitializeCustomerProfile("Test Customer", 30, "+201000000000");

        if (withAddress)
        {
            user.AddAddress(Address, makeDefault: true);
        }

        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    public static async Task<Cart> AddActiveCartAsync(
        TalabatDbContext dbContext,
        int customerId,
        CatalogProductSnapshot? firstProduct = null)
    {
        var cart = Cart.Create(
            customerId,
            firstProduct ?? SeedProduct101,
            quantity: 1,
            createdAt: Now);

        await dbContext.Carts.AddAsync(cart);
        await dbContext.SaveChangesAsync();

        return cart;
    }

    public static async Task<Order> AddOrderAsync(
        TalabatDbContext dbContext,
        int customerId,
        IReadOnlyCollection<CheckoutItemSnapshot>? items = null)
    {
        var order = Order.CreateFromCheckout(
            customerId,
            restaurantId: 1,
            items ?? [SeedCheckoutItem101],
            DeliveryAddress,
            Now);

        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        return order;
    }

    public static async Task<User> AddAvailableAgentAsync(
        TalabatDbContext dbContext)
    {
        var user = User.Register("agent@test.com", "agent@test.com", "Delivery Agent");
        user.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        user.ApproveDeliveryAgentApplication();
        user.GoOnline();
        user.UpdateLocation(new GeoLocation(30.0444m, 31.2357m));

        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        return user;
    }
}
