using Microsoft.Extensions.DependencyInjection;
using Talabat.Application.Basket.AddItem;
using Talabat.Application.Basket.ClearCart;
using Talabat.Application.Basket.GetCart;
using Talabat.Application.Basket.RemoveItem;
using Talabat.Application.Basket.UpdateQuantity;
using Talabat.Application.Catalog.BrowseRestaurants;
using Talabat.Application.Catalog.GetRestaurantMenu;
using Talabat.Application.Customers.AddAddress;
using Talabat.Application.Customers.CreateProfile;
using Talabat.Application.Customers.GetProfile;
using Talabat.Application.Customers.RemoveAddress;
using Talabat.Application.Customers.SetDefaultAddress;
using Talabat.Application.Customers.UpdateProfile;
using Talabat.Application.Ordering.Checkout;
using Talabat.Application.Ordering.GetOrderDetails;
using Talabat.Application.Ordering.GetOrderHistory;
using Talabat.Domain.DomainServices.Checkout;

namespace Talabat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Catalog
        services.AddScoped<BrowseRestaurantsHandler>();
        services.AddScoped<GetRestaurantMenuHandler>();

        // Basket
        services.AddScoped<GetCartHandler>();
        services.AddScoped<AddCartItemHandler>();
        services.AddScoped<UpdateCartItemQuantityHandler>();
        services.AddScoped<RemoveCartItemHandler>();
        services.AddScoped<ClearCartHandler>();

        // Customer Profile
        services.AddScoped<GetCustomerProfileHandler>();
        services.AddScoped<UpdateCustomerProfileHandler>();
        services.AddScoped<CreateCustomerProfileHandler>();
        services.AddScoped<AddCustomerAddressHandler>();
        services.AddScoped<RemoveCustomerAddressHandler>();
        services.AddScoped<SetDefaultCustomerAddressHandler>();

        // Ordering
        services.AddScoped<CheckoutHandler>();
        services.AddScoped<GetOrderHistoryHandler>();
        services.AddScoped<GetOrderDetailsHandler>();

        // Domain Services
        services.AddScoped<CheckoutDomainService>();

        return services;
    }
}
