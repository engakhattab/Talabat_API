using Talabat.Application.DeliveryAgents.GetActiveDelivery;
using Talabat.Application.DeliveryAgents.GetPendingDeliveries;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

/// <summary>Delivery agents see the order fulfillment status before they try to pick up (design D5, BR-DEL-020).</summary>
public sealed class DeliveryFulfillmentStatusTests
{
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private const int RestaurantId = 1;
    private const int AgentId = 10;

    // Design §4 test 26.
    [Fact]
    public async Task Pending_and_active_deliveries_include_fulfillment_status()
    {
        var deliveries = new FakeDeliveryRepository();
        var fulfillments = new FakeOrderFulfillmentRepository();

        var acceptedOrder = AddOrder(deliveries, fulfillments, orderId: 301, deliveryId: 1);
        var preparingOrder = AddOrder(deliveries, fulfillments, orderId: 302, deliveryId: 2);
        preparingOrder.Fulfillment.StartPreparing(RestaurantId, Now);
        var readyOrder = AddOrder(deliveries, fulfillments, orderId: 303, deliveryId: 3);
        readyOrder.Fulfillment.MarkReady(RestaurantId, Now);
        var activeOrder = AddOrder(deliveries, fulfillments, orderId: 304, deliveryId: 4);
        activeOrder.Fulfillment.StartPreparing(RestaurantId, Now);
        activeOrder.Delivery.AssignAgent(AgentId, Now.AddMinutes(1));

        var pending = await new GetPendingDeliveriesHandler(deliveries, fulfillments)
            .Handle(new GetPendingDeliveriesQuery(AgentId));
        var active = await new GetActiveDeliveryHandler(deliveries, CustomerRepository(), fulfillments)
            .Handle(new GetActiveDeliveryQuery(AgentId));

        Assert.True(pending.IsSuccess);
        Assert.Equal(
            new[]
            {
                (301, OrderFulfillmentStatus.Accepted),
                (302, OrderFulfillmentStatus.Preparing),
                (303, OrderFulfillmentStatus.ReadyForPickup)
            },
            pending.Value!.Select(dto => (dto.OrderId, dto.FulfillmentStatus)).OrderBy(pair => pair.OrderId));

        Assert.True(active.IsSuccess);
        Assert.Equal(304, active.Value!.OrderId);
        Assert.Equal(OrderFulfillmentStatus.Preparing, active.Value.FulfillmentStatus);
    }

    private static (Delivery Delivery, OrderFulfillment Fulfillment) AddOrder(
        FakeDeliveryRepository deliveries,
        FakeOrderFulfillmentRepository fulfillments,
        int orderId,
        int deliveryId)
    {
        var fulfillment = new OrderFulfillment(orderId, RestaurantId, Now, TimeSpan.FromMinutes(10));
        fulfillment.Accept(RestaurantId, Now);
        fulfillments.Fulfillments.Add(fulfillment);

        var delivery = new Delivery(orderId, 1, RestaurantId, "Test Restaurant", Address, Address, Now);
        TestIds.SetId(delivery, deliveryId);
        deliveries.Deliveries.Add(delivery);

        return (delivery, fulfillment);
    }

    private static FakeUserRepository CustomerRepository()
    {
        var customer = User.Register("customer@test.com", "customer@test.com", "Customer");
        customer.SetPhoneNumber("+201000000099");
        TestIds.SetId(customer, 1);
        var users = new FakeUserRepository();
        users.Users.Add(customer);
        return users;
    }
}
