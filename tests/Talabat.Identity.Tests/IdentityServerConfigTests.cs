using System.Net;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Domain.Aggregates.Users;
using Talabat.Identity.Tests.Infrastructure;
using Xunit;

namespace Talabat.Identity.Tests;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class IdentityServerConfigTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;

    public IdentityServerConfigTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Customer_client_has_postman_redirect_uri()
    {
        var client = IdentityServerConfig.Clients
            .Single(c => c.ClientId == "talabat-customer-spa");

        Assert.Contains(
            "https://oauth.pstmn.io/v1/browser-callback",
            client.RedirectUris);
    }

    [Fact]
    public void Delivery_client_has_postman_redirect_uri()
    {
        var client = IdentityServerConfig.Clients
            .Single(c => c.ClientId == "talabat-delivery-spa");

        Assert.Contains(
            "https://oauth.pstmn.io/v1/browser-callback",
            client.RedirectUris);
    }

    [Fact]
    public void Customer_client_has_update_access_token_claims_on_refresh()
    {
        var client = IdentityServerConfig.Clients
            .Single(c => c.ClientId == "talabat-customer-spa");

        Assert.True(client.UpdateAccessTokenClaimsOnRefresh);
    }

    [Fact]
    public void Delivery_client_has_update_access_token_claims_on_refresh()
    {
        var client = IdentityServerConfig.Clients
            .Single(c => c.ClientId == "talabat-delivery-spa");

        Assert.True(client.UpdateAccessTokenClaimsOnRefresh);
    }

    [Fact]
    public void Both_clients_have_900_second_access_token_lifetime()
    {
        foreach (var client in IdentityServerConfig.Clients)
        {
            Assert.Equal(900, client.AccessTokenLifetime);
        }
    }

    [Fact]
    public void Both_clients_use_sliding_refresh_token_expiration()
    {
        foreach (var client in IdentityServerConfig.Clients)
        {
            Assert.Equal(TokenExpiration.Sliding, client.RefreshTokenExpiration);
        }
    }

    [Fact]
    public void Api_resources_have_no_duplicate_role_claims()
    {
        foreach (var resource in IdentityServerConfig.ApiResources)
        {
            var roleClaims = resource.UserClaims
                .Where(c => c == JwtClaimTypes.Role || c == "role")
                .ToList();

            Assert.Single(roleClaims);
        }
    }
}
