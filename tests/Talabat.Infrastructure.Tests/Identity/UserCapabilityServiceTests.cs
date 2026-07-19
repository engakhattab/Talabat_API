using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Users;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;
using Talabat.Infrastructure.Tests.Persistence;

namespace Talabat.Infrastructure.Tests.Identity;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class UserCapabilityServiceTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;
    private TestDatabase? _database;

    public UserCapabilityServiceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _database = await _fixture.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task RegisterCustomerAsync_ReturnsPositiveId_WithCustomerFlagAndRole()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var result = await capabilityService.RegisterCustomerAsync(
            "newcustomer@test.com", "P@ssw0rd123!", "New Customer", 25, "+201000000000");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value > 0);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(result.Value.ToString());
        Assert.NotNull(user);
        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.Equal("New Customer", user.FullName);
        Assert.Equal(25, user.Age);
        Assert.Equal("+201000000000", user.PhoneNumber);
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
    }

    [Fact]
    public async Task RegisterCustomerAsync_DuplicateEmail_ReturnsFailureAndRollsBack()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var first = await capabilityService.RegisterCustomerAsync(
            "dup@test.com", "P@ssw0rd123!", "Customer One", 30, null);
        Assert.True(first.IsSuccess);

        var second = await capabilityService.RegisterCustomerAsync(
            "dup@test.com", "P@ssw0rd123!", "Customer Two", 25, null);

        Assert.True(second.IsFailure);
        Assert.Equal(ApplicationErrorCodes.IdentityOperationFailed, second.Error?.Code);
        Assert.Equal(ApplicationErrorCategory.Validation, second.Error?.Category);
    }

    [Fact]
    public async Task GrantCustomerCapabilityAsync_ExistingDeliveryAgent_PreservesBothCapabilitiesAndRoles()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var agentResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "agent-customer@test.com", "P@ssw0rd123!", "Agent Customer", VehicleType.Motorcycle, "+201111111111");
        Assert.True(agentResult.IsSuccess);

        var approveResult = await capabilityService.ApproveDeliveryAgentAsync(agentResult.Value);
        Assert.True(approveResult.IsSuccess);

        var customerResult = await capabilityService.GrantCustomerCapabilityAsync(
            agentResult.Value, "Agent Customer", 28, "+202222222222");
        Assert.True(customerResult.IsSuccess);
        Assert.Equal(agentResult.Value, customerResult.Value);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.Id == agentResult.Value);
        Assert.NotNull(user);
        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
        Assert.Equal(VehicleType.Motorcycle, user.VehicleType);
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
        Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task GrantCustomerCapabilityAsync_ExistingCustomer_ReturnsProfileAlreadyExists()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var registerResult = await capabilityService.RegisterCustomerAsync(
            "existing@test.com", "P@ssw0rd123!", "Existing Customer", 30, null);
        Assert.True(registerResult.IsSuccess);

        var result = await capabilityService.GrantCustomerCapabilityAsync(
            registerResult.Value, "Existing Customer", 30, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.ProfileAlreadyExists, result.Error?.Code);
        Assert.Equal(ApplicationErrorCategory.Conflict, result.Error?.Category);
    }

    [Fact]
    public async Task GrantCustomerCapabilityAsync_MissingUser_ReturnsUserNotFound()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var result = await capabilityService.GrantCustomerCapabilityAsync(
            999999, "Ghost User", 25, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
        Assert.Equal(ApplicationErrorCategory.NotFound, result.Error?.Category);
    }

    [Fact]
    public async Task RegisterCustomerAsync_MissingRoleDefinition_ReturnsIdentityOperationFailed()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        var customerRole = await roleManager.FindByNameAsync("Customer");
        if (customerRole is not null)
        {
            await roleManager.DeleteAsync(customerRole);
        }

        var result = await capabilityService.RegisterCustomerAsync(
            "norole@test.com", "P@ssw0rd123!", "No Role User", 30, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.IdentityOperationFailed, result.Error?.Code);
        Assert.Equal(ApplicationErrorCategory.Conflict, result.Error?.Category);
    }

    [Fact]
    public async Task RegisterDeliveryAgentApplicantAsync_PersistsVehiclePhonePendingApproval_NoFlagNoRoleNoStatus()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var result = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "applicant@test.com", "P@ssw0rd123!", "Applicant Agent", VehicleType.Car, "+201555555555");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value > 0);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(result.Value.ToString());
        Assert.NotNull(user);
        Assert.Equal(VehicleType.Car, user.VehicleType);
        Assert.Equal("+201555555555", user.PhoneNumber);
        Assert.Equal(AgentApprovalStatus.PendingApproval, user.AgentApprovalStatus);
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.DeliveryAgentStatus);
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task RegisterDeliveryAgentApplicantAsync_OmitsPhoneNumber_PersistsNullPhone()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var result = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "nophone@test.com", "P@ssw0rd123!", "No Phone Agent", VehicleType.Bike, null);

        Assert.True(result.IsSuccess);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(result.Value.ToString());
        Assert.NotNull(user);
        Assert.Null(user.PhoneNumber);
        Assert.Equal(VehicleType.Bike, user.VehicleType);
    }

    [Fact]
    public async Task ApproveDeliveryAgentAsync_GrantsFlagRoleOffline()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var regResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "approve@test.com", "P@ssw0rd123!", "Approve Agent", VehicleType.Motorcycle, null);
        Assert.True(regResult.IsSuccess);

        var approveResult = await capabilityService.ApproveDeliveryAgentAsync(regResult.Value);

        Assert.True(approveResult.IsSuccess);
        Assert.Equal(regResult.Value, approveResult.Value);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(regResult.Value.ToString());
        Assert.NotNull(user);
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
        Assert.Equal(AgentApprovalStatus.Approved, user.AgentApprovalStatus);
        Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task RejectDeliveryAgentAsync_SetsRejected_NoFlagNoRole()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var regResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "reject@test.com", "P@ssw0rd123!", "Reject Agent", VehicleType.Bike, null);
        Assert.True(regResult.IsSuccess);

        var rejectResult = await capabilityService.RejectDeliveryAgentAsync(regResult.Value);

        Assert.True(rejectResult.IsSuccess);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(regResult.Value.ToString());
        Assert.NotNull(user);
        Assert.Equal(AgentApprovalStatus.Rejected, user.AgentApprovalStatus);
        Assert.False(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.Null(user.DeliveryAgentStatus);
        Assert.False(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
    }

    [Fact]
    public async Task ApproveDeliveryAgentAsync_NonPendingUser_ReturnsConflict()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var regResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "nonpending@test.com", "P@ssw0rd123!", "Non Pending", VehicleType.Car, null);
        Assert.True(regResult.IsSuccess);
        await capabilityService.RejectDeliveryAgentAsync(regResult.Value);

        var result = await capabilityService.ApproveDeliveryAgentAsync(regResult.Value);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ApproveDeliveryAgentAsync_MissingUser_ReturnsUserNotFound()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var result = await capabilityService.ApproveDeliveryAgentAsync(999999);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task RejectDeliveryAgentAsync_MissingUser_ReturnsUserNotFound()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var result = await capabilityService.RejectDeliveryAgentAsync(999999);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task RegisterDeliveryAgentApplicantAsync_DuplicateEmail_ReturnsFailure()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var first = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "dup-agent@test.com", "P@ssw0rd123!", "First Agent", VehicleType.Car, null);
        Assert.True(first.IsSuccess);

        var second = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "dup-agent@test.com", "P@ssw0rd123!", "Second Agent", VehicleType.Bike, null);

        Assert.True(second.IsFailure);
        Assert.Equal(ApplicationErrorCodes.IdentityOperationFailed, second.Error?.Code);
    }

    [Fact]
    public async Task GrantCustomerCapabilityAsync_ExistingApprovedAgent_PreservesAgentCapabilitiesAndProfileOnSameUser()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var agentResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "agent-to-customer@test.com", "P@ssw0rd123!", "Dual User", VehicleType.Motorcycle, "+201777777777");
        Assert.True(agentResult.IsSuccess);
        await capabilityService.ApproveDeliveryAgentAsync(agentResult.Value);

        var customerResult = await capabilityService.GrantCustomerCapabilityAsync(
            agentResult.Value, "Dual User Customer", 30, "+201888888888");
        Assert.True(customerResult.IsSuccess);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(agentResult.Value.ToString());
        Assert.NotNull(user);
        Assert.True(user.UserType.HasFlag(UserType.DeliveryAgent));
        Assert.True(user.UserType.HasFlag(UserType.Customer));
        Assert.Equal(DeliveryAgentStatus.Offline, user.DeliveryAgentStatus);
        Assert.Equal(VehicleType.Motorcycle, user.VehicleType);
        Assert.Equal("Dual User Customer", user.FullName);
        Assert.Equal(30, user.Age);
        Assert.True(await userManager.IsInRoleAsync(user, "DeliveryAgent"));
        Assert.True(await userManager.IsInRoleAsync(user, "Customer"));
    }

    [Fact]
    public async Task ApproveDeliveryAgentAsync_MissingRoleDefinition_ReturnsIdentityOperationFailed()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        var agentRole = await roleManager.FindByNameAsync("DeliveryAgent");
        if (agentRole is not null)
        {
            await roleManager.DeleteAsync(agentRole);
        }

        var regResult = await capabilityService.RegisterDeliveryAgentApplicantAsync(
            "norole-agent@test.com", "P@ssw0rd123!", "No Role Agent", VehicleType.Car, null);
        Assert.True(regResult.IsSuccess);

        var result = await capabilityService.ApproveDeliveryAgentAsync(regResult.Value);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.IdentityOperationFailed, result.Error?.Code);
    }

    [Fact]
    public async Task DeactivateUserAsync_SetsIsActiveFalse()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var regResult = await capabilityService.RegisterCustomerAsync(
            "deactivate@test.com", "P@ssw0rd123!", "Deactivate Me", 30, null);
        Assert.True(regResult.IsSuccess);

        var result = await capabilityService.DeactivateUserAsync(regResult.Value);

        Assert.True(result.IsSuccess);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(regResult.Value.ToString());
        Assert.NotNull(user);
        Assert.False(user!.IsActive);
    }

    [Fact]
    public async Task DeactivateUserAsync_Idempotent()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var regResult = await capabilityService.RegisterCustomerAsync(
            "idempotent@test.com", "P@ssw0rd123!", "Idempotent Deactivate", 25, null);
        Assert.True(regResult.IsSuccess);

        var first = await capabilityService.DeactivateUserAsync(regResult.Value);
        Assert.True(first.IsSuccess);

        var second = await capabilityService.DeactivateUserAsync(regResult.Value);
        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task DeactivateUserAsync_MissingUser_ReturnsUserNotFound()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var result = await capabilityService.DeactivateUserAsync(999999);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorCodes.UserNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task DeactivateUserAsync_PreservesCapabilitiesAndSecurityStampChanges()
    {
        await using var provider = await CreateServiceProviderAsync();
        var capabilityService = provider.GetRequiredService<IUserCapabilityService>();

        var regResult = await capabilityService.RegisterCustomerAsync(
            "preserve@test.com", "P@ssw0rd123!", "Preserve Caps", 30, "+201000000000");
        Assert.True(regResult.IsSuccess);

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var userBefore = await userManager.FindByIdAsync(regResult.Value.ToString());
        Assert.NotNull(userBefore);
        var oldStamp = userBefore!.SecurityStamp;

        await capabilityService.DeactivateUserAsync(regResult.Value);

        var userAfter = await userManager.FindByIdAsync(regResult.Value.ToString());
        Assert.NotNull(userAfter);
        Assert.False(userAfter!.IsActive);
        Assert.True(userAfter.UserType.HasFlag(UserType.Customer));
        Assert.Equal("Preserve Caps", userAfter.FullName);
        Assert.NotEqual(oldStamp, userAfter.SecurityStamp);
    }

    private async Task<ServiceProvider> CreateServiceProviderAsync()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TalabatDbContext>(options =>
            options.UseSqlServer(_database!.ConnectionString));

        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
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
            {
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            }
        }

        return provider;
    }
}
