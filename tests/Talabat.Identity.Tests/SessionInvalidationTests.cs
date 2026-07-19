using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Domain.Aggregates.Users;
using Talabat.Identity.Tests.Infrastructure;
using Talabat.Infrastructure.Identity;
using Xunit;

namespace Talabat.Identity.Tests;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class SessionInvalidationTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;

    public SessionInvalidationTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_then_deactivate_user_rejects_next_request_with_401()
    {
        var factory = new IdentityWebApplicationFactory();
        factory.ConfigureZeroValidationInterval();
        await factory.InitializeAsync();

        try
        {
            var email = $"session_{Guid.NewGuid():N}@example.com";
            var client = factory.CreateClient();

            var regPayload = Json(new { Email = email, Password = "P@ssw0rd123!", FullName = "Session Test", Age = 25, PhoneNumber = (string?)null });
            var regResponse = await client.PostAsync("/account/register/customer", regPayload);
            Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

            var regBody = await regResponse.Content.ReadAsStringAsync();
            var regJson = JsonDocument.Parse(regBody);
            var userId = regJson.RootElement.GetProperty("id").GetInt32();

            var loginResponse = await client.PostAsync("/account/login", Json(new { Email = email, Password = "P@ssw0rd123!" }));
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var meResponse = await client.GetAsync("/account/me");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = await userManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(user);
                user!.Deactivate();
                await userManager.UpdateAsync(user);
                await userManager.UpdateSecurityStampAsync(user);
            }

            var meAfterDeactivate = await client.GetAsync("/account/me");
            Assert.Equal(HttpStatusCode.Unauthorized, meAfterDeactivate.StatusCode);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Login_then_soft_delete_user_rejects_next_request_with_401()
    {
        var factory = new IdentityWebApplicationFactory();
        factory.ConfigureZeroValidationInterval();
        await factory.InitializeAsync();

        try
        {
            var email = $"session_{Guid.NewGuid():N}@example.com";
            var client = factory.CreateClient();

            var regPayload = Json(new { Email = email, Password = "P@ssw0rd123!", FullName = "Session Delete Test", Age = 25, PhoneNumber = (string?)null });
            var regResponse = await client.PostAsync("/account/register/customer", regPayload);
            Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

            var regBody = await regResponse.Content.ReadAsStringAsync();
            var regJson = JsonDocument.Parse(regBody);
            var userId = regJson.RootElement.GetProperty("id").GetInt32();

            var loginResponse = await client.PostAsync("/account/login", Json(new { Email = email, Password = "P@ssw0rd123!" }));
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var meResponse = await client.GetAsync("/account/me");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

            using (var scope = factory.Factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
                var user = await dbContext.Users.FirstAsync(u => u.Id == userId);
                user.SoftDelete(DateTime.UtcNow, "test deletion");
                await dbContext.SaveChangesAsync();
                await userManager.UpdateSecurityStampAsync(user);
            }

            var meAfterDelete = await client.GetAsync("/account/me");
            Assert.Equal(HttpStatusCode.Unauthorized, meAfterDelete.StatusCode);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Logout_then_use_old_cookie_rejects_with_401()
    {
        var factory = new IdentityWebApplicationFactory();
        factory.ConfigureZeroValidationInterval();
        await factory.InitializeAsync();

        try
        {
            var email = $"session_{Guid.NewGuid():N}@example.com";
            var client = factory.CreateClient();

            var regPayload = Json(new { Email = email, Password = "P@ssw0rd123!", FullName = "Logout Test", Age = 25, PhoneNumber = (string?)null });
            var regResponse = await client.PostAsync("/account/register/customer", regPayload);
            Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

            var loginResponse = await client.PostAsync("/account/login", Json(new { Email = email, Password = "P@ssw0rd123!" }));
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var meResponse = await client.GetAsync("/account/me");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

            var logoutResponse = await client.PostAsync("/account/logout", null);
            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

            var meAfterLogout = await client.GetAsync("/account/me");
            Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);
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
