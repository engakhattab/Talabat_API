using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Admin.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Admin.API.Tests;

public sealed class DeliveryAgentApplicationsTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public DeliveryAgentApplicationsTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        using var _ = _factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/delivery-agent-applications")).StatusCode);
    }

    [Fact]
    public async Task Non_admin_or_missing_scope_is_forbidden()
    {
        using var nonAdmin = CreateClient(_factory.NonAdminUserId, null, "admin.api");
        using var noScope = CreateClient(_factory.AdminUserId, "Admin", "delivery.api");
        Assert.Equal(HttpStatusCode.Forbidden, (await nonAdmin.GetAsync("/api/delivery-agent-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noScope.GetAsync("/api/delivery-agent-applications")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_and_read_only_review_fields()
    {
        using var client = CreateClient(_factory.AdminUserId, "Admin", "admin.api");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/delivery-agent-applications");
        Assert.Contains(list.EnumerateArray(), item => item.GetProperty("userId").GetInt32() == _factory.PendingApplicantForListUserId);

        var response = await client.GetAsync($"/api/delivery-agent-applications/{_factory.PendingApplicantUserId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var details = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Pending Applicant", details.RootElement.GetProperty("fullName").GetString());
        Assert.True(details.RootElement.TryGetProperty("email", out _));
        Assert.True(details.RootElement.TryGetProperty("phoneNumber", out _));
        Assert.False(details.RootElement.TryGetProperty("userType", out _));
        Assert.False(details.RootElement.TryGetProperty("securityStamp", out _));
    }

    [Fact]
    public async Task Admin_can_approve_pending_application_and_sync_role_capability_and_offline_status()
    {
        using var client = CreateClient(_factory.AdminUserId, "Admin", "admin.api");
        var response = await client.PostAsync($"/api/delivery-agent-applications/{_factory.PendingApplicantUserId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByIdAsync(_factory.PendingApplicantUserId.ToString());
        Assert.Equal(AgentApprovalStatus.Approved, user!.AgentApprovalStatus);
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.True(await users.IsInRoleAsync(user, "DeliveryAgent"));

        var second = await client.PostAsync($"/api/delivery-agent-applications/{_factory.PendingApplicantUserId}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Admin_can_reject_pending_application_and_rejecting_again_conflicts()
    {
        using var client = CreateClient(_factory.AdminUserId, "Admin", "admin.api");
        var response = await client.PostAsync($"/api/delivery-agent-applications/{_factory.PendingApplicantForRejectionUserId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await client.PostAsync($"/api/delivery-agent-applications/{_factory.PendingApplicantForRejectionUserId}/reject", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var invalid = await client.PostAsync($"/api/delivery-agent-applications/{_factory.RejectedApplicantUserId}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
    }

    [Fact]
    public async Task Runtime_openapi_matches_committed_document()
    {
        using var client = _factory.CreateClient();
        var runtimeJson = await client.GetStringAsync("/openapi/v1.json");
        var projectDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Talabat", "Talabat.Admin.API"));
        var committedJson = await File.ReadAllTextAsync(Path.Combine(projectDirectory, "openapi", "admin-api.json"));

        using var runtime = JsonDocument.Parse(runtimeJson);
        using var committed = JsonDocument.Parse(committedJson);
        Assert.Equal(
            committed.RootElement.GetProperty("paths").GetRawText(),
            runtime.RootElement.GetProperty("paths").GetRawText());
    }

    [Fact]
    public async Task Missing_application_returns_problem_details_error_code()
    {
        using var client = CreateClient(_factory.AdminUserId, "Admin", "admin.api");
        var response = await client.GetAsync("/api/delivery-agent-applications/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("DeliveryAgentApplicationNotFound", json.RootElement.GetProperty("errorCode").GetString());
    }

    private HttpClient CreateClient(int subject, string? role, string scope)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, subject.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, scope);
        if (role is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
