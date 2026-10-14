using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Admin.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Admin.API.Tests;

/// <summary>Design §4 test 34 (part): the Admin accepts an order on the restaurant's behalf (D3, D11).</summary>
public sealed class OrderAcceptanceTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public OrderAcceptanceTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        using var _ = _factory.CreateClient();
    }

    [Fact]
    public async Task Accept_requires_admin_access()
    {
        var orderId = await PlaceOrderAsync();
        using var anonymous = _factory.CreateClient();
        using var nonAdmin = CreateClient(_factory.NonAdminUserId, null, "admin.api");
        using var noScope = CreateClient(_factory.AdminUserId, "Admin", "delivery.api");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(AcceptUrl(orderId), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nonAdmin.PostAsync(AcceptUrl(orderId), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noScope.PostAsync(AcceptUrl(orderId), null)).StatusCode);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Fact]
    public async Task Admin_accepts_a_placed_order_and_its_delivery_waits_for_an_agent()
    {
        var orderId = await PlaceOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(AcceptUrl(orderId), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, body.GetProperty("orderId").GetInt32());
        Assert.Equal("Accepted", body.GetProperty("fulfillmentStatus").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var fulfillment = await db.OrderFulfillments.AsNoTracking().SingleAsync(item => item.OrderId == orderId);
        var delivery = await db.Deliveries.AsNoTracking().SingleAsync(item => item.OrderId == orderId);
        Assert.Equal(OrderFulfillmentStatus.Accepted, fulfillment.Status);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Equal(delivery.Id, body.GetProperty("deliveryId").GetInt32());
        Assert.Equal("Cairo Grill", delivery.RestaurantName);
    }

    // Design §4 test 17 (part) at the API: the second accept is a 409 with the transition's error code.
    [Fact]
    public async Task Accepting_twice_conflicts()
    {
        var orderId = await PlaceOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(AcceptUrl(orderId), null)).StatusCode);
        var second = await admin.PostAsync(AcceptUrl(orderId), null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvalidOrderFulfillmentStatusTransitionException", problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Accept_unknown_order_returns_not_found()
    {
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(AcceptUrl(int.MaxValue), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OrderNotFound", problem.GetProperty("errorCode").GetString());
    }

    private static string AcceptUrl(int orderId) => $"/api/orders/{orderId}/accept";

    /// <summary>Arranges what checkout leaves behind: an order and its fulfillment awaiting the restaurant.</summary>
    private async Task<int> PlaceOrderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var placedAt = DateTime.UtcNow;
        var order = Order.CreateFromCheckout(
            _factory.NonAdminUserId,
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

    private async Task<OrderFulfillment> ReadFulfillmentAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        return await db.OrderFulfillments.AsNoTracking().SingleAsync(item => item.OrderId == orderId);
    }

    private HttpClient CreateClient(int subject, string? role, string scope)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, subject.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, scope);
        if (role is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
