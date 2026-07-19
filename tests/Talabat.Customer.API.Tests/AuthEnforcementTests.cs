using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class AuthEnforcementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEnforcementTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCatalogRestaurants_Anonymous_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/catalog/restaurants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCart_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/me/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCart_Authenticated_WithoutProfile_ReturnsProfileNotCreated409()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");

        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"errorCode\":\"ProfileNotCreated\"", body);
        Assert.Contains("\"status\":409", body);
    }

    [Fact]
    public async Task HealthCheck_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task MalformedSubject_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "not-a-number");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ZeroSubject_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "0");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NegativeSubject_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "-1");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentUserId_ReturnsProfileNotCreated404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "999999");

        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"errorCode\":\"ProfileNotCreated\"", body);
    }
}
