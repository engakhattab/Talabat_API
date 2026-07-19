using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Application.Abstractions;
using Talabat.Domain.Aggregates.Users;
using Talabat.Identity.Tests.Infrastructure;
using Talabat.Infrastructure.Identity;
using Xunit;

namespace Talabat.Identity.Tests;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class MultiRoleJourneyTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;

    public MultiRoleJourneyTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Approved_agent_granted_customer_has_both_flags_roles_and_one_ID()
    {
        var factory = new IdentityWebApplicationFactory();
        await factory.InitializeAsync();

        try
        {
            var email = $"agent_{Guid.NewGuid():N}@example.com";
            var client = factory.CreateClient();

            var regPayload = Json(new
            {
                Email = email,
                Password = "P@ssw0rd123!",
                FullName = "Multi Role Agent",
                VehicleType = 2,
                PhoneNumber = (string?)null
            });

            var regResponse = await client.PostAsync("/account/register/delivery-agent", regPayload);
            Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
            var regBody = await regResponse.Content.ReadAsStringAsync();
            var regJson = JsonDocument.Parse(regBody);
            var userId = regJson.RootElement.GetProperty("id").GetInt32();
            Assert.True(userId > 0);

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var capabilityService = scope.ServiceProvider.GetRequiredService<IUserCapabilityService>();
                var result = await capabilityService.ApproveDeliveryAgentAsync(userId);
                Assert.True(result.IsSuccess, $"ApproveDeliveryAgent failed: {result.Error?.Message ?? "unknown"}");
            }

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(user);
                Assert.True(user!.UserType.HasFlag(UserType.DeliveryAgent));
                Assert.False(user.UserType.HasFlag(UserType.Customer));
                Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
                Assert.False(await userManager.IsInRoleAsync(user, "Customer"));
                Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
                Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
            }

            var loginPayload = Json(new { Email = email, Password = "P@ssw0rd123!" });
            var loginResponse = await client.PostAsync("/account/login", loginPayload);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var capabilityService = scope.ServiceProvider.GetRequiredService<IUserCapabilityService>();
                var grantResult = await capabilityService.GrantCustomerCapabilityAsync(userId, "Multi Role Agent", 30, null);
                Assert.True(grantResult.IsSuccess, $"GrantCustomerCapability failed: {grantResult.Error?.Message ?? "unknown"}");
            }

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(user);
                Assert.True(user!.UserType.HasFlag(UserType.Customer));
                Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
                Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
                Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
                Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
                Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
            }
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Multi_role_journey_preserves_one_integer_ID()
    {
        var factory = new IdentityWebApplicationFactory();
        await factory.InitializeAsync();

        try
        {
            var email = $"journey_{Guid.NewGuid():N}@example.com";
            var client = factory.CreateClient();

            var regPayload = Json(new
            {
                Email = email,
                Password = "P@ssw0rd123!",
                FullName = "Journey Agent",
                VehicleType = 1,
                PhoneNumber = (string?)null
            });

            var regResponse = await client.PostAsync("/account/register/delivery-agent", regPayload);
            Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
            var regBody = await regResponse.Content.ReadAsStringAsync();
            var regJson = JsonDocument.Parse(regBody);
            var userId = regJson.RootElement.GetProperty("id").GetInt32();

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var capabilityService = scope.ServiceProvider.GetRequiredService<IUserCapabilityService>();
                await capabilityService.ApproveDeliveryAgentAsync(userId);
                await capabilityService.GrantCustomerCapabilityAsync(userId, "Journey Agent", 25, null);
            }

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var allUsers = await userManager.Users.Where(u => u.Email == email).ToListAsync();
                Assert.Single(allUsers);
                Assert.Equal(userId, allUsers[0].Id);
            }
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    private static StringContent Json(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
