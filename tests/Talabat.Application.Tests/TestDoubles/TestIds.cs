using System.Reflection;

namespace Talabat.Application.Tests.TestDoubles;

public static class TestIds
{
    public static void SetId<T>(T entity, int id)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        var property = typeof(T).GetProperty(
            "Id",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is null)
        {
            throw new InvalidOperationException($"{typeof(T).Name} does not expose an Id property.");
        }

        property.SetValue(entity, id);
    }
}
