using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Admin.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.DomainServices.DeliveryManagement;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Admin.API.Tests;

/// <summary>
/// Design §4 tests 20 and 34 (part): an Admin releases a delivery on behalf of an unresponsive agent (D6, Q3, D11).
/// </summary>
public sealed class AgentReleaseTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AgentReleaseTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        using var _ = _factory.CreateClient();
    }

    [Fact]
    public async Task Release_agent_requires_admin_access()
    {
        var (orderId, agentId) = await PlaceOrderClaimedByAgentAsync();
        using var anonymous = _factory.CreateClient();
        using var nonAdmin = CreateClient(_factory.NonAdminUserId, null, "admin.api");
        using var noScope = CreateClient(_factory.AdminUserId, "Admin", "delivery.api");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Url(orderId), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nonAdmin.PostAsync(Url(orderId), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noScope.PostAsync(Url(orderId), null)).StatusCode);

        var delivery = await ReadDeliveryAsync(orderId);
        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
        Assert.Equal(agentId, delivery.AssignedAgentId);
    }

    [Fact]
    public async Task Admin_release_returns_the_delivery_to_the_queue_and_the_agent_to_available()
    {
        var (orderId, agentId) = await PlaceOrderClaimedByAgentAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(Url(orderId), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var delivery = await ReadDeliveryAsync(orderId);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, body.GetProperty("orderId").GetInt32());
        Assert.Equal(delivery.Id, body.GetProperty("deliveryId").GetInt32());
        Assert.Equal(agentId, body.GetProperty("releasedAgentId").GetInt32());
        Assert.Equal("PendingAssignment", body.GetProperty("deliveryStatus").GetString());

        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Null(delivery.AssignedAgentId);
        Assert.Equal(DeliveryAgentStatus.Available, await ReadAgentStatusAsync(agentId));
        Assert.Equal(OrderFulfillmentStatus.Accepted, await ReadFulfillmentStatusAsync(orderId));
    }

    [Fact]
    public async Task Admin_release_after_pickup_conflicts()
    {
        var (orderId, agentId) = await PlaceOrderClaimedByAgentAsync(pickedUp: true);
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(Url(orderId), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvalidDeliveryStatusTransitionException", problem.GetProperty("errorCode").GetString());
        Assert.Equal(DeliveryStatus.PickedUp, (await ReadDeliveryAsync(orderId)).Status);
        Assert.Equal(DeliveryAgentStatus.Busy, await ReadAgentStatusAsync(agentId));
    }

    [Fact]
    public async Task Admin_release_of_unclaimed_delivery_conflicts()
    {
        var orderId = await PlaceAcceptedOrderAsync();
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(Url(orderId), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DeliveryNotAssignedException", problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Admin_release_of_unknown_order_returns_not_found()
    {
        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");

        var response = await admin.PostAsync(Url(int.MaxValue), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DeliveryNotFound", problem.GetProperty("errorCode").GetString());
    }

    private static string Url(int orderId) => $"/api/orders/{orderId}/release-agent";

    /// <summary>An accepted order whose delivery an online agent has claimed (and, optionally, picked up).</summary>
    private async Task<(int OrderId, int AgentId)> PlaceOrderClaimedByAgentAsync(bool pickedUp = false)
    {
        var orderId = await PlaceAcceptedOrderAsync();

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();

        var email = $"release-agent-{Guid.NewGuid():N}@test.com";
        var agent = User.Register(email, email, "Release Agent");
        agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        agent.ApproveDeliveryAgentApplication();
        agent.GoOnline();
        Assert.True((await users.CreateAsync(agent, "Password1!")).Succeeded);

        var now = DateTime.UtcNow;
        var delivery = await db.Deliveries.SingleAsync(item => item.OrderId == orderId);
        new DeliveryAssignmentDomainService().Assign(delivery, agent, now);

        if (pickedUp)
        {
            delivery.MarkArrivedAtRestaurant(agent.Id, now.AddSeconds(1));
            delivery.MarkPickedUp(agent.Id, now.AddSeconds(2));
        }

        await db.SaveChangesAsync();
        return (orderId, agent.Id);
    }

    private async Task<int> PlaceAcceptedOrderAsync()
    {
        int orderId;
        using (var scope = _factory.Services.CreateScope())
        {
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
            orderId = order.Id;
        }

        using var admin = CreateClient(_factory.AdminUserId, "Admin", "admin.api");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/orders/{orderId}/accept", null)).StatusCode);
        return orderId;
    }

    private async Task<Delivery> ReadDeliveryAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        return await db.Deliveries.AsNoTracking().SingleAsync(item => item.OrderId == orderId);
    }

    private async Task<DeliveryAgentStatus?> ReadAgentStatusAsync(int agentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == agentId)
            .Select(user => user.DeliveryAgentStatus)
            .SingleAsync();
    }

    private async Task<OrderFulfillmentStatus> ReadFulfillmentStatusAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        return await db.OrderFulfillments.AsNoTracking()
            .Where(item => item.OrderId == orderId)
            .Select(item => item.Status)
            .SingleAsync();
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
