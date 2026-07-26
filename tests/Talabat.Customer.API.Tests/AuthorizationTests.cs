using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class AuthorizationTests : IClassFixture<Infrastructure.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthorizationTests(Infrastructure.CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Profile: CustomerScopeOnly ──────────────────────────────────

    [Fact]
    public async Task Profile_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithWrongScope_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "delivery.api");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithCorrectScope_PassesAuthorization()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "customer.api");

        var response = await _client.GetAsync("/api/me/profile");

        // CustomerScopeOnly requires scope+auth, no role needed
        Assert.True(response.StatusCode is not System.Net.HttpStatusCode.Unauthorized
            and not System.Net.HttpStatusCode.Forbidden);
    }

    // ── Cart: CustomerAccess (scope + role) ────────────────────────

    [Fact]
    public async Task Cart_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cart_WithScopeOnly_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "customer.api");
        // No role header — should be 403

        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cart_WithCorrectPolicy_PassesAuthorization()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Customer");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "customer.api");

        var response = await _client.GetAsync("/api/me/cart");

        Assert.True(response.StatusCode is not System.Net.HttpStatusCode.Unauthorized
            and not System.Net.HttpStatusCode.Forbidden);
    }

    // ── Checkout: CustomerAccess (scope + role) ─────────────────────

    [Fact]
    public async Task Checkout_WithoutToken_Returns401()
    {
        var response = await _client.PostAsync("/api/me/checkout", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WithCorrectPolicy_PassesAuthorization()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Customer");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "customer.api");

        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/me/checkout", content);

        Assert.True(response.StatusCode is not System.Net.HttpStatusCode.Unauthorized
            and not System.Net.HttpStatusCode.Forbidden);
    }

    // ── Orders: CustomerAccess (scope + role) ───────────────────────

    [Fact]
    public async Task Orders_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/me/orders");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Catalog: Anonymous ──────────────────────────────────────────

    [Fact]
    public async Task Catalog_WithoutToken_Returns200()
    {
        var response = await _client.GetAsync("/api/catalog/restaurants");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Catalog_WithToken_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "customer.api");

        var response = await _client.GetAsync("/api/catalog/restaurants");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // ── Profile creation: CustomerScopeOnly (no role required) ──────

    [Fact]
    public async Task Profile_CreateProfile_WithNoRole_NotForbidden()
    {
        // Profile creation (POST /api/me/profile) uses CustomerScopeOnly
        // so it should NOT return 403 just because the role is missing
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "customer.api");

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { FullName = "New User", Age = 25, PhoneNumber = "1234567890" }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/me/profile", content);

        // Should not be 403 — profile creation uses CustomerScopeOnly (scope + auth)
        Assert.NotEqual(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
}
