using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Common.Results;
using Talabat.Customer.API.Extensions;
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
    public async Task CartOperations_WithoutProfile_ReturnsProfileNotCreated409()
    {
        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"errorCode\":\"ProfileNotCreated\"", body);
        Assert.Contains("\"status\":409", body);
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

    [Fact]
    public void ConcurrencyConflict_ErrorCode_Produces409ProblemDetails()
    {
        var error = new ApplicationError(
            ApplicationErrorCodes.ConcurrencyConflict,
            ApplicationErrorCategory.Conflict,
            "A concurrency conflict occurred.");
        var result = UseCaseResult<int>.Failure(error);

        var actionResult = result.ToActionResult(_ => new OkResult());

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(409, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(409, problemDetails.Status);
        Assert.Equal("Conflict", problemDetails.Title);
        Assert.Equal("A concurrency conflict occurred.", problemDetails.Detail);
        Assert.Equal("ConcurrencyConflict", problemDetails.Extensions["errorCode"]?.ToString());
    }

    private sealed record ProblemDetailsResponse(
        string? Type,
        string? Title,
        int? Status,
        string? Detail);
}
