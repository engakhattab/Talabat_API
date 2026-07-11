using Microsoft.EntityFrameworkCore;
using Talabat.Domain.Aggregates.Customer;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class CustomerPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public CustomerPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Customer_and_address_receive_ids_and_round_trip()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var provider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        var dbContext = provider.GetRequiredService<TalabatDbContext>();
        var repository = provider.GetRequiredService<ICustomerRepository>();

        var customer = new Customer("Address Customer", 29, "+202222222222");
        customer.AddAddress(new Address("Street 1", "Giza", "7", "2"), makeDefault: true);

        await repository.AddAsync(customer);
        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var saved = await repository.GetByIdWithAddressesAsync(customer.Id);

        Assert.True(customer.Id > 0);
        Assert.True(customer.Addresses.Single().Id > 0);
        Assert.NotNull(saved);
        Assert.Equal("Street 1", saved.Addresses.Single().Details.Street);
        Assert.True(saved.Addresses.Single().IsDefault);
        var customerColumns = dbContext.Model.FindEntityType(typeof(Customer))!
            .GetTableMappings()
            .SelectMany(mapping => mapping.Table.Columns)
            .ToList();

        Assert.DoesNotContain(customerColumns, column =>
            column.Name.Contains("Normalized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Second_default_address_for_customer_is_rejected_by_database()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO CustomerAddresses
                    (CustomerId, IsDefault, IsDeleted, Street, City, BuildingNumber, Floor)
                VALUES
                    ({customer.Id}, CAST(1 AS bit), CAST(0 AS bit), N'Another', N'Cairo', N'12', NULL);
                """));

        Assert.NotNull(exception);
    }
}
