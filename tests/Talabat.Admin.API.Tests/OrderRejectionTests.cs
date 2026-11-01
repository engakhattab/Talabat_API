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

/// <summary>Design §4 test 34 (part): the Admin rejects an order on the restaurant's behalf (D2, D11, D13).</summary>
public sealed class OrderRejectionTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public OrderRejectionTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        using var _ = _factory.CreateClient();
    }

    [Fact]
    public async Task Reject_requires_admin_access()
    {
        var orderId = await PlaceOrderAsync();
        using var anonymous = _factory.CreateClient();
        using var nonAdmin = CreateClient(_factory.NonAdminUserId, null, "admin.api");
        using var noScope = CreateClient(_factory.AdminUserId, "Admin", "delivery.api");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(RejectUrl(orderId), Busy)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nonAdmin.PostAsJsonAsync(RejectUrl(orderId), Busy)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noScope.PostAsJsonAsync(RejectUrl(orderId), Busy)).StatusCode);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Fact]
    public async Task Admin_rejects_a_placed_order_with_a_reason_code()
    {
        var orderId = await PlaceOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsJsonAsync(
            RejectUrl(orderId),
            new { reasonCode = "ItemsUnavailable", note = "Out of rice" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, body.GetProperty("orderId").GetInt32());
        Assert.Equal("Rejected", body.GetProperty("fulfillmentStatus").GetString());
        Assert.Equal("ItemsUnavailable", body.GetProperty("reasonCode").GetString());

        var fulfillment = await ReadFulfillmentAsync(orderId);
        Assert.Equal(OrderFulfillmentStatus.Rejected, fulfillment.Status);
        Assert.Equal(FulfillmentReasonCode.ItemsUnavailable, fulfillment.ReasonCode);
        Assert.Equal("Out of rice", fulfillment.ReasonNote);
        Assert.Null(fulfillment.CancelledBy);
    }

    // Design §4 test 17 (part) at the API: an accepted order can no longer be rejected.
    [Fact]
    public async Task Reject_after_accept_returns_409()
    {
        var orderId = await PlaceOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/orders/{orderId}/accept", null)).StatusCode);

        var response = await admin.PostAsJsonAsync(RejectUrl(orderId), Busy);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvalidOrderFulfillmentStatusTransitionException", problem.GetProperty("errorCode").GetString());
        Assert.Equal(OrderFulfillmentStatus.Accepted, (await ReadFulfillmentAsync(orderId)).Status);
    }

    // D13: a reject needs a reject reason code; the customer's codes and a missing code are 400.
    [Fact]
    public async Task Reject_with_a_missing_or_wrong_reason_returns_400()
    {
        var orderId = await PlaceOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var wrongCode = await admin.PostAsJsonAsync(RejectUrl(orderId), new { reasonCode = "ChangedMind" });
        var missingCode = await admin.PostAsJsonAsync(RejectUrl(orderId), new { note = "No code" });

        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        var problem = await wrongCode.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ReasonCodeNotAllowedException", problem.GetProperty("errorCode").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, missingCode.StatusCode);
        var missingProblem = await missingCode.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ReasonCodeRequiredException", missingProblem.GetProperty("errorCode").GetString());
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Fact]
    public async Task Reject_unknown_order_returns_not_found()
    {
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsJsonAsync(RejectUrl(int.MaxValue), Busy);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OrderNotFound", problem.GetProperty("errorCode").GetString());
    }

    private static readonly object Busy = new { reasonCode = "RestaurantBusy" };

    private static string RejectUrl(int orderId) => $"/api/orders/{orderId}/reject";

    /// <summary>Arranges what checkout leaves behind: an order and its fulfillment awaiting the restaurant.</summary>
    private async Task<int> PlaceOrderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var placedAt = DateTime.UtcNow.AddMinutes(-1);
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
