namespace Talabat.Domain.Aggregates.DeliveryManagement;

public enum DeliveryStatus
{
    PendingAssignment = 1,
    Assigned = 2,
    ArrivedAtRestaurant = 3,
    PickedUp = 4,
    OutForDelivery = 5,
    Delivered = 6,
    Cancelled = 7,
    Failed = 8
}
