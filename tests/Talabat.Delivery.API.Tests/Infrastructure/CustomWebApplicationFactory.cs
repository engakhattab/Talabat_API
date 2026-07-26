using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Delivery.API.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private string? _connectionString;

    public int DeliveryAgentUserId { get; private set; }
    public int AgentBUserId { get; private set; }
    public int DeliveryId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            var config = sp.GetRequiredService<IConfiguration>();
            var originalConnectionString = config.GetConnectionString("TalabatDb");

            var connectionBuilder = new SqlConnectionStringBuilder(originalConnectionString)
            {
                InitialCatalog = $"TalabatTest_Delivery_{Guid.NewGuid():N}"
            };
            _connectionString = connectionBuilder.ConnectionString;

            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TalabatDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<TalabatDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(_connectionString);
                var interceptor = serviceProvider.GetService<Talabat.Infrastructure.Persistence.Auditing.AuditableEntitySaveChangesInterceptor>();
                if (interceptor != null)
                {
                    options.AddInterceptors(interceptor);
                }
            });

            services.AddIdentityCore<User>()
                .AddRoles<IdentityRole<int>>()
                .AddEntityFrameworkStores<TalabatDbContext>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.AuthenticationScheme, options => { });
        });

        builder.UseEnvironment("Development");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<TalabatDbContext>();
            db.Database.EnsureCreated();

            IdentityDataSeeder.SeedRolesAsync(services).GetAwaiter().GetResult();

            var userManager = services.GetRequiredService<UserManager<User>>();

            // Create a DeliveryAgent user
            var agentUser = User.Register("testagent", "agent@test.com", "Test Agent");
            agentUser.InitializeCustomerProfile("Test Agent", 28, null);
            userManager.CreateAsync(agentUser, "Password1!").GetAwaiter().GetResult();
            userManager.AddToRoleAsync(agentUser, "Customer").GetAwaiter().GetResult();
            userManager.AddToRoleAsync(agentUser, "DeliveryAgent").GetAwaiter().GetResult();
            agentUser.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
            agentUser.ApproveDeliveryAgentApplication();
            userManager.UpdateAsync(agentUser).GetAwaiter().GetResult();

            DeliveryAgentUserId = agentUser.Id;

            // Create Agent B (second delivery agent — for ownership tests)
            var agentB = User.Register("testagentb", "agentb@test.com", "Test Agent B");
            userManager.CreateAsync(agentB, "Password1!").GetAwaiter().GetResult();
            userManager.AddToRoleAsync(agentB, "DeliveryAgent").GetAwaiter().GetResult();
            agentB.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
            agentB.ApproveDeliveryAgentApplication();
            userManager.UpdateAsync(agentB).GetAwaiter().GetResult();

            AgentBUserId = agentB.Id;

            // Create a customer for the order
            var customer = User.Register("testcustomer", "customer@test.com", "Test Customer");
            userManager.CreateAsync(customer, "Password1!").GetAwaiter().GetResult();
            userManager.AddToRoleAsync(customer, "Customer").GetAwaiter().GetResult();

            // Seed restaurant, product, order, and delivery for ownership tests
            var restaurant = new Restaurant(
                "Ownership Restaurant",
                "Ownership test fixture",
                null,
                new TimeRange(new TimeOnly(8, 0), new TimeOnly(23, 0)));
            db.Restaurants.Add(restaurant);
            db.SaveChanges();

            var product = restaurant.AddProduct(
                "Ownership Item",
                "Ownership test product",
                new Money(10m),
                null);
            db.SaveChanges();

            var order = Order.CreateFromCheckout(
                customer.Id,
                restaurant.Id,
                [new CheckoutItemSnapshot(product.Id, product.Name, product.CurrentPrice, 1)],
                new DeliveryAddressSnapshot("1 Test Street", "Cairo", "1"),
                DateTime.UtcNow);
            db.Orders.Add(order);
            db.SaveChanges();

            var delivery = new Talabat.Domain.Aggregates.DeliveryManagement.Delivery(
                order.Id,
                customer.Id,
                restaurant.Id,
                new DeliveryAddressSnapshot("1 Test Street", "Cairo", "1"),
                DateTime.UtcNow);
            delivery.AssignAgent(agentUser.Id, DateTime.UtcNow);
            db.Deliveries.Add(delivery);
            db.SaveChanges();

            DeliveryId = delivery.Id;
        }

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _connectionString != null)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString)
                {
                    InitialCatalog = "master"
                };

                using var connection = new SqlConnection(builder.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                var dbName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
                command.CommandText = $@"
                    IF DB_ID(N'{dbName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{dbName}];
                    END";
                command.ExecuteNonQuery();
            }
            catch
            {
            }
        }
        base.Dispose(disposing);
    }
}
