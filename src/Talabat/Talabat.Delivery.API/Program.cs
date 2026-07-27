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

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT token from IdentityServer"
        };
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
