using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.TestDoubles;

public static class TestData
{
    public static readonly DateTime UtcNow = new(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);

    public static Restaurant CreateRestaurant(
        int id = 1,
        bool active = true,
        bool openAllDay = true)
    {
        var restaurant = new Restaurant(
            $"Restaurant {id}",
            $"Restaurant {id} description",
            imageUrl: null,
            openingHours: openAllDay
                ? new TimeRange(new TimeOnly(0, 0), new TimeOnly(23, 59))
                : new TimeRange(new TimeOnly(23, 0), new TimeOnly(23, 59)),
            isActive: active);

        TestIds.SetId(restaurant, id);

        var koshary = restaurant.AddProduct("Koshary", "Rice and lentils", new Money(50m), imageUrl: null);
        TestIds.SetId(koshary, 10 + id);

        var unavailable = restaurant.AddProduct("Unavailable", "Unavailable item", new Money(70m), imageUrl: null, isAvailable: false);
        TestIds.SetId(unavailable, 20 + id);

        return restaurant;
    }

    public static Cart CreateCart(
        int cartId = 100,
        int customerId = 1,
        Restaurant? restaurant = null,
        int quantity = 2,
        DateTime? createdAt = null)
    {
        restaurant ??= CreateRestaurant();
        var product = restaurant.Products.First(product => product.IsAvailable);

        var cart = Cart.Create(
            customerId,
            new CatalogProductSnapshot(
                product.Id,
                restaurant.Id,
                product.Name,
                product.IsAvailable),
            quantity,
            createdAt ?? UtcNow);

        TestIds.SetId(cart, cartId);

        return cart;
    }

    public static User CreateCustomer(int id = 1)
    {
        var user = User.Register("customer1", "customer1@test.com", "Customer One");
        user.InitializeCustomerProfile("Customer One", 30, "01000000000");

        TestIds.SetId(user, id);

        user.AddAddress(new Address("Street", "Cairo", "10", "2"), makeDefault: true);
        user.AddAddress(new Address("Other Street", "Cairo", "11"), makeDefault: false);

        var addresses = user.Addresses.ToList();
        TestIds.SetId(addresses[0], 1);
        TestIds.SetId(addresses[1], 2);

        return user;
    }

    public static Order CreateOrder(
        int id,
        int customerId = 1,
        int restaurantId = 1,
        DateTime? createdAt = null)
    {
        var order = Order.CreateFromCheckout(
            customerId,
            restaurantId,
            new[]
            {
                new CheckoutItemSnapshot(11, "Koshary", new Money(50m), 2)
            },
            new DeliveryAddressSnapshot("Street", "Cairo", "10", "2"),
            createdAt ?? UtcNow);

        TestIds.SetId(order, id);

        return order;
    }
}
