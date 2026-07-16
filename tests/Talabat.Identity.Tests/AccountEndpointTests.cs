using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public async Task Register_with_new_email_returns_200_and_creates_user()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new { Email = email, Password = "P@ssw0rd123!" });

        var response = await client.PostAsync("/account/register", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.NotNull(json.RootElement.GetProperty("id").GetString());
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var user = await dbContext.Users
            .Where(u => u.Email == email)
            .FirstOrDefaultAsync();
        Assert.NotNull(user);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var payload = Json(new { Email = email, Password = "P@ssw0rd123!" });

        var first = await client.PostAsync("/account/register", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsync("/account/register", payload);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_credentials_returns_200_and_set_cookie()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        await RegisterUser(email, "P@ssw0rd123!");

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
        await RegisterUser(email, "P@ssw0rd123!");

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
        await RegisterUser(email, "P@ssw0rd123!");

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
        await RegisterUser(email, "P@ssw0rd123!");

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
    public async Task No_response_contains_password_hash_or_security_stamp()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var regResponse = await client.PostAsync("/account/register",
            Json(new { Email = email, Password = "P@ssw0rd123!" }));
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

    private async Task RegisterUser(string email, string password)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var payload = Json(new { Email = email, Password = password });
        var response = await client.PostAsync("/account/register", payload);
        response.EnsureSuccessStatusCode();
    }

    private static StringContent Json(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
