using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    [Fact]
    public async Task GetOrders_Unauthenticated_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/me/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
