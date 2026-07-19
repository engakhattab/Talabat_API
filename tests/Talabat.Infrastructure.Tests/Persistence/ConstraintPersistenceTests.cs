using Microsoft.EntityFrameworkCore;

namespace Talabat.Infrastructure.Tests.Persistence;

[Collection(SqlServerDatabaseCollection.Name)]
public sealed class ConstraintPersistenceTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public ConstraintPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Money_and_quantity_check_constraints_reject_invalid_rows()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();
        var customer = await PersistenceTestData.AddCustomerAsync(dbContext);
        var order = await PersistenceTestData.AddOrderAsync(dbContext, customer.Id);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlRawAsync("""
                INSERT INTO Products
                    (RestaurantId, Name, Description, IsAvailable, ImageUrl, CurrentPriceAmount, CreatedAt, IsDeleted)
                VALUES
                    (1, N'Negative Money', N'Invalid', CAST(1 AS bit), NULL, -1.00, SYSUTCDATETIME(), CAST(0 AS bit));
                """));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO OrderItems
                    (OrderId, ProductId, ProductName, Quantity, UnitPriceAmount, LineTotalAmount)
                VALUES
                    ({order.Id}, 102, N'Chicken Shawarma', 0, 95.00, 0.00);
                """));
    }

    [Fact]
    public async Task Eight_user_check_constraints_exist()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var constraintNames = await dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT CONSTRAINT_NAME AS [Value]
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                WHERE CONSTRAINT_TYPE = 'CHECK'
                  AND TABLE_NAME = 'AspNetUsers'
                ORDER BY CONSTRAINT_NAME
                """)
            .ToListAsync();

        Assert.Contains("CK_Users_Age", constraintNames);
        Assert.Contains("CK_Users_VehicleType", constraintNames);
        Assert.Contains("CK_Users_DeliveryAgentStatus", constraintNames);
        Assert.Contains("CK_Users_AgentApprovalStatus", constraintNames);
        Assert.Contains("CK_Users_UserType_Range", constraintNames);
        Assert.Contains("CK_Users_CurrentLocation_PairedNull", constraintNames);
        Assert.Contains("CK_Users_CurrentLatitude_Range", constraintNames);
        Assert.Contains("CK_Users_CurrentLongitude_Range", constraintNames);
        Assert.Equal(8, constraintNames.Count);
    }

    [Fact]
    public async Task Filtered_unique_index_on_UserAddresses_exists()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var indexExists = await dbContext.Database
            .SqlQueryRaw<int>("""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE name = 'UX_UserAddresses_UserId_Default'
                          AND object_id = OBJECT_ID('UserAddresses')
                    ) THEN 1 ELSE 0
                END AS [Value]
                """)
            .SingleAsync();

        Assert.Equal(1, indexExists);
    }

    [Fact]
    public async Task Roles_table_contains_exactly_four_roles()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var serviceProvider = InfrastructureTestServices.CreateServiceProvider(database.ConnectionString);
        await Talabat.Infrastructure.Identity.IdentityDataSeeder.SeedRolesAsync(serviceProvider);

        await using var dbContext = database.CreateContext();
        var roles = await dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT Name AS [Value] FROM AspNetRoles ORDER BY Name
                """)
            .ToListAsync();

        Assert.Equal(4, roles.Count);
        Assert.Equal("Admin", roles[0]);
        Assert.Equal("Customer", roles[1]);
        Assert.Equal("DeliveryAgent", roles[2]);
        Assert.Equal("RestaurantOwner", roles[3]);
    }

    [Fact]
    public async Task No_seeded_users_exist()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var userCount = await dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM AspNetUsers")
            .SingleAsync();

        Assert.Equal(0, userCount);
    }

    [Fact]
    public async Task One_migration_history_row_exists()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var historyRows = await dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT MigrationId AS [Value] FROM __EFMigrationsHistory
                """)
            .ToListAsync();

        Assert.Single(historyRows);
        Assert.Contains("InitialUnifiedUser", historyRows[0]);
    }

    [Fact]
    public async Task Cart_CustomerId_FK_references_AspNetUsers()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var fkExists = await dbContext.Database
            .SqlQueryRaw<int>("""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1 FROM sys.foreign_keys fk
                        WHERE fk.parent_object_id = OBJECT_ID('Carts')
                          AND fk.referenced_object_id = OBJECT_ID('AspNetUsers')
                    ) THEN 1 ELSE 0
                END AS [Value]
                """)
            .SingleAsync();

        Assert.Equal(1, fkExists);
    }

    [Fact]
    public async Task Order_CustomerId_FK_references_AspNetUsers()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var fkExists = await dbContext.Database
            .SqlQueryRaw<int>("""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1 FROM sys.foreign_keys fk
                        WHERE fk.parent_object_id = OBJECT_ID('Orders')
                          AND fk.referenced_object_id = OBJECT_ID('AspNetUsers')
                    ) THEN 1 ELSE 0
                END AS [Value]
                """)
            .SingleAsync();

        Assert.Equal(1, fkExists);
    }

    [Fact]
    public async Task Delivery_CustomerId_and_AssignedAgentId_FK_references_AspNetUsers()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var fkColumns = await dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT c.name AS [Value]
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                WHERE fk.parent_object_id = OBJECT_ID('Deliveries')
                  AND fk.referenced_object_id = OBJECT_ID('AspNetUsers')
                ORDER BY c.name
                """)
            .ToListAsync();

        Assert.Contains("AssignedAgentId", fkColumns);
        Assert.Contains("CustomerId", fkColumns);
    }

    [Fact]
    public async Task Zero_legacy_tables_exist()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var dbContext = database.CreateContext();

        var legacyTables = await dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT TABLE_NAME AS [Value]
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME IN ('Customers', 'CustomerAddresses', 'DeliveryAgents')
                """)
            .ToListAsync();

        Assert.Empty(legacyTables);
    }
}
