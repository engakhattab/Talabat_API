using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Delivery.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;
using Xunit;

namespace Talabat.Delivery.API.Tests;

public sealed class AssignmentConcurrencyTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();
    private int _pendingDeliveryId;
    private int _racerAId;
    private int _racerBId;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();

        var customer = User.Register("concurrency_customer", "conc@test.com", "Conc Customer");
        customer.InitializeCustomerProfile("Conc Customer", 30, null);
        db.Users.Add(customer);
        await db.SaveChangesAsync();

        var restaurant = db.Restaurants.First();

        var order = Order.CreateFromCheckout(
            customer.Id,
            restaurant.Id,
            [new CheckoutItemSnapshot(101, "Item", new Money(10m), 1)],
            new DeliveryAddressSnapshot("1 Test Street", "Cairo", "1"),
            DateTime.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var delivery = new Talabat.Domain.Aggregates.DeliveryManagement.Delivery(
            order.Id,
            customer.Id,
            restaurant.Id,
            restaurant.Name,
            new DeliveryAddressSnapshot(
                restaurant.PickupAddress.Street,
                restaurant.PickupAddress.City,
                restaurant.PickupAddress.BuildingNumber,
                restaurant.PickupAddress.Floor),
            new DeliveryAddressSnapshot("1 Test Street", "Cairo", "1"),
            DateTime.UtcNow);

        db.Deliveries.Add(delivery);
        await db.SaveChangesAsync();

        _pendingDeliveryId = delivery.Id;

        // Two approved agents that are online and idle, so both are eligible and the
        // only thing deciding the winner is the optimistic-concurrency race itself.
        _racerAId = await AddOnlineAgentAsync(db, "racer_a");
        _racerBId = await AddOnlineAgentAsync(db, "racer_b");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Concurrent_assign_from_two_agents_yields_one_200_and_one_409()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();

        AuthenticateAs(clientA, _racerAId);
        AuthenticateAs(clientB, _racerBId);

        var requestA = new HttpRequestMessage(HttpMethod.Post, $"/api/agent/deliveries/{_pendingDeliveryId}/assign");
        var requestB = new HttpRequestMessage(HttpMethod.Post, $"/api/agent/deliveries/{_pendingDeliveryId}/assign");

        var taskA = clientA.SendAsync(requestA);
        var taskB = clientB.SendAsync(requestB);

        var results = await Task.WhenAll(taskA, taskB);

        var statuses = results.Select(r => r.StatusCode).OrderBy(s => s).ToList();
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var delivery = await db.Deliveries.AsNoTracking().SingleAsync(d => d.Id == _pendingDeliveryId);
        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
        Assert.Contains(delivery.AssignedAgentId!.Value, new[] { _racerAId, _racerBId });

        var loserId = delivery.AssignedAgentId == _racerAId ? _racerBId : _racerAId;
        var loser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == loserId);
        Assert.Equal(DeliveryAgentStatus.Available, loser.DeliveryAgentStatus);
    }

    private static async Task<int> AddOnlineAgentAsync(TalabatDbContext db, string name)
    {
        var agent = User.Register($"{name}_{Guid.NewGuid():N}", $"{name}_{Guid.NewGuid():N}@test.com", name);
        agent.SubmitDeliveryAgentApplication(VehicleType.Bike);
        agent.ApproveDeliveryAgentApplication();
        agent.GoOnline();
        db.Users.Add(agent);
        await db.SaveChangesAsync();
        return agent.Id;
    }

    private static void AuthenticateAs(HttpClient client, int userId)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
    }
}
