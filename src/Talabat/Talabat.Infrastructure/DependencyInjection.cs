using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Application.Abstractions;
using Talabat.Domain.Interfaces;
using Talabat.Infrastructure.Persistence;
using Talabat.Infrastructure.Persistence.Auditing;
using Talabat.Infrastructure.Persistence.Repositories;
using Talabat.Infrastructure.Time;

namespace Talabat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("TalabatDb")
            ?? throw new InvalidOperationException(
                "Connection string 'TalabatDb' is not configured.");

        services.AddSingleton<AuditableEntitySaveChangesInterceptor>();
        services.AddDbContext<TalabatDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString);
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRestaurantRepository, RestaurantRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IDeliveryAgentRepository, DeliveryAgentRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRestaurantLocalTimeProvider, RestaurantLocalTimeProvider>();

        return services;
    }
}
