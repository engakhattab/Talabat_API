using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Talabat.Application;
using Talabat.Application.Abstractions;
using Talabat.Delivery.API.Auth;
using Talabat.Infrastructure;
using Talabat.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddUnifiedUserIdentityCore();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var identityAuthority = builder.Configuration["Identity:Authority"]
        ?? "https://localhost:7237";

    options.Authority = identityAuthority;
    options.Audience = "talabat.deliveryagent-api";
    options.RequireHttpsMetadata = builder.Environment.IsDevelopment() is false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = identityAuthority,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RoleClaimType = "role"
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
