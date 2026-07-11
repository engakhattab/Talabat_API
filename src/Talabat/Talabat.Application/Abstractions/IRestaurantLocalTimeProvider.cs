using Talabat.Domain.Aggregates.Catalog;

namespace Talabat.Application.Abstractions;

public interface IRestaurantLocalTimeProvider
{
    TimeOnly GetLocalTime(Restaurant restaurant, DateTime utcNow);
}
