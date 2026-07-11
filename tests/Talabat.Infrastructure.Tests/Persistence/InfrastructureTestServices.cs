using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Domain.Interfaces;
using Talabat.Infrastructure.Persistence;
using Talabat.Infrastructure.Persistence.Auditing;
using Talabat.Infrastructure.Persistence.Repositories;

namespace Talabat.Infrastructure.Tests.Persistence;

internal static class InfrastructureTestServices
{
    public static DbContextOptions<TalabatDbContext> CreateDbContextOptions(
        string connectionString)
    {
        var interceptor = new AuditableEntitySaveChangesInterceptor();

        return new DbContextOptionsBuilder<TalabatDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(interceptor)
            .Options;
    }

    public static ServiceProvider CreateServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();

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

        return services.BuildServiceProvider();
    }
}
