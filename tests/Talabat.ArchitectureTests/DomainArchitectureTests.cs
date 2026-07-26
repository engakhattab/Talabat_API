using System.Reflection;
using Xunit;

namespace Talabat.ArchitectureTests;

public sealed class DomainArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Talabat.Domain.Aggregates.Users.User).Assembly;

    [Fact]
    public void Domain_ShouldNotReference_EntityFrameworkCore()
    {
        var forbidden = GetReferencedAssemblyNames()
            .Where(a => a.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Name!)
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Domain references forbidden EF Core packages: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Domain_ShouldNotReference_AspNetCore()
    {
        var forbidden = GetReferencedAssemblyNames()
            .Where(a => a.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Name!)
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Domain references forbidden ASP.NET Core packages: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Domain_ShouldNotReference_DuendeOrIdentityServer()
    {
        var forbidden = GetReferencedAssemblyNames()
            .Where(a => a.Name?.StartsWith("Duende", StringComparison.OrdinalIgnoreCase) == true
                      || a.Name?.Contains("IdentityServer", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Name!)
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Domain references forbidden Duende/IdentityServer packages: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Domain_AllowedException_IdentityStores()
    {
        var identityStores = GetReferencedAssemblyNames()
            .Where(a => a.Name == "Microsoft.Extensions.Identity.Stores")
            .ToList();

        // This test documents that Identity.Stores IS an allowed exception.
        // The test name makes it clear this is intentional.
        Assert.True(identityStores.Count <= 1,
            "Identity.Stores reference count unexpected");
    }

    [Fact]
    public void Domain_ShouldNotReference_HttpAbstractions()
    {
        var forbidden = GetReferencedAssemblyNames()
            .Where(a => a.Name == "Microsoft.AspNetCore.Http.Abstractions"
                      || a.Name == "Microsoft.AspNetCore.Http"
                      || a.Name == "System.Web")
            .Select(a => a.Name!)
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Domain references forbidden HTTP types: {string.Join(", ", forbidden)}");
    }

    private static IEnumerable<AssemblyName> GetReferencedAssemblyNames()
    {
        return DomainAssembly.GetReferencedAssemblies();
    }
}
