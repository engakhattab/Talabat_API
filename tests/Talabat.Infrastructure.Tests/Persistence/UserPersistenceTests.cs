using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class UserPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public UserPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task User_and_address_receive_ids_and_round_trip()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        var repository = provider.GetRequiredService<IUserRepository>();

        var user = User.Register("addresscustomer", "addresscustomer@test.com", "Address Customer");
        user.InitializeCustomerProfile("Address Customer", 29, "+202222222222");
        user.AddAddress(new Address("Street 1", "Giza", "7", "2"), makeDefault: true);

        dbContext.Users.Add(user);
        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var saved = await repository.GetByIdWithAddressesAsync(user.Id);

        Assert.True(user.Id > 0);
        Assert.True(user.Addresses.Single().Id > 0);
        Assert.NotNull(saved);
        Assert.Equal("Street 1", saved.Addresses.Single().Details.Street);
        Assert.True(saved.Addresses.Single().IsDefault);
    }

    [Fact]
    public async Task Second_default_address_for_user_is_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var user = await PersistenceTestData.AddCustomerAsync(dbContext);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO UserAddresses
                    (UserId, IsDefault, IsDeleted, Street, City, BuildingNumber, Floor)
                VALUES
                    ({user.Id}, CAST(1 AS bit), CAST(0 AS bit), N'Another', N'Cairo', N'12', NULL);
                """));

        Assert.NotNull(exception);
    }

    [Fact]
    public async Task GetAvailableAgents_ReturnsOnlyAvailableAgents_OrderedByName()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var repository = provider.GetRequiredService<IUserRepository>();
        var dbContext = provider.GetRequiredService<TalabatDbContext>();

        var availableAgent = await PersistenceTestData.AddAvailableAgentAsync(dbContext);

        var pendingAgent = User.Register("pending@test.com", "pending@test.com", "Pending Agent");
        pendingAgent.SubmitDeliveryAgentApplication(VehicleType.Bike);
        dbContext.Users.Add(pendingAgent);

        var offlineAgent = User.Register("offline@test.com", "offline@test.com", "Offline Agent");
        offlineAgent.SubmitDeliveryAgentApplication(VehicleType.Car);
        offlineAgent.ApproveDeliveryAgentApplication();
        dbContext.Users.Add(offlineAgent);

        await dbContext.SaveChangesAsync();

        var agents = await repository.GetAvailableAgentsAsync();

        var agentList = agents.ToList();
        Assert.Single(agentList);
        Assert.Equal(availableAgent.Id, agentList[0].Id);
        Assert.Equal(DeliveryAgentStatus.Available, agentList[0].DeliveryAgentStatus);
    }

    [Fact]
    public async Task GetAvailableAgents_ExcludesPendingAndRejectedApplicants()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var repository = provider.GetRequiredService<IUserRepository>();
        var dbContext = provider.GetRequiredService<TalabatDbContext>();

        var pending = User.Register("pending2@test.com", "pending2@test.com", "Z Pending");
        pending.SubmitDeliveryAgentApplication(VehicleType.Car);
        dbContext.Users.Add(pending);

        var rejected = User.Register("rejected@test.com", "rejected@test.com", "A Rejected");
        rejected.SubmitDeliveryAgentApplication(VehicleType.Bike);
        rejected.RejectDeliveryAgentApplication();
        dbContext.Users.Add(rejected);

        await dbContext.SaveChangesAsync();

        var agents = await repository.GetAvailableAgentsAsync();

        Assert.Empty(agents);
    }
}
