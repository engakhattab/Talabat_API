using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.ProgressFail;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.DomainServices.DeliveryManagement;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class FailDeliveryHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DeliveryAddressSnapshot Address = new("Street", "City", "1", null);

    [Fact]
    public async Task Handle_FailDelivery_FromAssigned_ShouldSucceed()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        var (handler, clock) = CreateHandler(agent, delivery);

        clock.UtcNow = Now.AddMinutes(5);

        var result = await handler.Handle(new FailDeliveryCommand(DeliveryId: 1, Reason: "Customer unreachable"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(clock.UtcNow, delivery.FailedAt);
    }

    [Fact]
    public async Task Handle_FailDelivery_WrongAgent_ShouldFail()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        var wrongAgent = CreateAvailableAgent(20);
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = wrongAgent.Id,
            HasDeliveryAgentCapability = true,
            AgentId = wrongAgent.Id
        };

        var deliveryRepository = new FakeDeliveryRepository();
        deliveryRepository.Deliveries.Add(delivery);
        var userRepository = new FakeUserRepository();
        userRepository.Users.Add(wrongAgent);

        var handler = new FailDeliveryHandler(
            deliveryRepository,
            userRepository,
            new DeliveryAssignmentDomainService(),
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);

        var result = await handler.Handle(new FailDeliveryCommand(DeliveryId: 1, Reason: "Traffic jam"));

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(DeliveryAgentMismatchException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_FailDelivery_DeliveryNotFound_ShouldReturnNotFound()
    {
        var agent = CreateBusyAgent(10);
        var (handler, _) = CreateHandler(agent);

        var result = await handler.Handle(new FailDeliveryCommand(DeliveryId: 999, Reason: "No reason"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_FailDelivery_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new FailDeliveryCommand(DeliveryId: 1, Reason: "No reason"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_FailDelivery_WithReason_ShouldStoreReason()
    {
        var agent = CreateBusyAgent(10);
        var delivery = CreateAssignedDelivery(1, 10);
        var (handler, clock) = CreateHandler(agent, delivery);

        clock.UtcNow = Now.AddMinutes(5);

        var result = await handler.Handle(new FailDeliveryCommand(DeliveryId: 1, Reason: "Wrong address"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Wrong address", delivery.FailureReason);
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

    private static User CreateBusyAgent(int id)
    {
        var agent = CreateAvailableAgent(id);
        var throwaway = new Delivery(1, 1, 1, Address, Now);
        var domainService = new DeliveryAssignmentDomainService();
        domainService.Assign(throwaway, agent, Now);
        return agent;
    }

    private static Delivery CreatePendingDelivery(int id)
    {
        var delivery = new Delivery(1, 1, 1, Address, Now);
        TestIds.SetId(delivery, id);
        return delivery;
    }

    private static Delivery CreateAssignedDelivery(int id, int agentId)
    {
        var delivery = CreatePendingDelivery(id);
        delivery.AssignAgent(agentId, Now.AddMinutes(1));
        return delivery;
    }

    private static (FailDeliveryHandler Handler, FakeClock Clock) CreateHandler(
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

        var handler = new FailDeliveryHandler(
            deliveryRepository,
            userRepository,
            new DeliveryAssignmentDomainService(),
            new FakeUnitOfWork(),
            clock,
            currentUser);

        return (handler, clock);
    }

    private static FailDeliveryHandler CreateHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new FailDeliveryHandler(
            new FakeDeliveryRepository(),
            new FakeUserRepository(),
            new DeliveryAssignmentDomainService(),
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);
    }
}
