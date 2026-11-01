using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.CancelOrder;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Exceptions;

namespace Talabat.Application.Tests.Ordering.CancelOrder;

public sealed class CancelOrderHandlerTests
{
    private const int CustomerId = 9;
    private static readonly DateTime CancelledAt = TestData.UtcNow.AddMinutes(2);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeOrderFulfillmentRepository _fulfillments = new();
    private readonly FakeUnitOfWork _unitOfWork;

    public CancelOrderHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_orders, _fulfillments);

        var order = TestData.CreateOrder(id: 1, customerId: CustomerId, restaurantId: 1);
        _orders.Orders.Add(order);
        _fulfillments.Fulfillments.Add(TestData.CreateFulfillment(order));
    }

    // Design §4 test 21 (D7, Q2): the customer cancels while the order is Placed; once the restaurant has
    // accepted, the cancel conflicts with OrderNoLongerCancellable and changes nothing.
    [Fact]
    public async Task Customer_cancel_only_while_awaiting_restaurant()
    {
        var placed = await CreateHandler().Handle(
            new CancelOrderCommand(CustomerId, 1, FulfillmentReasonCode.ChangedMind, Note: null));

        Assert.True(placed.IsSuccess);
        Assert.Equal(new CancelledOrder(1, FulfillmentReasonCode.ChangedMind, CancelledAt), placed.Value);
        var cancelled = Assert.Single(_fulfillments.Fulfillments);
        Assert.Equal(OrderFulfillmentStatus.Cancelled, cancelled.Status);
        Assert.Equal(CancellationActor.Customer, cancelled.CancelledBy);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);

        var acceptedOrder = TestData.CreateOrder(id: 2, customerId: CustomerId, restaurantId: 1);
        _orders.Orders.Add(acceptedOrder);
        var accepted = TestData.CreateFulfillment(acceptedOrder);
        accepted.Accept(1, TestData.UtcNow.AddMinutes(1));
        _fulfillments.Fulfillments.Add(accepted);

        var afterAccept = await CreateHandler().Handle(
            new CancelOrderCommand(CustomerId, 2, ReasonCode: null, Note: null));

        Assert.True(afterAccept.IsFailure);
        Assert.Equal(ApplicationErrorCategory.Conflict, afterAccept.Error!.Category);
        Assert.Equal(ApplicationErrorCodes.OrderNoLongerCancellable, afterAccept.Error.Code);
        Assert.Equal(OrderFulfillmentStatus.Accepted, accepted.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    // Customer routes never reveal another customer's order: it is simply not found.
    [Fact]
    public async Task Customer_cancel_on_another_customers_order_returns_not_found()
    {
        var result = await CreateHandler().Handle(
            new CancelOrderCommand(CustomerId + 1, 1, ReasonCode: null, Note: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.NotFound, result.Error!.Category);
        Assert.Equal(ApplicationErrorCodes.OrderNotFound, result.Error.Code);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, Assert.Single(_fulfillments.Fulfillments).Status);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Design D13: KitchenIssue belongs to the restaurant's cancellation, not the customer's.
    [Fact]
    public async Task Customer_cancel_with_another_actions_reason_is_a_validation_error()
    {
        var result = await CreateHandler().Handle(
            new CancelOrderCommand(CustomerId, 1, FulfillmentReasonCode.KitchenIssue, Note: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.Validation, result.Error!.Category);
        Assert.Equal(nameof(ReasonCodeNotAllowedException), result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    private CancelOrderHandler CreateHandler()
    {
        return new CancelOrderHandler(_orders, _fulfillments, new FakeClock { UtcNow = CancelledAt }, _unitOfWork);
    }
}
