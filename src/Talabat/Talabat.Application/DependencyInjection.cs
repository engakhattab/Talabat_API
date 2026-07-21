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
using Talabat.Application.DeliveryAgents.AssignDelivery;
using Talabat.Application.DeliveryAgents.GetActiveDelivery;
using Talabat.Application.DeliveryAgents.GetDeliveryHistory;
using Talabat.Application.DeliveryAgents.GetPendingDeliveries;
using Talabat.Application.DeliveryAgents.GoOffline;
using Talabat.Application.DeliveryAgents.GoOnline;
using Talabat.Application.DeliveryAgents.ProgressArrive;
using Talabat.Application.DeliveryAgents.ProgressCancel;
using Talabat.Application.DeliveryAgents.ProgressDeliver;
using Talabat.Application.DeliveryAgents.ProgressFail;
using Talabat.Application.DeliveryAgents.ProgressOutForDelivery;
using Talabat.Application.DeliveryAgents.ProgressPickup;
using Talabat.Application.DeliveryAgents.UpdateLocation;
using Talabat.Application.Ordering.Checkout;
using Talabat.Application.Ordering.GetOrderDetails;
using Talabat.Application.Ordering.GetOrderHistory;
using Talabat.Domain.DomainServices.Checkout;
using Talabat.Domain.DomainServices.DeliveryManagement;

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

        // Delivery Agents — Status (US1)
        services.AddScoped<GoOnlineHandler>();
        services.AddScoped<GoOfflineHandler>();

        // Delivery Agents — Location (US2)
        services.AddScoped<UpdateLocationHandler>();

        // Delivery Agents — Assignment (US3)
        services.AddScoped<AssignDeliveryAgentHandler>();

        // Delivery Agents — Lifecycle (US4, US5, US6)
        services.AddScoped<ArrivedAtRestaurantHandler>();
        services.AddScoped<PickUpOrderHandler>();
        services.AddScoped<OutForDeliveryHandler>();
        services.AddScoped<DeliverOrderHandler>();
        services.AddScoped<CancelDeliveryHandler>();
        services.AddScoped<FailDeliveryHandler>();

        // Delivery Agents — Queries (US7, US8, US9)
        services.AddScoped<GetActiveDeliveryHandler>();
        services.AddScoped<GetPendingDeliveriesHandler>();
        services.AddScoped<GetDeliveryHistoryHandler>();

        // Domain Services
        services.AddScoped<CheckoutDomainService>();
        services.AddScoped<DeliveryAssignmentDomainService>();

        return services;
    }
}
