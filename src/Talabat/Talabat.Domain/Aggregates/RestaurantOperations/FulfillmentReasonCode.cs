using System.Text.Json.Serialization;

namespace Talabat.Domain.Aggregates.RestaurantOperations;

/// <summary>
/// Why an order fulfillment was rejected or cancelled (design D13). Each action accepts only its own codes; see
/// <see cref="FulfillmentReasons"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FulfillmentReasonCode
{
    ItemsUnavailable = 1,
    RestaurantBusy = 2,
    RestaurantClosing = 3,
    KitchenIssue = 4,
    ChangedMind = 5,
    OrderedByMistake = 6,
    CustomerRequested = 7,
    RestaurantRequested = 8,
    RestaurantUnresponsive = 9,
    NoDeliveryAgentAvailable = 10,
    SuspectedFraud = 11,
    AcceptanceTimeout = 12,
    Other = 13
}
