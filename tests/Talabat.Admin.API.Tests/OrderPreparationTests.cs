using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Admin.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Admin.API.Tests;

/// <summary>
/// Design §4 test 34 (part): the Admin starts preparing and marks ready on the restaurant's behalf (D2, D11).
/// </summary>
public sealed class OrderPreparationTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public OrderPreparationTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        using var _ = _factory.CreateClient();
    }

    [Theory]
    [InlineData("start-preparing")]
    [InlineData("mark-ready")]
    public async Task Preparation_endpoints_require_admin_access(string action)
    {
        var orderId = await PlaceAcceptedOrderAsync();
        using var anonymous = _factory.CreateClient();
        using var nonAdmin = CreateClient(_factory.NonAdminUserId, null, "admin.api");
        using var noScope = CreateClient(_factory.AdminUserId, "Admin", "delivery.api");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Url(orderId, action), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nonAdmin.PostAsync(Url(orderId, action), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noScope.PostAsync(Url(orderId, action), null)).StatusCode);
        Assert.Equal(OrderFulfillmentStatus.Accepted, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Fact]
    public async Task Admin_starts_preparing_then_marks_ready()
    {
        var orderId = await PlaceAcceptedOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var preparing = await admin.PostAsync(Url(orderId, "start-preparing"), null);

        Assert.Equal(HttpStatusCode.OK, preparing.StatusCode);
        var preparingBody = await preparing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, preparingBody.GetProperty("orderId").GetInt32());
        Assert.Equal("Preparing", preparingBody.GetProperty("fulfillmentStatus").GetString());
        Assert.Equal(OrderFulfillmentStatus.Preparing, (await ReadFulfillmentAsync(orderId)).Status);

        var ready = await admin.PostAsync(Url(orderId, "mark-ready"), null);

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var readyBody = await ready.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, readyBody.GetProperty("orderId").GetInt32());
        Assert.Equal("ReadyForPickup", readyBody.GetProperty("fulfillmentStatus").GetString());
        var fulfillment = await ReadFulfillmentAsync(orderId);
        Assert.Equal(OrderFulfillmentStatus.ReadyForPickup, fulfillment.Status);
        Assert.Equal(fulfillment.PreparingAt, preparingBody.GetProperty("preparingAt").GetDateTime());
        Assert.Equal(fulfillment.ReadyAt, readyBody.GetProperty("readyAt").GetDateTime());
    }

    // D2: instant items skip Preparing.
    [Fact]
    public async Task Admin_marks_an_accepted_order_ready_without_preparing()
    {
        var orderId = await PlaceAcceptedOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(Url(orderId, "mark-ready"), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderFulfillmentStatus.ReadyForPickup, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Theory]
    [InlineData("start-preparing")]
    [InlineData("mark-ready")]
    public async Task Preparation_before_acceptance_conflicts(string action)
    {
        var orderId = await PlaceOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(Url(orderId, action), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvalidOrderFulfillmentStatusTransitionException", problem.GetProperty("errorCode").GetString());
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Theory]
    [InlineData("start-preparing")]
    [InlineData("mark-ready")]
    public async Task Preparation_of_unknown_order_returns_not_found(string action)
    {
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(Url(int.MaxValue, action), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OrderNotFound", problem.GetProperty("errorCode").GetString());
    }

    private static string Url(int orderId, string action) => $"/api/orders/{orderId}/{action}";

    private async Task<int> PlaceAcceptedOrderAsync()
    {
        var orderId = await PlaceOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(Url(orderId, "accept"), null)).StatusCode);
        return orderId;
    }

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
