using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.GetCurrentStatus;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class GetCurrentDeliveryAgentStatusHandlerTests
{
    [Theory]
    [InlineData(DeliveryAgentStatus.Offline)]
    [InlineData(DeliveryAgentStatus.Available)]
    [InlineData(DeliveryAgentStatus.Busy)]
    [InlineData(DeliveryAgentStatus.Suspended)]
    public async Task Handle_ReturnsPersistedStatus(DeliveryAgentStatus expectedStatus)
    {
        var agent = CreateApprovedAgent();
        if (expectedStatus is DeliveryAgentStatus.Available or DeliveryAgentStatus.Busy)
        {
            agent.GoOnline();
        }

        if (expectedStatus == DeliveryAgentStatus.Busy)
        {
            agent.MarkBusy();
        }
        else if (expectedStatus == DeliveryAgentStatus.Suspended)
        {
            agent.Suspend();
        }

        var repository = new FakeUserRepository();
        repository.Users.Add(agent);
        var handler = new GetCurrentDeliveryAgentStatusHandler(repository);

        var result = await handler.Handle(new GetCurrentDeliveryAgentStatusQuery(agent.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus, result.Value.Status);
    }

    [Fact]
    public async Task Handle_WhenAgentIsMissing_ReturnsStableNotFoundCode()
    {
        var handler = new GetCurrentDeliveryAgentStatusHandler(new FakeUserRepository());

        var result = await handler.Handle(new GetCurrentDeliveryAgentStatusQuery(999));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    private static User CreateApprovedAgent()
    {
        var agent = User.Register("agent@test.com", "agent@test.com", "Agent");
        agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        agent.ApproveDeliveryAgentApplication();
        TestIds.SetId(agent, 1);
        return agent;
    }
}
