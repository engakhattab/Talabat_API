using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Interfaces;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class UserConcurrencyPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public UserConcurrencyPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Stale_User_update_rejected_by_RowVersion()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);

        var dbContext1 = provider.GetRequiredService<TalabatDbContext>();
        var unitOfWork1 = provider.GetRequiredService<IUnitOfWork>();

        var user = User.Register("concurrency@test.com", "concurrency@test.com", "Concurrency Test");
        user.InitializeCustomerProfile("Concurrency Test", 30, null);
        dbContext1.Users.Add(user);
        await unitOfWork1.SaveChangesAsync();

        var userId = user.Id;
        Assert.True(userId > 0);

        var rowVersion1 = user.RowVersion;
        Assert.NotEqual([], rowVersion1);

        using var scope2 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString).CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var userCopy = await dbContext2.Users.FirstAsync(u => u.Id == userId);

        user.UpdateCustomerProfile("First Update", 31, null);
        await unitOfWork1.SaveChangesAsync();

        userCopy.UpdateCustomerProfile("Second Update", 32, null);
        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            dbContext2.SaveChangesAsync());

        Assert.NotNull(ex);
    }
}
