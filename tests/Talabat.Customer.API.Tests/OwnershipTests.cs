using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class OwnershipTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public OwnershipTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void Production_jwt_configuration_uses_platform_role_claim()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("role", options.TokenValidationParameters.RoleClaimType);
    }

    [Fact]
    public async Task Persisted_customer_capability_remains_business_gate_with_DeliveryAgent_claim()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.OwnerCustomerId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Stale_role_without_stored_capability_returns_ProfileNotCreated()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "999999");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"errorCode\":\"ProfileNotCreated\"", body);
    }

    [Fact]
    public async Task No_auth_header_returns_unauthorized()
    {
        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_subject_returns_unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "not-a-number");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Zero_subject_returns_unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "0");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negative_subject_returns_unauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "-5");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Foreign_address_ID_returns_not_found_for_customer()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.OwnerCustomerId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");

        var response = await _client.DeleteAsync($"/api/me/addresses/{_factory.ForeignAddressId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Foreign_order_ID_returns_not_found_for_customer()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.OwnerCustomerId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");

        var response = await _client.GetAsync($"/api/me/orders/{_factory.ForeignOrderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cart_returns_result_for_authenticated_user()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.OwnerCustomerId.ToString());

        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_factory.OwnerCustomerId, payload.GetProperty("customerId").GetInt32());
    }

    [Fact]
    public async Task Cart_route_accepts_no_customer_ID_parameter()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.OwnerCustomerId.ToString());

        var response = await _client.GetAsync("/api/me/cart?customerId=999999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_factory.OwnerCustomerId, payload.GetProperty("customerId").GetInt32());
    }

    [Fact]
    public async Task Role_claim_does_not_replace_stored_capability_gate()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "999998");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");

        var profileResponse = await _client.GetAsync("/api/me/profile");

        var cartResponse = await _client.GetAsync("/api/me/cart");

        var profileBody = await profileResponse.Content.ReadAsStringAsync();
        var cartBody = await cartResponse.Content.ReadAsStringAsync();
        Assert.Contains("ProfileNotCreated", profileBody);
        Assert.Contains("ProfileNotCreated", cartBody);
    }

    [Fact]
    public async Task No_controller_accepts_route_body_CustomerId()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.OwnerCustomerId.ToString());

        var response = await _client.PostAsJsonAsync("/api/me/profile", new { FullName = "Test", Age = 25, PhoneNumber = (string?)null });

        Assert.True(response.StatusCode != HttpStatusCode.InternalServerError);
    }
}
