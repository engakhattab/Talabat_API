using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Domain.Aggregates.Users;
using Talabat.Identity.Tests.Infrastructure;
using Xunit;

namespace Talabat.Identity.Tests;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class AgentApprovalEndpointTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;

    public AgentApprovalEndpointTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Approve_pending_agent_sets_flag_role_offline_and_changes_security_stamp()
    {
        var factory = new IdentityWebApplicationFactory();
        await factory.InitializeAsync();

        try
        {
            var client = factory.CreateClient();
            var email = $"agent_{Guid.NewGuid():N}@example.com";

            var regResponse = await client.PostAsync("/account/register/delivery-agent", Json(new
            {
                Email = email,
                Password = "P@ssw0rd123!",
                FullName = "Approve Me",
                VehicleType = 2,
                PhoneNumber = (string?)null
            }));
            Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
            var regBody = await regResponse.Content.ReadAsStringAsync();
            var regJson = JsonDocument.Parse(regBody);
            var userId = regJson.RootElement.GetProperty("id").GetInt32();

            string securityStampBefore;
            using (var scope = factory.Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(user);
                securityStampBefore = user.SecurityStamp!;
                Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
                Assert.Null(user.DeliveryAgentStatus);
                Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
            }

            var approveResponse = await client.PostAsync($"/account/delivery-agents/{userId}/approve", null);
            Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(user);
                Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
                Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
                Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
                Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
                Assert.NotEqual(securityStampBefore, user.SecurityStamp);
            }
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Approve_already_approved_returns_409()
    {
        var factory = new IdentityWebApplicationFactory();
        await factory.InitializeAsync();

        try
        {
            var client = factory.CreateClient();
            var email = $"agent_{Guid.NewGuid():N}@example.com";

            var regResponse = await client.PostAsync("/account/register/delivery-agent", Json(new
            {
                Email = email,
                Password = "P@ssw0rd123!",
                FullName = "Double Approve",
                VehicleType = 1,
                PhoneNumber = (string?)null
            }));
            var regBody = await regResponse.Content.ReadAsStringAsync();
            var userId = JsonDocument.Parse(regBody).RootElement.GetProperty("id").GetInt32();

            var first = await client.PostAsync($"/account/delivery-agents/{userId}/approve", null);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await client.PostAsync($"/account/delivery-agents/{userId}/approve", null);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Approve_non_existent_user_returns_404()
    {
        var factory = new IdentityWebApplicationFactory();
        await factory.InitializeAsync();

        try
        {
            var client = factory.CreateClient();
            var response = await client.PostAsync("/account/delivery-agents/999999/approve", null);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Approve_outside_development_returns_404()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Staging");
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsync("/account/delivery-agents/1/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reject_outside_development_returns_404()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Staging");
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsync("/account/delivery-agents/1/reject", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static StringContent Json(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
