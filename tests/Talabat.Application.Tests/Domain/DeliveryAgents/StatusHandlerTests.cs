using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.GoOffline;
using Talabat.Application.DeliveryAgents.GoOnline;
using Talabat.Application.DeliveryAgents.UpdateLocation;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class StatusHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    #region GoOnline

    [Fact]
    public async Task Handle_GoOnline_WhenAvailable_ShouldTransitionToOnline()
    {
        var agent = CreateAvailableAgent(10);
        var (handler, _) = CreateGoOnlineHandler(agent);

        var result = await handler.Handle(new GoOnlineCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryAgentStatus.Available, agent.DeliveryAgentStatus);
    }

    [Fact]
    public async Task Handle_GoOnline_WhenOffline_ShouldTransitionToOnline()
    {
        var agent = CreateOfflineAgent(10);
        var (handler, _) = CreateGoOnlineHandler(agent);

        var result = await handler.Handle(new GoOnlineCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryAgentStatus.Available, agent.DeliveryAgentStatus);
    }

    [Fact]
    public async Task Handle_GoOnline_WhenBusy_ShouldFail()
    {
        var agent = CreateBusyAgent(10);
        var (handler, _) = CreateGoOnlineHandler(agent);

        var result = await handler.Handle(new GoOnlineCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(AgentNotAvailableException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_GoOnline_WhenSuspended_ShouldFail()
    {
        var agent = CreateSuspendedAgent(10);
        var (handler, _) = CreateGoOnlineHandler(agent);

        var result = await handler.Handle(new GoOnlineCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(AgentNotAvailableException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_GoOnline_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateGoOnlineHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new GoOnlineCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_GoOnline_AgentNotFound_ShouldReturnNotFound()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = 999,
            HasDeliveryAgentCapability = true,
            AgentId = 999
        };
        var handler = CreateGoOnlineHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new GoOnlineCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    #endregion

    #region GoOffline

    [Fact]
    public async Task Handle_GoOffline_WhenAvailable_ShouldTransitionToOffline()
    {
        var agent = CreateAvailableAgent(10);
        var (handler, _) = CreateGoOfflineHandler(agent);

        var result = await handler.Handle(new GoOfflineCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryAgentStatus.Offline, agent.DeliveryAgentStatus);
    }

    [Fact]
    public async Task Handle_GoOffline_WhenOffline_ShouldSucceed()
    {
        var agent = CreateOfflineAgent(10);
        var (handler, _) = CreateGoOfflineHandler(agent);

        var result = await handler.Handle(new GoOfflineCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryAgentStatus.Offline, agent.DeliveryAgentStatus);
    }

    [Fact]
    public async Task Handle_GoOffline_WhenBusy_ShouldFail()
    {
        var agent = CreateBusyAgent(10);
        var (handler, _) = CreateGoOfflineHandler(agent);

        var result = await handler.Handle(new GoOfflineCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(InvalidDeliveryAgentStatusTransitionException), result.Error?.Code);
    }

    [Fact]
    public async Task Handle_GoOffline_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateGoOfflineHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new GoOfflineCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    #endregion

    #region UpdateLocation

    [Fact]
    public async Task Handle_UpdateLocation_WhenAvailable_ShouldSucceed()
    {
        var agent = CreateAvailableAgent(10);
        var (handler, _) = CreateUpdateLocationHandler(agent);

        var result = await handler.Handle(new UpdateLocationCommand(Latitude: 30.0m, Longitude: 31.0m));

        Assert.True(result.IsSuccess);
        Assert.Equal(new GeoLocation(30.0m, 31.0m), agent.CurrentLocation);
    }

    [Fact]
    public async Task Handle_UpdateLocation_WhenOffline_ShouldSucceed()
    {
        var agent = CreateOfflineAgent(10);
        var (handler, _) = CreateUpdateLocationHandler(agent);

        var result = await handler.Handle(new UpdateLocationCommand(Latitude: 30.0m, Longitude: 31.0m));

        Assert.True(result.IsSuccess);
        Assert.Equal(new GeoLocation(30.0m, 31.0m), agent.CurrentLocation);
    }

    [Fact]
    public async Task Handle_UpdateLocation_Unauthenticated_ShouldReturnOwnershipMismatch()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = false,
            HasDeliveryAgentCapability = false,
            AgentId = null
        };
        var handler = CreateUpdateLocationHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new UpdateLocationCommand(Latitude: 30.0m, Longitude: 31.0m));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.AgentRequired, result.Error?.Code);
    }

    [Fact]
    public async Task Handle_UpdateLocation_AgentNotFound_ShouldReturnNotFound()
    {
        var currentUser = new FakeCurrentUser
        {
            IsAuthenticated = true,
            UserId = 999,
            HasDeliveryAgentCapability = true,
            AgentId = 999
        };
        var handler = CreateUpdateLocationHandlerWithCurrentUser(currentUser);

        var result = await handler.Handle(new UpdateLocationCommand(Latitude: 30.0m, Longitude: 31.0m));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    #endregion

    #region Helpers

    private static User CreateOfflineAgent(int id)
    {
        var agent = User.Register($"agent{id}@test.com", $"agent{id}@test.com", $"Agent {id}");
        agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        agent.ApproveDeliveryAgentApplication();
        TestIds.SetId(agent, id);
        return agent;
    }

    private static User CreateAvailableAgent(int id)
    {
        var agent = CreateOfflineAgent(id);
        agent.GoOnline();
        agent.UpdateLocation(new GeoLocation(30.0m, 31.0m));
        return agent;
    }

    private static User CreateBusyAgent(int id)
    {
        var agent = CreateAvailableAgent(id);
        agent.MarkBusy();
        return agent;
    }

    private static User CreateSuspendedAgent(int id)
    {
        var agent = CreateOfflineAgent(id);
        agent.Suspend();
        return agent;
    }

    private static (GoOnlineHandler Handler, FakeClock Clock) CreateGoOnlineHandler(User agent)
    {
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
        var handler = new GoOnlineHandler(userRepository, new FakeUnitOfWork(), clock, currentUser);
        return (handler, clock);
    }

    private static GoOnlineHandler CreateGoOnlineHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new GoOnlineHandler(
            new FakeUserRepository(),
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);
    }

    private static (GoOfflineHandler Handler, FakeClock Clock) CreateGoOfflineHandler(User agent)
    {
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
        var handler = new GoOfflineHandler(userRepository, new FakeUnitOfWork(), clock, currentUser);
        return (handler, clock);
    }

    private static GoOfflineHandler CreateGoOfflineHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new GoOfflineHandler(
            new FakeUserRepository(),
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);
    }

    private static (UpdateLocationHandler Handler, FakeClock Clock) CreateUpdateLocationHandler(User agent)
    {
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
        var handler = new UpdateLocationHandler(userRepository, new FakeUnitOfWork(), clock, currentUser);
        return (handler, clock);
    }

    private static UpdateLocationHandler CreateUpdateLocationHandlerWithCurrentUser(ICurrentUser currentUser)
    {
        return new UpdateLocationHandler(
            new FakeUserRepository(),
            new FakeUnitOfWork(),
            new FakeClock(),
            currentUser);
    }

    #endregion
}
