using Talabat.Application.Common.Results;
using Talabat.Application.Deliveries.CreateForOrder;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Deliveries.CreateForOrder;

public sealed class CreateDeliveryForOrderHandlerTests
{
    private static readonly DeliveryAddressSnapshot Address = new("1 Test St", "Cairo", "10", null);
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_CreatesPendingAssignmentDelivery()
    {
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        var clock = new FakeClock { UtcNow = Now };
        var handler = new CreateDeliveryForOrderHandler(repository, unitOfWork, clock);

        var cmd = new CreateDeliveryForOrderCommand(
            OrderId: 1,
            CustomerId: 100,
            RestaurantId: 200,
            DeliveryAddress: Address);

        var result = await handler.Handle(cmd);

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Deliveries);
        var delivery = repository.Deliveries[0];
        Assert.Equal(1, delivery.OrderId);
        Assert.Equal(100, delivery.CustomerId);
        Assert.Equal(200, delivery.RestaurantId);
        Assert.Null(delivery.AssignedAgentId);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Equal("1 Test St", delivery.DeliveryAddress.Street);
        Assert.Equal("Cairo", delivery.DeliveryAddress.City);
    }

    [Fact]
    public async Task Handle_DuplicateOrder_ReturnsConflict()
    {
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        var clock = new FakeClock { UtcNow = Now };
        var handler = new CreateDeliveryForOrderHandler(repository, unitOfWork, clock);

        var cmd = new CreateDeliveryForOrderCommand(
            OrderId: 1,
            CustomerId: 100,
            RestaurantId: 200,
            DeliveryAddress: Address);

        var first = await handler.Handle(cmd);
        Assert.True(first.IsSuccess);

        var second = await handler.Handle(cmd);
        Assert.True(second.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryAlreadyExists, second.Error?.Code);
    }

    [Fact]
    public async Task Handle_DuplicateOrder_DoesNotAddSecondDelivery()
    {
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        var clock = new FakeClock { UtcNow = Now };
        var handler = new CreateDeliveryForOrderHandler(repository, unitOfWork, clock);

        var cmd = new CreateDeliveryForOrderCommand(
            OrderId: 1,
            CustomerId: 100,
            RestaurantId: 200,
            DeliveryAddress: Address);

        await handler.Handle(cmd);
        await handler.Handle(cmd);

        Assert.Single(repository.Deliveries);
    }
}
