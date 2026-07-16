using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Talabat.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Talabat.Identity.Tests;

public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string? BaseConnectionString { get; private set; }

    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();

            await _container.StartAsync();
            BaseConnectionString = _container.GetConnectionString();
            return;
        }
        catch (Exception containerException)
        {
            _container = null;

            if (await TryLocalDbAsync())
            {
                return;
            }

            SkipReason =
                "SQL Server integration tests require Docker/Testcontainers or LocalDB. "
                + $"Testcontainers start failed: {containerException.GetType().Name}: {containerException.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        if (BaseConnectionString is null)
        {
            throw new InvalidOperationException(SkipReason ?? "SQL Server test database is unavailable.");
        }

        var databaseName = $"TalabatTest_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(BaseConnectionString)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true
        };

        var connectionString = builder.ConnectionString;
        var database = new TestDatabase(BaseConnectionString, databaseName, connectionString);

        await using var dbContext = database.CreateContext();
        await dbContext.Database.MigrateAsync();

        return database;
    }

    private async Task<bool> TryLocalDbAsync()
    {
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            BaseConnectionString = connectionString;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerDatabaseCollection :
    ICollectionFixture<SqlServerDatabaseFixture>
{
    public const string Name = "SqlServerDatabase";
}

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _baseConnectionString;
    private readonly string _databaseName;

    public TestDatabase(
        string baseConnectionString,
        string databaseName,
        string connectionString)
    {
        _baseConnectionString = baseConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public TalabatDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TalabatDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new TalabatDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        var builder = new SqlConnectionStringBuilder(_baseConnectionString)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END
            """;

        await command.ExecuteNonQueryAsync();
    }
}
