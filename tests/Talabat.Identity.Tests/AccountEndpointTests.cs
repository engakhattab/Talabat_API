using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Talabat.Domain.Aggregates.Users;


namespace Talabat.Identity.Tests;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class AccountEndpointTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;
    private TestDatabase? _database;

    public AccountEndpointTests(SqlServerDatabaseFixture fixture)
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
    public async Task Register_customer_returns_200_with_positive_id_and_email()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Customer Name",
            Age = 30,
            PhoneNumber = "+201000000000"
        });

        var response = await client.PostAsync("/account/register/customer", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_customer_persists_customer_flag_role_and_profile()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Profile Customer",
            Age = 28,
            PhoneNumber = "+201111111111"
        });

        var response = await client.PostAsync("/account/register/customer", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var userId = json.RootElement.GetProperty("id").GetInt32();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.Equal("Profile Customer", user.FullName);
        Assert.Equal(28, user.Age);
        Assert.Equal("+201111111111", user.PhoneNumber);
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
    }

    [Fact]
    public async Task Register_customer_duplicate_email_returns_400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "First Customer",
            Age = 25,
            PhoneNumber = (string?)null
        });

        var first = await client.PostAsync("/account/register/customer", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsync("/account/register/customer", payload);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_credentials_returns_200_and_set_cookie()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        await RegisterCustomer(email, "P@ssw0rd123!");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new { Email = email, Password = "P@ssw0rd123!" });

        var response = await client.PostAsync("/account/login", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, c => c.Contains(".AspNetCore.Identity.Application"));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
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
    public async Task Logout_after_login_returns_200()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        await RegisterCustomer(email, "P@ssw0rd123!");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var loginPayload = Json(new { Email = email, Password = "P@ssw0rd123!" });
        var loginResponse = await client.PostAsync("/account/login", loginPayload);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.PostAsync("/account/logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_me_without_login_returns_401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/account/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_me_after_login_returns_200_with_email()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        await RegisterCustomer(email, "P@ssw0rd123!");

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var loginPayload = Json(new { Email = email, Password = "P@ssw0rd123!" });
        var loginResponse = await client.PostAsync("/account/login", loginPayload);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First();

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        meRequest.Headers.TryAddWithoutValidation("Cookie", cookie);
        var response = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_delivery_agent_returns_200_with_pending_application()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Agent Name",
            VehicleType = 2,
            PhoneNumber = (string?)null
        });

        var response = await client.PostAsync("/account/register/delivery-agent", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(json.RootElement.GetProperty("id").GetInt32().ToString());
        Assert.NotNull(user);
        Assert.Equal(VehicleType.Motorcycle, user.VehicleType);
        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.DeliveryAgentStatus);
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task Register_delivery_agent_with_phone_persists_phone()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Agent With Phone",
            VehicleType = 3,
            PhoneNumber = "+201999999999"
        });

        var response = await client.PostAsync("/account/register/delivery-agent", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var userId = json.RootElement.GetProperty("id").GetInt32();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.Equal("+201999999999", user.PhoneNumber);
        Assert.Equal(VehicleType.Car, user.VehicleType);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Register_delivery_agent_accepts_numeric_vehicle_types(int vehicleType)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = $"Vehicle {vehicleType}",
            VehicleType = vehicleType,
            PhoneNumber = (string?)null
        });

        var response = await client.PostAsync("/account/register/delivery-agent", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_delivery_agent_invalid_vehicle_returns_400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Bad Vehicle",
            VehicleType = 99,
            PhoneNumber = (string?)null
        });

        var response = await client.PostAsync("/account/register/delivery-agent", payload);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_delivery_agent_duplicate_email_returns_error()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "First Agent",
            VehicleType = 2,
            PhoneNumber = (string?)null
        });

        var first = await client.PostAsync("/account/register/delivery-agent", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsync("/account/register/delivery-agent", payload);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Register_delivery_agent_no_role_input_accepted()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "No Role Agent",
            VehicleType = 1,
            PhoneNumber = (string?)null,
            Role = "Admin"
        });

        var response = await client.PostAsync("/account/register/delivery-agent", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var userId = json.RootElement.GetProperty("id").GetInt32();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.False(await userManager.IsInRoleAsync(user, "Admin"));
        Assert.False(user.UserType.HasFlag(UserType.Admin));
    }

    [Fact]
    public async Task No_response_contains_password_hash_or_security_stamp()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var regResponse = await client.PostAsync("/account/register/customer",
            Json(new
            {
                Email = email,
                Password = "P@ssw0rd123!",
                FullName = "Secure Customer",
                Age = 30,
                PhoneNumber = (string?)null
            }));
        var regBody = await regResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", regBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", regBody, StringComparison.OrdinalIgnoreCase);

        var loginResponse = await client.PostAsync("/account/login",
            Json(new { Email = email, Password = "P@ssw0rd123!" }));
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", loginBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", loginBody, StringComparison.OrdinalIgnoreCase);

        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First();
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        meRequest.Headers.TryAddWithoutValidation("Cookie", cookie);
        var meResponse = await client.SendAsync(meRequest);
        var meBody = await meResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", meBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", meBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_customer_request_accepts_no_role_field()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "No Role Customer",
            Age = 25,
            PhoneNumber = (string?)null,
            Role = "Admin"
        });

        var response = await client.PostAsync("/account/register/customer", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.False(await userManager.IsInRoleAsync(user, "Admin"));
        Assert.True(user.UserType.HasFlag(UserType.Customer));
    }

    [Fact]
    public async Task Discovery_endpoint_returns_200_with_api_scopes()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var scopes = json.RootElement.GetProperty("scopes_supported").EnumerateArray()
            .Select(s => s.GetString())
            .ToList();
        Assert.Contains("talabat.customer-api", scopes);
        Assert.Contains("talabat.deliveryagent-api", scopes);
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

    private async Task RegisterCustomer(string email, string password)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new
        {
            Email = email,
            Password = password,
            FullName = "Auto Registered",
            Age = 30,
            PhoneNumber = (string?)null
        });
        var response = await client.PostAsync("/account/register/customer", payload);
        response.EnsureSuccessStatusCode();
    }

    private static StringContent Json(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
