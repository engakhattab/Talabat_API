using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Customer.API.Contracts.Orders;
using Talabat.Customer.API.Tests.Infrastructure;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Persistence;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class OrderHistoryPagingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderHistoryPagingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Order_history_reports_item_counts_and_real_paging_metadata()
    {
        var customerId = await SeedCustomerWithOrdersAsync(orderCount: 3, quantityPerOrder: 4);
        using var client = CreateClientFor(customerId);

        var response = await client.GetAsync("/api/me/orders?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderListResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Page);
        Assert.Equal(2, body.PageSize);
        Assert.Equal(3, body.TotalCount);
        var onlyItemOnPageTwo = Assert.Single(body.Items);
        Assert.Equal(4, onlyItemOnPageTwo.ItemCount);
    }

    [Fact]
    public async Task Order_history_clamps_out_of_range_paging()
    {
        var customerId = await SeedCustomerWithOrdersAsync(orderCount: 1, quantityPerOrder: 1);
        using var client = CreateClientFor(customerId);

        var body = await client.GetFromJsonAsync<OrderListResponse>("/api/me/orders?page=0&pageSize=1000");

        Assert.NotNull(body);
        Assert.Equal(1, body.Page);
        Assert.Equal(50, body.PageSize);
        Assert.Equal(1, body.TotalCount);
    }

    [Fact]
    public async Task Restaurant_list_pages_in_the_database_and_reports_the_real_total()
    {
        using var client = _factory.CreateClient();

        var firstPage = await client.GetFromJsonAsync<Talabat.Customer.API.Contracts.Catalog.RestaurantListResponse>(
            "/api/catalog/restaurants?page=1&pageSize=1");
        var secondPage = await client.GetFromJsonAsync<Talabat.Customer.API.Contracts.Catalog.RestaurantListResponse>(
            "/api/catalog/restaurants?page=2&pageSize=1");

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Single(firstPage.Items);
        Assert.Single(secondPage.Items);
        Assert.True(firstPage.TotalCount >= 2, $"Expected at least 2 active restaurants, got {firstPage.TotalCount}");
        Assert.Equal(firstPage.TotalCount, secondPage.TotalCount);
        Assert.NotEqual(firstPage.Items.Single().Id, secondPage.Items.Single().Id);
    }

    private HttpClient CreateClientFor(int customerId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, customerId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");
        return client;
    }

    private async Task<int> SeedCustomerWithOrdersAsync(int orderCount, int quantityPerOrder)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<TalabatDbContext>();

        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Register($"history-{suffix}", $"history-{suffix}@test.com", "History Customer");
        customer.InitializeCustomerProfile("History Customer", 30, null);
        var created = await userManager.CreateAsync(customer, "Password1!");
        Assert.True(created.Succeeded);

        var product = db.Set<Talabat.Domain.Aggregates.Catalog.Product>().First();
        for (var i = 0; i < orderCount; i++)
        {
            db.Orders.Add(Order.CreateFromCheckout(
                customer.Id,
                product.RestaurantId,
                [new CheckoutItemSnapshot(product.Id, product.Name, new Money(10m), quantityPerOrder)],
                new DeliveryAddressSnapshot("1 History Street", "Cairo", "1"),
                DateTime.UtcNow.AddMinutes(i)));
        }

        await db.SaveChangesAsync();
        return customer.Id;
    }
}
