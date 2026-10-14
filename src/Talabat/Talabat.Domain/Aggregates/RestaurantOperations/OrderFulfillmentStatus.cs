using System.Text.Json.Serialization;

namespace Talabat.Domain.Aggregates.RestaurantOperations;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderFulfillmentStatus
{
    AwaitingRestaurant = 1,
    Accepted = 2,
    Preparing = 3,
    ReadyForPickup = 4,
    HandedOver = 5,
    Rejected = 6,
    Cancelled = 7
}
