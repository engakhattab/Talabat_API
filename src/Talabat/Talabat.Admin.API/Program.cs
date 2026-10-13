using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Talabat.Admin.API.Auth;
using Talabat.Application;
using Talabat.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "OIDC authorization code flow with PKCE against Talabat.Identity.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri("https://localhost:7237/connect/authorize"),
                    TokenUrl = new Uri("https://localhost:7237/connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"] = "Subject identifier",
                        ["profile"] = "Profile claims",
                        ["roles"] = "Role claims",
                        ["admin.api"] = "Admin API access"
                    }
                }
            }
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("oauth2", context.Document)] = new List<string> { "admin.api" }
                }
            ];
        }
        return Task.CompletedTask;
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PersistedAdminAccessHandler>();
builder.Services.AddAuthorizationBuilder().AddPolicy(AuthorizationPolicies.AdminAccess, policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireRole("Admin");
    policy.AddRequirements(new ScopeRequirement("admin.api"), new AdminAccessRequirement());
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    var authority = builder.Configuration["Identity:Authority"] ?? "https://localhost:7237";
    options.Authority = authority;
    options.Audience = "talabat.admin.api";
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = authority,
        ValidateAudience = true, ValidAudience = "talabat.admin.api",
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        RoleClaimType = "role", NameClaimType = "sub"
    };
});
builder.Services.AddCors(options => options.AddPolicy("AdminSpaCorsPolicy", policy =>
    policy.WithOrigins("http://localhost:4400").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Talabat Admin API v1");
        options.RoutePrefix = "swagger";
        options.OAuthClientId("talabat-admin-spa");
        options.OAuthUsePkce();
        options.OAuthScopes("openid", "profile", "roles", "admin.api");
    });
}
app.UseExceptionHandler(_ => { });
app.UseHttpsRedirection();
app.UseCors("AdminSpaCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
