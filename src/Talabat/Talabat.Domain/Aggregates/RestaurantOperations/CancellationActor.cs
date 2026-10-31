using System.Text.Json.Serialization;

namespace Talabat.Domain.Aggregates.RestaurantOperations;

/// <summary>Who cancelled an order fulfillment (design D2, D7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CancellationActor
{
    Customer = 1,
    Restaurant = 2,
    Admin = 3,
    System = 4
}
