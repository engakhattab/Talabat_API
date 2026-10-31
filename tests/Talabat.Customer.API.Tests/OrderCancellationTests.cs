using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Customer.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;
using Xunit;

namespace Talabat.Customer.API.Tests;

/// <summary>
/// Design §4 test 32 (part): the customer cancels a Placed order (D7, Q2) and order details show who cancelled and
/// why, never the free-text note (D9, D13).
/// </summary>
public sealed class OrderCancellationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderCancellationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customer_cancels_a_placed_order_and_details_show_cancelled_by_customer_without_the_note()
    {
        var (customerId, orderId) = await SeedCustomerWithPlacedOrderAsync();
        using var client = CreateClientFor(customerId);

        var response = await client.PostAsJsonAsync(
            CancelUrl(orderId),
            new { reasonCode = "Other", note = "Private note for support" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, body.GetProperty("orderId").GetInt32());
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        Assert.Equal("Other", body.GetProperty("reasonCode").GetString());

        var detailsJson = await client.GetStringAsync($"/api/me/orders/{orderId}");
        using var details = JsonDocument.Parse(detailsJson);
        Assert.Equal("Cancelled", details.RootElement.GetProperty("status").GetString());
        Assert.Equal("Customer", details.RootElement.GetProperty("cancelledBy").GetString());
        Assert.Equal("Other", details.RootElement.GetProperty("reasonCode").GetString());
        Assert.False(details.RootElement.GetProperty("deliveryAgentAssigned").GetBoolean());
        Assert.DoesNotContain("Private note for support", detailsJson);
        Assert.False(details.RootElement.TryGetProperty("note", out _));
        Assert.False(details.RootElement.TryGetProperty("reasonNote", out _));
    }

    // D13: giving a reason is optional for the customer, so the body may be left out.
    [Fact]
    public async Task Customer_cancel_without_a_reason_is_allowed()
    {
        var (customerId, orderId) = await SeedCustomerWithPlacedOrderAsync();
        using var client = CreateClientFor(customerId);

        var response = await client.PostAsync(CancelUrl(orderId), content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var details = JsonDocument.Parse(await client.GetStringAsync($"/api/me/orders/{orderId}"));
        Assert.Equal("Cancelled", details.RootElement.GetProperty("status").GetString());
        Assert.Equal("Customer", details.RootElement.GetProperty("cancelledBy").GetString());
        Assert.Equal(JsonValueKind.Null, details.RootElement.GetProperty("reasonCode").ValueKind);
    }

    // D7: once the restaurant has accepted, the customer can no longer cancel.
    [Fact]
    public async Task Customer_cancel_after_acceptance_returns_409_order_no_longer_cancellable()
    {
        var (customerId, orderId) = await SeedCustomerWithPlacedOrderAsync();
        await UpdateFulfillmentAsync(orderId, fulfillment =>
            fulfillment.Accept(fulfillment.RestaurantId, DateTime.UtcNow));
        using var client = CreateClientFor(customerId);

        var response = await client.PostAsJsonAsync(CancelUrl(orderId), new { reasonCode = "ChangedMind" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OrderNoLongerCancellable", problem.GetProperty("errorCode").GetString());
        Assert.Equal(OrderFulfillmentStatus.Accepted, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Fact]
    public async Task Customer_cancel_on_another_customers_order_returns_404()
    {
        var (_, orderId) = await SeedCustomerWithPlacedOrderAsync();
        var (otherCustomerId, _) = await SeedCustomerWithPlacedOrderAsync();
        using var otherClient = CreateClientFor(otherCustomerId);

        var response = await otherClient.PostAsync(CancelUrl(orderId), content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OrderNotFound", problem.GetProperty("errorCode").GetString());
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, (await ReadFulfillmentAsync(orderId)).Status);
    }

    // D13: another action's code, or Other without a note, is a 400 and changes nothing.
    [Theory]
    [InlineData("KitchenIssue", null, "ReasonCodeNotAllowedException")]
    [InlineData("Other", null, "ReasonNoteRequiredException")]
    public async Task Customer_cancel_with_an_invalid_reason_returns_400(
        string reasonCode,
        string? note,
        string expectedErrorCode)
    {
        var (customerId, orderId) = await SeedCustomerWithPlacedOrderAsync();
        using var client = CreateClientFor(customerId);

        var response = await client.PostAsJsonAsync(CancelUrl(orderId), new { reasonCode, note });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedErrorCode, problem.GetProperty("errorCode").GetString());
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, (await ReadFulfillmentAsync(orderId)).Status);
    }

    [Fact]
    public async Task Customer_cancel_requires_authentication()
    {
        var (_, orderId) = await SeedCustomerWithPlacedOrderAsync();
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsync(CancelUrl(orderId), content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // D9: a rejected order shows the restaurant's reason code; nobody "cancelled" it.
    [Fact]
    public async Task Rejected_order_details_show_the_reason_code_without_cancelled_by()
    {
        var (customerId, orderId) = await SeedCustomerWithPlacedOrderAsync();
        await UpdateFulfillmentAsync(orderId, fulfillment => fulfillment.Reject(
            fulfillment.RestaurantId, FulfillmentReasonCode.RestaurantBusy, "Kitchen overloaded", DateTime.UtcNow));
        using var client = CreateClientFor(customerId);

        var detailsJson = await client.GetStringAsync($"/api/me/orders/{orderId}");

        using var details = JsonDocument.Parse(detailsJson);
        Assert.Equal("Rejected", details.RootElement.GetProperty("status").GetString());
        Assert.Equal("RestaurantBusy", details.RootElement.GetProperty("reasonCode").GetString());
        Assert.Equal(JsonValueKind.Null, details.RootElement.GetProperty("cancelledBy").ValueKind);
        Assert.DoesNotContain("Kitchen overloaded", detailsJson);

        using var list = JsonDocument.Parse(await client.GetStringAsync("/api/me/orders"));
        var summary = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == orderId);
        Assert.Equal("Rejected", summary.GetProperty("status").GetString());
    }

    private static string CancelUrl(int orderId) => $"/api/me/orders/{orderId}/cancel";

    private HttpClient CreateClientFor(int customerId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, customerId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");
        return client;
    }

    /// <summary>Arranges what checkout leaves behind for a fresh customer: an order awaiting the restaurant.</summary>
    private async Task<(int CustomerId, int OrderId)> SeedCustomerWithPlacedOrderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();

        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Register($"cancel-{suffix}", $"cancel-{suffix}@test.com", "Cancel Customer");
        customer.InitializeCustomerProfile("Cancel Customer", 30, null);
        Assert.True((await userManager.CreateAsync(customer, "Password1!")).Succeeded);

        var product = await db.Set<Talabat.Domain.Aggregates.Catalog.Product>().FirstAsync();
        var placedAt = DateTime.UtcNow.AddMinutes(-1);
        var order = Order.CreateFromCheckout(
            customer.Id,
            product.RestaurantId,
            [new CheckoutItemSnapshot(product.Id, product.Name, new Money(10m), 1)],
            new DeliveryAddressSnapshot("1 Cancel Street", "Cairo", "1"),
            placedAt);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.OrderFulfillments.Add(new OrderFulfillment(
            order.Id, order.RestaurantId, placedAt, TimeSpan.FromMinutes(10)));
        await db.SaveChangesAsync();

        return (customer.Id, order.Id);
    }

    private async Task UpdateFulfillmentAsync(int orderId, Action<OrderFulfillment> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var fulfillment = await db.OrderFulfillments.SingleAsync(item => item.OrderId == orderId);
        change(fulfillment);
        await db.SaveChangesAsync();
    }

    private async Task<OrderFulfillment> ReadFulfillmentAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        return await db.OrderFulfillments.AsNoTracking().SingleAsync(item => item.OrderId == orderId);
    }
}
