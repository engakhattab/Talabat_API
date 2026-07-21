using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.AssignDelivery;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.DomainServices.DeliveryManagement;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class AssignDeliveryHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public async Task Handle_AssignDelivery_ValidAgent_ShouldSucceed()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreatePendingDelivery(1);
        var (handler, clock) = CreateHandler(agent, delivery);

        clock.UtcNow = Now.AddMinutes(1);

        var result = await handler.Handle(new AssignDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
        Assert.Equal(10, delivery.AssignedAgentId);
    }

    [Fact]
    public async Task Handle_AssignDelivery_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new AssignDeliveryCommand(DeliveryId: 1, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_AssignDelivery_DeliveryNotFound_ShouldReturnNotFound()
    {
        var agent = CreateAvailableAgent(10);
        var (handler, _) = CreateHandler(agent);

        var result = await handler.Handle(new AssignDeliveryCommand(DeliveryId: 999, AgentId: 10));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_AssignDelivery_AgentNotFound_ShouldReturnNotFound()
    {
        var agent = CreateAvailableAgent(10);
        var delivery = CreatePendingDelivery(1);
        var (handler, _) = CreateHandler(agent, delivery);

        var result = await handler.Handle(new AssignDeliveryCommand(DeliveryId: 1, AgentId: 999));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_AssignDelivery_DeliveryAlreadyAssigned_ShouldFail()
    {
        var agent1 = CreateAvailableAgent(10);
        var agent2 = CreateAvailableAgent(20);
        var delivery = CreatePendingDelivery(1);
        var domainService = new DeliveryAssignmentDomainService();
        domainService.Assign(delivery, agent1, Now);

        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = 1,
            HasDeliveryAgentCapability = true,
            AgentId = 1
        };
        var deliveryRepository = new FakeDeliveryRepository();
        deliveryRepository.Deliveries.Add(delivery);
        var userRepository = new FakeUserRepository();
        userRepository.Users.Add(agent2);

        var handler = new AssignDeliveryAgentHandler(
            deliveryRepository,
            userRepository,
            domainService,
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);

        var result = await handler.Handle(new AssignDeliveryCommand(DeliveryId: 1, AgentId: 20));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(DeliveryAlreadyAssignedException), result.Error?.Code);
    }

    private static User CreateAvailableAgent(int id)
    {
        var agent = User.Register($"agent{id}@test.com", $"agent{id}@test.com", $"Agent {id}");
        agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        agent.ApproveDeliveryAgentApplication();
        agent.GoOnline();
        agent.UpdateLocation(new GeoLocation(30.0m, 31.0m));
        TestIds.SetId(agent, id);
        return agent;
    }

    private static Delivery CreatePendingDelivery(int id)
    {
        var delivery = new Delivery(1, 1, 1, Address, Now);
        TestIds.SetId(delivery, id);
        return delivery;
    }

    private static (AssignDeliveryAgentHandler Handler, FakeClock Clock) CreateHandler(
        User agent,
        params Delivery[] deliveries)
    {
        var deliveryRepository = new FakeDeliveryRepository();
        foreach (var delivery in deliveries)
        {
            deliveryRepository.Deliveries.Add(delivery);
        }

        var userRepository = new FakeUserRepository();
        userRepository.Users.Add(agent);

        var clock = new FakeClock { UtcNow = Now };
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = agent.Id,
            HasDeliveryAgentCapability = true,
            AgentId = agent.Id
        };

        var handler = new AssignDeliveryAgentHandler(
            deliveryRepository,
            userRepository,
            new DeliveryAssignmentDomainService(),
            new FakeUnitOfWork(),
            clock,
            currentUser);

        return (handler, clock);
    }

    private static AssignDeliveryAgentHandler CreateHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new AssignDeliveryAgentHandler(
            new FakeDeliveryRepository(),
            new FakeUserRepository(),
            new DeliveryAssignmentDomainService(),
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);
    }
}
