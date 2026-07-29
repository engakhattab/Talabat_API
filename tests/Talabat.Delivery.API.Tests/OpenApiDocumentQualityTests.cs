using System.Text.Json;
using Talabat.Delivery.API.Tests.Infrastructure;
using Xunit;

namespace Talabat.Delivery.API.Tests;

public sealed class OpenApiDocumentQualityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiDocumentQualityTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AllOperations_HaveUniqueOperationId()
    {
        var doc = await FetchDocument();
        var paths = doc.RootElement.GetProperty("paths");

        var operationIds = new HashSet<string>();
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var opId = method.Value.GetProperty("operationId").GetString();
                Assert.NotNull(opId);
                Assert.NotEmpty(opId);
                Assert.DoesNotContain(opId, operationIds);
                operationIds.Add(opId);
            }
        }

        Assert.NotEmpty(operationIds);
    }

    [Fact]
    public async Task AllOperations_HaveTag()
    {
        var doc = await FetchDocument();
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var tags = method.Value.GetProperty("tags");
                Assert.True(tags.GetArrayLength() >= 1);
            }
        }
    }

    [Fact]
    public async Task AllOperations_DeclareSuccessResponse()
    {
        var doc = await FetchDocument();
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var responses = method.Value.GetProperty("responses");
                var hasSuccess = false;
                foreach (var response in responses.EnumerateObject())
                {
                    var code = response.Name;
                    if (code.StartsWith('2'))
                    {
                        hasSuccess = true;
                        if (response.Value.TryGetProperty("content", out var content))
                        {
                            foreach (var mediaType in content.EnumerateObject())
                            {
                                mediaType.Value.GetProperty("schema");
                            }
                        }
                    }
                }
                Assert.True(hasSuccess, $"Operation {method.Name} on {path.Name} has no success response");
            }
        }
    }

    [Fact]
    public async Task NoOperation_HasEmptyResponseSchema()
    {
        var doc = await FetchDocument();
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var responses = method.Value.GetProperty("responses");
                foreach (var response in responses.EnumerateObject())
                {
                    if (response.Value.TryGetProperty("content", out var content))
                    {
                        foreach (var mediaType in content.EnumerateObject())
                        {
                            var schema = mediaType.Value.GetProperty("schema");
                            if (schema.ValueKind == JsonValueKind.Object)
                            {
                                var props = schema.EnumerateObject().ToList();
                                Assert.NotEmpty(props);
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task ProtectedOperations_Declare401()
    {
        var doc = await FetchDocument();
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var hasSecurity = method.Value.TryGetProperty("security", out _);
                var responses = method.Value.GetProperty("responses");
                var has401 = responses.TryGetProperty("401", out _);

                if (hasSecurity)
                {
                    Assert.True(has401, $"Protected operation {method.Name} on {path.Name} should declare 401");
                }
            }
        }
    }

    [Fact]
    public async Task ErrorResponses_ReferenceProblemDetails()
    {
        var doc = await FetchDocument();
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var responses = method.Value.GetProperty("responses");
                foreach (var response in responses.EnumerateObject())
                {
                    var code = response.Name;
                    if (code is "400" or "403" or "404" or "409" or "422" or "500")
                    {
                        if (response.Value.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("application/json", out var mediaType))
                        {
                            var schema = mediaType.GetProperty("schema");
                            var @ref = schema.GetProperty("$ref").GetString();
                            Assert.Contains("ProblemDetails", @ref);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task EnumsAreStringValued()
    {
        var doc = await FetchDocument();
        if (!doc.RootElement.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("schemas", out var schemas))
        {
            return;
        }

        foreach (var schema in schemas.EnumerateObject())
        {
            if (schema.Value.TryGetProperty("enum", out var enumValues))
            {
                if (enumValues.GetArrayLength() > 0)
                {
                    var firstValue = enumValues[0].GetString();
                    Assert.NotNull(firstValue);
                    Assert.NotEqual("0", firstValue);
                }
            }
        }
    }

    [Fact]
    public async Task CommittedOpenApi_MatchesRuntimeDocument()
    {
        var runtimeJson = await _client.GetStringAsync("/openapi/v1.json");

        var projectDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Talabat", "Talabat.Delivery.API"));
        var committedPath = Path.Combine(projectDir, "openapi", "delivery-api.json");
        var generatedPath = Path.Combine(projectDir, "openapi", "Talabat.Delivery.API.json");

        var sourcePath = File.Exists(committedPath) ? committedPath : generatedPath;
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var sourceJson = await File.ReadAllTextAsync(sourcePath);
        var runtimeDoc = JsonDocument.Parse(runtimeJson);
        var sourceDoc = JsonDocument.Parse(sourceJson);

        var runtimePaths = runtimeDoc.RootElement.GetProperty("paths");
        var sourcePaths = sourceDoc.RootElement.GetProperty("paths");

        Assert.Equal(sourcePaths.GetRawText(), runtimePaths.GetRawText());
    }

    private async Task<JsonDocument> FetchDocument()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        return JsonDocument.Parse(json);
    }
}
