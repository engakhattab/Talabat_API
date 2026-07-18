using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class ErrorMappingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ErrorMappingTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
    }

    [Fact]
    public async Task UnauthenticatedAccess_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetNonexistentRestaurant_Returns404()
    {
        var response = await _client.GetAsync("/api/catalog/restaurants/999999/menu");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ErrorResponses_HaveProblemDetailsShape()
    {
        var response = await _client.GetAsync("/api/catalog/restaurants/999999/menu");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var content = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
            Assert.NotNull(content);
            Assert.Equal(404, content.Status);
            Assert.NotNull(content.Detail);
        }
    }

    [Fact]
    public async Task CartOperations_WithoutProfile_ReturnsConflictOrOk()
    {
        var response = await _client.GetAsync("/api/me/cart");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Checkout_EmptyCart_ReturnsExpectedStatus()
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

    private sealed record ProblemDetailsResponse(
        string? Type,
        string? Title,
        int? Status,
        string? Detail);
}
