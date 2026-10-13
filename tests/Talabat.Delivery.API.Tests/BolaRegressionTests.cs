using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Delivery.API.Tests.Infrastructure;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Delivery.API.Tests;

public sealed class BolaRegressionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public BolaRegressionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateLocation_with_extra_agentId_in_body_does_not_hijack_location()
    {
        // BOLA attack: spoof AgentId in request body to try updating another agent's location.
        // The server must derive agentId from the authenticated token, ignoring any body-supplied AgentId.
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.DeliveryAgentUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");

        // Location updates are only accepted while the agent is Available or Busy.
        var online = await _client.PutAsync("/api/agent/status/online", null);
        Assert.Equal(HttpStatusCode.NoContent, online.StatusCode);

        var agentBLocationBefore = await GetLocationAsync(_factory.AgentBUserId);

        var response = await _client.PutAsJsonAsync("/api/agent/location", new
        {
            Latitude = 30.0m,
            Longitude = 31.0m,
            AgentId = _factory.AgentBUserId
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal((30.0m, 31.0m), await GetLocationAsync(_factory.DeliveryAgentUserId));
        Assert.Equal(agentBLocationBefore, await GetLocationAsync(_factory.AgentBUserId));
    }

    private async Task<(decimal Latitude, decimal Longitude)?> GetLocationAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);

        return user.CurrentLocation is null
            ? null
            : (user.CurrentLocation.Latitude, user.CurrentLocation.Longitude);
    }
}
