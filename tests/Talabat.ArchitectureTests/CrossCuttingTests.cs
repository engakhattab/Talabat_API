using System.Reflection;
using Xunit;

namespace Talabat.ArchitectureTests;

public sealed class CrossCuttingTests
{
    [Fact]
    public void Domain_ShouldNotReference_ICurrentUser()
    {
        var domainAssembly = typeof(Talabat.Domain.Aggregates.Users.User).Assembly;
        var applicationAssembly = typeof(Talabat.Application.Abstractions.ICurrentUser).Assembly;

        var domainTypes = domainAssembly.GetTypes();
        var iCurrentUserType = typeof(Talabat.Application.Abstractions.ICurrentUser);

        foreach (var type in domainTypes)
        {
            if (type.GetInterfaces().Any(i => i == iCurrentUserType))
            {
                Assert.Fail($"Domain type {type.FullName} implements ICurrentUser");
            }

            if (type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(f => f.FieldType == iCurrentUserType))
            {
                Assert.Fail($"Domain type {type.FullName} has a field of type ICurrentUser");
            }

            if (type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(p => p.PropertyType == iCurrentUserType))
            {
                Assert.Fail($"Domain type {type.FullName} has a property of type ICurrentUser");
            }
        }
    }

    [Fact]
    public void Domain_ShouldNotReference_ClaimOrRoleTypes()
    {
        var domainAssembly = typeof(Talabat.Domain.Aggregates.Users.User).Assembly;
        var domainTypes = domainAssembly.GetTypes();

        var forbiddenTypes = new[] { "System.Security.Claims.Claim", "System.Security.Claims.ClaimsPrincipal" };

        foreach (var type in domainTypes)
        {
            if (type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(f => forbiddenTypes.Contains(f.FieldType.FullName)))
            {
                Assert.Fail($"Domain type {type.FullName} has a forbidden claim/role field");
            }

            if (type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(p => forbiddenTypes.Contains(p.PropertyType.FullName)))
            {
                Assert.Fail($"Domain type {type.FullName} has a forbidden claim/role property");
            }
        }
    }

    [Fact]
    public void DeliveryApi_ShouldNotReference_IdentityProject()
    {
        var deliveryApiAssembly = typeof(Talabat.Delivery.API.Controllers.DeliveriesController).Assembly;

        var references = deliveryApiAssembly.GetReferencedAssemblies()
            .Where(a => a.Name == "Talabat.Identity")
            .ToList();

        Assert.True(references.Count == 0,
            "Delivery.API references Talabat.Identity project");
    }

    [Fact]
    public void CustomerApi_ShouldNotReference_DeliveryApi()
    {
        var customerApiAssembly = typeof(Talabat.Customer.API.Controllers.CustomerController).Assembly;

        var references = customerApiAssembly.GetReferencedAssemblies()
            .Where(a => a.Name == "Talabat.Delivery.API")
            .ToList();

        Assert.True(references.Count == 0,
            "Customer.API references Delivery.API project");
    }

    [Fact]
    public void Application_ShouldNotReference_IdentityProject()
    {
        var applicationAssembly = typeof(Talabat.Application.Abstractions.ICurrentUser).Assembly;

        var references = applicationAssembly.GetReferencedAssemblies()
            .Where(a => a.Name == "Talabat.Identity")
            .ToList();

        Assert.True(references.Count == 0,
            "Application references Talabat.Identity project");
    }

    [Fact]
    public void Infrastructure_ShouldNotReference_ApiProjects()
    {
        var infraAssembly = typeof(Talabat.Infrastructure.Persistence.TalabatDbContext).Assembly;

        var references = infraAssembly.GetReferencedAssemblies()
            .Where(a => a.Name == "Talabat.Customer.API" || a.Name == "Talabat.Delivery.API")
            .ToList();

        Assert.True(references.Count == 0,
            $"Infrastructure references API projects: {string.Join(", ", references.Select(r => r.Name))}");
    }
}
