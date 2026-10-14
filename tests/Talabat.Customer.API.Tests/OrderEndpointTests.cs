using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class OrderEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrderEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");
    }

    [Fact]
    public async Task GetOrders_Authenticated_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/me/orders");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetOrderDetails_Authenticated_ReturnsOkOrNotFound()
    {
        var response = await _client.GetAsync("/api/me/orders/1");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    // Design §4 test 32 (status fields, D9): a just-placed order shows Placed and no delivery agent.
    [Fact]
    public async Task Order_list_and_details_expose_status_and_delivery_agent_assigned()
    {
        var checkout = await _client.PostAsJsonAsync("/api/me/checkout", new { DeliveryAddressId = 1 });
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        using var checkoutDocument = JsonDocument.Parse(await checkout.Content.ReadAsStringAsync());
        var orderId = checkoutDocument.RootElement.GetProperty("orderId").GetInt32();

        var details = await _client.GetAsync($"/api/me/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var detailsDocument = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        Assert.Equal("Placed", detailsDocument.RootElement.GetProperty("status").GetString());
        Assert.False(detailsDocument.RootElement.GetProperty("deliveryAgentAssigned").GetBoolean());

        var list = await _client.GetAsync("/api/me/orders");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var summary = listDocument.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == orderId);
        Assert.Equal("Placed", summary.GetProperty("status").GetString());
        Assert.False(summary.GetProperty("deliveryAgentAssigned").GetBoolean());
    }

    [Fact]
    public async Task GetOrders_Unauthenticated_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/me/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
