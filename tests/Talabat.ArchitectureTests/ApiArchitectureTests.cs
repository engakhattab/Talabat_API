using System.Reflection;
using Xunit;

namespace Talabat.ArchitectureTests;

public sealed class ApiArchitectureTests
{
    private static readonly Assembly CustomerApiAssembly = typeof(Talabat.Customer.API.Auth.CurrentUser).Assembly;
    private static readonly Assembly DeliveryApiAssembly = typeof(Talabat.Delivery.API.Auth.CurrentUser).Assembly;
    private static readonly Type DbContextType = typeof(Talabat.Infrastructure.Persistence.TalabatDbContext);

    [Fact]
    public void Customer_API_Types_ShouldNotReference_TalabatDbContext_Directly()
    {
        var violations = new List<string>();

        foreach (var type in CustomerApiAssembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType == DbContextType)
                    violations.Add($"{type.Name}.{field.Name} (field)");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (prop.PropertyType == DbContextType)
                    violations.Add($"{type.Name}.{prop.Name} (property)");
            }

            foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (param.ParameterType == DbContextType)
                        violations.Add($"{type.Name} constructor param '{param.Name}'");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Customer API types directly reference TalabatDbContext: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Delivery_API_Types_ShouldNotReference_TalabatDbContext_Directly()
    {
        var violations = new List<string>();

        foreach (var type in DeliveryApiAssembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType == DbContextType)
                    violations.Add($"{type.Name}.{field.Name} (field)");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (prop.PropertyType == DbContextType)
                    violations.Add($"{type.Name}.{prop.Name} (property)");
            }

            foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (param.ParameterType == DbContextType)
                        violations.Add($"{type.Name} constructor param '{param.Name}'");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Delivery API types directly reference TalabatDbContext: {string.Join(", ", violations)}");
    }
}
