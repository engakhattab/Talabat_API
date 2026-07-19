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
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Customer.API.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private string? _connectionString;

    public int ForeignCustomerId { get; private set; }
    public int OwnerCustomerId { get; private set; }
    public int ForeignAddressId { get; private set; }
    public int ForeignOrderId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            var config = sp.GetRequiredService<IConfiguration>();
            var originalConnectionString = config.GetConnectionString("TalabatDb");

            var connectionBuilder = new SqlConnectionStringBuilder(originalConnectionString)
            {
                InitialCatalog = $"TalabatTest_API_{Guid.NewGuid():N}"
            };
            _connectionString = connectionBuilder.ConnectionString;

            // Remove existing DbContextOptions
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
            if (userManager.Users.All(u => u.Id != TestAuthHandler.TestUserId))
            {
                var user = User.Register("testuser", "test@test.com", "Test User");
                userManager.CreateAsync(user, "Password1!").GetAwaiter().GetResult();
            }

            var ownerUser = User.Register(
                $"owner-{Guid.NewGuid():N}",
                $"owner-{Guid.NewGuid():N}@test.com",
                "Owner Customer");
            ownerUser.InitializeCustomerProfile("Owner Customer", 30, null);
            ownerUser.AddAddress(new Address("1 Owner Street", "Cairo", "1"), makeDefault: true);
            userManager.CreateAsync(ownerUser, "Password1!").GetAwaiter().GetResult();
            userManager.AddToRoleAsync(ownerUser, "Customer").GetAwaiter().GetResult();


            var foreignUser = User.Register(
                $"foreign-{Guid.NewGuid():N}",
                $"foreign-{Guid.NewGuid():N}@test.com",
                "Foreign Customer");
            foreignUser.InitializeCustomerProfile("Foreign Customer", 31, null);
            foreignUser.AddAddress(new Address("2 Foreign Street", "Giza", "2"), makeDefault: true);
            userManager.CreateAsync(foreignUser, "Password1!").GetAwaiter().GetResult();
            userManager.AddToRoleAsync(foreignUser, "Customer").GetAwaiter().GetResult();

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

            var foreignOrder = Order.CreateFromCheckout(
                foreignUser.Id,
                restaurant.Id,
                [new CheckoutItemSnapshot(product.Id, product.Name, product.CurrentPrice, 1)],
                foreignUser.CreateDeliveryAddressSnapshot(foreignUser.Addresses.Single().Id),
                DateTime.UtcNow);
            db.Orders.Add(foreignOrder);
            db.SaveChanges();

            ForeignCustomerId = foreignUser.Id;
            OwnerCustomerId = ownerUser.Id;
            ForeignAddressId = foreignUser.Addresses.Single().Id;
            ForeignOrderId = foreignOrder.Id;
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
                // Suppress disposal errors to not crash test run
            }
        }
        base.Dispose(disposing);
    }
}
