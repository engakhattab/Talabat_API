using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class DeliveryAgentPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public DeliveryAgentPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Delivery_agent_round_trips_and_available_query_filters_status()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var repository = provider.GetRequiredService<IDeliveryAgentRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var agent = new DeliveryAgent(
            "Available Agent",
            VehicleType.Bike,
            PersistenceTestData.Now,
            currentLocation: new GeoLocation(30.1m, 31.2m));
        agent.GoOnline();

        await repository.AddAsync(agent);
        await unitOfWork.SaveChangesAsync();

        var saved = await repository.GetByIdAsync(agent.Id);
        var available = await repository.GetAvailableAgentsAsync();

        Assert.True(agent.Id > 0);
        Assert.NotNull(saved);
        Assert.Equal(30.1m, saved.CurrentLocation?.Latitude);
        Assert.Contains(available, item => item.Id == agent.Id);
    }

    [Fact]
    public async Task Invalid_coordinates_are_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlRawAsync("""
                INSERT INTO DeliveryAgents
                    (FullName, PhoneNumber, VehicleType, Status, CurrentLatitude, CurrentLongitude, CreatedAt, IsDeleted)
                VALUES
                    (N'Invalid Agent', NULL, 2, 1, 120.000000, 31.200000, SYSUTCDATETIME(), CAST(0 AS bit));
                """));

        Assert.NotNull(exception);
    }
}
