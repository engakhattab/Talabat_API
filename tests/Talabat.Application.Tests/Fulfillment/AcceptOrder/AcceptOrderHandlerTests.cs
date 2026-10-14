using Talabat.Application.Common.Results;
using Talabat.Application.Fulfillment;
using Talabat.Application.Fulfillment.AcceptOrder;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Application.Tests.Fulfillment.AcceptOrder;

public sealed class AcceptOrderHandlerTests
{
    private static readonly DateTime AcceptedAt = TestData.UtcNow.AddMinutes(3);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeOrderFulfillmentRepository _fulfillments = new();
    private readonly FakeDeliveryRepository _deliveries = new();
    private readonly FakeRestaurantRepository _restaurants = new();
    private readonly FakeUnitOfWork _unitOfWork;

    public AcceptOrderHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_orders, _fulfillments, _deliveries);

        var order = TestData.CreateOrder(id: 1, customerId: 9, restaurantId: 1);
        _orders.Orders.Add(order);
        _fulfillments.Fulfillments.Add(TestData.CreateFulfillment(order));
        _restaurants.Restaurants.Add(TestData.CreateRestaurant(1));
    }

    // Design §4 test 16 (D3; revises BR-DEL-001): accepting moves the fulfillment to Accepted and creates the
    // order's delivery, waiting for an agent, in the same transaction.
    [Fact]
    public async Task Accept_creates_pending_delivery_with_pickup_snapshot_in_one_transaction()
    {
        var result = await CreateHandler().Handle(new AcceptOrderCommand(1, new FulfillmentActor.Admin()));

        Assert.True(result.IsSuccess);

        var fulfillment = Assert.Single(_fulfillments.Fulfillments);
        Assert.Equal(OrderFulfillmentStatus.Accepted, fulfillment.Status);
        Assert.Equal(AcceptedAt, fulfillment.AcceptedAt);

        var delivery = Assert.Single(_deliveries.Deliveries);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Null(delivery.AssignedAgentId);
        Assert.Equal(1, delivery.OrderId);
        Assert.Equal(9, delivery.CustomerId);
        Assert.Equal(1, delivery.RestaurantId);
        Assert.Equal("Restaurant 1", delivery.RestaurantName);
        Assert.Equal("1 Test Pickup Street", delivery.RestaurantPickupAddress.Street);
        Assert.Equal("Cairo", delivery.RestaurantPickupAddress.City);
        Assert.Equal("1", delivery.RestaurantPickupAddress.BuildingNumber);
        Assert.Equal("Street", delivery.DeliveryAddress.Street);
        Assert.Equal("10", delivery.DeliveryAddress.BuildingNumber);
        Assert.Equal(AcceptedAt, delivery.CreatedAt);

        Assert.Equal(new AcceptedOrder(1, delivery.Id, AcceptedAt), result.Value);
        Assert.Equal(1, _unitOfWork.TransactionCount);
        Assert.Equal(1, _unitOfWork.SaveChangesInsideTransactionCount);
        Assert.Equal(_unitOfWork.SaveChangesCount, _unitOfWork.SaveChangesInsideTransactionCount);
    }

    // Design §4 test 17 (part): an order is accepted once; the second accept conflicts and creates nothing.
    [Fact]
    public async Task Accept_twice_conflicts()
    {
        var handler = CreateHandler();
        var command = new AcceptOrderCommand(1, new FulfillmentActor.Admin());

        var first = await handler.Handle(command);
        var second = await handler.Handle(command);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(ApplicationErrorCategory.Conflict, second.Error!.Category);
        Assert.Equal(nameof(Talabat.Domain.Exceptions.InvalidOrderFulfillmentStatusTransitionException), second.Error.Code);
        Assert.Single(_deliveries.Deliveries);
    }

    [Fact]
    public async Task Accept_unknown_order_returns_not_found()
    {
        var result = await CreateHandler().Handle(new AcceptOrderCommand(404, new FulfillmentActor.Admin()));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.NotFound, result.Error!.Category);
        Assert.Equal(ApplicationErrorCodes.OrderNotFound, result.Error.Code);
        Assert.Empty(_deliveries.Deliveries);
    }

    // D11: the restaurant itself (Owner API, M3) may accept only its own orders; an Admin acts on behalf of the
    // order's restaurant.
    [Fact]
    public async Task Accept_by_another_restaurant_is_refused_and_creates_nothing()
    {
        var result = await CreateHandler().Handle(new AcceptOrderCommand(1, new FulfillmentActor.Restaurant(2)));

        Assert.True(result.IsFailure);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, Assert.Single(_fulfillments.Fulfillments).Status);
        Assert.Empty(_deliveries.Deliveries);
    }

    [Fact]
    public async Task Accept_by_the_orders_own_restaurant_succeeds()
    {
        var result = await CreateHandler().Handle(new AcceptOrderCommand(1, new FulfillmentActor.Restaurant(1)));

        Assert.True(result.IsSuccess);
        Assert.Single(_deliveries.Deliveries);
    }

    private AcceptOrderHandler CreateHandler()
    {
        return new AcceptOrderHandler(
            _orders,
            _fulfillments,
            _deliveries,
            _restaurants,
            new FakeClock { UtcNow = AcceptedAt },
            _unitOfWork);
    }
}
