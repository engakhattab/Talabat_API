using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Exceptions;

namespace Talabat.Domain.Tests.RestaurantOperations;

public sealed class OrderFulfillmentTests
{
    private const int OrderId = 300;
    private const int RestaurantId = 1;

    private static readonly DateTime PlacedAt =
        new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan AcceptanceWindow = TimeSpan.FromMinutes(10);

    // Design §4 test 1 (D2, D10).
    [Fact]
    public void New_fulfillment_starts_awaiting_restaurant_with_acceptance_deadline()
    {
        var fulfillment = new OrderFulfillment(OrderId, RestaurantId, PlacedAt, AcceptanceWindow);

        Assert.Equal(OrderId, fulfillment.OrderId);
        Assert.Equal(RestaurantId, fulfillment.RestaurantId);
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, fulfillment.Status);
        Assert.Equal(PlacedAt, fulfillment.PlacedAt);
        Assert.Equal(PlacedAt.AddMinutes(10), fulfillment.AcceptanceDeadline);
        Assert.Equal(PlacedAt, fulfillment.CreatedAt);
        Assert.Null(fulfillment.AcceptedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Acceptance_window_must_be_positive(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OrderFulfillment(OrderId, RestaurantId, PlacedAt, TimeSpan.FromMinutes(minutes)));
    }

    // Design §4 test 2, pulled forward into T1: Accept is the transition tests 9, 10 and 28 exercise.
    [Fact]
    public void Accept_from_awaiting_restaurant_sets_accepted_and_timestamp()
    {
        var fulfillment = NewFulfillment();
        var acceptedAt = PlacedAt.AddMinutes(3);

        fulfillment.Accept(RestaurantId, acceptedAt);

        Assert.Equal(OrderFulfillmentStatus.Accepted, fulfillment.Status);
        Assert.Equal(acceptedAt, fulfillment.AcceptedAt);
    }

    [Fact]
    public void Accept_only_from_awaiting_restaurant()
    {
        var fulfillment = NewFulfillment();
        fulfillment.Accept(RestaurantId, PlacedAt.AddMinutes(1));

        Assert.Throws<InvalidOrderFulfillmentStatusTransitionException>(() =>
            fulfillment.Accept(RestaurantId, PlacedAt.AddMinutes(2)));
    }

    // Design §4 test 9.
    [Fact]
    public void Wrong_restaurant_id_throws_mismatch()
    {
        var fulfillment = NewFulfillment();

        Assert.Throws<OrderFulfillmentRestaurantMismatchException>(() =>
            fulfillment.Accept(RestaurantId + 1, PlacedAt.AddMinutes(1)));
        Assert.Equal(OrderFulfillmentStatus.AwaitingRestaurant, fulfillment.Status);
    }

    // Design §4 test 10.
    [Fact]
    public void Transition_timestamps_must_be_utc_and_monotonic()
    {
        var localPlacedAt = DateTime.SpecifyKind(PlacedAt, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() =>
            new OrderFulfillment(OrderId, RestaurantId, localPlacedAt, AcceptanceWindow));

        var fulfillment = NewFulfillment();
        var unspecifiedTime = DateTime.SpecifyKind(PlacedAt.AddMinutes(1), DateTimeKind.Unspecified);
        Assert.Throws<ArgumentException>(() => fulfillment.Accept(RestaurantId, unspecifiedTime));

        Assert.Throws<InvalidOrderFulfillmentTimestampException>(() =>
            fulfillment.Accept(RestaurantId, PlacedAt.AddTicks(-1)));

        fulfillment.Accept(RestaurantId, PlacedAt);
        Assert.Equal(PlacedAt, fulfillment.AcceptedAt);
    }

    private static OrderFulfillment NewFulfillment()
    {
        return new OrderFulfillment(OrderId, RestaurantId, PlacedAt, AcceptanceWindow);
    }
}
