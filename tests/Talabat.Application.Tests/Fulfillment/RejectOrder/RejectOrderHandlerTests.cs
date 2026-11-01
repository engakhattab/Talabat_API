using Talabat.Application.Common.Results;
using Talabat.Application.Fulfillment;
using Talabat.Application.Fulfillment.RejectOrder;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Exceptions;

namespace Talabat.Application.Tests.Fulfillment.RejectOrder;

public sealed class RejectOrderHandlerTests
{
    private static readonly DateTime RejectedAt = TestData.UtcNow.AddMinutes(2);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeOrderFulfillmentRepository _fulfillments = new();
    private readonly FakeUnitOfWork _unitOfWork;

    public RejectOrderHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_orders, _fulfillments);

        var order = TestData.CreateOrder(id: 1, customerId: 9, restaurantId: 1);
        _orders.Orders.Add(order);
        _fulfillments.Fulfillments.Add(TestData.CreateFulfillment(order));
    }

    // Design D2, D11, D13: an Admin rejects on behalf of the order's restaurant with a reject reason code.
    [Fact]
    public async Task Reject_from_awaiting_restaurant_records_the_reason_and_saves()
    {
        var result = await CreateHandler().Handle(new RejectOrderCommand(
            1, new FulfillmentActor.Admin(), FulfillmentReasonCode.ItemsUnavailable, "No more koshari"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new RejectedOrder(1, FulfillmentReasonCode.ItemsUnavailable, RejectedAt), result.Value);

        var fulfillment = Assert.Single(_fulfillments.Fulfillments);
        Assert.Equal(OrderFulfillmentStatus.Rejected, fulfillment.Status);
        Assert.Equal("No more koshari", fulfillment.ReasonNote);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    // Design §4 test 17 (part): once accepted, an order can no longer be rejected.
    [Fact]
    public async Task Reject_after_accept_conflicts()
    {
        var fulfillment = Assert.Single(_fulfillments.Fulfillments);
        fulfillment.Accept(1, TestData.UtcNow.AddMinutes(1));

        var result = await CreateHandler().Handle(new RejectOrderCommand(
            1, new FulfillmentActor.Admin(), FulfillmentReasonCode.RestaurantBusy, Note: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.Conflict, result.Error!.Category);
        Assert.Equal(nameof(InvalidOrderFulfillmentStatusTransitionException), result.Error.Code);
        Assert.Equal(OrderFulfillmentStatus.Accepted, fulfillment.Status);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Design D13: a code from another action's list, or Other without a note, is a validation error (400).
    [Theory]
    [InlineData(FulfillmentReasonCode.ChangedMind, null, nameof(ReasonCodeNotAllowedException))]
    [InlineData(FulfillmentReasonCode.Other, null, nameof(ReasonNoteRequiredException))]
    public async Task Reject_with_an_invalid_reason_is_a_validation_error(
        FulfillmentReasonCode reasonCode,
        string? note,
        string expectedCode)
    {
        var result = await CreateHandler().Handle(new RejectOrderCommand(
            1, new FulfillmentActor.Admin(), reasonCode, note));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.Validation, result.Error!.Category);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, Assert.Single(_fulfillments.Fulfillments).Status);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Reject_unknown_order_returns_not_found()
    {
        var result = await CreateHandler().Handle(new RejectOrderCommand(
            404, new FulfillmentActor.Admin(), FulfillmentReasonCode.RestaurantBusy, Note: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.NotFound, result.Error!.Category);
        Assert.Equal(ApplicationErrorCodes.OrderNotFound, result.Error.Code);
    }

    // D11: a restaurant (Owner API, M3) may reject only its own orders.
    [Fact]
    public async Task Reject_by_another_restaurant_is_refused()
    {
        var result = await CreateHandler().Handle(new RejectOrderCommand(
            1, new FulfillmentActor.Restaurant(2), FulfillmentReasonCode.RestaurantBusy, Note: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCategory.OwnershipMismatch, result.Error!.Category);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, Assert.Single(_fulfillments.Fulfillments).Status);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    private RejectOrderHandler CreateHandler()
    {
        return new RejectOrderHandler(_fulfillments, new FakeClock { UtcNow = RejectedAt }, _unitOfWork);
    }
}
