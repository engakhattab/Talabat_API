using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Customer;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Ordering;
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

    public static async Task<Customer> AddCustomerAsync(
        TalabatDbContext dbContext,
        bool withAddress = true)
    {
        var customer = new Customer("Test Customer", 30, "+201000000000");

        if (withAddress)
        {
            customer.AddAddress(Address, makeDefault: true);
        }

        await dbContext.Customers.AddAsync(customer);
        await dbContext.SaveChangesAsync();

        return customer;
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

    public static async Task<DeliveryAgent> AddAvailableAgentAsync(
        TalabatDbContext dbContext)
    {
        var agent = new DeliveryAgent(
            "Delivery Agent",
            VehicleType.Motorcycle,
            Now,
            phoneNumber: "+201111111111",
            currentLocation: new GeoLocation(30.0444m, 31.2357m));

        agent.GoOnline();

        await dbContext.DeliveryAgents.AddAsync(agent);
        await dbContext.SaveChangesAsync();

        return agent;
    }
}
