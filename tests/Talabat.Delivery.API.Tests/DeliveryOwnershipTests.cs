using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Talabat.Delivery.API.Tests.Infrastructure;

namespace Talabat.Delivery.API.Tests;

public sealed class DeliveryOwnershipTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public DeliveryOwnershipTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void AuthenticateAsAgentA()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.DeliveryAgentUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
    }

    private void AuthenticateAsAgentB()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.AgentBUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
    }

    private void ClearAuthHeaders()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.SubjectHeader);
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeader);
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.ScopeHeader);
    }

    // ── Agent B cannot progress Agent A's delivery ──────────────────

    [Fact]
    public async Task AgentB_OutForDelivery_OnAgentADelivery_Returns404()
    {
        AuthenticateAsAgentB();

        var response = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/out-for-delivery", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentB_ArrivedAtRestaurant_OnAgentADelivery_Returns404()
    {
        AuthenticateAsAgentB();

        var response = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/arrived-at-restaurant", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentB_PickedUp_OnAgentADelivery_Returns404()
    {
        AuthenticateAsAgentB();

        var response = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/picked-up", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentB_Delivered_OnAgentADelivery_Returns404()
    {
        AuthenticateAsAgentB();

        var response = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/delivered", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentB_Cancel_OnAgentADelivery_Returns404()
    {
        AuthenticateAsAgentB();

        var response = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AgentB_Fail_OnAgentADelivery_Returns404()
    {
        AuthenticateAsAgentB();
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { Reason = "Test" }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/fail", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Agent A can progress their own delivery ─────────────────────

    [Fact]
    public async Task AgentA_ArrivedAtRestaurant_OnOwnDelivery_ReturnsSuccess()
    {
        ClearAuthHeaders();
        AuthenticateAsAgentA();

        var response = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/arrived-at-restaurant", null);

        Assert.True(response.StatusCode is not HttpStatusCode.Unauthorized
            and not HttpStatusCode.Forbidden
            and not HttpStatusCode.NotFound);
    }

    // ── Query endpoints are agent-scoped ────────────────────────────

    [Fact]
    public async Task AgentB_ActiveDelivery_DoesNotReturnAgentADelivery()
    {
        ClearAuthHeaders();
        AuthenticateAsAgentB();

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.True(response.StatusCode is not HttpStatusCode.Unauthorized
            and not HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(_factory.DeliveryId.ToString(), body);
        }
    }

    [Fact]
    public async Task AgentA_ActiveDelivery_ReturnsOwnDelivery()
    {
        ClearAuthHeaders();
        AuthenticateAsAgentA();

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.True(response.StatusCode is not HttpStatusCode.Unauthorized
            and not HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AgentA_History_ContainsOwnDelivery()
    {
        ClearAuthHeaders();
        AuthenticateAsAgentA();

        var response = await _client.GetAsync("/api/agent/deliveries/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(_factory.DeliveryId.ToString(), body);
    }

    [Fact]
    public async Task AgentB_History_DoesNotContainAgentADelivery()
    {
        ClearAuthHeaders();
        AuthenticateAsAgentB();

        var response = await _client.GetAsync("/api/agent/deliveries/history");

        Assert.True(response.StatusCode is not HttpStatusCode.Unauthorized
            and not HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(_factory.DeliveryId.ToString(), body);
        }
    }

    // ── No lifecycle endpoint accepts agent id from route/query/body ─

    [Fact]
    public void No_DeliveriesController_Action_Accepts_AgentId_Parameter()
    {
        var controllerType = typeof(Talabat.Delivery.API.Controllers.DeliveriesController);
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            foreach (var param in parameters)
            {
                Assert.False(
                    param.Name?.Contains("AgentId", StringComparison.OrdinalIgnoreCase) == true,
                    $"{method.Name} has parameter '{param.Name}' which could expose agent id via route/body.");
            }
        }
    }
}
