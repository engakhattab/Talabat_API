using Talabat.Application.Abstractions;
using Talabat.Domain.Aggregates.Catalog;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeRestaurantLocalTimeProvider : IRestaurantLocalTimeProvider
{
    public TimeOnly LocalTime { get; set; } = new(12, 0);

    public TimeOnly GetLocalTime(Restaurant restaurant, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(restaurant);

        return LocalTime;
    }
}
