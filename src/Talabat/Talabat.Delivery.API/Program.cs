using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Talabat.Application;
using Talabat.Application.Abstractions;
using Talabat.Delivery.API.Auth;
using Talabat.Infrastructure;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
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
                    TokenUrl         = new Uri("https://localhost:7237/connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"]       = "Subject identifier",
                        ["profile"]      = "Profile claims",
                        ["roles"]        = "Role claims",
                        ["delivery.api"] = "Delivery API access"
                    }
                }
            }
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var requiresAuth = metadata.OfType<IAuthorizeData>().Any()
                        && !metadata.OfType<IAllowAnonymous>().Any();

        if (requiresAuth)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("oauth2", context.Document)] =
                        new List<string> { "delivery.api" }
                }
            ];
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddUnifiedUserIdentityCore();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.DeliveryAgentAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ScopeRequirement("delivery.api"));
        policy.RequireRole("DeliveryAgent");
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;   // keep "sub", "role", "scope" verbatim from the token
    var identityAuthority = builder.Configuration["Identity:Authority"]
        ?? "https://localhost:7237";

    options.Authority = identityAuthority;
    options.Audience = "talabat.delivery.api";
    options.RequireHttpsMetadata = builder.Environment.IsDevelopment() is false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = identityAuthority,
        ValidateAudience = true,
        ValidAudience = "talabat.delivery.api",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RoleClaimType = "role",
        NameClaimType = "sub"
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaCorsPolicy", policy =>
        policy.WithOrigins("http://localhost:4300")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<TalabatDbContext>();

builder.Services.AddExceptionHandler<Talabat.Delivery.API.Middleware.DomainExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Talabat Delivery API v1");
        options.RoutePrefix = "swagger";

        options.OAuthClientId("talabat-delivery-spa");
        options.OAuthUsePkce();
        options.OAuthScopes("openid", "profile", "roles", "delivery.api");
        options.OAuthAppName("Talabat Delivery API — Swagger");
    });
}

app.UseExceptionHandler(_ => {});

app.UseHttpsRedirection();

app.UseCors("SpaCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
