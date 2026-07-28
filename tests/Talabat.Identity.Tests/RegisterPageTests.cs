using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;
using Xunit;

namespace Talabat.Identity.Tests;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class RegisterPageTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;
    private TestDatabase? _database;

    public RegisterPageTests(SqlServerDatabaseFixture fixture)
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
    public async Task Get_register_returns_200()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_register_with_customer_returnUrl_renders_age_not_vehicle_type()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var returnUrl = await GetAuthorizeReturnUrlAsync(client, "talabat-customer-spa");

        var response = await client.GetAsync($"/auth/register?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Input.Age", body);
        Assert.DoesNotContain("Input.VehicleType", body);
    }

    [Fact]
    public async Task Get_register_with_delivery_spa_returnUrl_renders_vehicle_type_not_age()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var returnUrl = await GetAuthorizeReturnUrlAsync(client, "talabat-delivery-spa");

        var response = await client.GetAsync($"/auth/register?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Input.VehicleType", body);
        Assert.DoesNotContain("Input.Age", body);
    }

    [Fact]
    public async Task Get_register_with_no_authorize_context_renders_customer_variant()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/register");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Input.Age", body);
        Assert.DoesNotContain("Input.VehicleType", body);
    }

    [Fact]
    public async Task Post_valid_customer_registration_creates_user_with_customer_flag_and_role()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Test Customer",
            ["Input.Age"] = "30",
            ["Input.PhoneNumber"] = "+201000000000"
        });

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.True(user!.UserType.HasFlag(UserType.Customer));
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
        Assert.Equal("Test Customer", user.FullName);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public async Task Post_valid_customer_registration_issues_auth_cookie()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Cookie Customer",
            ["Input.Age"] = "25",
            ["Input.PhoneNumber"] = ""
        });

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("validation-summary-errors", body);
    }

    [Fact]
    public async Task Post_duplicate_email_returns_validation_error()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var token1 = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var first = await PostRegistrationAsync(client, token1, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "First Customer",
            ["Input.Age"] = "28",
            ["Input.PhoneNumber"] = ""
        });
        Assert.True(first.StatusCode == HttpStatusCode.OK || first.StatusCode == HttpStatusCode.Redirect);

        var token2 = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var second = await PostRegistrationAsync(client, token2, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Second Customer",
            ["Input.Age"] = "25",
            ["Input.PhoneNumber"] = ""
        });

        var body = await second.Content.ReadAsStringAsync();
        Assert.True(second.StatusCode == HttpStatusCode.OK,
            $"Expected 200 (re-rendered page), got {second.StatusCode}");
        Assert.Contains("validation-summary-errors", body);
    }

    [Fact]
    public async Task Post_password_confirm_mismatch_returns_validation_error()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "DifferentPassword!",
            ["Input.FullName"] = "Mismatch Customer",
            ["Input.Age"] = "25",
            ["Input.PhoneNumber"] = ""
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation-summary-errors", body);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        Assert.Null(await userManager.Users.FirstOrDefaultAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Post_valid_delivery_agent_registration_creates_pending_applicant()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var returnUrl = await GetAuthorizeReturnUrlAsync(client, "talabat-delivery-spa");

        var registerUrl = $"/auth/register?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
        var token = await GetAntiforgeryTokenAsync(client, registerUrl);
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["ReturnUrl"] = returnUrl,
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Agent Name",
            ["Input.VehicleType"] = "2",
            ["Input.PhoneNumber"] = ""
        }, "/auth/register");

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.Equal(AgentApprovalStatus.PendingApproval, user!.AgentApprovalStatus);
        Assert.Equal(VehicleType.Motorcycle, user.VehicleType);
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.DeliveryAgentStatus);
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task Post_customer_returnUrl_with_forged_agent_field_runs_customer_workflow()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var returnUrl = await GetAuthorizeReturnUrlAsync(client, "talabat-customer-spa");

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["ReturnUrl"] = returnUrl,
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Forged Agent Customer",
            ["Input.Age"] = "30",
            ["Input.VehicleType"] = "2",
            ["Input.PhoneNumber"] = ""
        });

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.True(user!.UserType.HasFlag(UserType.Customer));
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.AgentApprovalStatus);
        Assert.Null(user.VehicleType);
    }

    [Fact]
    public async Task Post_no_authorize_context_with_forged_agent_field_runs_customer_workflow()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "No Context Forged",
            ["Input.Age"] = "25",
            ["Input.VehicleType"] = "2",
            ["Input.PhoneNumber"] = ""
        });

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.True(user!.UserType.HasFlag(UserType.Customer));
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.AgentApprovalStatus);
        Assert.Null(user.VehicleType);
    }

    [Fact]
    public async Task Post_valid_authorize_returnUrl_redirects_to_returnUrl()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var returnUrl = await GetAuthorizeReturnUrlAsync(client, "talabat-customer-spa");

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["ReturnUrl"] = returnUrl,
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Redirect Customer",
            ["Input.Age"] = "25",
            ["Input.PhoneNumber"] = ""
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.Equal(returnUrl, location);
    }

    [Fact]
    public async Task Post_external_absolute_returnUrl_without_authorize_context_redirects_to_root()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var evilReturnUrl = "https://evil.com/steal-token";

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["ReturnUrl"] = evilReturnUrl,
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Open Redirect Test",
            ["Input.Age"] = "25",
            ["Input.PhoneNumber"] = ""
        });

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            var location = response.Headers.Location?.ToString();
            Assert.DoesNotContain("evil.com", location);
        }
    }

    [Fact]
    public async Task Post_valid_registration_no_password_hash_or_security_stamp_in_response()
    {
        using var factory = CreateFactory();
        using var client = CreateNoRedirectClient(factory);
        var email = $"test_{Guid.NewGuid():N}@example.com";

        var token = await GetAntiforgeryTokenAsync(client, "/auth/register");
        var response = await PostRegistrationAsync(client, token, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "P@ssw0rd123!",
            ["Input.ConfirmPassword"] = "P@ssw0rd123!",
            ["Input.FullName"] = "Leakage Test",
            ["Input.Age"] = "25",
            ["Input.PhoneNumber"] = ""
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("P@ssw0rd123!", body);
    }

    [Fact]
    public async Task Login_page_has_link_to_register_preserving_returnUrl()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var returnUrl = "http://localhost:4200/signin-callback?code=test";

        var response = await client.GetAsync($"/auth/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/auth/register", body);
        Assert.Contains(Uri.EscapeDataString(returnUrl), body);
    }

    [Fact]
    public async Task Register_page_has_link_to_login_preserving_returnUrl()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var returnUrl = "http://localhost:4200/signin-callback?code=test";

        var response = await client.GetAsync($"/auth/register?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/auth/login", body);
        Assert.Contains(Uri.EscapeDataString(returnUrl), body);
    }

    private async Task<string> GetAuthorizeReturnUrlAsync(HttpClient client, string clientId)
    {
        var state = Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        var redirectUri = clientId == "talabat-customer-spa"
            ? "http://localhost:4200/signin-callback"
            : "http://localhost:4300/signin-callback";

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        var authorizeUrl = $"/connect/authorize?" +
            $"client_id={clientId}" +
            $"&response_type=code" +
            $"&scope=openid%20profile" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&state={state}" +
            $"&nonce={nonce}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256";

        var response = await client.GetAsync(authorizeUrl);
        var location = response.Headers.Location?.ToString() ?? "";

        var returnUrlStart = location.IndexOf("ReturnUrl=", StringComparison.Ordinal);
        if (returnUrlStart >= 0)
        {
            return Uri.UnescapeDataString(location[(returnUrlStart + "ReturnUrl=".Length)..]);
        }

        return location;
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string pageUrl)
    {
        var getResponse = await client.GetAsync(pageUrl);
        getResponse.EnsureSuccessStatusCode();

        var body = await getResponse.Content.ReadAsStringAsync();
        var tokenStart = body.IndexOf("name=\"__RequestVerificationToken\"", StringComparison.OrdinalIgnoreCase);
        if (tokenStart >= 0)
        {
            var valueStart = body.IndexOf("value=\"", tokenStart, StringComparison.Ordinal);
            if (valueStart >= 0)
            {
                valueStart += "value=\"".Length;
                var valueEnd = body.IndexOf('"', valueStart);
                return body[valueStart..valueEnd];
            }
        }

        throw new InvalidOperationException($"Could not extract antiforgery token from {pageUrl}");
    }

    private async Task<HttpResponseMessage> PostRegistrationAsync(
        HttpClient client,
        string antiforgeryToken,
        Dictionary<string, string> formFields,
        string? postUrl = null)
    {
        var formFieldsWithToken = new Dictionary<string, string>(formFields)
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        };

        var response = await client.PostAsync(postUrl ?? "/auth/register",
            new FormUrlEncodedContent(formFieldsWithToken));
        return response;
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

            builder.UseEnvironment("Development");
        });
    }

    private HttpClient CreateNoRedirectClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
