using Talabat.Application.Abstractions;
using Talabat.Domain.Aggregates.Catalog;

namespace Talabat.Infrastructure.Time;

public sealed class RestaurantLocalTimeProvider : IRestaurantLocalTimeProvider
{
    public TimeOnly GetLocalTime(Restaurant restaurant, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(restaurant);

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(
            utcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));

        return TimeOnly.FromDateTime(localTime);
    }
}
