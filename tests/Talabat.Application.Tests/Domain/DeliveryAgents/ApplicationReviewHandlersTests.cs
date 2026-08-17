using Talabat.Application.DeliveryAgents.ApplicationReview;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class ApplicationReviewHandlersTests
{
    [Fact]
    public async Task List_defaults_to_requested_status_and_details_exclude_non_applicants()
    {
        var repository = new FakeUserRepository();
        var pending = User.Register("pending", "pending@test.com", "Pending");
        pending.SubmitDeliveryAgentApplication(VehicleType.Bike);
        repository.Users.Add(pending);
        repository.Users.Add(User.Register("customer", "customer@test.com", "Customer"));

        var list = await new ListDeliveryAgentApplicationsHandler(repository).Handle(AgentApprovalStatus.PendingApproval);
        var missing = await new GetDeliveryAgentApplicationHandler(repository).Handle(2);

        Assert.Single(list);
        Assert.Equal("Pending", list.Single().FullName);
        Assert.True(missing.IsFailure);
        Assert.Equal("DeliveryAgentApplicationNotFound", missing.Error!.Code);
    }
}
