using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Customer.API.Tests.Infrastructure;
using Talabat.Infrastructure.Persistence;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class CheckoutEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public CheckoutEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");
    }

    [Fact]
    public async Task Checkout_Authenticated_CreatesDeliveryWithRestaurantPickupSnapshot()
    {
        var request = new { DeliveryAddressId = 1 };
        var response = await _client.PostAsJsonAsync("/api/me/checkout", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var responseDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var orderId = responseDocument.RootElement.GetProperty("orderId").GetInt32();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var delivery = await dbContext.Deliveries.SingleAsync(item => item.OrderId == orderId);

        Assert.Equal("Ownership Restaurant", delivery.RestaurantName);
        Assert.Equal("5 Pickup Street", delivery.RestaurantPickupAddress.Street);
        Assert.Equal("Cairo", delivery.RestaurantPickupAddress.City);
        Assert.Equal("5", delivery.RestaurantPickupAddress.BuildingNumber);
        Assert.Equal("Ground Floor", delivery.RestaurantPickupAddress.Floor);
    }

    [Fact]
    public async Task Checkout_Unauthenticated_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var request = new { DeliveryAddressId = 1 };
        var response = await _client.PostAsJsonAsync("/api/me/checkout", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
