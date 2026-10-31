using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Talabat.Delivery.API.Tests.Infrastructure;

namespace Talabat.Delivery.API.Tests;

/// <summary>Design §4 test 33 (part): <c>/release</c> replaces the agent <c>/cancel</c> endpoint (D6).</summary>
public sealed class ReleaseEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReleaseEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        // Building the first client starts the host, which seeds the agents and deliveries the tests read.
        _ = factory.CreateClient();
    }

    [Fact]
    public async Task Release_returns_the_delivery_to_the_queue_and_the_agent_to_available()
    {
        var agentB = CreateAgentClient(_factory.AgentBUserId);
        Assert.Equal(HttpStatusCode.NoContent, (await agentB.PutAsync("/api/agent/status/online", null)).StatusCode);
        var claim = await agentB.PostAsync($"/api/agent/deliveries/{_factory.PendingDeliveryId}/assign", null);
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        Assert.DoesNotContain(_factory.PendingDeliveryId, await GetPendingDeliveryIdsAsync(agentB));

        var release = await agentB.PostAsync($"/api/agent/deliveries/{_factory.PendingDeliveryId}/release", null);

        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
        using (var body = JsonDocument.Parse(await release.Content.ReadAsStringAsync()))
        {
            Assert.Equal(_factory.PendingDeliveryId, body.RootElement.GetProperty("id").GetInt32());
        }

        Assert.Contains(_factory.PendingDeliveryId, await GetPendingDeliveryIdsAsync(agentB));
        Assert.Equal(HttpStatusCode.NotFound, (await agentB.GetAsync("/api/agent/deliveries/active")).StatusCode);
        Assert.Equal("Available", await GetAgentStatusAsync(agentB));
    }

    [Fact]
    public async Task Cancel_endpoint_is_gone()
    {
        var agentA = CreateAgentClient(_factory.DeliveryAgentUserId);

        var response = await agentA.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateAgentClient(int agentId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, agentId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
        return client;
    }

    private static async Task<IReadOnlyCollection<int>> GetPendingDeliveryIdsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/agent/deliveries/pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32())
            .ToList();
    }

    private static async Task<string?> GetAgentStatusAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/agent/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("status").GetString();
    }
}
