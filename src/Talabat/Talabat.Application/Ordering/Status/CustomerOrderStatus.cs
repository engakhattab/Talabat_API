using System.Text.Json.Serialization;

namespace Talabat.Application.Ordering.Status;

/// <summary>
/// The single progress status customers see (design D9). Computed by
/// <see cref="CustomerOrderStatusMapper"/>, never stored.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomerOrderStatus
{
    Placed = 1,
    Accepted = 2,
    Preparing = 3,
    ReadyForPickup = 4,
    OnTheWay = 5,
    Delivered = 6,
    Rejected = 7,
    Cancelled = 8,
    DeliveryFailed = 9
}
