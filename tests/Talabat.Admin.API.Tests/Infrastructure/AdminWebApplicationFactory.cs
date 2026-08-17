using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Admin.API.Tests.Infrastructure;

public sealed class AdminWebApplicationFactory : WebApplicationFactory<Program>
{
    private string? _connectionString;
    public int AdminUserId { get; private set; }
    public int NonAdminUserId { get; private set; }
    public int PendingApplicantUserId { get; private set; }
    public int PendingApplicantForRejectionUserId { get; private set; }
    public int PendingApplicantForListUserId { get; private set; }
    public int RejectedApplicantUserId { get; private set; }
    public int ApprovedDeliveryAgentUserId { get; private set; }
    public int BusyDeliveryAgentUserId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var provider = services.BuildServiceProvider();
            var configuration = provider.GetRequiredService<IConfiguration>();
            var originalConnection = configuration.GetConnectionString("TalabatDb");
            _connectionString = new SqlConnectionStringBuilder(originalConnection)
            {
                InitialCatalog = $"TalabatTest_Admin_{Guid.NewGuid():N}"
            }.ConnectionString;
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<TalabatDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<TalabatDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(_connectionString);
                var interceptor = serviceProvider.GetService<Talabat.Infrastructure.Persistence.Auditing.AuditableEntitySaveChangesInterceptor>();
                if (interceptor is not null) options.AddInterceptors(interceptor);
            });
            services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<TalabatDbContext>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, _ => { });
        });
        builder.UseEnvironment("Development");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<TalabatDbContext>();
        db.Database.Migrate();
        IdentityDataSeeder.SeedRolesAsync(services).GetAwaiter().GetResult();
        var users = services.GetRequiredService<UserManager<User>>();

        var admin = User.Register("admin@test.com", "admin@test.com", "Admin User");
        users.CreateAsync(admin, "Password1!").GetAwaiter().GetResult();
        db.Entry(admin).Property(nameof(User.UserType)).CurrentValue = UserType.Admin;
        db.SaveChanges();
        users.AddToRoleAsync(admin, "Admin").GetAwaiter().GetResult();
        AdminUserId = admin.Id;

        var nonAdmin = User.Register("customer@test.com", "customer@test.com", "Customer User");
        users.CreateAsync(nonAdmin, "Password1!").GetAwaiter().GetResult();
        NonAdminUserId = nonAdmin.Id;

        var pending = User.Register("pending@test.com", "pending@test.com", "Pending Applicant");
        pending.SetPhoneNumber("+201000000001");
        pending.SubmitDeliveryAgentApplication(VehicleType.Bike);
        users.CreateAsync(pending, "Password1!").GetAwaiter().GetResult();
        PendingApplicantUserId = pending.Id;

        var pendingForRejection = User.Register("pending-reject@test.com", "pending-reject@test.com", "Pending Rejection Applicant");
        pendingForRejection.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        users.CreateAsync(pendingForRejection, "Password1!").GetAwaiter().GetResult();
        PendingApplicantForRejectionUserId = pendingForRejection.Id;

        var pendingForList = User.Register("pending-list@test.com", "pending-list@test.com", "Pending List Applicant");
        pendingForList.SubmitDeliveryAgentApplication(VehicleType.Bike);
        users.CreateAsync(pendingForList, "Password1!").GetAwaiter().GetResult();
        PendingApplicantForListUserId = pendingForList.Id;

        var rejected = User.Register("rejected@test.com", "rejected@test.com", "Rejected Applicant");
        rejected.SubmitDeliveryAgentApplication(VehicleType.Car);
        rejected.RejectDeliveryAgentApplication();
        users.CreateAsync(rejected, "Password1!").GetAwaiter().GetResult();
        RejectedApplicantUserId = rejected.Id;

        var approvedAgent = User.Register("approved-agent@test.com", "approved-agent@test.com", "Approved Delivery Agent");
        approvedAgent.SubmitDeliveryAgentApplication(VehicleType.Motorcycle);
        approvedAgent.ApproveDeliveryAgentApplication();
        users.CreateAsync(approvedAgent, "Password1!").GetAwaiter().GetResult();
        users.AddToRoleAsync(approvedAgent, "DeliveryAgent").GetAwaiter().GetResult();
        ApprovedDeliveryAgentUserId = approvedAgent.Id;

        var busyAgent = User.Register("busy-agent@test.com", "busy-agent@test.com", "Busy Delivery Agent");
        busyAgent.SubmitDeliveryAgentApplication(VehicleType.Car);
        busyAgent.ApproveDeliveryAgentApplication();
        users.CreateAsync(busyAgent, "Password1!").GetAwaiter().GetResult();
        users.AddToRoleAsync(busyAgent, "DeliveryAgent").GetAwaiter().GetResult();
        db.Entry(busyAgent).Property(nameof(User.DeliveryAgentStatus)).CurrentValue = DeliveryAgentStatus.Busy;
        db.SaveChanges();
        BusyDeliveryAgentUserId = busyAgent.Id;
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _connectionString is not null)
        {
            try
            {
                var databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
                var masterConnection = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
                using var connection = new SqlConnection(masterConnection.ConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
                command.ExecuteNonQuery();
            }
            catch { }
        }
        base.Dispose(disposing);
    }
}
