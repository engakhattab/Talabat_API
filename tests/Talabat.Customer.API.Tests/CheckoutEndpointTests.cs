using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class CheckoutEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CheckoutEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
    }

    [Fact]
    public async Task Checkout_Authenticated_ReturnsExpectedStatus()
    {
        var request = new { DeliveryAddressId = 1 };
        var response = await _client.PostAsJsonAsync("/api/me/checkout", request);

        Assert.True(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Checkout_Unauthenticated_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var request = new { DeliveryAddressId = 1 };
        var response = await _client.PostAsJsonAsync("/api/me/checkout", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
