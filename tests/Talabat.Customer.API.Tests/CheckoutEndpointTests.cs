using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Customer.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.RestaurantOperations;
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

    // Replaces Checkout_Authenticated_CreatesDeliveryWithRestaurantPickupSnapshot (design §4.1,
    // BR-DEL-001/016 revised by D3): checkout places the order with the restaurant and creates no
    // delivery; the pickup snapshot is taken on acceptance (test 16, T2).
    [Fact]
    public async Task Checkout_Authenticated_PlacesOrderWithRestaurantAndCreatesNoDelivery()
    {
        var request = new { DeliveryAddressId = 1 };
        var response = await _client.PostAsJsonAsync("/api/me/checkout", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var responseDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var orderId = responseDocument.RootElement.GetProperty("orderId").GetInt32();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var fulfillment = await dbContext.Set<OrderFulfillment>().SingleAsync(item => item.OrderId == orderId);

        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, fulfillment.Status);
        Assert.Equal(fulfillment.PlacedAt.AddMinutes(10), fulfillment.AcceptanceDeadline);
        Assert.False(await dbContext.Deliveries.AnyAsync(item => item.OrderId == orderId));
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
