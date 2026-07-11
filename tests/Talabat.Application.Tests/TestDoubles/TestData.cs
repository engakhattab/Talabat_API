using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.Customer;
using Talabat.Domain.Aggregates.Ordering;
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
            id,
            $"Restaurant {id}",
            $"Restaurant {id} description",
            imageUrl: null,
            openingHours: openAllDay
                ? new TimeRange(new TimeOnly(0, 0), new TimeOnly(23, 59))
                : new TimeRange(new TimeOnly(23, 0), new TimeOnly(23, 59)),
            isActive: active);

        restaurant.AddProduct(10 + id, "Koshary", "Rice and lentils", new Money(50m), imageUrl: null);
        restaurant.AddProduct(20 + id, "Unavailable", "Unavailable item", new Money(70m), imageUrl: null, isAvailable: false);

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

        return Cart.Create(
            cartId,
            customerId,
            new CatalogProductSnapshot(
                product.Id,
                restaurant.Id,
                product.Name,
                product.IsAvailable),
            quantity,
            createdAt ?? UtcNow);
    }

    public static Customer CreateCustomer(int id = 1)
    {
        var customer = new Customer(id, "Customer One", 30, "01000000000");
        customer.AddAddress(1, new Address("Street", "Cairo", "10", "2"), makeDefault: true);
        customer.AddAddress(2, new Address("Other Street", "Cairo", "11"), makeDefault: false);

        return customer;
    }

    public static Order CreateOrder(
        int id,
        int customerId = 1,
        int restaurantId = 1,
        DateTime? createdAt = null)
    {
        return Order.CreateFromCheckout(
            id,
            customerId,
            restaurantId,
            new[]
            {
                new CheckoutItemSnapshot(11, "Koshary", new Money(50m), 2)
            },
            new DeliveryAddressSnapshot("Street", "Cairo", "10", "2"),
            createdAt ?? UtcNow);
    }
}
