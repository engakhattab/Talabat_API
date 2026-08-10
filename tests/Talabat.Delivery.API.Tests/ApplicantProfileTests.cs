using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Delivery.API.Tests.Infrastructure;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Delivery.API.Tests;

public sealed class ApplicantProfileTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApplicantProfileTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        using var _ = _factory.CreateClient();
    }

    [Fact]
    public async Task Pending_rejected_and_approved_users_can_read_their_own_application_status()
    {
        await AssertApplicationStatus(_factory.PendingApplicantUserId, "PendingApproval");
        await AssertApplicationStatus(_factory.RejectedApplicantUserId, "Rejected");
        await AssertApplicationStatus(_factory.DeliveryAgentUserId, "Approved");
    }

    [Fact]
    public async Task Pending_applicant_can_update_profile_without_changing_vehicle_or_application_state()
    {
        using var client = CreateApplicantClient(_factory.PendingApplicantUserId);

        var profileBeforeUpdate = await client.GetAsync("/api/agent/me/profile");
        Assert.True(
            profileBeforeUpdate.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {(int)profileBeforeUpdate.StatusCode}: {await profileBeforeUpdate.Content.ReadAsStringAsync()}");

        var response = await client.PutAsJsonAsync("/api/agent/me/profile", new
        {
            fullName = "Updated Pending Applicant",
            phoneNumber = "+201000000099",
            vehicleType = "Car"
        });

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Updated Pending Applicant", document.RootElement.GetProperty("fullName").GetString());
        Assert.Equal("Bike", document.RootElement.GetProperty("vehicleType").GetString());
        Assert.Equal("PendingApproval", document.RootElement.GetProperty("applicationStatus").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("deliveryAgentStatus").ValueKind);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == _factory.PendingApplicantUserId);
        Assert.Equal("Updated Pending Applicant", user.FullName);
        Assert.Equal("+201000000099", user.PhoneNumber);
        Assert.Equal(Talabat.Domain.Aggregates.Users.VehicleType.Bike, user.VehicleType);
        Assert.Equal(Talabat.Domain.Aggregates.Users.AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Null(user.DeliveryAgentStatus);
        Assert.Null(user.CurrentLocation);
    }

    [Fact]
    public async Task Applicant_scope_does_not_grant_operational_delivery_access()
    {
        using var client = CreateApplicantClient(_factory.PendingApplicantUserId);

        var response = await client.PutAsync("/api/agent/status/online", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_application_returns_problem_details_with_stable_error_code()
    {
        using var client = CreateApplicantClient(_factory.RoleOnlyAgentUserId, "DeliveryAgent");

        var response = await client.GetAsync("/api/agent/me/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "DeliveryAgentApplicationNotFound",
            document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Applicant_contract_requires_delivery_scope_but_not_delivery_agent_role()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.PendingApplicantUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");

        var response = await client.GetAsync("/api/agent/me/application");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task AssertApplicationStatus(int userId, string expectedStatus)
    {
        using var client = CreateApplicantClient(userId);
        var response = await client.GetAsync("/api/agent/me/application");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("applicationStatus").GetString());
    }

    private HttpClient CreateApplicantClient(int userId, string? roles = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        }

        return client;
    }
}
