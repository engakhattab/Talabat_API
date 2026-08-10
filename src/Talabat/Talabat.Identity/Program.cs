using Microsoft.AspNetCore.Identity;
using Talabat.Domain.Aggregates.Users;
using Talabat.Identity;
using Talabat.Infrastructure;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;
using Talabat.Infrastructure.Development.E2E;

var e2eCommand = E2EProvisioningCommand.Parse(args);
if (e2eCommand.IsRequested && e2eCommand.Error is not null)
{
    Console.Error.WriteLine($"E2E provisioning failed [invalid_command]: {e2eCommand.Error}");
    Environment.ExitCode = 2;
    return;
}

var builder = WebApplication.CreateBuilder(args);

if (e2eCommand.IsRequested)
{
    builder.Logging.ClearProviders();
}

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
        policy.WithOrigins(
                "http://localhost:4200",
                "http://localhost:4300",
                "https://localhost:7056",
                "http://localhost:5213",
                "https://localhost:7225",
                "http://localhost:5092")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var identityServerBuilder = builder.Services.AddIdentityServer(options =>
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
    .AddProfileService<TalabatProfileService>();

if (builder.Environment.IsDevelopment())
{
    identityServerBuilder.AddDeveloperSigningCredential();
}
else
{
    // TODO: Configure a production signing credential (RSA key pair or X.509 certificate)
    // identityServerBuilder.AddSigningCredential(new X509Certificate2("path-to-cert.pfx", "password"));
}

var app = builder.Build();

if (e2eCommand.Operation is not null)
{
    Environment.ExitCode = await E2EProvisioningCommand.RunAsync(
        app.Services,
        e2eCommand.Operation.Value);
    return;
}

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
