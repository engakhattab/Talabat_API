using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class AddressEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AddressEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
    }

    [Fact]
    public async Task AddAddress_Authenticated_ReturnsOkOrConflict()
    {
        var request = new
        {
            Street = "123 Main St",
            City = "Cairo",
            BuildingNumber = "42",
            Floor = (string?)null,
            MakeDefault = false
        };
        var response = await _client.PostAsJsonAsync("/api/me/addresses", request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveAddress_Authenticated_ReturnsOkOrNotFound()
    {
        var response = await _client.DeleteAsync("/api/me/addresses/1");

        Assert.True(
            response.StatusCode == HttpStatusCode.NoContent ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SetDefaultAddress_Authenticated_ReturnsOkOrNotFound()
    {
        var response = await _client.PutAsJsonAsync("/api/me/addresses/1/default", new { });

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }
}
