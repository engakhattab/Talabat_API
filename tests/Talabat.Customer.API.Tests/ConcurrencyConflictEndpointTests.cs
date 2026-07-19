using System.Net;
using System.Net.Http.Json;
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
using Talabat.Domain.Interfaces;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;
using Xunit;

namespace Talabat.Customer.API.Tests.Infrastructure;

public sealed class ConcurrencyConflictEndpointTests : IClassFixture<ConcurrencyConflictEndpointTests.Factory>
{
    private readonly HttpClient _client;

    public ConcurrencyConflictEndpointTests(Factory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateProfile_concurrent_save_returns_409_ConcurrencyConflict()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", "test-token");

        var update = new { FullName = "Updated Name", Age = 30, PhoneNumber = (string?)null };
        var response = await _client.PutAsJsonAsync("/api/me/profile", update);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.NotNull(body);
        Assert.Equal(409, body!.Status);
        Assert.Equal("ConcurrencyConflict", body.ErrorCode);
        Assert.Contains("modified by another process", body.Detail);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private string? _connectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var sp = services.BuildServiceProvider();
                var config = sp.GetRequiredService<IConfiguration>();
                var originalConnectionString = config.GetConnectionString("TalabatDb");

                var connectionBuilder = new SqlConnectionStringBuilder(originalConnectionString)
                {
                    InitialCatalog = $"TalabatTest_API_Conc_{Guid.NewGuid():N}"
                };
                _connectionString = connectionBuilder.ConnectionString;

                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<TalabatDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<TalabatDbContext>((serviceProvider, options) =>
                {
                    options.UseSqlServer(_connectionString);
                    var interceptor = serviceProvider.GetService<Talabat.Infrastructure.Persistence.Auditing.AuditableEntitySaveChangesInterceptor>();
                    if (interceptor != null)
                        options.AddInterceptors(interceptor);
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

                var unitOfWorkDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IUnitOfWork));
                if (unitOfWorkDescriptor != null)
                    services.Remove(unitOfWorkDescriptor);

                services.AddScoped<IUnitOfWork>(sp =>
                {
                    var inner = new UnitOfWork(sp.GetRequiredService<TalabatDbContext>());
                    return new ThrowingUnitOfWork(inner);
                });
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
                        user.InitializeCustomerProfile("Test User", 25, null);
                        userManager.CreateAsync(user, "Password1!").GetAwaiter().GetResult();
                        userManager.AddToRoleAsync(user, "Customer").GetAwaiter().GetResult();
                    }
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

    private sealed class ProblemDetailsResponse
    {
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? ErrorCode { get; set; }
    }
}
