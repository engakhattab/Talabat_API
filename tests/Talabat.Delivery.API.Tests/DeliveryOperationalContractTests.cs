using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Talabat.Delivery.API.Tests.Infrastructure;

namespace Talabat.Delivery.API.Tests;

public sealed class DeliveryOperationalContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public DeliveryOperationalContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(
            TestAuthHandler.SubjectHeader,
            factory.DeliveryAgentUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
    }

    [Fact]
    public async Task Pending_ExposesPickupDecisionDataWithoutCustomerDestination()
    {
        var response = await _client.GetAsync("/api/agent/deliveries/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var delivery = document.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == _factory.PendingDeliveryId);

        Assert.Equal("Ownership Restaurant", delivery.GetProperty("restaurantName").GetString());
        Assert.Equal("5 Pickup Street", delivery.GetProperty("pickupStreet").GetString());
        Assert.Equal("Cairo", delivery.GetProperty("pickupCity").GetString());
        Assert.False(delivery.TryGetProperty("customerId", out _));
        Assert.False(delivery.TryGetProperty("customerPhoneNumber", out _));
        Assert.False(delivery.TryGetProperty("street", out _));
        Assert.False(delivery.TryGetProperty("city", out _));
        Assert.False(delivery.TryGetProperty("buildingNumber", out _));
        Assert.False(delivery.TryGetProperty("floor", out _));
        Assert.False(delivery.TryGetProperty("dropOffStreet", out _));
        Assert.DoesNotContain("Private Destination", delivery.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Active_ExposesPickupDropOffAndPhoneWithoutCustomerId()
    {
        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var delivery = document.RootElement;

        Assert.Equal("Ownership Restaurant", delivery.GetProperty("restaurantName").GetString());
        Assert.Equal("5 Pickup Street", delivery.GetProperty("pickupStreet").GetString());
        Assert.Equal("Cairo", delivery.GetProperty("pickupCity").GetString());
        Assert.Equal("5", delivery.GetProperty("pickupBuildingNumber").GetString());
        Assert.Equal("Ground Floor", delivery.GetProperty("pickupFloor").GetString());
        Assert.Equal("1 Test Street", delivery.GetProperty("dropOffStreet").GetString());
        Assert.Equal("Cairo", delivery.GetProperty("dropOffCity").GetString());
        Assert.Equal("1", delivery.GetProperty("dropOffBuildingNumber").GetString());
        Assert.Equal("+201000000099", delivery.GetProperty("customerPhoneNumber").GetString());
        Assert.False(delivery.TryGetProperty("customerId", out _));
    }

    [Fact]
    public async Task Active_AnotherAgentCannotReadAssignedDelivery()
    {
        using var otherAgentClient = _factory.CreateClient();
        otherAgentClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        otherAgentClient.DefaultRequestHeaders.Add(
            TestAuthHandler.SubjectHeader,
            _factory.AgentBUserId.ToString());
        otherAgentClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        otherAgentClient.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");

        var response = await otherAgentClient.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task History_ExposesNeitherCustomerIdNorPhoneNumber()
    {
        var response = await _client.GetAsync("/api/agent/deliveries/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var delivery = document.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == _factory.DeliveryId);

        Assert.False(delivery.TryGetProperty("customerId", out _));
        Assert.False(delivery.TryGetProperty("customerPhoneNumber", out _));
    }

    [Fact]
    public async Task OpenApi_DeliverySchemasContainNoRestaurantCoordinatesOrForbiddenPii()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var pending = schemas.GetProperty("PendingDeliveryResponse").GetProperty("properties");
        var active = schemas.GetProperty("ActiveDeliveryResponse").GetProperty("properties");
        var history = schemas.GetProperty("DeliveryHistoryResponse").GetProperty("properties");

        Assert.False(pending.TryGetProperty("customerId", out _));
        Assert.False(pending.TryGetProperty("customerPhoneNumber", out _));
        Assert.False(pending.TryGetProperty("dropOffCity", out _));
        Assert.False(active.TryGetProperty("customerId", out _));
        Assert.False(history.TryGetProperty("customerId", out _));
        Assert.False(history.TryGetProperty("customerPhoneNumber", out _));

        foreach (var properties in new[] { pending, active, history })
        {
            Assert.DoesNotContain(
                properties.EnumerateObject(),
                property => property.Name.Contains("latitude", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("longitude", StringComparison.OrdinalIgnoreCase));
        }
    }
}
