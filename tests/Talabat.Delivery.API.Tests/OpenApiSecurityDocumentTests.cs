using System.Text.Json;
using Talabat.Delivery.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Delivery.API.Tests;

public sealed class OpenApiSecurityDocumentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiSecurityDocumentTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiDocument_DeclaresOAuth2SchemeWithPkceFlow()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        using var doc = JsonDocument.Parse(json);

        var scheme = doc.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("oauth2");

        Assert.Equal("oauth2", scheme.GetProperty("type").GetString());

        var flow = scheme.GetProperty("flows").GetProperty("authorizationCode");
        Assert.Equal("https://localhost:7237/connect/authorize", flow.GetProperty("authorizationUrl").GetString());
        Assert.Equal("https://localhost:7237/connect/token",     flow.GetProperty("tokenUrl").GetString());
        Assert.True(flow.GetProperty("scopes").TryGetProperty("delivery.api", out _));
    }

    [Fact]
    public async Task ProtectedOperations_DeclareSecurityRequirement()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        using var doc = JsonDocument.Parse(json);

        var operation = doc.RootElement
            .GetProperty("paths")
            .GetProperty("/api/agent/deliveries/pending")
            .GetProperty("get");

        Assert.True(operation.TryGetProperty("security", out var security));
        Assert.True(security.GetArrayLength() > 0);
    }
}
