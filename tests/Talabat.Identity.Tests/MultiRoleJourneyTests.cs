using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Application.Abstractions;
using Talabat.Application.Basket.AddItem;
using Talabat.Application.Basket.GetCart;
using Talabat.Application.Catalog.BrowseRestaurants;
using Talabat.Application.Ordering.Checkout;
using Talabat.Application.Ordering.GetOrderDetails;
using Talabat.Application.Ordering.GetOrderHistory;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;
using Talabat.Identity.Tests.Infrastructure;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;
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
                var dbContext = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();
                var user = await dbContext.Users.Include("_addresses").SingleAsync(u => u.Id == userId);
                user.AddAddress(new Address("10 Journey Street", "Cairo", "10"), makeDefault: true);

                var restaurant = new Restaurant(
                    "Journey Restaurant",
                    "Multi-role journey restaurant",
                    null,
                    new TimeRange(TimeOnly.MinValue, TimeOnly.MaxValue));
                dbContext.Restaurants.Add(restaurant);
                await dbContext.SaveChangesAsync();

                var product = restaurant.AddProduct(
                    "Journey Meal",
                    "Multi-role journey product",
                    new Money(25m),
                    null);
                await dbContext.SaveChangesAsync();

                var browse = await scope.ServiceProvider.GetRequiredService<BrowseRestaurantsHandler>()
                    .Handle(new BrowseRestaurantsQuery());
                Assert.True(browse.IsSuccess);
                Assert.Contains(browse.Value, item => item.Id == restaurant.Id);

                var addCart = await scope.ServiceProvider.GetRequiredService<AddCartItemHandler>()
                    .Handle(new AddCartItemCommand(userId, restaurant.Id, product.Id, 2));
                Assert.True(addCart.IsSuccess, addCart.Error?.Message);
                Assert.Equal(userId, addCart.Value.CustomerId);

                var cart = await scope.ServiceProvider.GetRequiredService<GetCartHandler>()
                    .Handle(new GetCartQuery(userId));
                Assert.True(cart.IsSuccess, cart.Error?.Message);
                Assert.Single(cart.Value.Items);

                var checkout = await scope.ServiceProvider.GetRequiredService<CheckoutHandler>()
                    .Handle(new CheckoutCommand(userId, user.Addresses.Single().Id));
                Assert.True(checkout.IsSuccess, checkout.Error?.Message);
                var checkoutSucceeded = Assert.IsType<CheckoutSucceededOutcome>(checkout.Value);

                var orders = await scope.ServiceProvider.GetRequiredService<GetOrderHistoryHandler>()
                    .Handle(new GetOrderHistoryQuery(userId));
                Assert.True(orders.IsSuccess, orders.Error?.Message);
                Assert.Single(orders.Value);

                var order = await scope.ServiceProvider.GetRequiredService<GetOrderDetailsHandler>()
                    .Handle(new GetOrderDetailsQuery(userId, checkoutSucceeded.OrderId));
                Assert.True(order.IsSuccess, order.Error?.Message);
                Assert.Equal(userId, order.Value.CustomerId);
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
