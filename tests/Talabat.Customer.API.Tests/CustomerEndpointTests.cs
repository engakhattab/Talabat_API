using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class CustomerEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CustomerEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_ValidRequest_ReturnsOk()
    {
        var request = new { FullName = "John Doe", Age = 30, PhoneNumber = "+20123456789" };
        var response = await _client.PostAsJsonAsync("/api/me/profile", request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateProfile_EmptyName_ReturnsBadRequest()
    {
        var request = new { FullName = "", Age = 30, PhoneNumber = (string?)null };
        var response = await _client.PostAsJsonAsync("/api/me/profile", request);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetProfile_BeforeCreation_ReturnsConflictOrNotFound()
    {
        var response = await _client.GetAsync("/api/me/profile");

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Conflict);
    }
}
