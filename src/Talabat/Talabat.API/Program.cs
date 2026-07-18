using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Talabat.Application;
using Talabat.Application.Abstractions;
using Talabat.Customer.API.Auth;
using Talabat.Customer.API.Middleware;
using Talabat.Infrastructure;
using Talabat.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ProfileEnforcementFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
    options.Audience = "talabat.customer-api";
    options.RequireHttpsMetadata = builder.Environment.IsDevelopment() is false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = identityAuthority,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.SetIsOriginAllowed(origin =>
        {
            try
            {
                return new Uri(origin).Host == "localhost";
            }
            catch
            {
                return false;
            }
        })
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<TalabatDbContext>();

builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Talabat Customer API v1");
        options.RoutePrefix = "swagger";
    });
    app.UseCors("Development");
}

app.UseExceptionHandler(_ => {});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
