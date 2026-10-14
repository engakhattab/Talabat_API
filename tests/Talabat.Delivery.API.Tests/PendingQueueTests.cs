using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Application.Fulfillment;
using Talabat.Application.Fulfillment.AcceptOrder;
using Talabat.Delivery.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Delivery.API.Tests;

/// <summary>Design §4 test 33 (part): delivery agents only ever see orders a restaurant has accepted (D3).</summary>
public sealed class PendingQueueTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PendingQueueTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, factory.DeliveryAgentUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
    }

    [Fact]
    public async Task Pending_queue_excludes_unaccepted_orders_and_includes_them_once_accepted()
    {
        var orderId = await PlaceOrderAsync();

        Assert.DoesNotContain(orderId, await GetPendingOrderIdsAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            var accept = scope.ServiceProvider.GetRequiredService<AcceptOrderHandler>();
            var result = await accept.Handle(new AcceptOrderCommand(orderId, new FulfillmentActor.Admin()));
            Assert.True(result.IsSuccess);
        }

        Assert.Contains(orderId, await GetPendingOrderIdsAsync());
    }

    private async Task<IReadOnlyCollection<int>> GetPendingOrderIdsAsync()
    {
        var response = await _client.GetAsync("/api/agent/deliveries/pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("orderId").GetInt32())
            .ToList();
    }

    /// <summary>Arranges what checkout leaves behind: an order and its fulfillment awaiting the restaurant.</summary>
    private async Task<int> PlaceOrderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var customerId = await db.Orders.Select(order => order.CustomerId).FirstAsync();
        var placedAt = DateTime.UtcNow;
        var order = Order.CreateFromCheckout(
            customerId,
            restaurantId: 1,
            [new CheckoutItemSnapshot(101, "Seeded item", new Money(50m), 1)],
            new DeliveryAddressSnapshot("Street", "Cairo", "10"),
            placedAt);
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();

        await db.OrderFulfillments.AddAsync(
            new OrderFulfillment(order.Id, order.RestaurantId, placedAt, TimeSpan.FromMinutes(10)));
        await db.SaveChangesAsync();

        return order.Id;
    }
}
