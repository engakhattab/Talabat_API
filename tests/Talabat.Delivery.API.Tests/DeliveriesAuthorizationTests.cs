namespace Talabat.Delivery.API.Tests;

public sealed class DeliveriesAuthorizationTests : IClassFixture<Infrastructure.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Infrastructure.CustomWebApplicationFactory _factory;

    public DeliveriesAuthorizationTests(Infrastructure.CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetActiveDelivery_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetActiveDelivery_WithWrongScope_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "customer.api");

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetActiveDelivery_WithWrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Customer");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "delivery.api");

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetActiveDelivery_WithCorrectPolicy_PassesAuthorization()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "DeliveryAgent");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "delivery.api");

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        // Authorization passed — handler may return 404 or empty result for no active delivery
        Assert.True(response.StatusCode is not System.Net.HttpStatusCode.Unauthorized
            and not System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPendingDeliveries_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/agent/deliveries/pending");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingDeliveries_WithCorrectPolicy_PassesAuthorization()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "DeliveryAgent");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "delivery.api");

        var response = await _client.GetAsync("/api/agent/deliveries/pending");

        // Authorization passed — handler may return 200 with empty list
        Assert.True(response.StatusCode is not System.Net.HttpStatusCode.Unauthorized
            and not System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignDelivery_WithoutToken_Returns401()
    {
        var response = await _client.PostAsync("/api/agent/deliveries/1/assign", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AssignDelivery_WithCorrectPolicy_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "DeliveryAgent");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "delivery.api");

        var response = await _client.PostAsync("/api/agent/deliveries/999999/assign", null);

        // Should be a domain error (400 or 500), not 401/403
        Assert.True(response.StatusCode is not System.Net.HttpStatusCode.Unauthorized
            and not System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FailDelivery_WithoutToken_Returns401()
    {
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { Reason = "Test" }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/agent/deliveries/1/fail", content);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingDeliveries_RoleOnlyNoCapability_Returns403()
    {
        var userId = _factory.RoleOnlyAgentUserId;

        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add("X-Test-Subject", userId.ToString());
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "DeliveryAgent");
        _client.DefaultRequestHeaders.Add("X-Test-Scope", "delivery.api");

        var response = await _client.GetAsync("/api/agent/deliveries/pending");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
}
