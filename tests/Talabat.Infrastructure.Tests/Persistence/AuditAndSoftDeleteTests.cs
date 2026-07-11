using Talabat.Domain.Aggregates.Customer;
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
        var repository = provider.GetRequiredService<ICustomerRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var customer = new Customer("Audited Customer", 28);
        await repository.AddAsync(customer);
        await unitOfWork.SaveChangesAsync();

        customer.UpdateProfile("Audited Customer Updated", 29);
        repository.Update(customer);
        await unitOfWork.SaveChangesAsync();

        Assert.NotEqual(default, customer.CreatedAt);
        Assert.NotNull(customer.ModifiedAt);
        Assert.Null(customer.CreatedBy);
        Assert.Null(customer.ModifiedBy);
    }

    [Fact]
    public async Task Soft_deleted_rows_are_excluded_from_repository_reads()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var repository = provider.GetRequiredService<ICustomerRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var customer = new Customer("Deleted Customer", 40);
        await repository.AddAsync(customer);
        await unitOfWork.SaveChangesAsync();

        customer.SoftDelete(PersistenceTestData.Now, deletedBy: null);
        repository.Update(customer);
        await unitOfWork.SaveChangesAsync();

        var found = await repository.GetByIdAsync(customer.Id);

        Assert.Null(found);
    }
}
