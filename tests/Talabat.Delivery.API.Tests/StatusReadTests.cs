using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Delivery.API.Tests.Infrastructure;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Delivery.API.Tests;

public sealed class StatusReadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StatusReadTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStatus_ReturnsOfflineThenAvailableAfterGoOnline()
    {
        using var client = CreateAgentClient(_factory.DeliveryAgentUserId);

        var offline = await client.GetFromJsonAsync<DeliveryAgentStatusResponse>("/api/agent/status");
        Assert.NotNull(offline);
        Assert.Equal("Offline", offline.Status);

        var goOnline = await client.PutAsync("/api/agent/status/online", null);
        Assert.Equal(HttpStatusCode.NoContent, goOnline.StatusCode);

        var available = await client.GetFromJsonAsync<DeliveryAgentStatusResponse>("/api/agent/status");
        Assert.NotNull(available);
        Assert.Equal("Available", available.Status);

        var goOffline = await client.PutAsync("/api/agent/status/offline", null);
        Assert.Equal(HttpStatusCode.NoContent, goOffline.StatusCode);

        var offlineAgain = await client.GetFromJsonAsync<DeliveryAgentStatusResponse>("/api/agent/status");
        Assert.NotNull(offlineAgain);
        Assert.Equal("Offline", offlineAgain.Status);
    }

    [Theory]
    [InlineData(nameof(CustomWebApplicationFactory.PendingApplicantUserId))]
    [InlineData(nameof(CustomWebApplicationFactory.RejectedApplicantUserId))]
    public async Task NonApprovedApplicant_CannotReadOperationalStatus(string applicantKind)
    {
        var userId = applicantKind == nameof(CustomWebApplicationFactory.PendingApplicantUserId)
            ? _factory.PendingApplicantUserId
            : _factory.RejectedApplicantUserId;
        using var client = CreateApplicantClient(userId);

        var response = await client.GetAsync("/api/agent/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAgentClient(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
        return client;
    }

    private HttpClient CreateApplicantClient(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
        return client;
    }

    private sealed record DeliveryAgentStatusResponse(string Status);

}

public sealed class SuspendedStatusReadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SuspendedStatusReadTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SuspendedAgent_CanReadStatusAndProfile_ButCannotPerformOperationalWork()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
            var agent = db.Users.Single(user => user.Id == _factory.DeliveryAgentUserId);
            agent.Suspend();
            await db.SaveChangesAsync();
        }

        using var client = CreateAgentClient(_factory.DeliveryAgentUserId);
        var status = await client.GetFromJsonAsync<DeliveryAgentStatusResponse>("/api/agent/status");
        Assert.NotNull(status);
        Assert.Equal("Suspended", status.Status);

        var profile = await client.GetFromJsonAsync<DeliveryApplicantProfileResponse>("/api/agent/me/profile");
        Assert.NotNull(profile);
        Assert.Equal("Suspended", profile.DeliveryAgentStatus);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync("/api/agent/location", new { Latitude = 30m, Longitude = 31m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/agent/deliveries/pending")).StatusCode);
    }

    private HttpClient CreateAgentClient(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
        return client;
    }

    private sealed record DeliveryAgentStatusResponse(string Status);
    private sealed record DeliveryApplicantProfileResponse(string? DeliveryAgentStatus);
}
