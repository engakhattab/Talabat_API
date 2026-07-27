using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Talabat.ArchitectureTests;

public sealed class ApplicationArchitectureTests
{
    private static readonly Assembly ApplicationAssembly = typeof(Talabat.Application.Abstractions.ICurrentUser).Assembly;
    private static readonly string ApplicationCsprojPath = FindCsproj("Talabat.Application", "Talabat.Application.csproj");

    [Fact]
    public void Application_ShouldNotReference_EntityFrameworkCore()
    {
        var forbidden = GetReferencedAssemblyNames()
            .Where(a => a.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Name!)
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Application references forbidden EF Core packages: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Application_ShouldNotReference_AspNetCore()
    {
        var forbidden = GetReferencedAssemblyNames()
            .Where(a => a.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Name!)
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Application references forbidden ASP.NET Core packages: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Application_ShouldNotReference_DuendeOrIdentityServer()
    {
        var forbidden = GetReferencedAssemblyNames()
            .Where(a => a.Name?.StartsWith("Duende", StringComparison.OrdinalIgnoreCase) == true
                      || a.Name?.Contains("IdentityServer", StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => a.Name!)
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Application references forbidden Duende/IdentityServer packages: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Application_ShouldNotReference_HttpContext()
    {
        var types = ApplicationAssembly.GetTypes();
        var httpContextTypes = types
            .Where(t => t.Namespace?.Contains("Microsoft.AspNetCore.Http", StringComparison.OrdinalIgnoreCase) == true)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(httpContextTypes.Count == 0,
            $"Application contains ASP.NET Core HTTP types: {string.Join(", ", httpContextTypes)}");
    }

    [Fact]
    public void Application_ShouldNotReference_ClaimsPrincipal()
    {
        var types = ApplicationAssembly.GetTypes();
        var claimsTypes = types
            .Where(t => t.Namespace?.Contains("System.Security.Claims", StringComparison.OrdinalIgnoreCase) == true)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(claimsTypes.Count == 0,
            $"Application contains Claims types: {string.Join(", ", claimsTypes)}");
    }

    [Fact]
    public void Application_Csproj_ShouldNotHaveForbiddenPackageReferences()
    {
        var bannedPrefixes = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Duende" };

        var doc = XDocument.Load(ApplicationCsprojPath);
        var references = doc.Descendants("PackageReference")
            .Select(r => (string?)r.Attribute("Include") ?? string.Empty)
            .ToList();

        var forbidden = references
            .Where(r => bannedPrefixes.Any(b => r.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(forbidden.Count == 0,
            $"Application csproj has forbidden PackageReferences: {string.Join(", ", forbidden)}");
    }

    private static IEnumerable<AssemblyName> GetReferencedAssemblyNames()
    {
        return ApplicationAssembly.GetReferencedAssemblies();
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
