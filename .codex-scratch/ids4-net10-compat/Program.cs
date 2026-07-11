using IdentityServer4.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryIdentityResources(Array.Empty<IdentityResource>())
    .AddInMemoryApiScopes(Array.Empty<ApiScope>())
    .AddInMemoryClients(Array.Empty<Client>());

var app = builder.Build();

app.UseIdentityServer();

app.MapGet("/", () => "Hello World!");

app.Run();
