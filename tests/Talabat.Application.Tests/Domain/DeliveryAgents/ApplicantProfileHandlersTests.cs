using Talabat.Application.Common.Results;
using Talabat.Application.DeliveryAgents.ApplicantProfile;
using Talabat.Application.Tests.TestDoubles;
using Talabat.Domain.Aggregates.Users;

namespace Talabat.Application.Tests.Domain.DeliveryAgents;

public sealed class ApplicantProfileHandlersTests
{
    [Fact]
    public async Task Get_returns_current_applicant_profile()
    {
        var users = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork(users);
        var user = User.Register("applicant", "applicant@example.com", "Applicant");
        user.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        users.Users.Add(user);
        await unitOfWork.SaveChangesAsync();

        var result = await new GetDeliveryApplicantProfileHandler(users).Handle(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(AgentApprovalStatus.PendingApproval, result.Value.ApplicationStatus);
        Assert.Equal(VehicleType.Motorcycle, result.Value.VehicleType);
        Assert.Null(result.Value.DeliveryAgentStatus);
    }

    [Fact]
    public async Task Update_changes_only_editable_profile_fields()
    {
        var users = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork(users);
        var user = User.Register("applicant", "applicant@example.com", "Applicant");
        user.SubmitDeliveryAgentApplication(VehicleType.Car);
        users.Users.Add(user);
        await unitOfWork.SaveChangesAsync();

        var result = await new UpdateDeliveryApplicantProfileHandler(users, unitOfWork)
            .Handle(user.Id, "Updated Applicant", "+201000000000");

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Applicant", user.FullName);
        Assert.Equal("+201000000000", user.PhoneNumber);
        Assert.Equal(VehicleType.Car, user.VehicleType);
        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Null(user.DeliveryAgentStatus);
        Assert.Equal(1, users.UpdateCount);
    }

    [Fact]
    public async Task Get_missing_application_returns_stable_not_found_error()
    {
        var users = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork(users);
        var user = User.Register("person", "person@example.com", "Person");
        users.Users.Add(user);
        await unitOfWork.SaveChangesAsync();

        var result = await new GetDeliveryApplicantProfileHandler(users).Handle(user.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.DeliveryAgentApplicationNotFound, result.Error!.Code);
    }
}
