using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Customer.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class CartEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CartEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");
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
    public async Task AddItem_AfterExpiredCart_StartsNewActiveCart()
    {
        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        ownerClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");
        ownerClient.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");
        ownerClient.DefaultRequestHeaders.Add(
            TestAuthHandler.SubjectHeader,
            _factory.OwnerCustomerId.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
            var expiredCart = Cart.Create(
                _factory.OwnerCustomerId,
                new CatalogProductSnapshot(101, 1, "Mixed Grill Plate", isAvailable: true),
                quantity: 1,
                DateTime.UtcNow.AddHours(-3));

            db.Carts.Add(expiredCart);
            await db.SaveChangesAsync();
        }

        var request = new { RestaurantId = 1, ProductId = 101, Quantity = 2 };
        var response = await ownerClient.PostAsJsonAsync("/api/me/cart/items", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
            var carts = await db.Carts
                .Where(cart => cart.CustomerId == _factory.OwnerCustomerId)
                .ToListAsync();

            Assert.Equal(2, carts.Count);
            var active = Assert.Single(carts, cart => cart.Status == CartStatus.Active);
            Assert.Single(carts, cart => cart.Status == CartStatus.Expired);
            Assert.Equal(active.Id, body?.Id);
            Assert.Contains(active.Items, item => item.ProductId == 101 && item.Quantity == 2);
        }
    }

    private sealed record CartResponse(int? Id, int CustomerId, int? RestaurantId, string Status);

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
