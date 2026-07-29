using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Common.Results;
using Talabat.Customer.API.Extensions;
using Talabat.Customer.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Customer.API.Tests;

public sealed class ProfileNotCreatedShapeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProfileNotCreatedShapeTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Customer");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "customer.api");
    }

    [Fact]
    public async Task ProfileNotCreatedBody_HasErrorCodeAtRoot_NoExtensionsProperty()
    {
        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("errorCode", out var errorCode), "errorCode must exist at root");
        Assert.Equal("ProfileNotCreated", errorCode.GetString());

        Assert.False(root.TryGetProperty("extensions", out _), "extensions property must not exist");
    }

    [Fact]
    public async Task GetProfile_Missing_Returns404_WithCorrectShape()
    {
        var response = await _client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"errorCode\":\"ProfileNotCreated\"", body);
        Assert.Contains("\"status\":404", body);

        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("errorCode", out _));
        Assert.False(doc.RootElement.TryGetProperty("extensions", out _));
    }

    [Fact]
    public async Task CartOperations_WithoutProfile_Returns409_WithCorrectShape()
    {
        var response = await _client.GetAsync("/api/me/cart");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"errorCode\":\"ProfileNotCreated\"", body);
        Assert.Contains("\"status\":409", body);

        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("errorCode", out _));
        Assert.False(doc.RootElement.TryGetProperty("extensions", out _));
    }

    [Fact]
    public void ProfileNotCreatedBody_HasSameTopLevelPropertySet_AsToActionResultProblemDetails()
    {
        var error = new ApplicationError(
            ApplicationErrorCodes.ConcurrencyConflict,
            ApplicationErrorCategory.Conflict,
            "A concurrency conflict occurred.");
        var result = UseCaseResult<int>.Failure(error);
        var actionResult = result.ToActionResult(_ => new OkResult());
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);

        var toActionResultProperties = problemDetails.Extensions.Keys
            .Concat(new[] { "type", "title", "status", "detail" })
            .Select(k => k.ToLowerInvariant())
            .OrderBy(k => k)
            .ToHashSet();

        var profileNotCreated = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Title = "Conflict",
            Status = 409,
            Detail = "A customer profile has not been created yet. Use POST /api/me/profile to create one.",
            Extensions = { ["errorCode"] = "ProfileNotCreated" }
        };

        var profileNotCreatedProperties = profileNotCreated.Extensions.Keys
            .Concat(new[] { "type", "title", "status", "detail" })
            .Select(k => k.ToLowerInvariant())
            .OrderBy(k => k)
            .ToHashSet();

        Assert.Equal(toActionResultProperties, profileNotCreatedProperties);
    }
}
