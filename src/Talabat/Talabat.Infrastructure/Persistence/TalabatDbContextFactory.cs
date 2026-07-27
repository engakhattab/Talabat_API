using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Talabat.Infrastructure.Persistence;

public sealed class TalabatDbContextFactory : IDesignTimeDbContextFactory<TalabatDbContext>
{
    public TalabatDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "../Talabat.API");
        
        var searchPath = Directory.Exists(apiPath) ? apiPath : basePath;

        var builder = new ConfigurationBuilder()
            .SetBasePath(searchPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();
        var connectionString = configuration.GetConnectionString("TalabatDb")
            ?? "Server=DESKTOP-5IHGJ9F\\SQLEXPRESS;Database=Talabat;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<TalabatDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new TalabatDbContext(optionsBuilder.Options);
    }
}
