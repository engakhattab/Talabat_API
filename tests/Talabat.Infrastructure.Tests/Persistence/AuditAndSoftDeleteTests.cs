using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.Interfaces;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class AuditAndSoftDeleteTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public AuditAndSoftDeleteTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Audit_interceptor_stamps_created_and_modified_timestamps()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var repository = provider.GetRequiredService<IUserRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var user = User.Register("audited", "audited@test.com", "Audited Customer");
        user.InitializeCustomerProfile("Audited Customer", 28, null);

        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        dbContext.Users.Add(user);
        await unitOfWork.SaveChangesAsync();

        user.UpdateCustomerProfile("Audited Customer Updated", 29, null);
        repository.Update(user);
        await unitOfWork.SaveChangesAsync();

        Assert.NotEqual(default, user.CreatedAt);
        Assert.NotNull(user.ModifiedAt);
        Assert.Null(user.CreatedBy);
        Assert.Null(user.ModifiedBy);
    }

    [Fact]
    public async Task Soft_deleted_rows_are_excluded_from_repository_reads()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var repository = provider.GetRequiredService<IUserRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var user = User.Register("deleted", "deleted@test.com", "Deleted Customer");
        user.InitializeCustomerProfile("Deleted Customer", 40, null);

        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        dbContext.Users.Add(user);
        await unitOfWork.SaveChangesAsync();

        user.SoftDelete(PersistenceTestData.Now, deletedBy: null);
        repository.Update(user);
        await unitOfWork.SaveChangesAsync();

        var found = await repository.GetByIdAsync(user.Id);

        Assert.Null(found);
    }
}
