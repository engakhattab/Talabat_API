using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Development.E2E;
using Talabat.Infrastructure.Persistence;
using Talabat.Infrastructure.Tests.Persistence;

namespace Talabat.Infrastructure.Tests.Development.E2E;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class E2EProvisionerTests
{
    private const string Marker = "TALABAT-E2E-T008-TEST";
    private const string Email = "e2e.provisioner@talabat.test";
    private const string Password = "E2e-Provisioner1!";

    private readonly SqlServerDatabaseFixture _fixture;

    public E2EProvisionerTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Provisioning_sequence_is_idempotent_and_recovers_fixture_state()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = CreateProvider(
            database.ConnectionString,
            Environments.Development,
            enabled: true);

        var firstPrepare = await ExecuteAsync(provider, E2EProvisioningOperation.Prepare);
        await AddActiveFixtureCartAsync(provider, firstPrepare);
        var secondPrepare = await ExecuteAsync(provider, E2EProvisioningOperation.Prepare);

        Assert.Equal(firstPrepare.Addresses.Default.Id, secondPrepare.Addresses.Default.Id);
        Assert.Equal(firstPrepare.Addresses.Alternate.Id, secondPrepare.Addresses.Alternate.Id);
        Assert.Equal(firstPrepare.Catalog.Restaurant.Id, secondPrepare.Catalog.Restaurant.Id);
        Assert.Equal(firstPrepare.Catalog.HappyProduct.Id, secondPrepare.Catalog.HappyProduct.Id);
        Assert.Equal(firstPrepare.Catalog.NegativeProduct.Id, secondPrepare.Catalog.NegativeProduct.Id);
        await AssertBaselineAsync(provider, secondPrepare);

        var unavailable = await ExecuteAsync(
            provider,
            E2EProvisioningOperation.MakeNegativeProductUnavailable);
        Assert.False(unavailable.Catalog.NegativeProduct.Available);
        await AssertNegativeAvailabilityAsync(provider, unavailable, expected: false);

        var firstRestore = await ExecuteAsync(provider, E2EProvisioningOperation.Restore);
        var secondRestore = await ExecuteAsync(provider, E2EProvisioningOperation.Restore);
        Assert.True(firstRestore.Catalog.NegativeProduct.Available);
        Assert.True(secondRestore.Catalog.NegativeProduct.Available);
        await AssertBaselineAsync(provider, secondRestore);

        var finalPrepare = await ExecuteAsync(provider, E2EProvisioningOperation.Prepare);
        await AssertBaselineAsync(provider, finalPrepare);

        var json = E2EProvisioningJson.Serialize(finalPrepare);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("prepare", document.RootElement.GetProperty("operation").GetString());
        Assert.DoesNotContain(Password, json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production", true, "unsupported_environment")]
    [InlineData("Development", false, "provisioning_disabled")]
    public async Task Provisioning_refuses_unsafe_configuration_before_database_access(
        string environment,
        bool enabled,
        string expectedCode)
    {
        const string unreachableConnection =
            "Server=127.0.0.1,1;Database=NeverContacted;User Id=none;Password=none;Connect Timeout=1;TrustServerCertificate=True";
        await using var provider = CreateProvider(unreachableConnection, environment, enabled);

        await using var scope = provider.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<IE2EProvisioner>();
        var exception = await Assert.ThrowsAsync<E2EProvisioningException>(
            () => provisioner.ExecuteAsync(E2EProvisioningOperation.Prepare));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public async Task Prepare_refuses_an_existing_customer_that_is_not_marker_owned()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = CreateProvider(
            database.ConnectionString,
            Environments.Development,
            enabled: true);

        await using (var scope = provider.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var existingUser = User.Register(Email, Email, "Regular customer");
            existingUser.InitializeCustomerProfile("Regular customer", 30, null);
            var result = await userManager.CreateAsync(existingUser, Password);
            Assert.True(result.Succeeded);
        }

        await using var verificationScope = provider.CreateAsyncScope();
        var provisioner = verificationScope.ServiceProvider.GetRequiredService<IE2EProvisioner>();
        var exception = await Assert.ThrowsAsync<E2EProvisioningException>(
            () => provisioner.ExecuteAsync(E2EProvisioningOperation.Prepare));

        Assert.Equal("customer_fixture_not_owned", exception.Code);
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        string environment,
        bool enabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TalabatDb"] = connectionString,
                ["E2E_CUSTOMER_EMAIL"] = Email,
                ["E2E_CUSTOMER_PASSWORD"] = Password,
                ["E2E:Provisioning:Enabled"] = enabled.ToString(),
                ["E2E:Provisioning:Marker"] = Marker,
                ["E2E:Provisioning:CustomerEmailMarker"] = "e2e",
                ["E2E:Provisioning:DisplayName"] = $"Talabat E2E Test Customer [{Marker}]",
                ["E2E:Provisioning:Age"] = "30",
                ["E2E:Provisioning:PhoneNumber"] = "+201000000099",
                ["E2E:Provisioning:DefaultAddress:Label"] = $"E2E Default Address [{Marker}]",
                ["E2E:Provisioning:DefaultAddress:City"] = "Cairo",
                ["E2E:Provisioning:DefaultAddress:BuildingNumber"] = "TEST-A",
                ["E2E:Provisioning:DefaultAddress:Floor"] = "1",
                ["E2E:Provisioning:AlternateAddress:Label"] = $"E2E Alternate Address [{Marker}]",
                ["E2E:Provisioning:AlternateAddress:City"] = "Giza",
                ["E2E:Provisioning:AlternateAddress:BuildingNumber"] = "TEST-B",
                ["E2E:Provisioning:AlternateAddress:Floor"] = "2",
                ["E2E:Provisioning:Catalog:RestaurantName"] = $"Talabat E2E Restaurant [{Marker}]",
                ["E2E:Provisioning:Catalog:HappyProductName"] = $"E2E Happy Product [{Marker}]",
                ["E2E:Provisioning:Catalog:HappyProductPrice"] = "125.00",
                ["E2E:Provisioning:Catalog:NegativeProductName"] = $"E2E Negative Product [{Marker}]",
                ["E2E:Provisioning:Catalog:NegativeProductPrice"] = "95.00"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment));
        services.AddDataProtection();
        services.AddInfrastructure(configuration);
        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<TalabatDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    private static async Task<E2EProvisioningResult> ExecuteAsync(
        ServiceProvider provider,
        E2EProvisioningOperation operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<IE2EProvisioner>();
        return await provisioner.ExecuteAsync(operation);
    }

    private static async Task AddActiveFixtureCartAsync(
        ServiceProvider provider,
        E2EProvisioningResult fixture)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var user = await dbContext.Users.SingleAsync(user => user.Email == Email);
        var cart = Cart.Create(
            user.Id,
            new CatalogProductSnapshot(
                fixture.Catalog.NegativeProduct.Id,
                fixture.Catalog.Restaurant.Id,
                fixture.Catalog.NegativeProduct.Name,
                isAvailable: true),
            quantity: 1,
            DateTime.UtcNow);

        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertBaselineAsync(
        ServiceProvider provider,
        E2EProvisioningResult fixture)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var users = await dbContext.Users
            .Include("_addresses")
            .Where(user => user.Email == Email)
            .ToListAsync();
        var user = Assert.Single(users);
        Assert.True(user.IsActive);
        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.Equal($"Talabat E2E Test Customer [{Marker}]", user.FullName);
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
        Assert.True(await userManager.CheckPasswordAsync(user, Password));

        var ownedAddresses = user.Addresses
            .Where(address => address.Details.Street.Contains(Marker, StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, ownedAddresses.Count);
        Assert.Single(user.Addresses, address => address.IsDefault);
        Assert.Equal(fixture.Addresses.Default.Id, ownedAddresses.Single(address => address.IsDefault).Id);
        Assert.Contains(ownedAddresses, address => address.Id == fixture.Addresses.Alternate.Id && !address.IsDefault);

        Assert.False(await dbContext.Carts.AnyAsync(
            cart => cart.CustomerId == user.Id && cart.Status == CartStatus.Active));
        Assert.Equal(1, await dbContext.Restaurants.CountAsync(
            restaurant => restaurant.Name == fixture.Catalog.Restaurant.Name));
        Assert.Equal(2, await dbContext.Products.CountAsync(
            product => product.RestaurantId == fixture.Catalog.Restaurant.Id
                && (product.Id == fixture.Catalog.HappyProduct.Id
                    || product.Id == fixture.Catalog.NegativeProduct.Id)));
        Assert.True(fixture.Catalog.HappyProduct.Available);
        Assert.True(fixture.Catalog.NegativeProduct.Available);
        await AssertNegativeAvailabilityAsync(provider, fixture, expected: true);
    }

    private static async Task AssertNegativeAvailabilityAsync(
        ServiceProvider provider,
        E2EProvisioningResult fixture,
        bool expected)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var persistedAvailability = await dbContext.Products
            .Where(product => product.Id == fixture.Catalog.NegativeProduct.Id)
            .Select(product => product.IsAvailable)
            .SingleAsync();

        Assert.Equal(expected, persistedAvailability);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Talabat.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
