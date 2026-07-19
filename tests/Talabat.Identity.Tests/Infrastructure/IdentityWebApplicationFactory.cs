using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Talabat.Application;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Identity.Tests.Infrastructure;

public sealed class IdentityWebApplicationFactory : IAsyncLifetime
{
    private SqlServerDatabaseFixture? _fixture;
    private TestDatabase? _database;
    private bool _zeroValidationInterval;

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _fixture = new SqlServerDatabaseFixture();
        await _fixture.InitializeAsync();
        _database = await _fixture.CreateDatabaseAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    ServiceDescriptor => ServiceDescriptor.ServiceType == typeof(DbContextOptions<TalabatDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<TalabatDbContext>(options =>
                    options.UseSqlServer(_database!.ConnectionString));
                services.AddApplication();

                if (_zeroValidationInterval)
                {
                    var stampDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IOptions<SecurityStampValidatorOptions>));
                    if (stampDescriptor is not null)
                        services.Remove(stampDescriptor);

                    services.Configure<SecurityStampValidatorOptions>(options =>
                    {
                        options.ValidationInterval = TimeSpan.Zero;
                    });
                }
            });

            builder.UseEnvironment("Development");
        });

        using var scope = Factory.Services.CreateScope();
        IdentityDataSeeder.SeedRolesAsync(scope.ServiceProvider).GetAwaiter().GetResult();
    }

    public void ConfigureZeroValidationInterval()
    {
        _zeroValidationInterval = true;
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        if (_database is not null)
            await _database.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory!.CreateClient();
}
