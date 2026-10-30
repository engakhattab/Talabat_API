using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Delivery.API.Tests.Infrastructure;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Delivery.API.Tests;

/// <summary>Design §4 test 33 (part): the pickup gate over HTTP (D5, BR-DEL-019).</summary>
public sealed class PickupGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PickupGateTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, factory.DeliveryAgentUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
    }

    [Fact]
    public async Task Pickup_before_ready_returns_409_FulfillmentNotReady_and_succeeds_once_ready()
    {
        var arrive = await _client.PostAsync(
            $"/api/agent/deliveries/{_factory.DeliveryId}/arrived-at-restaurant", null);
        Assert.Equal(HttpStatusCode.OK, arrive.StatusCode);

        var early = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/picked-up", null);

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        using (var problem = JsonDocument.Parse(await early.Content.ReadAsStringAsync()))
        {
            Assert.Equal("FulfillmentNotReady", problem.RootElement.GetProperty("errorCode").GetString());
        }

        Assert.Equal("ArrivedAtRestaurant", await GetActiveAsync("status"));
        Assert.Equal("Accepted", await GetActiveAsync("fulfillmentStatus"));

        await MarkReadyAsync();
        Assert.Equal("ReadyForPickup", await GetActiveAsync("fulfillmentStatus"));

        var pickup = await _client.PostAsync($"/api/agent/deliveries/{_factory.DeliveryId}/picked-up", null);

        Assert.Equal(HttpStatusCode.OK, pickup.StatusCode);
        Assert.Equal("PickedUp", await GetActiveAsync("status"));
        Assert.Equal("HandedOver", await GetActiveAsync("fulfillmentStatus"));
    }

    private async Task<string?> GetActiveAsync(string property)
    {
        var response = await _client.GetAsync("/api/agent/deliveries/active");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(property).GetString();
    }

    /// <summary>The kitchen side runs on the Admin host; here the restaurant marks the order ready directly.</summary>
    private async Task MarkReadyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var fulfillment = await db.OrderFulfillments.SingleAsync(f => f.OrderId == _factory.OrderId);
        fulfillment.MarkReady(fulfillment.RestaurantId, DateTime.UtcNow);
        await db.SaveChangesAsync();
    }
}
