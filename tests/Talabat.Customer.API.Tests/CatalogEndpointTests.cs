using System.Net;
using System.Net.Http.Json;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class CatalogEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CatalogEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRestaurants_Anonymous_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/catalog/restaurants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRestaurants_ReturnsExpectedShape()
    {
        var response = await _client.GetAsync("/api/catalog/restaurants");
        var content = await response.Content.ReadFromJsonAsync<CatalogListResponse>();

        Assert.NotNull(content);
        Assert.NotNull(content.Items);
    }

    [Fact]
    public async Task GetMenu_RestaurantExists_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/catalog/restaurants/1/menu");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound);
    }

    private sealed record CatalogListResponse(
        IReadOnlyCollection<CatalogItem> Items,
        int Page,
        int PageSize,
        int TotalCount);

    private sealed record CatalogItem(
        int Id,
        string Name,
        string Description,
        string? ImageUrl,
        bool IsActive);
}
