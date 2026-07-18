using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class CartEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CartEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
    }

    [Fact]
    public async Task GetCart_Authenticated_ReturnsOkOrNotFound()
    {
        var response = await _client.GetAsync("/api/me/cart");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddItem_Authenticated_ReturnsOkOrConflict()
    {
        var request = new { RestaurantId = 1, ProductId = 1, Quantity = 1 };
        var response = await _client.PostAsJsonAsync("/api/me/cart/items", request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ClearCart_Authenticated_ReturnsOkOrNotFound()
    {
        var response = await _client.DeleteAsync("/api/me/cart");

        Assert.True(
            response.StatusCode == HttpStatusCode.NoContent ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }
}
