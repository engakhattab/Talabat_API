using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;
using Talabat.Infrastructure.Persistence;
using Talabat.Infrastructure.Tests.Persistence;
using Xunit;

namespace Talabat.Infrastructure.Tests.Identity;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class ConcurrencyConflictTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public ConcurrencyConflictTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Stale_user_save_viaUnitOfWork_throws_ConcurrencyConflictException()
    {
        await using var database = await _fixture.CreateDatabaseAsync();

        var provider1 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var provider2 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);

        var dbContext1 = provider1.GetRequiredService<TalabatDbContext>();
        var unitOfWork1 = provider1.GetRequiredService<IUnitOfWork>();

        var user = User.Register("concurrency@test.com", "concurrency@test.com", "Concurrency Test");
        user.InitializeCustomerProfile("Concurrency Test", 25, null);
        dbContext1.Users.Add(user);
        await unitOfWork1.SaveChangesAsync();

        var userId = user.Id;

        var scope2 = provider2.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var staleCopy = await dbContext2.Users.FirstAsync(u => u.Id == userId);

        user.UpdateCustomerProfile("Session1 Update", 26, null);
        await unitOfWork1.SaveChangesAsync();

        staleCopy.UpdateCustomerProfile("Session2 Stale Update", 27, null);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => unitOfWork2.SaveChangesAsync());

        Assert.Contains("modified by another process", ex.Message);

        scope2.Dispose();
    }

    [Fact]
    public async Task Concurrent_customer_profile_save_viaUnitOfWork_throws_ConcurrencyConflictException()
    {
        await using var database = await _fixture.CreateDatabaseAsync();

        var provider1 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var provider2 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);

        var dbContext1 = provider1.GetRequiredService<TalabatDbContext>();
        var unitOfWork1 = provider1.GetRequiredService<IUnitOfWork>();

        var user = User.Register("concurrency_agent@test.com", "concurrency_agent@test.com", "Concurrency Agent");
        user.InitializeCustomerProfile("Concurrency Agent", 30, null);
        dbContext1.Users.Add(user);
        await unitOfWork1.SaveChangesAsync();

        var userId = user.Id;

        var scope2 = provider2.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<TalabatDbContext>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var staleCopy = await dbContext2.Users.FirstAsync(u => u.Id == userId);

        user.UpdateCustomerProfile("Agent First", 28, null);
        await unitOfWork1.SaveChangesAsync();

        staleCopy.UpdateCustomerProfile("Agent Stale", 29, null);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => unitOfWork2.SaveChangesAsync());

        Assert.Contains("modified by another process", ex.Message);

        scope2.Dispose();
    }

    [Fact]
    public async Task Fresh_save_after_concurrent_update_succeeds()
    {
        await using var database = await _fixture.CreateDatabaseAsync();

        var provider1 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var provider2 = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);

        var dbContext1 = provider1.GetRequiredService<TalabatDbContext>();
        var unitOfWork1 = provider1.GetRequiredService<IUnitOfWork>();

        var user = User.Register("fresh@test.com", "fresh@test.com", "Fresh Test");
        user.InitializeCustomerProfile("Fresh Test", 25, null);
        dbContext1.Users.Add(user);
        await unitOfWork1.SaveChangesAsync();

        var userId = user.Id;

        var dbContext2 = provider2.GetRequiredService<TalabatDbContext>();
        var unitOfWork2 = provider2.GetRequiredService<IUnitOfWork>();

        var staleCopy = await dbContext2.Users.FirstAsync(u => u.Id == userId);

        user.UpdateCustomerProfile("Session1 Update", 26, null);
        await unitOfWork1.SaveChangesAsync();

        staleCopy.UpdateCustomerProfile("Session2 Stale", 27, null);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => unitOfWork2.SaveChangesAsync());

        dbContext2.ChangeTracker.Clear();
        var fresh = await dbContext2.Users.FirstAsync(u => u.Id == userId);
        fresh.UpdateCustomerProfile("Session2 Retry After Reload", 28, null);
        await unitOfWork2.SaveChangesAsync();

        var finalUser = await dbContext2.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal("Session2 Retry After Reload", finalUser.FullName);
        Assert.Equal(28, finalUser.Age);
    }
}
