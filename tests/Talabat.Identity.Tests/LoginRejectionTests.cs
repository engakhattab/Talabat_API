using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Identity.Tests;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class LoginRejectionTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;
    private TestDatabase? _database;

    public LoginRejectionTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _database = await _fixture.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Login_active_user_returns_200()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        await RegisterCustomer(email, "P@ssw0rd123!");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new { Email = email, Password = "P@ssw0rd123!" });

        var response = await client.PostAsync("/account/login", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_inactive_user_returns_401()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var userId = await RegisterCustomer(email, "P@ssw0rd123!");
        await DeactivateUser(userId);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new { Email = email, Password = "P@ssw0rd123!" });

        var response = await client.PostAsync("/account/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_deleted_user_returns_401()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var userId = await RegisterCustomer(email, "P@ssw0rd123!");
        await SoftDeleteUser(userId);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new { Email = email, Password = "P@ssw0rd123!" });

        var response = await client.PostAsync("/account/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_wrong_password_returns_401()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        await RegisterCustomer(email, "P@ssw0rd123!");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new { Email = email, Password = "WrongPassword!" });

        var response = await client.PostAsync("/account/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_inactive_returns_same_shape_as_wrong_password_401()
    {
        var activeEmail = $"test_{Guid.NewGuid():N}@example.com";
        await RegisterCustomer(activeEmail, "P@ssw0rd123!");

        var inactiveEmail = $"test_{Guid.NewGuid():N}@example.com";
        var inactiveId = await RegisterCustomer(inactiveEmail, "P@ssw0rd123!");
        await DeactivateUser(inactiveId);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var wrongPwResponse = await client.PostAsync("/account/login",
            Json(new { Email = activeEmail, Password = "WrongPassword!" }));
        var inactiveResponse = await client.PostAsync("/account/login",
            Json(new { Email = inactiveEmail, Password = "P@ssw0rd123!" }));

        Assert.Equal(wrongPwResponse.StatusCode, inactiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPwResponse.StatusCode);
        var wrongDoc = JsonDocument.Parse(await wrongPwResponse.Content.ReadAsStringAsync());
        var inactiveDoc = JsonDocument.Parse(await inactiveResponse.Content.ReadAsStringAsync());
        Assert.Equal(wrongDoc.RootElement.GetProperty("status").GetInt32(),
                     inactiveDoc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(wrongDoc.RootElement.TryGetProperty("title", out var wrongTitle) ? wrongTitle.GetString() : null,
                     inactiveDoc.RootElement.TryGetProperty("title", out var inactiveTitle) ? inactiveTitle.GetString() : null);
    }

    [Fact]
    public async Task SecurityStampValidator_ValidationInterval_is_five_minutes()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>>();
        Assert.Equal(TimeSpan.FromMinutes(5), options.Value.ValidationInterval);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TalabatDbContext));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<TalabatDbContext>(options =>
                    options.UseSqlServer(_database!.ConnectionString));
            });
        });
    }

    private async Task<int> RegisterCustomer(string email, string password)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new
        {
            Email = email,
            Password = password,
            FullName = "Test Customer",
            Age = 30,
            PhoneNumber = (string?)null
        });
        var response = await client.PostAsync("/account/register/customer", payload);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private async Task DeactivateUser(int userId)
    {
        using var scope = CreateFactory().Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user!.Deactivate();
        await userManager.UpdateAsync(user);
    }

    private async Task SoftDeleteUser(int userId)
    {
        using var scope = CreateFactory().Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var user = await dbContext.Users.FindAsync(userId);
        Assert.NotNull(user);
        user!.SoftDelete(DateTime.UtcNow, "test");
        await dbContext.SaveChangesAsync();
    }

    private static StringContent Json(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
