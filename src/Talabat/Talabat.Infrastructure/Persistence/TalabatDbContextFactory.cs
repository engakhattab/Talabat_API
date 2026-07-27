using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Talabat.Infrastructure.Persistence;

public sealed class TalabatDbContextFactory : IDesignTimeDbContextFactory<TalabatDbContext>
{
    public TalabatDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TalabatDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=Talabat;Trusted_Connection=True;TrustServerCertificate=True");

        return new TalabatDbContext(optionsBuilder.Options);
    }
}
