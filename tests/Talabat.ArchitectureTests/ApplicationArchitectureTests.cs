using System.Reflection;
using Xunit;

namespace Talabat.ArchitectureTests;

public sealed class ApplicationArchitectureTests
{
    private static readonly Assembly ApplicationAssembly = typeof(Talabat.Application.Abstractions.ICurrentUser).Assembly;

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

    private static IEnumerable<AssemblyName> GetReferencedAssemblyNames()
    {
        return ApplicationAssembly.GetReferencedAssemblies();
    }
}
