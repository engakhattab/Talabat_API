using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Talabat.Domain.Aggregates.Users;
using Xunit;

namespace Talabat.Delivery.API.Tests;

public sealed class RealTokenPipelineTests : IClassFixture<RealTokenPipelineTests.Factory>
{
    private readonly HttpClient _client;
    private readonly int _agentId;

    private static readonly SymmetricSecurityKey TestSigningKey = new(
        Encoding.UTF8.GetBytes("Talabat-Test-Secret-Key-For-Real-Pipeline-Tests-2024!"));

    public RealTokenPipelineTests(Factory factory)
    {
        _client = factory.CreateClient();
        _agentId = factory.SeededAgentId;
    }

    [Fact]
    public async Task ValidToken_Returns200()
    {
        var token = MintToken(_agentId, "DeliveryAgent", "delivery.api", "talabat.delivery.api");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WrongAudience_Returns401()
    {
        var token = MintToken(_agentId, "DeliveryAgent", "delivery.api", "talabat.customer.api");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingScope_Returns403()
    {
        var token = MintToken(_agentId, "DeliveryAgent", "customer.api", "talabat.delivery.api");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/agent/deliveries/pending");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WrongRole_Returns403()
    {
        var token = MintToken(_agentId, "Customer", "delivery.api", "talabat.delivery.api");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/agent/deliveries/pending");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var token = MintToken(_agentId, "DeliveryAgent", "delivery.api", "talabat.delivery.api", expired: true);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NoAuthorizationHeader_Returns401()
    {
        var response = await _client.GetAsync("/api/agent/deliveries/active");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string MintToken(
        int subjectId,
        string role,
        string scope,
        string audience,
        bool expired = false)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new("role", role),
            new("scope", scope),
            new("delivery_agent_id", subjectId.ToString()),
        };

        var expires = expired
            ? DateTime.UtcNow.AddHours(-1)
            : DateTime.UtcNow.AddHours(1);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = "https://localhost:7237",
            Audience = audience,
            SigningCredentials = new SigningCredentials(
                TestSigningKey, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(descriptor);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private string? _connectionString;

        public int SeededAgentId { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var sp = services.BuildServiceProvider();
                var config = sp.GetRequiredService<IConfiguration>();
                var originalConnectionString = config.GetConnectionString("TalabatDb");

                var connectionBuilder = new SqlConnectionStringBuilder(originalConnectionString)
                {
                    InitialCatalog = $"TalabatTest_DeliveryJWT_{Guid.NewGuid():N}"
                };
                _connectionString = connectionBuilder.ConnectionString;

                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<global::Talabat.Infrastructure.Persistence.TalabatDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<global::Talabat.Infrastructure.Persistence.TalabatDbContext>((serviceProvider, options) =>
                {
                    options.UseSqlServer(_connectionString);
                    var interceptor = serviceProvider.GetService<global::Talabat.Infrastructure.Persistence.Auditing.AuditableEntitySaveChangesInterceptor>();
                    if (interceptor != null)
                    {
                        options.AddInterceptors(interceptor);
                    }
                });

                services.AddIdentityCore<global::Talabat.Domain.Aggregates.Users.User>()
                    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<int>>()
                    .AddEntityFrameworkStores<global::Talabat.Infrastructure.Persistence.TalabatDbContext>();

                // Keep the real AddJwtBearer — override only the signing key
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    o.Authority = null!;
                    o.MetadataAddress = null!;
                    o.ConfigurationManager = null;
                    o.RequireHttpsMetadata = false;
                    o.TokenValidationParameters.IssuerSigningKey = TestSigningKey;
                    o.TokenValidationParameters.ValidIssuer = "https://localhost:7237";
                    o.TokenValidationParameters.ValidAudience = "talabat.delivery.api";
                });
            });

            builder.UseEnvironment("Development");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<global::Talabat.Infrastructure.Persistence.TalabatDbContext>();
                db.Database.Migrate();

                global::Talabat.Infrastructure.Identity.IdentityDataSeeder.SeedRolesAsync(services).GetAwaiter().GetResult();

                var userManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
                var agent = User.Register("jwttestagent", "jwtagent@test.com", "JWT Test Agent");
                userManager.CreateAsync(agent, "Password1!").GetAwaiter().GetResult();
                userManager.AddToRoleAsync(agent, "DeliveryAgent").GetAwaiter().GetResult();
                agent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
                agent.ApproveDeliveryAgentApplication();
                userManager.UpdateAsync(agent).GetAwaiter().GetResult();
                SeededAgentId = agent.Id;
            }

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _connectionString != null)
            {
                try
                {
                    var builder = new SqlConnectionStringBuilder(_connectionString)
                    {
                        InitialCatalog = "master"
                    };

                    using var connection = new SqlConnection(builder.ConnectionString);
                    connection.Open();

                    using var command = connection.CreateCommand();
                    var dbName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
                    command.CommandText = $@"
                        IF DB_ID(N'{dbName}') IS NOT NULL
                        BEGIN
                            ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE [{dbName}];
                        END";
                    command.ExecuteNonQuery();
                }
                catch
                {
                }
            }
            base.Dispose(disposing);
        }
    }
}
