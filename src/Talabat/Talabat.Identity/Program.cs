using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using Talabat.Domain.Aggregates.Users;
using Talabat.Identity;
using Talabat.Infrastructure;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<TalabatDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager<TalabatSignInManager>();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/error";

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/account") ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/account") ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:4300")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();
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

if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "A production signing credential must be configured. " +
        "AddDeveloperSigningCredential is only permitted in the Development environment.");
}

builder.Services.AddIdentityServer(options =>
{
    options.EmitStaticAudienceClaim = true;
    options.UserInteraction.LoginUrl = "/auth/login";
    options.UserInteraction.LogoutUrl = "/auth/logout";
    options.UserInteraction.ErrorUrl = "/auth/error";
})
    .AddInMemoryIdentityResources(IdentityServerConfig.IdentityResources)
    .AddInMemoryApiScopes(IdentityServerConfig.ApiScopes)
    .AddInMemoryApiResources(IdentityServerConfig.ApiResources)
    .AddInMemoryClients(IdentityServerConfig.Clients)
    .AddAspNetIdentity<User>()
    .AddProfileService<TalabatProfileService>()
    .AddDeveloperSigningCredential();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await IdentityDataSeeder.SeedRolesAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Talabat Identity API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("SpaCorsPolicy");
app.UseIdentityServer();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();

public partial class Program { }
