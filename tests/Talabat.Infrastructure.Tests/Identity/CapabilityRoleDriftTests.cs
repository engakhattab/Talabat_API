using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Domain.Aggregates.Users;
using Talabat.Application.Abstractions;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Tests.Persistence;
using Xunit;

namespace Talabat.Infrastructure.Tests.Identity;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class CapabilityRoleDriftTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public CapabilityRoleDriftTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegisterCustomer_projects_exact_role_and_flag()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = await CreateServiceProviderAsync(database.ConnectionString);

        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();
        var result = await capabilityService.RegisterCustomerAsync(
            $"drift_{Guid.NewGuid():N}@test.com", "P@ssw0rd123!", "Drift Customer", 25, null);

        Assert.True(result.IsSuccess);

        await using var freshProvider = await CreateServiceProviderAsync(database.ConnectionString);
        var userManager = freshProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(result.Value.ToString());

        Assert.NotNull(user);
        Assert.True(user!.UserType.HasFlag(UserType.Customer));
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task RegisterApplicant_projects_no_flag_no_role()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = await CreateServiceProviderAsync(database.ConnectionString);

        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();
        var result = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            $"drift_{Guid.NewGuid():N}@test.com", "P@ssw0rd123!", "Drift Agent", VehicleType.Car, null);

        Assert.True(result.IsSuccess);

        await using var freshProvider = await CreateServiceProviderAsync(database.ConnectionString);
        var userManager = freshProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(result.Value.ToString());

        Assert.NotNull(user);
        Assert.False(user!.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.False(user.UserType.HasFlag(UserType.Customer));
        Assert.Null(user.DeliveryAgentStatus);
        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
        Assert.False(await userManager.IsInRoleAsync(user, "Customer"));
    }

    [Fact]
    public async Task ApproveAgent_projects_flag_role_and_offline()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = await CreateServiceProviderAsync(database.ConnectionString);

        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();
        var registerResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            $"drift_{Guid.NewGuid():N}@test.com", "P@ssw0rd123!", "Drift Agent", VehicleType.Motorcycle, null);
        Assert.True(registerResult.IsSuccess);

        var approveResult = await capabilityService.ApproveDeliveryAgentAsync(registerResult.Value);
        Assert.True(approveResult.IsSuccess);

        await using var freshProvider = await CreateServiceProviderAsync(database.ConnectionString);
        var userManager = freshProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(registerResult.Value.ToString());

        Assert.NotNull(user);
        Assert.True(user!.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.False(user.UserType.HasFlag(UserType.Customer));
        Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
        Assert.False(await userManager.IsInRoleAsync(user, "Customer"));
        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public async Task GrantCustomer_to_agent_preserves_both_capabilities()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = await CreateServiceProviderAsync(database.ConnectionString);

        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();
        var regResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            $"drift_{Guid.NewGuid():N}@test.com", "P@ssw0rd123!", "Dual Agent", VehicleType.Bike, null);
        Assert.True(regResult.IsSuccess);
        await capabilityService.ApproveDeliveryAgentAsync(regResult.Value);

        var grantResult = await capabilityService.GrantCustomerCapabilityAsync(
            regResult.Value, "Dual Agent", 30, null);
        Assert.True(grantResult.IsSuccess);

        await using var freshProvider = await CreateServiceProviderAsync(database.ConnectionString);
        var userManager = freshProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(regResult.Value.ToString());

        Assert.NotNull(user);
        Assert.True(user!.UserType.HasFlag(UserType.Customer));
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
        Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
    }

    [Fact]
    public async Task RejectAgent_projects_rejected_no_flag_no_role()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = await CreateServiceProviderAsync(database.ConnectionString);

        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();
        var regResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            $"drift_{Guid.NewGuid():N}@test.com", "P@ssw0rd123!", "Rejected Agent", VehicleType.Car, null);
        Assert.True(regResult.IsSuccess);

        var rejectResult = await capabilityService.RejectDeliveryAgentAsync(regResult.Value);
        Assert.True(rejectResult.IsSuccess);

        await using var freshProvider = await CreateServiceProviderAsync(database.ConnectionString);
        var userManager = freshProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(regResult.Value.ToString());

        Assert.NotNull(user);
        Assert.False(user!.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.False(user.UserType.HasFlag(UserType.Customer));
        Assert.Null(user.DeliveryAgentStatus);
        Assert.Equal(AgentApprovalStatus.Rejected, user.AgentApprovalStatus);
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task Deactivate_preserves_capabilities_and_roles()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = await CreateServiceProviderAsync(database.ConnectionString);

        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();
        var regResult = await capabilityService.RegisterCustomerAsync(
            $"drift_{Guid.NewGuid():N}@test.com", "P@ssw0rd123!", "Deactivate Customer", 25, null);
        Assert.True(regResult.IsSuccess);

        var deactivateResult = await capabilityService.DeactivateUserAsync(regResult.Value);
        Assert.True(deactivateResult.IsSuccess);

        await using var freshProvider = await CreateServiceProviderAsync(database.ConnectionString);
        var userManager = freshProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(regResult.Value.ToString());

        Assert.NotNull(user);
        Assert.False(user!.IsActive);
        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
    }

    [Fact]
    public async Task Missing_role_definition_during_approval_rolls_back_flags()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var services = new ServiceCollection();
        services.AddDbContext<TalabatDbContext>(options =>
            options.UseSqlServer(database.ConnectionString));
        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 0;
        })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<TalabatDbContext>();
        services.AddScoped<IUserCapabilityService, UserCapabilityService>();
        var provider = services.BuildServiceProvider();

        var regResult = await capabilityService(provider).RegisterDeliveryAgentApplicantAsync(
            $"drift_{Guid.NewGuid():N}@test.com", "P@ssw0rd123!", "Drift Agent", VehicleType.Car, null);
        Assert.True(regResult.IsSuccess);

        var userId = regResult.Value;
        using (var scope = provider.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var deliveryRole = await roleManager.FindByNameAsync("DeliveryAgent");
            if (deliveryRole is not null)
                await roleManager.DeleteAsync(deliveryRole);
        }

        var approveResult = await capabilityService(provider).ApproveDeliveryAgentAsync(userId);
        Assert.False(approveResult.IsSuccess);

        await using var freshProvider = await CreateServiceProviderAsync(database.ConnectionString);
        var userManager = freshProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());

        Assert.NotNull(user);
        Assert.False(user!.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.Null(user.DeliveryAgentStatus);
    }

    private static IUserCapabilityService capabilityService(ServiceProvider provider)
        => provider.GetRequiredService<IUserCapabilityService>();

    private static async Task<ServiceProvider> CreateServiceProviderAsync(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TalabatDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 0;
        })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<TalabatDbContext>();
        services.AddScoped<IUserCapabilityService, UserCapabilityService>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach (var roleName in new[] { "Customer", "DeliveryAgent", "Admin", "RestaurantOwner" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));
        }

        return provider;
    }
}
