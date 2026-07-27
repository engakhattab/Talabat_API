using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Talabat.ArchitectureTests;

public sealed class DomainArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Talabat.Domain.Aggregates.Users.User).Assembly;
    private static readonly string DomainCsprojPath = FindCsproj("Talabat.Domain", "Talabat.Domain.csproj");

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

    [Fact]
    public void Domain_Csproj_ShouldNotHaveForbiddenPackageReferences()
    {
        var bannedPrefixes = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Duende" };

        var doc = XDocument.Load(DomainCsprojPath);
        var references = doc.Descendants("PackageReference")
            .Select(r => (string?)r.Attribute("Include") ?? string.Empty)
            .ToList();

        var forbidden = references
            .Where(r => bannedPrefixes.Any(b => r.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Domain csproj has forbidden PackageReferences: {string.Join(", ", forbidden)}");
    }

    private static IEnumerable<AssemblyName> GetReferencedAssemblyNames()
    {
        return DomainAssembly.GetReferencedAssemblies();
    }

    private static string FindCsproj(string projectFolder, string csprojFileName)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src", "Talabat", projectFolder, csprojFileName);
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir, "tests", "Talabat.ArchitectureTests", csprojFileName);
            if (File.Exists(candidate))
                return Path.Combine(dir, "src", "Talabat", projectFolder, csprojFileName);
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException($"Could not locate {csprojFileName} from {AppContext.BaseDirectory}");
    }
}
