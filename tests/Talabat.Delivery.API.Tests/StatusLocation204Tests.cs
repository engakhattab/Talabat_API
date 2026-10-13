using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Talabat.Delivery.API.Tests.Infrastructure;

namespace Talabat.Delivery.API.Tests;

public sealed class StatusLocation204Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public StatusLocation204Tests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-token");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, _factory.DeliveryAgentUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "DeliveryAgent");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "delivery.api");
    }

    [Fact]
    public async Task GoOnline_Returns204()
    {
        var response = await _client.PutAsync("/api/agent/status/online", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task GoOffline_Returns204()
    {
        var response = await _client.PutAsync("/api/agent/status/offline", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task UpdateLocation_Returns204()
    {
        await _client.PutAsync("/api/agent/status/online", null);

        var response = await _client.PutAsJsonAsync("/api/agent/location", new
        {
            Latitude = 30.0m,
            Longitude = 31.0m
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task OpenApiDocument_Declares204_ForStatusOperations()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var statusPaths = new[] { "/api/agent/status/online", "/api/agent/status/offline" };
        foreach (var path in statusPaths)
        {
            var putNode = paths.GetProperty(path).GetProperty("put");
            var responses = putNode.GetProperty("responses");

            Assert.True(responses.TryGetProperty("204", out var noContent),
                $"{path} should declare 204");

            if (noContent.TryGetProperty("content", out var content))
            {
                foreach (var mediaType in content.EnumerateObject())
                {
                    Assert.False(mediaType.Value.TryGetProperty("schema", out _),
                        $"{path} should not have a schema for 204");
                }
            }
        }
    }

    [Fact]
    public async Task OpenApiDocument_DeclaresCurrentStatusContract()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        var doc = JsonDocument.Parse(json);
        var getNode = doc.RootElement
            .GetProperty("paths")
            .GetProperty("/api/agent/status")
            .GetProperty("get");

        Assert.Equal("GetCurrentDeliveryAgentStatus", getNode.GetProperty("operationId").GetString());
        Assert.True(getNode.GetProperty("responses").TryGetProperty("200", out var success));
        Assert.Contains("DeliveryAgentStatusResponse", success.GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task OpenApiDocument_Declares204_ForLocationOperation()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var path = "/api/agent/location";
        var putNode = paths.GetProperty(path).GetProperty("put");
        var responses = putNode.GetProperty("responses");

        Assert.True(responses.TryGetProperty("204", out var noContent),
            $"{path} should declare 204");

        if (noContent.TryGetProperty("content", out var content))
        {
            foreach (var mediaType in content.EnumerateObject())
            {
                Assert.False(mediaType.Value.TryGetProperty("schema", out _),
                    $"{path} should not have a schema for 204");
            }
        }
    }
}
